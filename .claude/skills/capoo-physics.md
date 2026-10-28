---
name: capoo-physics
description: Use when working on collision detection, stacking mechanics, multi-entity physics, or screen boundary systems
---

# Capoo Physics System

## World (DesktopWorld.cs)
The overlay window covers the **work area** of one screen; world coords = screen pixels − `DesktopWindow.Origin`.

| Layer | Bit | What |
|---|---|---|
| World | 1 | `WorldBoundaryShape2D` floor (= top of the taskbar), ceiling, left/right walls |
| Platform | 2 | one-way `SegmentShape2D` per visible top-edge span of other windows; StaticBody2D meta `"hwnd"` |
| Pet | 4 | CapooController bodies (mask World\|Platform — pets never collide physically with each other) |

WindowTracker enumerates windows front to back (DWM extended frame bounds; skips cloaked, minimized, tool, shell,
own and WS_EX_TRANSPARENT overlay windows), subtracts occluding windows from each top edge and ignores maximized
windows. Full rescan every 0.25 s; windows with Capoos on them are polled every physics tick so the pets ride along
(`PetManager.CarryPetsOn`).

## Capoo body (CapooController.cs)
- Origin at the feet. Physics shape: RectangleShape2D 0.55×0.8 of the body outline. Mouse picking: the outline polygon
  (× scale × collider_Adjust) plus the accessory skeletons' bounds.
- Gravity 2200 px/s², max fall 4000, throws clamped to 3500 px/s.
- Ground states set X velocity from the walk/run speed; airborne states (Air/Roll/Hop) bounce off walls (−0.45 X).
- Resting pets skip MoveAndSlide and only `TestMove` downwards every 8 ticks; no ground for >0.1 s → Air.
- Landing plays `fall`; impact > 2500 → dizzy_dizzy, > 3300 → dizzy_soul.
- Walking on a window: a raycast ahead finds the edge → 50% stop (Idle) / 50% hop down.

## Pet ↔ pet (PetManager.cs, runs after the pets: process priority 10)
- **Stacking**: a falling Capoo whose feet cross another's `HeadY` (|dx| < 0.3·width) attaches on top (`LandOnHead`):
  the upper one becomes Stacked (stackFog1 → prone), the lower one plays stackDown1-3. Max 12 high. The chain follows
  the bottom Capoo every frame (`FollowStack`), also while it is dragged or thrown. The top Capoo hops off after
  25–70 s. Recycling a middle one drops the ones above.
- **Bump**: an airborne Capoo faster than 1100 px/s overlapping a grounded one → the target plays dizzy1-4 + bump,
  the thrower bounces back.
- **Separation**: resting Capoos on the same floor that overlap a lot slide apart at ≤30 px/s.
