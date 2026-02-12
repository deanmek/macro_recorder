using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using MacroRecorder.App.Models;
using MacroRecorder.Core.Domain;
using MacroRecorder.Core.Editing;
using MacroRecorder.Core.Serialization;
using MacroRecorder.Input;

namespace MacroRecorder.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private const string PathPointsMetadataKey = "PathPoints";
    private static readonly long MouseMoveIdleSplitTicks = Stopwatch.Frequency / 8;

    private readonly IGlobalInputHookService _inputHookService;
    private readonly TimelineEditor _timelineEditor = new();
    private readonly IInputPlaybackService _inputPlaybackService;
    private readonly List<MousePoint> _pendingMousePoints = new();

    private CancellationTokenSource? _playbackCts;
    private long? _recordingStart;
    private bool _isRecording;
    private bool _isPlaying;
    private string _status = "Idle";
    private ActionRow? _selectedAction;
    private string _editorOffsetTicks = "0";
    private string _editorDurationTicks = "0";
    private string _editorMouseX = "0";
    private string _editorMouseY = "0";
    private string _editorMonitorId = "0";
    private string _editorMouseButton = MouseButton.Left.ToString();
    private string _editorScanCode = "0";
    private string _editorTextPayload = string.Empty;
    private string _playbackSpeedMultiplier = "1.0";
    private string _playbackLoopCount = "1";

    public MainWindowViewModel(IGlobalInputHookService inputHookService, IInputPlaybackService inputPlaybackService)
    {
        _inputHookService = inputHookService;
        _inputPlaybackService = inputPlaybackService;
        _inputHookService.InputCaptured += OnInputCaptured;

        StartRecordingCommand = new RelayCommand(StartRecording, () => !IsRecording && !IsPlaying);
        StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
        UndoCommand = new RelayCommand(Undo, () => Macro.UndoStack.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => Macro.RedoStack.Count > 0);
        ToggleModeCommand = new RelayCommand(ToggleMode);
        PlaybackCommand = new RelayCommand(Playback, () => !IsRecording && !IsPlaying && Macro.Actions.Count > 0);
        StopPlaybackCommand = new RelayCommand(StopPlayback, () => IsPlaying);
        ApplyEditCommand = new RelayCommand(ApplySelectionEdits, () => SelectedAction is not null && !IsRecording && !IsPlaying);
        DeleteSelectedCommand = new RelayCommand(DeleteSelected, () => SelectedAction is not null && !IsRecording && !IsPlaying);
        InsertWaitAfterSelectedCommand = new RelayCommand(InsertWaitAfterSelected, () => SelectedAction is not null && !IsRecording && !IsPlaying);

        Actions = new ObservableCollection<ActionRow>();
        Macro = new Macro { Name = "Session Macro" };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public Macro Macro { get; }
    public ObservableCollection<ActionRow> Actions { get; }

    public RelayCommand StartRecordingCommand { get; }
    public RelayCommand StopRecordingCommand { get; }
    public RelayCommand UndoCommand { get; }
    public RelayCommand RedoCommand { get; }
    public RelayCommand ToggleModeCommand { get; }
    public RelayCommand PlaybackCommand { get; }
    public RelayCommand StopPlaybackCommand { get; }
    public RelayCommand ApplyEditCommand { get; }
    public RelayCommand DeleteSelectedCommand { get; }
    public RelayCommand InsertWaitAfterSelectedCommand { get; }

    public bool IsRecording
    {
        get => _isRecording;
        private set
        {
            if (_isRecording == value)
            {
                return;
            }

            _isRecording = value;
            OnPropertyChanged();
            UpdateCommandStates();
        }
    }

    public bool IsPlaying
    {
        get => _isPlaying;
        private set
        {
            if (_isPlaying == value)
            {
                return;
            }

            _isPlaying = value;
            OnPropertyChanged();
            UpdateCommandStates();
        }
    }

    public string Status
    {
        get => _status;
        private set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
        }
    }

    public TimelineEditMode EditMode
    {
        get => _timelineEditor.EditMode;
        private set
        {
            _timelineEditor.EditMode = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditModeLabel));
        }
    }

    public string EditModeLabel => EditMode == TimelineEditMode.Relative ? "Relative (default)" : "Absolute";

    public ActionRow? SelectedAction
    {
        get => _selectedAction;
        set
        {
            if (_selectedAction == value)
            {
                return;
            }

            _selectedAction = value;
            OnPropertyChanged();

            if (value is not null)
            {
                EditorOffsetTicks = value.TimeOffsetTicks.ToString();
                EditorDurationTicks = value.DurationTicks.ToString();
                LoadTypeSpecificEditorValues(value.ActionId);
            }

            OnPropertyChanged(nameof(IsMouseEditorVisible));
            OnPropertyChanged(nameof(IsMouseButtonEditorVisible));
            OnPropertyChanged(nameof(IsKeyEditorVisible));
            OnPropertyChanged(nameof(IsTextEditorVisible));
            UpdateCommandStates();
        }
    }

    public bool IsMouseEditorVisible => SelectedMacroAction is MouseMoveAction;
    public bool IsMouseButtonEditorVisible => SelectedMacroAction is MouseDownAction or MouseUpAction or MouseClickAction;
    public bool IsKeyEditorVisible => SelectedMacroAction is KeyDownAction or KeyUpAction;
    public bool IsTextEditorVisible => SelectedMacroAction is TextInputAction;

    public string EditorOffsetTicks
    {
        get => _editorOffsetTicks;
        set
        {
            if (_editorOffsetTicks == value)
            {
                return;
            }

            _editorOffsetTicks = value;
            OnPropertyChanged();
        }
    }

    public string EditorDurationTicks
    {
        get => _editorDurationTicks;
        set
        {
            if (_editorDurationTicks == value)
            {
                return;
            }

            _editorDurationTicks = value;
            OnPropertyChanged();
        }
    }

    public string EditorMouseX { get => _editorMouseX; set { if (_editorMouseX == value) return; _editorMouseX = value; OnPropertyChanged(); } }
    public string EditorMouseY { get => _editorMouseY; set { if (_editorMouseY == value) return; _editorMouseY = value; OnPropertyChanged(); } }
    public string EditorMonitorId { get => _editorMonitorId; set { if (_editorMonitorId == value) return; _editorMonitorId = value; OnPropertyChanged(); } }
    public string EditorMouseButton { get => _editorMouseButton; set { if (_editorMouseButton == value) return; _editorMouseButton = value; OnPropertyChanged(); } }
    public string EditorScanCode { get => _editorScanCode; set { if (_editorScanCode == value) return; _editorScanCode = value; OnPropertyChanged(); } }
    public string EditorTextPayload { get => _editorTextPayload; set { if (_editorTextPayload == value) return; _editorTextPayload = value; OnPropertyChanged(); } }
    public string PlaybackSpeedMultiplier { get => _playbackSpeedMultiplier; set { if (_playbackSpeedMultiplier == value) return; _playbackSpeedMultiplier = value; OnPropertyChanged(); } }
    public string PlaybackLoopCount { get => _playbackLoopCount; set { if (_playbackLoopCount == value) return; _playbackLoopCount = value; OnPropertyChanged(); } }

    public int ActionCount => Macro.Actions.Count;

    private MacroAction? SelectedMacroAction =>
        SelectedAction is null ? null : Macro.Actions.FirstOrDefault(x => x.ActionId == SelectedAction.ActionId);

    public void Dispose()
    {
        _inputHookService.InputCaptured -= OnInputCaptured;
        _inputHookService.Dispose();
        _playbackCts?.Dispose();
    }

    public void SaveToFile(string path)
    {
        using var fs = File.Create(path);
        BinaryMacroSerializer.Write(fs, Macro);
        Status = $"Saved macro to {path}";
    }

    public void LoadFromFile(string path)
    {
        using var fs = File.OpenRead(path);
        var loaded = BinaryMacroSerializer.Read(fs);

        Macro.Actions.Clear();
        Macro.Actions.AddRange(loaded.Actions);
        Macro.Name = loaded.Name;
        Macro.UpdatedAt = loaded.UpdatedAt;
        Macro.Version = loaded.Version;
        Macro.UndoStack.Clear();
        Macro.RedoStack.Clear();
        Macro.History.Clear();

        RefreshRows();
        UpdateCommandStates();
        Status = $"Loaded macro from {path}";
    }

    private void StartRecording()
    {
        _recordingStart = null;
        _pendingMousePoints.Clear();
        _inputHookService.Start();
        IsRecording = true;
        Status = "Recording...";
    }

    private void StopRecording()
    {
        FlushPendingMouseBundle();
        _inputHookService.Stop();
        IsRecording = false;
        Status = $"Stopped. Captured {Macro.Actions.Count} actions.";
    }

    private void Undo()
    {
        if (_timelineEditor.Undo(Macro))
        {
            RefreshRows();
        }

        UpdateCommandStates();
    }

    private void Redo()
    {
        if (_timelineEditor.Redo(Macro))
        {
            RefreshRows();
        }

        UpdateCommandStates();
    }

    private async void Playback()
    {
        if (Macro.Actions.Count == 0)
        {
            return;
        }

        if (!double.TryParse(PlaybackSpeedMultiplier, out var speedMultiplier) || speedMultiplier <= 0)
        {
            Status = "Invalid playback speed multiplier.";
            return;
        }

        if (!int.TryParse(PlaybackLoopCount, out var loopCount) || loopCount <= 0)
        {
            Status = "Invalid loop count.";
            return;
        }

        IsPlaying = true;
        _playbackCts?.Dispose();
        _playbackCts = new CancellationTokenSource();

        try
        {
            for (var i = 0; i < loopCount; i++)
            {
                Status = loopCount == 1 ? "Playing..." : $"Playing loop {i + 1}/{loopCount}...";
                var plannedActions = BuildPlaybackPlan(speedMultiplier);
                await _inputPlaybackService.PlaybackAsync(plannedActions, _playbackCts.Token);
            }

            Status = "Playback complete.";
        }
        catch (OperationCanceledException)
        {
            Status = "Playback stopped.";
        }
        catch (Exception ex)
        {
            Status = $"Playback failed: {ex.Message}";
        }
        finally
        {
            _playbackCts?.Dispose();
            _playbackCts = null;
            IsPlaying = false;
        }
    }

    private void StopPlayback() => _playbackCts?.Cancel();

    private void ApplySelectionEdits()
    {
        if (SelectedAction is null)
        {
            return;
        }

        if (!long.TryParse(EditorOffsetTicks, out var newOffset) || newOffset < 0)
        {
            Status = "Invalid offset ticks value.";
            return;
        }

        if (!long.TryParse(EditorDurationTicks, out var newDuration) || newDuration < 0)
        {
            Status = "Invalid duration ticks value.";
            return;
        }

        var selectedMacroAction = SelectedMacroAction;
        if (selectedMacroAction is null)
        {
            return;
        }

        var updatedPayloadAction = BuildUpdatedPayloadAction(selectedMacroAction, newOffset, newDuration);
        if (updatedPayloadAction is null)
        {
            return;
        }

        _timelineEditor.ApplyCommand(Macro, new MoveActionCommand(SelectedAction.ActionId, newOffset, EditMode));
        _timelineEditor.ApplyCommand(Macro, new UpdateDurationCommand(SelectedAction.ActionId, newDuration, EditMode));
        _timelineEditor.ApplyCommand(Macro, new UpdateActionPayloadCommand(SelectedAction.ActionId, updatedPayloadAction));
        RefreshRows(selectActionId: SelectedAction.ActionId);
        Status = "Applied action edits.";
    }

    private MacroAction? BuildUpdatedPayloadAction(MacroAction selectedMacroAction, long newOffset, long newDuration)
    {
        var updated = selectedMacroAction.Clone();
        updated.TimeOffsetTicks = newOffset;
        updated.DurationTicks = newDuration;

        switch (updated)
        {
            case MouseMoveAction move:
                if (!int.TryParse(EditorMouseX, out var mouseX) || !int.TryParse(EditorMouseY, out var mouseY) || !int.TryParse(EditorMonitorId, out var monitorId))
                {
                    Status = "Invalid mouse coordinate or monitor value.";
                    return null;
                }

                move.X = mouseX;
                move.Y = mouseY;
                move.MonitorId = monitorId;
                break;
            case MouseDownAction down:
                if (!Enum.TryParse<MouseButton>(EditorMouseButton, ignoreCase: true, out var downButton))
                {
                    Status = "Invalid mouse button value.";
                    return null;
                }

                down.Button = downButton;
                break;
            case MouseUpAction up:
                if (!Enum.TryParse<MouseButton>(EditorMouseButton, ignoreCase: true, out var upButton))
                {
                    Status = "Invalid mouse button value.";
                    return null;
                }

                up.Button = upButton;
                break;
            case MouseClickAction click:
                if (!Enum.TryParse<MouseButton>(EditorMouseButton, ignoreCase: true, out var clickButton))
                {
                    Status = "Invalid mouse button value.";
                    return null;
                }

                click.Button = clickButton;
                break;
            case KeyDownAction keyDown:
                if (!int.TryParse(EditorScanCode, out var keyDownScanCode) || keyDownScanCode < 0)
                {
                    Status = "Invalid scan code value.";
                    return null;
                }

                keyDown.ScanCode = keyDownScanCode;
                break;
            case KeyUpAction keyUp:
                if (!int.TryParse(EditorScanCode, out var keyUpScanCode) || keyUpScanCode < 0)
                {
                    Status = "Invalid scan code value.";
                    return null;
                }

                keyUp.ScanCode = keyUpScanCode;
                break;
            case TextInputAction text:
                text.Text = EditorTextPayload;
                break;
        }

        return updated;
    }

    private List<MacroAction> BuildPlaybackPlan(double speedMultiplier)
    {
        var planned = new List<MacroAction>(Macro.Actions.Count);
        foreach (var action in Macro.Actions.OrderBy(x => x.TimeOffsetTicks))
        {
            var cloned = action.Clone();
            cloned.TimeOffsetTicks = (long)(cloned.TimeOffsetTicks / speedMultiplier);
            cloned.DurationTicks = (long)(cloned.DurationTicks / speedMultiplier);
            planned.Add(cloned);
        }

        return planned;
    }

    private void DeleteSelected()
    {
        if (SelectedAction is null)
        {
            return;
        }

        _timelineEditor.ApplyCommand(Macro, new RemoveActionCommand(SelectedAction.ActionId, EditMode));
        RefreshRows();
        Status = "Deleted selected action.";
    }

    private void InsertWaitAfterSelected()
    {
        if (SelectedAction is null)
        {
            return;
        }

        var insertAt = SelectedAction.TimeOffsetTicks + SelectedAction.DurationTicks;
        var wait = new WaitAction
        {
            TimeOffsetTicks = insertAt,
            DurationTicks = Stopwatch.Frequency / 4
        };

        _timelineEditor.ApplyCommand(Macro, new InsertActionCommand(wait, insertAt, EditMode));
        RefreshRows(selectActionId: wait.ActionId);
        Status = "Inserted wait action after selection.";
    }

    private void ToggleMode()
    {
        EditMode = EditMode == TimelineEditMode.Relative
            ? TimelineEditMode.Absolute
            : TimelineEditMode.Relative;
    }

    private void OnInputCaptured(object? sender, RawInputEvent e)
    {
        _recordingStart ??= e.TimestampTicks;

        if (e.EventType == RawInputEventType.MouseMove)
        {
            AddMousePoint(e);
            return;
        }

        FlushPendingMouseBundle();

        var offset = e.TimestampTicks - _recordingStart.Value;
        var action = ConvertToMacroAction(e, offset);
        if (action is null)
        {
            return;
        }

        _timelineEditor.ApplyCommand(Macro, new InsertActionCommand(action, action.TimeOffsetTicks, EditMode));
        RefreshRows(selectActionId: action.ActionId);
    }

    private void AddMousePoint(RawInputEvent e)
    {
        if (_pendingMousePoints.Count > 0)
        {
            var last = _pendingMousePoints[^1];
            if (e.TimestampTicks - last.TimestampTicks > MouseMoveIdleSplitTicks)
            {
                FlushPendingMouseBundle();
            }
        }

        _pendingMousePoints.Add(new MousePoint(e.TimestampTicks, e.X, e.Y));
    }

    private void FlushPendingMouseBundle()
    {
        if (_pendingMousePoints.Count == 0 || _recordingStart is null)
        {
            return;
        }

        var first = _pendingMousePoints[0];
        var last = _pendingMousePoints[^1];

        var action = new MouseMoveAction
        {
            TimeOffsetTicks = first.TimestampTicks - _recordingStart.Value,
            DurationTicks = Math.Max(0, last.TimestampTicks - first.TimestampTicks),
            X = last.X,
            Y = last.Y,
            MonitorId = 0
        };

        action.Metadata["PathPointCount"] = _pendingMousePoints.Count.ToString();
        action.Metadata["PathStart"] = $"{first.X},{first.Y}";
        action.Metadata["PathEnd"] = $"{last.X},{last.Y}";
        action.Metadata[PathPointsMetadataKey] = string.Join("|", _pendingMousePoints.Select(x => $"{x.TimestampTicks - first.TimestampTicks},{x.X},{x.Y}"));

        _timelineEditor.ApplyCommand(Macro, new InsertActionCommand(action, action.TimeOffsetTicks, EditMode));

        _pendingMousePoints.Clear();
        RefreshRows(selectActionId: action.ActionId);
    }

    private static MacroAction? ConvertToMacroAction(RawInputEvent raw, long offset)
    {
        return raw.EventType switch
        {
            RawInputEventType.MouseDown => new MouseDownAction
            {
                TimeOffsetTicks = offset,
                DurationTicks = 0,
                Button = MapButton(raw.Data)
            },
            RawInputEventType.MouseUp => new MouseUpAction
            {
                TimeOffsetTicks = offset,
                DurationTicks = 0,
                Button = MapButton(raw.Data)
            },
            RawInputEventType.KeyDown => new KeyDownAction
            {
                TimeOffsetTicks = offset,
                DurationTicks = 0,
                ScanCode = raw.Data
            },
            RawInputEventType.KeyUp => new KeyUpAction
            {
                TimeOffsetTicks = offset,
                DurationTicks = 0,
                ScanCode = raw.Data
            },
            _ => null
        };
    }

    private static MouseButton MapButton(int data)
    {
        return data switch
        {
            1 => MouseButton.Left,
            2 => MouseButton.Right,
            3 => MouseButton.Middle,
            _ => MouseButton.Left
        };
    }

    private void LoadTypeSpecificEditorValues(Guid actionId)
    {
        var action = Macro.Actions.FirstOrDefault(x => x.ActionId == actionId);
        if (action is null)
        {
            return;
        }

        switch (action)
        {
            case MouseMoveAction move:
                EditorMouseX = move.X.ToString();
                EditorMouseY = move.Y.ToString();
                EditorMonitorId = move.MonitorId.ToString();
                break;
            case MouseDownAction down:
                EditorMouseButton = down.Button.ToString();
                break;
            case MouseUpAction up:
                EditorMouseButton = up.Button.ToString();
                break;
            case MouseClickAction click:
                EditorMouseButton = click.Button.ToString();
                break;
            case KeyDownAction keyDown:
                EditorScanCode = keyDown.ScanCode.ToString();
                break;
            case KeyUpAction keyUp:
                EditorScanCode = keyUp.ScanCode.ToString();
                break;
            case TextInputAction text:
                EditorTextPayload = text.Text;
                break;
        }
    }

    private void RefreshRows(Guid? selectActionId = null)
    {
        Actions.Clear();
        foreach (var action in Macro.Actions)
        {
            Actions.Add(ActionRow.FromAction(action));
        }

        if (selectActionId is not null)
        {
            SelectedAction = Actions.FirstOrDefault(x => x.ActionId == selectActionId.Value);
        }
        else if (SelectedAction is not null)
        {
            SelectedAction = Actions.FirstOrDefault(x => x.ActionId == SelectedAction.ActionId);
        }

        OnPropertyChanged(nameof(ActionCount));
        UpdateCommandStates();
    }

    private void UpdateCommandStates()
    {
        StartRecordingCommand.RaiseCanExecuteChanged();
        StopRecordingCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        PlaybackCommand.RaiseCanExecuteChanged();
        StopPlaybackCommand.RaiseCanExecuteChanged();
        ApplyEditCommand.RaiseCanExecuteChanged();
        DeleteSelectedCommand.RaiseCanExecuteChanged();
        InsertWaitAfterSelectedCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private readonly record struct MousePoint(long TimestampTicks, int X, int Y);
}
