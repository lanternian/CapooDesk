---
name: desktop-pet-behavior
description: Use when implementing autonomous movement, state machines, mood/hunger systems, interaction responses, or any pet behavior logic
---

# Desktop Pet Behavior Design

## State Machine (CapooState)

States: Idle, Walking, Running, Sleeping, Dragged, Falling, Petting, Eating, Pooping, Stacked, Dizzy

### State Transition Rules
```
Idle     -> Walking (random timer 3-15s)
Idle     -> Running (small chance per cycle)
Idle     -> Sleeping (long idle > 30s)
Idle     -> Dragged (user grab)
Idle     -> Eating (food nearby)
Idle     -> Petting (user click head)
Idle     -> Dizzy (user click body)
Walking  -> Idle (reached target/edge)
Walking  -> Running (random chance)
Walking  -> Dragged (user grab)
Running  -> Idle (timer expires)
Running  -> Dragged (user grab)
Dragged  -> Falling (user release)
Falling  -> Idle (landed on floor)
Petting  -> Idle (animation complete)
Eating   -> Idle (animation chain complete)
Pooping  -> Idle (animation chain complete)
Sleeping -> Idle (user interaction / timer)
Stacked  -> Falling (removed from stack)
Dizzy    -> Idle (timer expires)
```

## Needs System (CapooNeeds)
```
hungerLevel:   0.0-1.0, increases over time (~5 min to full)
poopUrgency:   0.0-1.0, increases after eating (~3 min to full)
moodLevel:     0.0-1.0, affected by interactions

Thresholds:
  hunger > 0.7  -> show talk_hungry bubble
  hunger > 0.9  -> mood decreases faster
  poopUrgency >= 1.0 -> trigger pooping
  mood < 0.3   -> prefer sad animations
  mood > 0.7   -> prefer happy animations
```

## Action-to-Animation Mapping (80 actions from Capoo_Action.json)

### Idle Group
- idle_idle1-6: varied faces (emoji, delighted, sleep, happy)
- idle_full1-2: full body idle variants

### Movement Group
- walk1-6: basic walk with varied faces/effects
- run1-9: running with varied faces (some with sweat_run mood)
- wild1-5: sprint with angry/stunned/tired faces + speed effects

### Reaction Group
- hit1-7: click reactions with varied faces + talk
- pet1-5: petting with blissful/eager/like faces + heart effects

### Special Group
- catchUp1-4: grabbed animation
- air + fall: thrown and landing
- roll1-5: rolling with stunned/dizzy faces
- prone1-4: lying flat (for stacking)
- dizzy1-4 + dizzy_dizzy + dizzy_soul: dizzy variants

### Lifecycle Group
- summonAppear: spawn animation
- recycle: despawn animation
- merge: combine animation

### Food/Poop Group
- foodAppear1-4: food appears
- chew: eating
- enjoy1-3: after eating satisfaction
- pooing + waitCleanPoo + pooRemoving + pooRemoveAfter: poop cycle

## Movement AI Pattern
```csharp
// In Idle state:
idleTimer -= delta;
if (idleTimer <= 0) {
    // Decide next: 70% idle again, 20% walk, 8% run, 2% sleep
    var roll = Random.Shared.NextDouble();
    if (roll < 0.70) PickNewIdle();
    else if (roll < 0.90) StartWalking();
    else if (roll < 0.98) StartRunning();
    else StartSleeping();
}

// In Walking state:
// Move toward target at walk speed
// If near screen edge, turn or stop
// Random duration 2-8 seconds
```

## Interaction Mapping
- Left click on body -> hit1-7 (random), mood slightly decreases
- Double click on head -> pet1-5, mood increases
- Drag -> catchUp1-4 (grab animation)
- Release -> air + fall (thrown with mouse velocity)
- Click on poo -> pooRemoving + pooRemoveAfter, mood increases
