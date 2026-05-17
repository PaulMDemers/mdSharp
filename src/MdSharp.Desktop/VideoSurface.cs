using MdSharp.Core.Video;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MdSharp.Desktop;

internal sealed class VideoSurface : Control
{
    private readonly Bitmap _bitmap = new(Vdp.ScreenWidth, Vdp.ScreenHeight, PixelFormat.Format24bppRgb);
    private VideoAspectMode _aspectMode = VideoAspectMode.Native;
    private bool _integerScale;
    private bool _smoothing;

    public VideoSurface()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Black;
        MinimumSize = new Size(Vdp.ScreenWidth, Vdp.ScreenHeight);
    }

    public void Configure(VideoAspectMode aspectMode, bool integerScale, bool smoothing)
    {
        _aspectMode = Enum.IsDefined(aspectMode) ? aspectMode : VideoAspectMode.Native;
        _integerScale = integerScale;
        _smoothing = smoothing;
        Invalidate();
    }

    public void SetFrame(byte[] bgr)
    {
        if (bgr.Length != Vdp.ScreenWidth * Vdp.ScreenHeight * 3)
        {
            throw new ArgumentException("Unexpected Genesis framebuffer size.", nameof(bgr));
        }

        Rectangle rect = new(0, 0, _bitmap.Width, _bitmap.Height);
        BitmapData data = _bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
        try
        {
            int rowBytes = Vdp.ScreenWidth * 3;
            if (data.Stride == rowBytes)
            {
                Marshal.Copy(bgr, 0, data.Scan0, bgr.Length);
            }
            else
            {
                for (int y = 0; y < Vdp.ScreenHeight; y++)
                {
                    Marshal.Copy(bgr, y * rowBytes, data.Scan0 + (y * data.Stride), rowBytes);
                }
            }
        }
        finally
        {
            _bitmap.UnlockBits(data);
        }

        Invalidate();
    }

    public bool TryClientToFrame(Point point, out int x, out int y)
    {
        Rectangle target = TargetRectangle(ClientRectangle);
        if (target.Width <= 0 || target.Height <= 0 || !target.Contains(point))
        {
            x = 0;
            y = 0;
            return false;
        }

        x = Math.Clamp(((point.X - target.Left) * Vdp.ScreenWidth) / target.Width, 0, Vdp.ScreenWidth - 1);
        y = Math.Clamp(((point.Y - target.Top) * Vdp.ScreenHeight) / target.Height, 0, Vdp.ScreenHeight - 1);
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(Color.Black);
        e.Graphics.InterpolationMode = _smoothing ? InterpolationMode.HighQualityBicubic : InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = _smoothing ? PixelOffsetMode.HighQuality : PixelOffsetMode.Half;

        Rectangle target = TargetRectangle(ClientRectangle);
        e.Graphics.DrawImage(_bitmap, target);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bitmap.Dispose();
        }

        base.Dispose(disposing);
    }

    private Rectangle TargetRectangle(Rectangle bounds)
    {
        return _aspectMode switch
        {
            VideoAspectMode.Stretch => bounds.Width <= 0 || bounds.Height <= 0 ? Rectangle.Empty : bounds,
            VideoAspectMode.FourThree => FitAspect(bounds, 4.0 / 3.0, _integerScale),
            _ => FitNative(bounds, _integerScale),
        };
    }

    private static Rectangle FitNative(Rectangle bounds, bool integerScale)
    {
        if (integerScale)
        {
            return FitInteger(bounds, Vdp.ScreenWidth, Vdp.ScreenHeight);
        }

        return FitAspect(bounds, Vdp.ScreenWidth / (double)Vdp.ScreenHeight, integerScale: false);
    }

    private static Rectangle FitAspect(Rectangle bounds, double aspect, bool integerScale)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        if (integerScale)
        {
            int scale = Math.Max(1, bounds.Height / Vdp.ScreenHeight);
            int scaledWidth = (int)Math.Round(Vdp.ScreenHeight * scale * aspect);
            while (scale > 1 && scaledWidth > bounds.Width)
            {
                scale--;
                scaledWidth = (int)Math.Round(Vdp.ScreenHeight * scale * aspect);
            }

            int scaledHeight = Vdp.ScreenHeight * scale;
            return Center(bounds, Math.Min(scaledWidth, bounds.Width), Math.Min(scaledHeight, bounds.Height));
        }

        int width = bounds.Width;
        int height = (int)Math.Round(width / aspect);
        if (height > bounds.Height)
        {
            height = bounds.Height;
            width = (int)Math.Round(height * aspect);
        }

        return Center(bounds, Math.Max(1, width), Math.Max(1, height));
    }

    private static Rectangle FitInteger(Rectangle bounds, int sourceWidth, int sourceHeight)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        int scale = Math.Max(1, Math.Min(bounds.Width / sourceWidth, bounds.Height / sourceHeight));
        return Center(bounds, sourceWidth * scale, sourceHeight * scale);
    }

    private static Rectangle Center(Rectangle bounds, int width, int height)
    {
        int x = bounds.Left + ((bounds.Width - width) / 2);
        int y = bounds.Top + ((bounds.Height - height) / 2);
        return new Rectangle(x, y, width, height);
    }
}
