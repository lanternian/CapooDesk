---
name: capoo-physics
description: Use when working on collision detection, stacking mechanics, multi-entity physics, or screen boundary systems
---

# Capoo Physics System

## Current Collision Setup
- CapooController (CharacterBody2D) with 2 hardcoded CollisionPolygon2D:
  - `capoo_collision`: 12-vertex body polygon
  - `talk_collision`: 12-vertex speech bubble polygon
- `InputPickable = true` for mouse detection

## Collision Layer Design (Planned)
- **Layer 1**: Capoo bodies (Capoo-to-Capoo collision)
- **Layer 2**: Interaction zones (speech bubbles, food areas)
- **Layer 3**: World boundaries (invisible walls at screen edges)

## Screen Boundary
```csharp
var screenSize = DisplayServer.ScreenGetSize(0);
// Capoo's world position should stay within screen bounds
// Account for Capoo's collision polygon extents
```

## Stacking Mechanics (Reference: Capoo Pals supports 12)
- Detect Capoo A landing on Capoo B from above via `GetSlideCollision()`
- Capoo A enters Stacked state, position-locked to Capoo B
- Animations: `stackFog1` (bottom), `stackDown1-3` (reaction), `prone1-4` (lying flat)
- Movement speed decreases with stack size
- Click stacked Capoo to remove from stack

## CharacterBody2D vs RigidBody2D
- Current: CharacterBody2D (controlled movement via MoveAndSlide)
- Stacked Capoos: consider switching to kinematic child of parent Capoo
- Thrown state: CharacterBody2D with velocity + gravity (current approach works)

## Merge System (Planned)
- When two specific variants collide, both play `merge` animation
- Spawn new variant at collision point
- Merge rules defined in Capoo_Merge.json

## Important Gotchas
- `MoveAndSlide()` handles collision response automatically
- Check `IsOnFloor()` for landing detection
- `GetSlideCollision(index)` for detailed collision info
- CharacterBody2D doesn't respond to forces — all movement must be set via Velocity
