---
name: capoo-test
description: Use when writing tests, verifying features, running benchmarks, or creating test scenes
---

# Capoo Testing & Verification

## 3-Layer Verification System

### L1: dotnet build (automated)
```bash
dotnet build    # Must pass with 0 errors
```

### L2: Programmatic Tests (automated via Godot headless)
```bash
godot4 --path D:/program/Godot/capoo --headless --scene res://tests/test_runner.tscn
```

Test categories:
- **Config loading**: CapooInfo 126 entries, CapooAction 80 entries, all fields valid
- **State machine**: valid transitions accepted, invalid transitions rejected
- **Needs system**: timer increments, thresholds trigger correct states
- **Animation mapping**: all action names resolve to valid CapooAction entries

Results written to `tests/results.json`.

### L3: Screenshot Visual Analysis (semi-automated)
```bash
# Launch Godot, wait, screenshot
godot4 --path D:/program/Godot/capoo &
# PowerShell screenshot + analyze_image MCP tool
```

## Performance Benchmarks
Target: 60 FPS with 12 Capoos all animating

Monitor via (from test.cs):
```csharp
Performance.GetMonitor(Performance.Monitor.TimeFps);
Performance.GetMonitor(Performance.Monitor.TimeProcess);
Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess);
Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame);
```

## Items Requiring Human Confirmation
- Window transparency (Win32 color-key on actual desktop)
- Drag feel (mouse follow latency, throw physics feel)
- Animation smoothness (visual 60fps vs 30fps)
- Mouse passthrough regions (clickable on Capoo, passthrough on transparent)
