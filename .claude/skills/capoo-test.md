---
name: capoo-test
description: Use when writing tests, verifying features, running benchmarks, or creating test scenes
---

# Capoo Testing & Verification

Godot binary: `D:/app/Godot/godot-editor-windows-mono/godot-4.1-4.2.1-stable-mono.exe` (on PATH as `godot4`).
Everything after `--` is a user argument read by Main / AppSettings / DesktopWindow.

## L1: build
```bash
dotnet build    # must be 0 errors, 0 warnings
```

## L2: scripted self-tests (SelfTest.cs, virtual cursor — never moves the real mouse)
```bash
godot4 --path D:/playground/CapooDesk -- --test --selftest     # click-through, hit, petting, drag/throw, stacking, bump,
                                                              # hungry/eat, poo/clean, nap/wake, accessory hit-test,
                                                              # book UI captures, away->sleep->wake, recycle
godot4 --path D:/playground/CapooDesk -- --test --windowtest   # needs a top-level window titled "CapooTestWindow":
                                                              # platform detection, landing, edge walk, carry, fall on close
godot4 --path D:/playground/CapooDesk -- --test --perftest     # CPU % of one core for 0/1/5/10/20 pets
```
- Output lines `[selftest] PASS|FAIL ...`, summary `[selftest] done: N passed, M failed`; exit code 1 on failure.
- Captures: `%APPDATA%/CapooDesk/selftest_NN_<name>.png` (the viewport only, not the user's screen).
- `--test` uses `user://settings_test.json` instead of the real settings. Put `"HideOnFullscreen": false` there when a
  fullscreen app is in front, otherwise the pets are auto-hidden.
- Other flags: `--debug-overlay` (outlines, states, platforms, FPS), `--spawn=N`, `--tmode=alpha|colorkey`.
- Test window: a PowerShell WinForms form with `TopMost` + `WS_EX_NOACTIVATE` (doesn't steal focus).

`Performance.Monitor.TimeProcess` reports the whole frame period here (≈16.7 ms at 60 fps even with 0 pets) —
measure real CPU with `Process.TotalProcessorTime` (see SelfTest.RunPerf).

## L3: exported build
```bash
godot4 --headless --path D:/playground/CapooDesk --export-release "CapooDesk" D:/program/Godot/build/CapooDesk/CapooDesk.exe
D:/program/Godot/build/CapooDesk/CapooDesk.exe -- --test --selftest
```
New asset files (sounds, images) must be imported before running from the CLI; `--export-pack "CapooDesk" <tmp>.pck`
performs the import.

## Needs a human
- Drag/throw feel, rubbing threshold, walk speed vs. foot animation
- Tray icon menu (real right-click on the notification area)
- Multi-monitor and DPI changes
