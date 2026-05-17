using MdSharp.Core.Input;
using System.Runtime.InteropServices;

namespace MdSharp.Desktop;

internal static class XInputGamepad
{
    private const int MaxControllers = 4;
    private const short StickDeadZone = 8_000;

    public static bool IsConnected(int gamepadIndex)
    {
        if (gamepadIndex < 0 || gamepadIndex >= MaxControllers)
        {
            return false;
        }

        try
        {
            return XInputGetState(gamepadIndex, out _) == 0;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    public static GenesisButton Poll(ControllerInputSettings settings)
    {
        if (settings.GamepadIndex < 0)
        {
            return GenesisButton.None;
        }

        try
        {
            if (settings.GamepadIndex < MaxControllers && XInputGetState(settings.GamepadIndex, out XInputState state) == 0)
            {
                return MapState(state.Gamepad, settings);
            }
        }
        catch (DllNotFoundException)
        {
            return GenesisButton.None;
        }
        catch (EntryPointNotFoundException)
        {
            return GenesisButton.None;
        }

        return GenesisButton.None;
    }

    private static GenesisButton MapState(XInputGamepadState state, ControllerInputSettings settings)
    {
        GenesisButton pressed = GenesisButton.None;
        foreach ((GenesisButton button, GamepadControl control) in settings.Gamepad)
        {
            if (IsPressed(state, control))
            {
                pressed |= button;
            }
        }

        return pressed;
    }

    private static bool IsPressed(XInputGamepadState state, GamepadControl control)
    {
        return control switch
        {
            GamepadControl.DPadUp => HasButton(state, XInputButtons.DPadUp),
            GamepadControl.DPadDown => HasButton(state, XInputButtons.DPadDown),
            GamepadControl.DPadLeft => HasButton(state, XInputButtons.DPadLeft),
            GamepadControl.DPadRight => HasButton(state, XInputButtons.DPadRight),
            GamepadControl.A => HasButton(state, XInputButtons.A),
            GamepadControl.B => HasButton(state, XInputButtons.B),
            GamepadControl.X => HasButton(state, XInputButtons.X),
            GamepadControl.Y => HasButton(state, XInputButtons.Y),
            GamepadControl.LeftShoulder => HasButton(state, XInputButtons.LeftShoulder),
            GamepadControl.RightShoulder => HasButton(state, XInputButtons.RightShoulder),
            GamepadControl.Back => HasButton(state, XInputButtons.Back),
            GamepadControl.Start => HasButton(state, XInputButtons.Start),
            GamepadControl.LeftThumb => HasButton(state, XInputButtons.LeftThumb),
            GamepadControl.RightThumb => HasButton(state, XInputButtons.RightThumb),
            GamepadControl.LeftStickUp => state.LeftThumbY > StickDeadZone,
            GamepadControl.LeftStickDown => state.LeftThumbY < -StickDeadZone,
            GamepadControl.LeftStickLeft => state.LeftThumbX < -StickDeadZone,
            GamepadControl.LeftStickRight => state.LeftThumbX > StickDeadZone,
            GamepadControl.RightStickUp => state.RightThumbY > StickDeadZone,
            GamepadControl.RightStickDown => state.RightThumbY < -StickDeadZone,
            GamepadControl.RightStickLeft => state.RightThumbX < -StickDeadZone,
            GamepadControl.RightStickRight => state.RightThumbX > StickDeadZone,
            _ => false,
        };
    }

    private static bool HasButton(XInputGamepadState state, XInputButtons button)
    {
        return (state.Buttons & button) != 0;
    }

    [DllImport("xinput1_4.dll")]
    private static extern uint XInputGetState(int userIndex, out XInputState state);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepadState Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepadState
    {
        public XInputButtons Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short LeftThumbX;
        public short LeftThumbY;
        public short RightThumbX;
        public short RightThumbY;
    }

    [Flags]
    private enum XInputButtons : ushort
    {
        DPadUp = 0x0001,
        DPadDown = 0x0002,
        DPadLeft = 0x0004,
        DPadRight = 0x0008,
        Start = 0x0010,
        Back = 0x0020,
        LeftThumb = 0x0040,
        RightThumb = 0x0080,
        LeftShoulder = 0x0100,
        RightShoulder = 0x0200,
        A = 0x1000,
        B = 0x2000,
        X = 0x4000,
        Y = 0x8000,
    }
}
