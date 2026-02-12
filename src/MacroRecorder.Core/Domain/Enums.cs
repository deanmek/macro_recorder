namespace MacroRecorder.Core.Domain;

public enum TimelineEditMode
{
    Relative = 0,
    Absolute = 1
}

public enum ActionType : ushort
{
    MouseMove = 0,
    MouseDown = 1,
    MouseUp = 2,
    MouseClick = 3,
    KeyDown = 4,
    KeyUp = 5,
    TextInput = 6,
    Wait = 7
}

public enum MouseButton : byte
{
    Left = 0,
    Right = 1,
    Middle = 2,
    X1 = 3,
    X2 = 4
}
