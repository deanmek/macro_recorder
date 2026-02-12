using MacroRecorder.Core.Domain;

namespace MacroRecorder.App.Models;

public sealed class ActionRow
{
    public Guid ActionId { get; init; }
    public string Type { get; init; } = string.Empty;
    public long TimeOffsetTicks { get; init; }
    public long DurationTicks { get; init; }
    public string Details { get; init; } = string.Empty;

    public static ActionRow FromAction(MacroAction action)
    {
        var details = action switch
        {
            MouseMoveAction mouseMove => $"X={mouseMove.X}, Y={mouseMove.Y}, Monitor={mouseMove.MonitorId}",
            MouseDownAction mouseDown => $"Button={mouseDown.Button}",
            MouseUpAction mouseUp => $"Button={mouseUp.Button}",
            MouseClickAction mouseClick => $"Button={mouseClick.Button}",
            KeyDownAction keyDown => $"ScanCode={keyDown.ScanCode}",
            KeyUpAction keyUp => $"ScanCode={keyUp.ScanCode}",
            TextInputAction text => $"Text={text.Text}",
            WaitAction => "Wait",
            _ => string.Empty
        };

        return new ActionRow
        {
            ActionId = action.ActionId,
            Type = action.Type.ToString(),
            TimeOffsetTicks = action.TimeOffsetTicks,
            DurationTicks = action.DurationTicks,
            Details = details
        };
    }
}
