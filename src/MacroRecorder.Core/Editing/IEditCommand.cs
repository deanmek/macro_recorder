using MacroRecorder.Core.Domain;

namespace MacroRecorder.Core.Editing;

public interface IEditCommand
{
    string Description { get; }
    IReadOnlyList<HistoryDelta> BuildDeltas();
    void Apply(Macro macro);
    void Undo(Macro macro);
}
