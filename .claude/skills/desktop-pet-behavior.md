---
name: desktop-pet-behavior
description: Use when implementing autonomous movement, state machines, mood/hunger systems, interaction responses, or any pet behavior logic
---

# Desktop Pet Behavior

Design goal: a companion, not a game — no progression, no punishment, all 126 Capoos unlocked.

## States (CapooStateMachine.cs) and where they lead
```
Appear (summon effect, 1 s)            -> Idle | Air (spawned mid-air) | Stacked
Idle (idle_idle1-6, idle_full after eating, 2-5 s) -> Decide()
Walk / Run / Wild                      -> Idle (target, wall, timeout) | Hop (window edge)
React (hit1-7, stackDown1-3)           -> Idle | Stacked
Pet (pet1-5 while being rubbed)        -> Enjoy (1 s after rubbing stops) -> Idle | Stacked
Dragged (catchUp1-4)                   -> Roll (thrown upwards) | Air
Roll -> Air (descending) -> Land (fall) -> Idle | Dizzy (hard landing);  Air -> Stacked (landed on a head)
Hungry (bubble, 45 s)                  -> Eating (foodAppear -> chew -> enjoy) -> Idle (idle_full)
Pooing -> WaitCleanPoo (broom bubble, 45 s) -> PooRemoving (end -> pooRemoveAfter) -> Idle
Sleepy (sleepy1) -> Sleeping (sleep1-2) -> Waking (wakeUp1-2) -> Idle
Stacked (prone)                        -> Hop (after 25-70 s) | Air (base removed)
Recycle (recycle effect)               -> freed
```

## Decide() weights (Game_Parameter.json)
idle 1, walk 5, run 1, wild 1 (run/wild 0 while carrying a stack, reduced at night); hungry 3 / poo 3 only once
their care timer allows; nap 0.05 by day, 2.5 at night (NightStartHour–NightEndHour).
User away (system idle ≥ SleepIdleMinutes) → Sleepy until the user returns, then a staggered wake (0.5–6 s).
Radii 200/400/600 and speeds 1/2/4 are scaled by the pet size (speed unit 150 px/s at scale 1).

## Care timers (CapooNeeds.cs) — minutes between requests
| CareLevel | hungry | poo |
|---|---|---|
| 0 off | never | never |
| 1 low | 40 | 50 |
| 2 normal | 15 | 20 |
| 3 often | 5 | 7 |

Ignored hunger asks again after half an interval; eating brings the poo a minute closer. First requests are staggered.

## Interaction (CapooController.Interaction.cs)
PetManager routes the mouse to the top-most Capoo; speech bubbles win over bodies.
- click (< 0.25 s, < 10 px): Hungry → feed, WaitCleanPoo → clean, asleep → wake, otherwise hit1-7 (0.4 s cooldown)
- click on the speech bubble: feed / clean
- hold 0.25 s or move 10 px: drag; release throws with the cursor velocity of the last 0.1 s
- rubbing the cursor over it (no button) > 260 px·(scale/0.5): Pet
- right click: PetContextMenu (摸摸 / 喂食 / 睡觉·叫醒 / 回收)
