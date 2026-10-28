---
name: spine-animation
description: Use when working with Spine animations, CapooView, Capoo_Action.json, or any animation-related code
---

# Spine Animation in Capoo Project

## Spine-Godot C# API

```csharp
// Core types
SpineSprite              // Main Spine node, extends Node2D
SpineBoneNode            // Attaches child nodes to Spine bones
SpineSkeletonDataResource // Skeleton data (.res files)
SpineAnimationState      // Controls animation playback

// Key methods on SpineSprite
spineSprite.GetSkeleton()
spineSprite.GetAnimationState()
spineSprite.SkeletonDataRes = GD.Load<SpineSkeletonDataResource>(path)

// Key methods on SpineAnimationState
animState.SetAnimation(track, animName, loop)  // Returns SpineTrackEntry
animState.AddAnimation(track, animName, loop, delay)
animState.ClearTrack(trackIndex)
animState.SetEmptyAnimation(trackIndex, mixDuration)

// Skeleton methods
skeleton.FindBone("boneName")
skeleton.SetSkinByName("skinName")
skeleton.SetToSetupPose()
```

## 13-Layer Component Architecture

Each Capoo consists of layers attached to named bones:

| Component | Bone | Resource Dir |
|-----------|------|-------------|
| body | (root) | spine/body/ |
| face | body_face_root | spine/face/ |
| faceDecoration | mask | spine/face_decoration/ |
| hat | hat | spine/hat/ |
| clothes | clothes | spine/clothes/ |
| back | back | spine/back/ |
| rightHandItem | prop_r | spine/prop_r/ |
| accTop | root | spine/acc/ |
| accBody | root | spine/acc/ |
| accBot | root | spine/acc/ |
| mood | body_mood | spine/mood/ |
| efxBot | root | spine/efx_bot/ |
| efxTop | root | spine/efx_top/ |
| efxAppear | root | spine/efx_appear/ |
| talk | talk | spine/char_talk/ |
| food | root | spine/food/ |
| poo | root | spine/poo/ |

## Animation Action JSON Schema

Each action in Capoo_Action.json maps to multiple layers:
```json
{
  "action_name": "idle_idle1",
  "body": "idle_basic", "body_loop": "true",
  "face": "emoji", "face_loop": "true",
  "mood": "", "mood_loop": "",
  "effectTop": "", "effectTop_loop": "",
  "effectBot": "", "effectBot_loop": "",
  "talk": "", "talk_loop": "",
  "appear": "", "appear_loop": "",
  "food": "", "food_loop": "",
  "poo": "", "poo_loop": ""
}
```

Empty string = don't change that layer.

## Important Gotchas

- **Always call `ClearAnimation()` before `SetAnimation()`** to prevent track conflicts
- `AddAnimation()` queues after current; use for animation chains
- Component visibility: if a CapooInfo field is empty (""), that component is NOT created — don't try to access its animation state
- Face animations include: emoji, delighted, sleep, happy, basic, eager, confidence, kawaii, thrilled, stunned, tired, angry, annoyed, dizzy, die, blankly, pleased, shy, blissful, like, flushed, beaten, tear, blood, full, chew, poo
- Body animations include: idle_basic, walk_basic, run, sprint, roll, fall, jump_down, click, pet, prone, grasp, chew, poo
