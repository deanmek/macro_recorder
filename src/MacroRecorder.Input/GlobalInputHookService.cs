using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MacroRecorder.Input;

public sealed class GlobalInputHookService : IGlobalInputHookService
{
    private const int WhMouseLl = 14;
    private const int WhKeyboardLl = 13;

    private const int WmMouseMove = 0x0200;
    private const int WmLButtonDown = 0x0201;
    private const int WmLButtonUp = 0x0202;
    private const int WmRButtonDown = 0x0204;
    private const int WmRButtonUp = 0x0205;
    private const int WmMButtonDown = 0x0207;
    private const int WmMButtonUp = 0x0208;

    private const int WmKeyDown = 0x0100;
    private const int WmKeyUp = 0x0101;
    private const int WmSysKeyDown = 0x0104;
    private const int WmSysKeyUp = 0x0105;

    private HookProc? _mouseProc;
    private HookProc? _keyboardProc;
    private nint _mouseHook;
    private nint _keyboardHook;

    public bool IsRecording { get; private set; }

    public event EventHandler<RawInputEvent>? InputCaptured;

    public void Start()
    {
        if (IsRecording)
        {
            return;
        }

        _mouseProc = MouseHookCallback;
        _keyboardProc = KeyboardHookCallback;
        _mouseHook = SetWindowsHookEx(WhMouseLl, _mouseProc, GetModuleHandle(null), 0);
        _keyboardHook = SetWindowsHookEx(WhKeyboardLl, _keyboardProc, GetModuleHandle(null), 0);

        if (_mouseHook == 0 || _keyboardHook == 0)
        {
            Stop();
            throw new InvalidOperationException("Unable to install one or more global input hooks.");
        }

        IsRecording = true;
    }

    public void Stop()
    {
        if (_mouseHook != 0)
        {
            UnhookWindowsHookEx(_mouseHook);
            _mouseHook = 0;
        }

        if (_keyboardHook != 0)
        {
            UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = 0;
        }

        IsRecording = false;
    }

    public void Dispose() => Stop();

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var info = Marshal.PtrToStructure<MsLlHookStruct>(lParam);

            RawInputEvent? evt = message switch
            {
                WmMouseMove => NewMouseEvent(RawInputEventType.MouseMove, info.pt.x, info.pt.y, 0),
                WmLButtonDown => NewMouseEvent(RawInputEventType.MouseDown, info.pt.x, info.pt.y, 1),
                WmLButtonUp => NewMouseEvent(RawInputEventType.MouseUp, info.pt.x, info.pt.y, 1),
                WmRButtonDown => NewMouseEvent(RawInputEventType.MouseDown, info.pt.x, info.pt.y, 2),
                WmRButtonUp => NewMouseEvent(RawInputEventType.MouseUp, info.pt.x, info.pt.y, 2),
                WmMButtonDown => NewMouseEvent(RawInputEventType.MouseDown, info.pt.x, info.pt.y, 3),
                WmMButtonUp => NewMouseEvent(RawInputEventType.MouseUp, info.pt.x, info.pt.y, 3),
                _ => null
            };

            if (evt is not null)
            {
                InputCaptured?.Invoke(this, evt);
            }
        }

        return CallNextHookEx(_mouseHook, nCode, wParam, lParam);
    }

    private nint KeyboardHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            var info = Marshal.PtrToStructure<KbdLlHookStruct>(lParam);

            RawInputEvent? evt = message switch
            {
                WmKeyDown or WmSysKeyDown => NewKeyboardEvent(RawInputEventType.KeyDown, info.scanCode),
                WmKeyUp or WmSysKeyUp => NewKeyboardEvent(RawInputEventType.KeyUp, info.scanCode),
                _ => null
            };

            if (evt is not null)
            {
                InputCaptured?.Invoke(this, evt);
            }
        }

        return CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    private static RawInputEvent NewMouseEvent(RawInputEventType type, int x, int y, int button) => new()
    {
        EventType = type,
        TimestampTicks = Stopwatch.GetTimestamp(),
        X = x,
        Y = y,
        Data = button
    };

    private static RawInputEvent NewKeyboardEvent(RawInputEventType type, int scanCode) => new()
    {
        EventType = type,
        TimestampTicks = Stopwatch.GetTimestamp(),
        Data = scanCode
    };

    private delegate nint HookProc(int nCode, nint wParam, nint lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MsLlHookStruct
    {
        public Point pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KbdLlHookStruct
    {
        public uint vkCode;
        public int scanCode;
        public uint flags;
        public uint time;
        public nuint dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int idHook, HookProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern nint GetModuleHandle(string? lpModuleName);
}
