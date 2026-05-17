using MdSharp.Core.Video;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace MdSharp.Desktop;

internal sealed class VideoSurface : Control
{
    private readonly Bitmap _bitmap = new(Vdp.ScreenWidth, Vdp.ScreenHeight, PixelFormat.Format24bppRgb);

    public VideoSurface()
    {
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
        BackColor = Color.Black;
        MinimumSize = new Size(Vdp.ScreenWidth, Vdp.ScreenHeight);
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
        Rectangle target = Fit(ClientRectangle, Vdp.ScreenWidth, Vdp.ScreenHeight);
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
        e.Graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        e.Graphics.PixelOffsetMode = PixelOffsetMode.Half;

        Rectangle target = Fit(ClientRectangle, Vdp.ScreenWidth, Vdp.ScreenHeight);
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

    private static Rectangle Fit(Rectangle bounds, int sourceWidth, int sourceHeight)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return Rectangle.Empty;
        }

        double scale = Math.Min(bounds.Width / (double)sourceWidth, bounds.Height / (double)sourceHeight);
        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        int x = bounds.Left + ((bounds.Width - width) / 2);
        int y = bounds.Top + ((bounds.Height - height) / 2);
        return new Rectangle(x, y, width, height);
    }
}
