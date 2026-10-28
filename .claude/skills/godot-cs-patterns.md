---
name: godot-cs-patterns
description: Use when creating new C# scripts, node structures, scene files, or working with Godot-specific APIs
---

# Godot 4.2 C# Patterns for This Project

## Node Class Requirements
```csharp
// ALL Godot nodes must be:
// 1. public partial class
// 2. No namespace
// 3. Extend appropriate Godot type
public partial class MyNode : CharacterBody2D { }
public partial class MyNode : Node { }
public partial class MyNode : Node2D { }
public partial class MyNode : CanvasLayer { }
```

## Lifecycle Methods
```csharp
public override void _EnterTree() { }    // Added to scene tree
public override void _Ready() { }         // All children ready
public override void _Process(double delta) { }       // Every frame (logic)
public override void _PhysicsProcess(double delta) { } // Physics step (fixed)
```

## Signal Patterns
```csharp
// Define signal
[Signal]
public delegate void StateChangedEventHandler(CapooState newState);

// Emit
EmitSignal(SignalName.StateChanged, (int)newState);

// Connect (Godot 4 style)
node.Connect("signal_name", Callable.From(() => { }));

// Built-in signal connection
Connect("mouse_entered", Callable.From(() => { isMouseOver = true; }));
```

## Autoload Access
```csharp
var config = GetNode<CapooConfig>("/root/CapooConfig");
```

## Physics (CharacterBody2D)
```csharp
float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
Velocity += new Vector2(0, gravity * (float)delta);
MoveAndSlide();
```

## Input
```csharp
Input.IsActionJustPressed("left_click");
Input.IsActionJustReleased("left_click");
Input.GetVector("left", "right", "up", "down");
GetGlobalMousePosition();
Input.GetLastMouseVelocity();
```

## Desktop overlay (DesktopWindow.cs)
```csharp
var window = DesktopWindow.Instance;
window.CursorWorld;                  // cursor in world coords, polled every frame (works while click-through)
window.AddHitTester(p => ...);       // world point -> should the window take clicks here?
window.RegisterInteractive(control); // UI that must receive clicks while visible
window.ForceOpaque = true;           // while dragging something
DisplayServer.ScreenGetUsableRect(screen); // work area (the floor is the taskbar top)
```
Click-through = WS_EX_LAYERED|WS_EX_TRANSPARENT toggled by hit testing. Godot rewrites GWL_EXSTYLE whenever its
window flags change, so DesktopWindow re-applies its styles (Maintain()). Don't use WindowSetMousePassthrough: it
clips rendering to the region.

## Scene Instancing (when needed)
```csharp
// Programmatic (current pattern)
var node = new MyNode();
parent.AddChild(node);

// From .tscn (if created later)
var scene = GD.Load<PackedScene>("res://scenes/my_scene.tscn");
var instance = scene.Instantiate();
parent.AddChild(instance);
```

## Important Gotchas

- `delta` in `_Process` and `_PhysicsProcess` is `double`, cast to `float` for Vector2 math
- Godot 4 uses `Callable.From()` for signal connections, NOT old signal string syntax
- Use `GD.Load<T>()` not `ResourceLoader.Load()` for resources
- CollisionPolygon2D vertices are in LOCAL coordinates
- Never declare global-namespace classes named like Godot types (`Node2D`, `CharacterBody2D`, ...): they silently
  shadow `using Godot;`. Likewise `Mutex` is ambiguous between Godot and System.Threading.
- Partial classes may span files (CapooController.cs / .Behavior.cs / .Interaction.cs)
- Pet mouse input goes through PetManager (`HandleLeftPress/Release`), not `Input.IsActionJustPressed` — otherwise
  every pet reacts to the same click
- Nodes created in code only get `_Ready` on AddChild if the parent is already in the tree: add first, then configure
  Spine children
