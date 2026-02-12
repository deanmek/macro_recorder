using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MacroRecorder.App.Models;
using MacroRecorder.Core.Domain;
using MacroRecorder.Core.Editing;
using MacroRecorder.Input;

namespace MacroRecorder.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IGlobalInputHookService _inputHookService;
    private readonly TimelineEditor _timelineEditor = new();
    private long? _recordingStart;
    private bool _isRecording;
    private string _status = "Idle";

    public MainWindowViewModel(IGlobalInputHookService inputHookService)
    {
        _inputHookService = inputHookService;
        _inputHookService.InputCaptured += OnInputCaptured;

        StartRecordingCommand = new RelayCommand(StartRecording, () => !IsRecording);
        StopRecordingCommand = new RelayCommand(StopRecording, () => IsRecording);
        UndoCommand = new RelayCommand(Undo, () => Macro.UndoStack.Count > 0);
        RedoCommand = new RelayCommand(Redo, () => Macro.RedoStack.Count > 0);
        ToggleModeCommand = new RelayCommand(ToggleMode);

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
        _inputHookService.Start();
        IsRecording = true;
        Status = "Recording...";
    }

    private void StopRecording()
    {
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

    private void ToggleMode()
    {
        EditMode = EditMode == TimelineEditMode.Relative
            ? TimelineEditMode.Absolute
            : TimelineEditMode.Relative;
    }

    private void OnInputCaptured(object? sender, RawInputEvent e)
    {
        _recordingStart ??= e.TimestampTicks;
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

    private static MacroAction? ConvertToMacroAction(RawInputEvent raw, long offset)
    {
        return raw.EventType switch
        {
            RawInputEventType.MouseMove => new MouseMoveAction
            {
                TimeOffsetTicks = offset,
                DurationTicks = 0,
                X = raw.X,
                Y = raw.Y,
                MonitorId = 0
            },
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
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
