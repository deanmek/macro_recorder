# Macro Recorder

This repository contains a precision-first macro recorder/replayer scaffold in C# with a WPF testing UI.

## Solution Layout

- `src/MacroRecorder.Core`
  - Domain model (`Macro`, `MacroAction` variants)
  - Two-tier versioning primitives (undo/redo + detailed history)
  - Timeline editor with relative/absolute editing behavior
  - `MCR1` binary serializer/deserializer
- `src/MacroRecorder.Input`
  - Global low-level keyboard/mouse hooks (`SetWindowsHookEx`)
  - Playback injection service (`SendInput`) for mouse and keyboard actions
  - High-resolution timestamp capture (`Stopwatch.GetTimestamp`)
- `src/MacroRecorder.App`
  - WPF UI test harness for direct functionality testing
  - Start/Stop recording, Undo/Redo, Relative/Absolute mode toggle
  - Playback button for immediate replay testing
  - Save/Load buttons for `.mcr1` macro files

## Current State

- Relative timeline mode is the default.
- Mouse movement is bundled into single timeline entries, split by input boundaries and idle gaps.
- Recorded macros can be saved/loaded in `MCR1` format.
- Basic playback for move/click/key actions is wired and testable.

## Next Steps

- Add action inspector editing (double-click to modify action properties)
- Preserve full bundled mouse path points for path-accurate playback
- Expand playback controls (cancel, speed multiplier, loop)
