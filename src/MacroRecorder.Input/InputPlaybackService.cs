using System.Diagnostics;
using System.Runtime.InteropServices;
using MacroRecorder.Core.Domain;

namespace MacroRecorder.Input;

public sealed class InputPlaybackService : IInputPlaybackService
{
    public async Task PlaybackAsync(IReadOnlyList<MacroAction> actions, CancellationToken cancellationToken = default)
    {
        if (actions.Count == 0)
        {
            return;
        }

        var ordered = actions.OrderBy(x => x.TimeOffsetTicks).ToList();
        var startTicks = Stopwatch.GetTimestamp();

        foreach (var action in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await WaitUntilAsync(startTicks, action.TimeOffsetTicks, cancellationToken);
            Execute(action);
        }
    }

    private static async Task WaitUntilAsync(long playbackStartTicks, long targetOffsetTicks, CancellationToken cancellationToken)
    {
        while (true)
        {
            var elapsedTicks = Stopwatch.GetTimestamp() - playbackStartTicks;
            var remainingTicks = targetOffsetTicks - elapsedTicks;
            if (remainingTicks <= 0)
            {
                return;
            }

            // Coarse wait then loop for precision.
            var remainingMs = (int)(remainingTicks * 1000 / Stopwatch.Frequency);
            if (remainingMs > 2)
            {
                await Task.Delay(remainingMs - 1, cancellationToken);
            }
            else
            {
                await Task.Yield();
            }
        }
    }

    private static void Execute(MacroAction action)
    {
        switch (action)
        {
            case MouseMoveAction move:
                SendMouseMove(move.X, move.Y);
                break;
            case MouseDownAction down:
                SendMouseButton(down.Button, isDown: true);
                break;
            case MouseUpAction up:
                SendMouseButton(up.Button, isDown: false);
                break;
            case MouseClickAction click:
                SendMouseButton(click.Button, isDown: true);
                SendMouseButton(click.Button, isDown: false);
                break;
            case KeyDownAction keyDown:
                SendKey(keyDown.ScanCode, keyUp: false);
                break;
            case KeyUpAction keyUp:
                SendKey(keyUp.ScanCode, keyUp: true);
                break;
            case WaitAction:
            case TextInputAction:
                break;
        }
    }

    private static void SendMouseMove(int x, int y)
    {
        var screenWidth = GetSystemMetrics(0);
        var screenHeight = GetSystemMetrics(1);

        var input = new Input
        {
            type = 0,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = (int)(x * 65535.0 / Math.Max(1, screenWidth - 1)),
                    dy = (int)(y * 65535.0 / Math.Max(1, screenHeight - 1)),
                    mouseData = 0,
                    dwFlags = MouseEventfMove | MouseEventfAbsolute,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendMouseButton(MouseButton button, bool isDown)
    {
        uint flags = button switch
        {
            MouseButton.Left => isDown ? MouseEventfLeftDown : MouseEventfLeftUp,
            MouseButton.Right => isDown ? MouseEventfRightDown : MouseEventfRightUp,
            MouseButton.Middle => isDown ? MouseEventfMiddleDown : MouseEventfMiddleUp,
            _ => isDown ? MouseEventfLeftDown : MouseEventfLeftUp
        };

        var input = new Input
        {
            type = 0,
            U = new InputUnion
            {
                mi = new MouseInput
                {
                    dx = 0,
                    dy = 0,
                    mouseData = 0,
                    dwFlags = flags,
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private static void SendKey(int scanCode, bool keyUp)
    {
        var input = new Input
        {
            type = 1,
            U = new InputUnion
            {
                ki = new KeyboardInput
                {
                    wVk = 0,
                    wScan = (ushort)scanCode,
                    dwFlags = KeyeventfScancode | (keyUp ? KeyeventfKeyUp : 0),
                    time = 0,
                    dwExtraInfo = 0
                }
            }
        };

        SendInput(1, [input], Marshal.SizeOf<Input>());
    }

    private const uint MouseEventfMove = 0x0001;
    private const uint MouseEventfLeftDown = 0x0002;
    private const uint MouseEventfLeftUp = 0x0004;
    private const uint MouseEventfRightDown = 0x0008;
    private const uint MouseEventfRightUp = 0x0010;
    private const uint MouseEventfMiddleDown = 0x0020;
    private const uint MouseEventfMiddleUp = 0x0040;
    private const uint MouseEventfAbsolute = 0x8000;
    private const uint KeyeventfScancode = 0x0008;
    private const uint KeyeventfKeyUp = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint type;
        public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)]
        public MouseInput mi;

        [FieldOffset(0)]
        public KeyboardInput ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, Input[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
