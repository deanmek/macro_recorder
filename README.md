# Macro Recorder

This repository now contains a runnable implementation scaffold for a precision-first macro recorder/replayer in C# with a WPF test UI.

## Solution Layout

- `src/MacroRecorder.Core`
  - Domain model (`Macro`, `MacroAction` variants)
  - Two-tier versioning primitives (undo/redo + detailed history)
  - Timeline editor with relative/absolute editing behavior
  - `MCR1` binary serializer/deserializer
- `src/MacroRecorder.Input`
  - Global low-level keyboard/mouse hooks (`SetWindowsHookEx`)
  - High-resolution timestamp capture (`Stopwatch.GetTimestamp`)
- `src/MacroRecorder.App`
  - WPF UI scaffold for direct functionality testing
  - Start/Stop recording buttons, Undo/Redo, Relative/Absolute mode toggle
  - Live action timeline grid for captured events

## Current State

- Relative timeline mode is the default.
- Absolute mode is available via the toggle in the UI.
- The WPF app is designed as a direct testing harness for recording hooks + timeline operations.

## Next Steps

- Add action inspector editing (double-click to modify action properties)
- Add macro playback engine
- Add save/load integration in the WPF UI using `BinaryMacroSerializer`
