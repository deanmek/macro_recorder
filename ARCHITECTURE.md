# Macro Recorder/Replayer Architecture (WPF, C#)

This document captures the core domain model, timeline editing rules, and the binary serialization format for a high-precision macro recorder/replayer. It also includes a two-tiered versioning/undo design: quick back/forward UI controls and a detailed history log.

## 1. C# Domain Model (Data Structures)

### 1.1 Macro and Timeline

```csharp
public sealed class Macro
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "New Macro";
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Ordered by TimeOffsetTicks
    public List<MacroAction> Actions { get; } = new();

    // Versioning
    public long Version { get; set; } = 0;

    // Two-tier versioning:
    // 1) Quick back/forward (bounded stack of commands)
    public LimitedStack<IEditCommand> UndoStack { get; } = new(maxSize: 200);
    public LimitedStack<IEditCommand> RedoStack { get; } = new(maxSize: 200);

    // 2) Detailed history log (append-only list of change records)
    public List<HistoryEntry> History { get; } = new();
}

public enum TimelineEditMode
{
    Relative, // Default (shifts later actions)
    Absolute  // Optional setting (no shifting)
}
```

### 1.2 Action Base and Derived Types

All time values are stored in **ticks from a monotonic clock** (e.g., `Stopwatch` ticks) to preserve accuracy.

```csharp
public abstract class MacroAction
{
    public Guid ActionId { get; init; } = Guid.NewGuid();
    public long TimeOffsetTicks { get; set; }
    public long DurationTicks { get; set; }
    public ActionType Type { get; init; }
    public Dictionary<string, string> Metadata { get; } = new();
}

public enum ActionType
{
    MouseMove,
    MouseDown,
    MouseUp,
    MouseClick,
    KeyDown,
    KeyUp,
    TextInput,
    Wait
}

public sealed class MouseMoveAction : MacroAction
{
    public int X { get; set; }
    public int Y { get; set; }
    public int MonitorId { get; set; }
}

public sealed class MouseDownAction : MacroAction
{
    public MouseButton Button { get; set; }
}

public sealed class MouseUpAction : MacroAction
{
    public MouseButton Button { get; set; }
}

public sealed class MouseClickAction : MacroAction
{
    public MouseButton Button { get; set; }
}

public sealed class KeyDownAction : MacroAction
{
    public int ScanCode { get; set; }
}

public sealed class KeyUpAction : MacroAction
{
    public int ScanCode { get; set; }
}

public sealed class TextInputAction : MacroAction
{
    public string Text { get; set; } = string.Empty;
}

public sealed class WaitAction : MacroAction
{
    // Uses DurationTicks; no extra fields.
}

public enum MouseButton
{
    Left,
    Right,
    Middle,
    X1,
    X2
}
```

### 1.3 Two-Tier Versioning Structures

#### Quick Back/Forward (Undo/Redo)

```csharp
public interface IEditCommand
{
    string Description { get; }
    void Apply(Macro macro);
    void Undo(Macro macro);
}

public sealed class LimitedStack<T>
{
    private readonly int _maxSize;
    private readonly LinkedList<T> _items = new();

    public LimitedStack(int maxSize) => _maxSize = maxSize;

    public void Push(T item)
    {
        _items.AddLast(item);
        if (_items.Count > _maxSize)
        {
            _items.RemoveFirst();
        }
    }

    public bool TryPop(out T? item)
    {
        if (_items.Count == 0)
        {
            item = default;
            return false;
        }

        var last = _items.Last!;
        item = last.Value;
        _items.RemoveLast();
        return true;
    }

    public void Clear() => _items.Clear();
}
```

#### Detailed History (VSCode-style)

```csharp
public sealed class HistoryEntry
{
    public Guid EntryId { get; init; } = Guid.NewGuid();
    public long Version { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public string Summary { get; init; } = string.Empty;
    public IReadOnlyList<HistoryDelta> Deltas { get; init; } = Array.Empty<HistoryDelta>();
}

public sealed class HistoryDelta
{
    public Guid ActionId { get; init; }
    public string Field { get; init; } = string.Empty;
    public string? Before { get; init; }
    public string? After { get; init; }
}
```

## 2. Timeline Editing Rules (Pseudocode)

### 2.1 Insert Action (Relative Mode Default)

```text
function insertAction(macro, newAction, insertAtTicks, mode):
    newAction.TimeOffsetTicks = insertAtTicks
    index = first action where action.TimeOffsetTicks > insertAtTicks
    if index not found:
        append newAction
    else:
        insert newAction at index

    if mode == Relative:
        shiftDelta = newAction.DurationTicks
        for each action after newAction:
            action.TimeOffsetTicks += shiftDelta
```

### 2.2 Insert Action (Absolute Mode)

```text
function insertAction(macro, newAction, insertAtTicks, mode):
    newAction.TimeOffsetTicks = insertAtTicks
    index = first action where action.TimeOffsetTicks > insertAtTicks
    insert or append newAction
    // No shifting
```

### 2.3 Modify Duration (Relative Mode)

```text
function updateDuration(macro, actionId, newDurationTicks, mode):
    action = find action by actionId
    delta = newDurationTicks - action.DurationTicks
    action.DurationTicks = newDurationTicks

    if mode == Relative:
        for each action after action:
            action.TimeOffsetTicks += delta
```

### 2.4 Move Action (Relative Mode)

```text
function moveAction(macro, actionId, newTimeOffsetTicks, mode):
    action = find action by actionId
    oldTime = action.TimeOffsetTicks
    action.TimeOffsetTicks = newTimeOffsetTicks

    if mode == Relative:
        // Shift intermediate actions to preserve ordering and spacing
        delta = newTimeOffsetTicks - oldTime
        for each action between oldTime and newTimeOffsetTicks:
            action.TimeOffsetTicks -= delta
```

### 2.5 Undo/Redo Rules

```text
function applyCommand(macro, command):
    command.Apply(macro)
    macro.UndoStack.Push(command)
    macro.RedoStack.Clear()
    macro.Version += 1
    macro.History.Add(createHistoryEntry(command, macro.Version))

function undo(macro):
    if macro.UndoStack.TryPop(out command):
        command.Undo(macro)
        macro.RedoStack.Push(command)
        macro.Version += 1
        macro.History.Add(createHistoryEntry(command, macro.Version))

function redo(macro):
    if macro.RedoStack.TryPop(out command):
        command.Apply(macro)
        macro.UndoStack.Push(command)
        macro.Version += 1
        macro.History.Add(createHistoryEntry(command, macro.Version))
```

## 3. Binary Serialization Schema (Detailed)

### 3.1 Binary Goals
- Preserve exact timing (Int64 ticks).
- Forward compatible (unknown action types can be skipped).
- Minimal storage overhead.

### 3.2 File Layout

```text
[Header]
  Magic           4 bytes  // 'MCR1'
  FormatVersion   2 bytes  // UInt16
  Flags           2 bytes  // UInt16 (reserved)
  CreatedAtUtc    8 bytes  // Int64 (Unix epoch ticks)
  UpdatedAtUtc    8 bytes  // Int64 (Unix epoch ticks)
  MacroId         16 bytes // Guid
  NameLength      4 bytes  // Int32
  NameUtf8        N bytes

[ActionBlock]
  ActionCount     4 bytes  // Int32
  Repeated ActionCount times:
    ActionType    2 bytes  // UInt16
    PayloadLength 4 bytes  // Int32
    Payload       N bytes  // type-specific

[MetadataBlock] (optional)
  MetadataCount   4 bytes  // Int32
  Repeated:
    KeyLength     4 bytes
    KeyUtf8       N bytes
    ValueLength   4 bytes
    ValueUtf8     N bytes
```

### 3.3 Action Payloads (All use Int64 ticks)

Each payload begins with the common fields, followed by type-specific fields.

```text
CommonPayload:
  ActionId        16 bytes // Guid
  TimeOffsetTicks 8 bytes  // Int64
  DurationTicks   8 bytes  // Int64

MouseMovePayload:
  CommonPayload
  X              4 bytes  // Int32
  Y              4 bytes  // Int32
  MonitorId      4 bytes  // Int32

MouseDownPayload / MouseUpPayload / MouseClickPayload:
  CommonPayload
  Button         1 byte   // UInt8

KeyDownPayload / KeyUpPayload:
  CommonPayload
  ScanCode       4 bytes  // Int32

TextInputPayload:
  CommonPayload
  TextLength     4 bytes  // Int32
  TextUtf8       N bytes

WaitPayload:
  CommonPayload
```

### 3.4 Forward Compatibility

Unknown action types can be skipped using the `PayloadLength`.
New versions can add fields inside payloads by:
1. Bumping `FormatVersion`.
2. Appending new fields (readers can ignore extra bytes).

---

## 4. Next Implementation Step (After This Document)

1. Scaffold WPF solution and projects.
2. Add the domain model from section 1.
3. Implement timeline editing rules from section 2.
4. Implement binary serialization reader/writer from section 3.
5. Build the timeline UI and action inspector.
