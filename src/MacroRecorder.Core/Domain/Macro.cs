using MacroRecorder.Core.Editing;

namespace MacroRecorder.Core.Domain;

public sealed class Macro
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "New Macro";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<MacroAction> Actions { get; } = new();
    public long Version { get; set; }

    public LimitedStack<IEditCommand> UndoStack { get; } = new(maxSize: 200);
    public LimitedStack<IEditCommand> RedoStack { get; } = new(maxSize: 200);
    public List<HistoryEntry> History { get; } = new();

    public void SortActions() => Actions.Sort((a, b) => a.TimeOffsetTicks.CompareTo(b.TimeOffsetTicks));
}
