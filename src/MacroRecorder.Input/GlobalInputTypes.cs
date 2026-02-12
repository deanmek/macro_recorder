namespace MacroRecorder.Input;

public enum RawInputEventType
{
    MouseMove,
    MouseDown,
    MouseUp,
    KeyDown,
    KeyUp
}

public sealed class RawInputEvent
{
    public RawInputEventType EventType { get; init; }
    public long TimestampTicks { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Data { get; init; }
}

public interface IGlobalInputHookService : IDisposable
{
    bool IsRecording { get; }
    event EventHandler<RawInputEvent>? InputCaptured;
    void Start();
    void Stop();
}
