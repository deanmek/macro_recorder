using System.Collections.ObjectModel;
using System.Diagnostics;
using MacroRecorder.Core.Serialization;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MacroRecorder.App.Models;
using MacroRecorder.Core.Domain;
using MacroRecorder.Core.Editing;
using MacroRecorder.Input;

namespace MacroRecorder.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private static readonly long MouseMoveIdleSplitTicks = Stopwatch.Frequency / 8;

    private readonly IGlobalInputHookService _inputHookService;
    private readonly TimelineEditor _timelineEditor = new();
    private readonly IInputPlaybackService _inputPlaybackService;
    private readonly List<MousePoint> _pendingMousePoints = new();
    private long? _recordingStart;
    private bool _isRecording;
    private bool _isPlaying;
    private string _status = "Idle";

    public MainWindowViewModel(IGlobalInputHookService inputHookService, IInputPlaybackService inputPlaybackService)
    {
        _inputHookService = inputHookService;
        _inputPlaybackService = inputPlaybackService;
        _inputHookService.InputCaptured += OnInputCaptured;

        StartRecordingCommand = new RelayCommand(StartRecording, () => !IsRecording);
        StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
        UndoCommand = new RelayCommand(Undo, () => Macro.UndoStack.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => Macro.RedoStack.Count > 0);
        ToggleModeCommand = new RelayCommand(ToggleMode);
        PlaybackCommand = new RelayCommand(Playback, () => !IsRecording && !IsPlaying && Macro.Actions.Count > 0);

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

    public void Dispose()
    {
        _inputHookService.InputCaptured -= OnInputCaptured;
        _inputHookService.Dispose();
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

        IsPlaying = true;
        Status = "Playing...";

        try
        {
            await _inputPlaybackService.PlaybackAsync(Macro.Actions);
            Status = "Playback complete.";
        }
        catch (Exception ex)
        {
            Status = $"Playback failed: {ex.Message}";
        }
        finally
        {
            IsPlaying = false;
        }
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

        var command = new InsertActionCommand(action, action.TimeOffsetTicks, EditMode);
        _timelineEditor.ApplyCommand(Macro, command);

        RefreshRows();
        UpdateCommandStates();
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

        var command = new InsertActionCommand(action, action.TimeOffsetTicks, EditMode);
        _timelineEditor.ApplyCommand(Macro, command);

        _pendingMousePoints.Clear();
        RefreshRows();
        UpdateCommandStates();
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

    private void RefreshRows()
    {
        Actions.Clear();
        foreach (var action in Macro.Actions)
        {
            Actions.Add(ActionRow.FromAction(action));
        }

        OnPropertyChanged(nameof(ActionCount));
    }

    public int ActionCount => Macro.Actions.Count;

    private void UpdateCommandStates()
    {
        StartRecordingCommand.RaiseCanExecuteChanged();
        StopRecordingCommand.RaiseCanExecuteChanged();
        UndoCommand.RaiseCanExecuteChanged();
        RedoCommand.RaiseCanExecuteChanged();
        PlaybackCommand.RaiseCanExecuteChanged();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private readonly record struct MousePoint(long TimestampTicks, int X, int Y);
}
