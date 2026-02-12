using MacroRecorder.Core.Domain;

namespace MacroRecorder.Core.Editing;

public sealed class TimelineEditor
{
    public TimelineEditMode EditMode { get; set; } = TimelineEditMode.Relative;

    public void ApplyCommand(Macro macro, IEditCommand command)
    {
        command.Apply(macro);
        macro.UndoStack.Push(command);
        macro.RedoStack.Clear();
        AppendHistory(macro, command);
    }

    public bool Undo(Macro macro)
    {
        if (!macro.UndoStack.TryPop(out var command) || command is null)
        {
            return false;
        }

        command.Undo(macro);
        macro.RedoStack.Push(command);
        AppendHistory(macro, command, "Undo: ");
        return true;
    }

    public bool Redo(Macro macro)
    {
        if (!macro.RedoStack.TryPop(out var command) || command is null)
        {
            return false;
        }

        command.Apply(macro);
        macro.UndoStack.Push(command);
        AppendHistory(macro, command, "Redo: ");
        return true;
    }

    public void InsertAction(Macro macro, MacroAction newAction, long insertAtTicks)
    {
        newAction.TimeOffsetTicks = insertAtTicks;

        var index = macro.Actions.FindIndex(a => a.TimeOffsetTicks > insertAtTicks);
        if (index < 0)
        {
            macro.Actions.Add(newAction);
            index = macro.Actions.Count - 1;
        }
        else
        {
            macro.Actions.Insert(index, newAction);
        }

        if (EditMode is TimelineEditMode.Relative)
        {
            ShiftSubsequentActions(macro.Actions, index + 1, newAction.DurationTicks);
        }
    }

    public void RemoveAction(Macro macro, Guid actionId)
    {
        var index = macro.Actions.FindIndex(x => x.ActionId == actionId);
        if (index < 0)
        {
            return;
        }

        var removed = macro.Actions[index];
        macro.Actions.RemoveAt(index);

        if (EditMode is TimelineEditMode.Relative)
        {
            ShiftSubsequentActions(macro.Actions, index, -removed.DurationTicks);
        }
    }

    public void UpdateDuration(Macro macro, Guid actionId, long newDurationTicks)
    {
        var index = macro.Actions.FindIndex(x => x.ActionId == actionId);
        if (index < 0)
        {
            return;
        }

        var action = macro.Actions[index];
        var delta = newDurationTicks - action.DurationTicks;
        action.DurationTicks = newDurationTicks;

        if (EditMode is TimelineEditMode.Relative)
        {
            ShiftSubsequentActions(macro.Actions, index + 1, delta);
        }
    }

    public void MoveAction(Macro macro, Guid actionId, long newOffsetTicks)
    {
        var index = macro.Actions.FindIndex(x => x.ActionId == actionId);
        if (index < 0)
        {
            return;
        }

        var action = macro.Actions[index];
        if (EditMode is TimelineEditMode.Absolute)
        {
            action.TimeOffsetTicks = newOffsetTicks;
            macro.SortActions();
            return;
        }

        var oldOffset = action.TimeOffsetTicks;
        var delta = newOffsetTicks - oldOffset;
        action.TimeOffsetTicks = newOffsetTicks;

        if (delta == 0)
        {
            return;
        }

        if (delta > 0)
        {
            foreach (var candidate in macro.Actions)
            {
                if (candidate.ActionId == actionId)
                {
                    continue;
                }

                if (candidate.TimeOffsetTicks > oldOffset && candidate.TimeOffsetTicks <= newOffsetTicks)
                {
                    candidate.TimeOffsetTicks -= delta;
                }
            }
        }
        else
        {
            foreach (var candidate in macro.Actions)
            {
                if (candidate.ActionId == actionId)
                {
                    continue;
                }

                if (candidate.TimeOffsetTicks >= newOffsetTicks && candidate.TimeOffsetTicks < oldOffset)
                {
                    candidate.TimeOffsetTicks -= delta;
                }
            }
        }

        macro.SortActions();
    }

    private static void ShiftSubsequentActions(List<MacroAction> actions, int startIndex, long delta)
    {
        if (delta == 0)
        {
            return;
        }

        for (var i = startIndex; i < actions.Count; i++)
        {
            actions[i].TimeOffsetTicks += delta;
        }
    }

    private static void AppendHistory(Macro macro, IEditCommand command, string prefix = "")
    {
        macro.Version += 1;
        macro.UpdatedAt = DateTimeOffset.UtcNow;
        macro.History.Add(new HistoryEntry
        {
            Version = macro.Version,
            Timestamp = DateTimeOffset.UtcNow,
            Summary = $"{prefix}{command.Description}",
            Deltas = command.BuildDeltas()
        });
    }
}
