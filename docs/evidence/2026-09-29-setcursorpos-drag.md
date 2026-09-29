# Evidence: injected drags need real mouse-move input

- Session: a06990d1-e3bb-4a87-8bcd-ec3dcdeff981 · 2026-09-29
- Versions: Uno.Sdk 6.8.0-dev.23 (Uno.WinUI 6.8.0-dev.87), SkiaSharp 3.119.2, `net10.0-desktop`, Windows 11 Win32 host
- Gotcha: a Win32 harness drag built from `SetCursorPos` steps delivers press/release but no PointerMoved

## Symptom
Harness drag (`mouse_event` down → 24 × `SetCursorPos` → `mouse_event` up) over the harbour scene left the camera unchanged; two captures were identical. Tag clicks and preset clicks through the same harness worked.

## Probe
A DEBUG trace in `HarbourScene` pointer handlers, same drag:

```
19:52:23.146 pressed 1 [592, 363]
19:52:23.829 released 1 drag=0
```

Press and release arrive; zero `moved` lines. The app's input path is live; the harness produces no move input.

## Fix that held
Each step injected as real input: `mouse_event(0x8001 /* MOVE|ABSOLUTE */, x*65535/GetSystemMetrics(0), y*65535/GetSystemMetrics(1))`.

```
19:53:03.365 pressed 1 [592, 363]
19:53:03.381 moved 1 [608, 360] tracked=True contact=True
...
19:53:04.085 moved 1 [991, 312] tracked=True contact=True
19:53:04.117 released 1 drag=450
```

25 moves, 450 px of travel, and the capture shows the camera orbited. The trace was removed after the probe.
