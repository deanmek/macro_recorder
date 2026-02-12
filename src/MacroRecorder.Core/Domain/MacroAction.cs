namespace MacroRecorder.Core.Domain;

public abstract class MacroAction
{
    public Guid ActionId { get; init; } = Guid.NewGuid();
    public long TimeOffsetTicks { get; set; }
    public long DurationTicks { get; set; }
    public abstract ActionType Type { get; }
    public Dictionary<string, string> Metadata { get; } = new(StringComparer.Ordinal);

    public abstract MacroAction Clone();
}

public sealed class MouseMoveAction : MacroAction
{
    public int X { get; set; }
    public int Y { get; set; }
    public int MonitorId { get; set; }
    public override ActionType Type => ActionType.MouseMove;

    public override MacroAction Clone() => new MouseMoveAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        X = X,
        Y = Y,
        MonitorId = MonitorId
    }.WithMetadataFrom(this);
}

public sealed class MouseDownAction : MacroAction
{
    public MouseButton Button { get; set; }
    public override ActionType Type => ActionType.MouseDown;

    public override MacroAction Clone() => new MouseDownAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        Button = Button
    }.WithMetadataFrom(this);
}

public sealed class MouseUpAction : MacroAction
{
    public MouseButton Button { get; set; }
    public override ActionType Type => ActionType.MouseUp;

    public override MacroAction Clone() => new MouseUpAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        Button = Button
    }.WithMetadataFrom(this);
}

public sealed class MouseClickAction : MacroAction
{
    public MouseButton Button { get; set; }
    public override ActionType Type => ActionType.MouseClick;

    public override MacroAction Clone() => new MouseClickAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        Button = Button
    }.WithMetadataFrom(this);
}

public sealed class KeyDownAction : MacroAction
{
    public int ScanCode { get; set; }
    public override ActionType Type => ActionType.KeyDown;

    public override MacroAction Clone() => new KeyDownAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        ScanCode = ScanCode
    }.WithMetadataFrom(this);
}

public sealed class KeyUpAction : MacroAction
{
    public int ScanCode { get; set; }
    public override ActionType Type => ActionType.KeyUp;

    public override MacroAction Clone() => new KeyUpAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        ScanCode = ScanCode
    }.WithMetadataFrom(this);
}

public sealed class TextInputAction : MacroAction
{
    public string Text { get; set; } = string.Empty;
    public override ActionType Type => ActionType.TextInput;

    public override MacroAction Clone() => new TextInputAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks,
        Text = Text
    }.WithMetadataFrom(this);
}

public sealed class WaitAction : MacroAction
{
    public override ActionType Type => ActionType.Wait;

    public override MacroAction Clone() => new WaitAction
    {
        ActionId = ActionId,
        TimeOffsetTicks = TimeOffsetTicks,
        DurationTicks = DurationTicks
    }.WithMetadataFrom(this);
}

internal static class MacroActionExtensions
{
    internal static T WithMetadataFrom<T>(this T target, MacroAction source)
        where T : MacroAction
    {
        foreach (var (key, value) in source.Metadata)
        {
            target.Metadata[key] = value;
        }

        return target;
    }
}
