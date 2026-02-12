using MacroRecorder.Core.Domain;

namespace MacroRecorder.Core.Editing;

public sealed class InsertActionCommand : IEditCommand
{
    private readonly MacroAction _action;
    private readonly long _insertAtTicks;
    private readonly TimelineEditMode _mode;

    public InsertActionCommand(MacroAction action, long insertAtTicks, TimelineEditMode mode)
    {
        _action = action;
        _insertAtTicks = insertAtTicks;
        _mode = mode;
    }

    public string Description => $"Insert { _action.Type } at {_insertAtTicks}";

    public IReadOnlyList<HistoryDelta> BuildDeltas() =>
    [
        new HistoryDelta(_action.ActionId, "Insert", null, _action.Type.ToString())
    ];

    public void Apply(Macro macro)
    {
        var editor = new TimelineEditor { EditMode = _mode };
        editor.InsertAction(macro, _action, _insertAtTicks);
    }

    public void Undo(Macro macro)
    {
        var editor = new TimelineEditor { EditMode = _mode };
        editor.RemoveAction(macro, _action.ActionId);
    }
}

public sealed class RemoveActionCommand : IEditCommand
{
    private readonly Guid _actionId;
    private readonly TimelineEditMode _mode;
    private MacroAction? _snapshot;

    public RemoveActionCommand(Guid actionId, TimelineEditMode mode)
    {
        _actionId = actionId;
        _mode = mode;
    }

    public string Description => $"Remove action {_actionId}";

    public IReadOnlyList<HistoryDelta> BuildDeltas() =>
    [
        new HistoryDelta(_actionId, "Remove", _snapshot?.Type.ToString(), null)
    ];

    public void Apply(Macro macro)
    {
        _snapshot ??= macro.Actions.First(x => x.ActionId == _actionId).Clone();
        var editor = new TimelineEditor { EditMode = _mode };
        editor.RemoveAction(macro, _actionId);
    }

    public void Undo(Macro macro)
    {
        if (_snapshot is null)
        {
            return;
        }

        var editor = new TimelineEditor { EditMode = _mode };
        editor.InsertAction(macro, _snapshot.Clone(), _snapshot.TimeOffsetTicks);
    }
}

public sealed class UpdateDurationCommand : IEditCommand
{
    private readonly Guid _actionId;
    private readonly long _newDuration;
    private readonly TimelineEditMode _mode;
    private long? _previousDuration;

    public UpdateDurationCommand(Guid actionId, long newDuration, TimelineEditMode mode)
    {
        _actionId = actionId;
        _newDuration = newDuration;
        _mode = mode;
    }

    public string Description => $"Update duration for {_actionId}";

    public IReadOnlyList<HistoryDelta> BuildDeltas() =>
    [
        new HistoryDelta(_actionId, "DurationTicks", _previousDuration?.ToString(), _newDuration.ToString())
    ];

    public void Apply(Macro macro)
    {
        var action = macro.Actions.First(x => x.ActionId == _actionId);
        _previousDuration ??= action.DurationTicks;

        var editor = new TimelineEditor { EditMode = _mode };
        editor.UpdateDuration(macro, _actionId, _newDuration);
    }

    public void Undo(Macro macro)
    {
        if (_previousDuration is null)
        {
            return;
        }

        var editor = new TimelineEditor { EditMode = _mode };
        editor.UpdateDuration(macro, _actionId, _previousDuration.Value);
    }
}

public sealed class MoveActionCommand : IEditCommand
{
    private readonly Guid _actionId;
    private readonly long _newOffset;
    private readonly TimelineEditMode _mode;
    private long? _oldOffset;

    public MoveActionCommand(Guid actionId, long newOffset, TimelineEditMode mode)
    {
        _actionId = actionId;
        _newOffset = newOffset;
        _mode = mode;
    }

    public string Description => $"Move action {_actionId} to {_newOffset}";

    public IReadOnlyList<HistoryDelta> BuildDeltas() =>
    [
        new HistoryDelta(_actionId, "TimeOffsetTicks", _oldOffset?.ToString(), _newOffset.ToString())
    ];

    public void Apply(Macro macro)
    {
        var action = macro.Actions.First(x => x.ActionId == _actionId);
        _oldOffset ??= action.TimeOffsetTicks;

        var editor = new TimelineEditor { EditMode = _mode };
        editor.MoveAction(macro, _actionId, _newOffset);
    }

    public void Undo(Macro macro)
    {
        if (_oldOffset is null)
        {
            return;
        }

        var editor = new TimelineEditor { EditMode = _mode };
        editor.MoveAction(macro, _actionId, _oldOffset.Value);
    }
}

public sealed class UpdateActionPayloadCommand : IEditCommand
{
    private readonly Guid _actionId;
    private readonly MacroAction _updatedAction;
    private MacroAction? _previousAction;

    public UpdateActionPayloadCommand(Guid actionId, MacroAction updatedAction)
    {
        _actionId = actionId;
        _updatedAction = updatedAction;
    }

    public string Description => $"Update payload for {_actionId}";

    public IReadOnlyList<HistoryDelta> BuildDeltas() =>
    [
        new HistoryDelta(_actionId, "Payload", _previousAction?.Type.ToString(), _updatedAction.Type.ToString())
    ];

    public void Apply(Macro macro)
    {
        var index = macro.Actions.FindIndex(x => x.ActionId == _actionId);
        if (index < 0)
        {
            return;
        }

        _previousAction ??= macro.Actions[index].Clone();
        macro.Actions[index] = _updatedAction.Clone();
    }

    public void Undo(Macro macro)
    {
        if (_previousAction is null)
        {
            return;
        }

        var index = macro.Actions.FindIndex(x => x.ActionId == _actionId);
        if (index < 0)
        {
            return;
        }

        macro.Actions[index] = _previousAction.Clone();
    }
}
