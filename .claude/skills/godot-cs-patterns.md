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

## Display Server (for desktop pet)
```csharp
DisplayServer.WindowSetMousePassthrough(vector2Array);
DisplayServer.ScreenGetSize(screenIndex);
```

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
- `InputPickable = true` required for `mouse_entered`/`mouse_exited` signals on PhysicsBody2D
