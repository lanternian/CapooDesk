---
name: spine-animation
description: Use when working with Spine animations, CapooView, Capoo_Action.json, or any animation-related code
---

# Spine Animation in Capoo Project

Runtime: spine-godot **4.1** (custom Godot 4.2.1 mono build). Assets are Spine 4.1.23 JSON — do not upgrade the runtime.

## Spine-Godot C# API (as used here)

```csharp
sprite.SkeletonDataRes = GD.Load<SpineSkeletonDataResource>("res://assets/spine/body/capoo_basic_001.res");
var state = sprite.GetAnimationState();
state.SetAnimation(animName, loop, trackIndex);            // name first, then loop, then track
state.AddAnimation(animName, delay, loop, trackIndex);
state.ClearTrack(trackIndex);
sprite.SkeletonDataRes.FindAnimation(name)?.GetDuration(); // animation length
sprite.GetSkeleton().GetBounds();                           // AABB of the current pose, sprite-local
sprite.GetGlobalBoneTransform("talk").Origin;               // bone position in global coords
skeleton.SetSkinByName("poo_01"); skeleton.SetToSetupPose();
```

`.res` files (SpineSkeletonDataResource) are generated from `.spine-json` + `.atlas` by `batch_create_spine_resources.gd`.

## Component layers (CapooView)

| Component | Bone | Resource dir |
|-----------|------|-------------|
| body | (the CapooView itself) | spine/body/ |
| face | body_face_root | spine/face/ |
| face_decoration | mask | spine/mask/ |
| hat | hat | spine/hat/ |
| clothes | clothes | spine/clothes/ |
| back | back (behind the body when back_hierarchy == "0") | spine/back/ |
| right_hand_item | prop_r | spine/prop_r/ |
| capoo_acc_top/body/bot | root | spine/capoo_acc_top/, capoo_acc_body/, capoo_acc_bot/ |
| mood | body_mood | spine/mood/ |
| efx_bot | root (behind the body) | spine/efx_bot/ |
| efx_top | root | spine/efx_top/ |
| efx_appears | root | spine/efx_appear/ |
| talk | talk | spine/char_talk/ |
| food | root | spine/food/ |
| poo | root (skin poo_01) | spine/poo/ |

Components are SpineSprites (scale 2.0) under SpineBoneNodes that follow body bones. Facing = sign of
CapooView.Scale.X (the art faces left); the talk sprite is counter-flipped so "?" stays readable.

## Actions

`Capoo_Action.json` (80 official) + `Capoo_Action_Extra.json` (ours: sleepy1, sleep1-2, wakeUp1-2, hungry1-2,
waitCleanPooTalk, comfortable1-2, hop) — same schema, merged by CapooConfig into a dictionary.

`CapooView.SetAnimation(action)` follows the original app:
- body/face: set when non-empty
- mood, effectTop, effectBot, talk: **empty = hide_all**, so bubbles and effects never linger
- appear, food, poo: only touched when non-empty (hide them explicitly with `HideLayer(Layer.Food/Poo)`)
- unknown action name → warning, returns false (never throws)

`SetLayer(layer, anim, loop)` plays one raw layer animation (e.g. an extra "bump" on EffectBot).

## Pitfall: SpineAttachment wrappers (spine-godot 4.1 bug)
`SpineSlot.GetAttachment()` (and `GetAttachmentBySlot*`) returns a `SpineAttachment` that never calls
`reference()` on the attachment it wraps, but its destructor calls `dereference()`
(`spine-godot/spine_godot/SpineAttachment.cpp:39`). Every wrapper freed with its pointer intact corrupts the
attachment's reference count, and one freed after the skeleton data is gone (e.g. by .NET's shutdown
clean-up) touches freed memory → `Fatal error 0xC0000005 ... godotsharp_internal_refcounted_disposed`.
After reading what you need, fire `SkeletonDataRes.EmitSignal("_internal_spine_objects_invalidated")`
(only these wrappers listen to it; they drop their pointer) and `Dispose()` the wrappers right away —
see `CapooView.ReleaseSpineWrappers`. Every `GetSlots()` call also creates new `SpineSlot` wrappers that
connect a signal on the sprite: dispose them too.

## Performance notes
- Overlay sprites (mood, efx, talk, food, poo, appear) are frozen (`ProcessMode = Disabled`, hidden) two frames
  after they go to hide_all, and woken by the next animation on that layer.
- Book thumbnails are CapooViews with processing disabled until hovered.

## Durations (body, capoo_basic_001)
click 1.5, idle_basic 2, walk_basic 0.83, run 0.4, sprint 0.43, roll 0.67, jump_down 0.67, jump_up 0.33,
pet 2, poo 2.33, grasp 2, prone 1, chew 0.33; `fall` is a pose (0 s).
