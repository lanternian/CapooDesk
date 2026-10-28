using Godot;
using System;
using static CapooActionPools;

/// <summary>
/// Behaviour state machine: autonomous roaming, care loop, sleep, reactions and landings.
/// Timing follows the official app (Game_Parameter.json, body animation lengths).
/// </summary>
public partial class CapooController
{
	private CapooState state = CapooState.Appear;
	private double stateTime;
	private double stateDuration;
	private int phase;
	private CapooNeeds needs;

	private int moveDirection;
	private float moveSpeed;
	private float moveTargetX;
	private float landImpact;
	private string reactAction;

	private bool sleepUntilUserReturns;
	private double sleepDuration;
	private double wakeDelay = -1;
	private double stackHopTime;

	public bool HasBubble => state is CapooState.Hungry or CapooState.WaitCleanPoo;
	public bool IsAsleep => state is CapooState.Sleepy or CapooState.Sleeping;

	public override void _Process(double delta)
	{
		stateTime += delta;
		hitCooldown -= delta;
		rubAmount = Math.Max(0, rubAmount - RubDecay * delta);
		if (!IsAsleep) needs.Tick(delta);

		if (state == CapooState.Dragged) UpdateDrag();
		UpdateState(delta);
	}

	private bool StateDone => stateTime >= stateDuration;

	private void EnterState(CapooState next)
	{
		if (next.IsAirborne() || next is CapooState.Hop) offFloorTime = 0;
		state = next;
		stateTime = 0;
		phase = 0;
		stateDuration = 0;

		switch (next)
		{
			case CapooState.Appear:
				Velocity = Vector2.Zero;
				view.SetCharacterVisible(false);
				view.SetAnimation(RandomFrom(Idle));
				view.SetAnimation("summonAppear");
				stateDuration = 1.0;
				break;

			case CapooState.Idle:
				moveDirection = 0;
				view.SetAnimation(needs.JustAte ? RandomFrom(IdleFull) : RandomFrom(Idle));
				needs.JustAte = false;
				stateDuration = Rand(Parameters.idleTimeMin, Parameters.idleTimeMax);
				break;

			case CapooState.React:
			case CapooState.Dizzy:
				moveDirection = 0;
				view.SetAnimation(reactAction, force: true);
				stateDuration = Math.Max(view.GetActionDuration(reactAction), next == CapooState.Dizzy ? 1.8 : 0.8);
				break;

			case CapooState.Pet:
				moveDirection = 0;
				view.SetAnimation(RandomFrom(Pet));
				stateDuration = 10;
				break;

			case CapooState.Enjoy:
				view.SetAnimation(RandomFrom(Enjoy));
				stateDuration = 2.5;
				break;

			case CapooState.Dragged:
				moveDirection = 0;
				Velocity = Vector2.Zero;
				view.SetAnimation(RandomFrom(CatchUp));
				break;

			case CapooState.Air:
				view.SetAnimation("air");
				break;

			case CapooState.Roll:
				view.SetAnimation(RandomFrom(Roll));
				break;

			case CapooState.Hop:
				view.SetAnimation("hop", force: true);
				break;

			case CapooState.Land:
				moveDirection = 0;
				Velocity = Vector2.Zero;
				view.SetAnimation("fall", force: true);
				stateDuration = Math.Max(view.GetActionDuration("fall"), 0.5);
				break;

			case CapooState.Hungry:
				moveDirection = 0;
				view.SetAnimation(RandomFrom(Hungry));
				stateDuration = 45;
				break;

			case CapooState.Eating:
				moveDirection = 0;
				view.SetAnimation(RandomFrom(FoodAppear), force: true);
				stateDuration = Math.Max(view.GetLayerAnimationDuration(CapooView.Layer.Food, "fall_01"), 0.5) + 0.25;
				break;

			case CapooState.Pooing:
				moveDirection = 0;
				view.SetAnimation("pooing", force: true);
				stateDuration = Math.Max(view.GetActionDuration("pooing"), 1.0);
				break;

			case CapooState.WaitCleanPoo:
				view.SetAnimation("waitCleanPooTalk");
				stateDuration = 45;
				break;

			case CapooState.PooRemoving:
				view.SetAnimation("pooRemoving", force: true);
				stateDuration = Math.Max(view.GetLayerAnimationDuration(CapooView.Layer.Poo, "end"), 1.0);
				break;

			case CapooState.Sleepy:
				moveDirection = 0;
				view.SetAnimation("sleepy1");
				stateDuration = 2.5;
				break;

			case CapooState.Sleeping:
				view.SetAnimation(RandomFrom(Sleep));
				stateDuration = sleepDuration;
				break;

			case CapooState.Waking:
				wakeDelay = -1;
				view.SetAnimation(RandomFrom(WakeUp), force: true);
				stateDuration = Math.Max(view.GetActionDuration("wakeUp1"), 1.0);
				break;

			case CapooState.Stacked:
				moveDirection = 0;
				Velocity = Vector2.Zero;
				view.SetAnimation("stackFog1", force: true);
				stackHopTime = Rand(25, 70);
				break;

			case CapooState.Recycle:
				moveDirection = 0;
				view.SetAnimation("recycle", force: true);
				stateDuration = 0.85;
				break;
		}
	}

	private void UpdateState(double delta)
	{
		switch (state)
		{
			case CapooState.Appear:
				if (stateTime >= 0.45) view.SetCharacterVisible(true);
				if (StateDone)
				{
					if (StackBelow != null) EnterState(CapooState.Stacked);
					else if (IsResting()) EnterState(CapooState.Idle);
					else EnterState(CapooState.Air);
				}
				break;

			case CapooState.Idle:
				if (StateDone) Decide();
				break;

			case CapooState.Walk:
			case CapooState.Run:
			case CapooState.Wild:
				bool arrived = moveDirection > 0 ? GlobalPosition.X >= moveTargetX : GlobalPosition.X <= moveTargetX;
				if (arrived || StateDone) EnterState(CapooState.Idle);
				break;

			case CapooState.React:
			case CapooState.Dizzy:
			case CapooState.Enjoy:
			case CapooState.Waking:
				if (StateDone) Resume();
				break;

			case CapooState.Pet:
				if (Now - lastRubTime > 1.0 || StateDone) EnterState(CapooState.Enjoy);
				break;

			case CapooState.Land:
				if (!StateDone) break;
				if (landImpact > 3300)
					Dizzy("dizzy_soul");
				else if (landImpact > 2500)
					Dizzy("dizzy_dizzy");
				else
					EnterState(CapooState.Idle);
				break;

			case CapooState.Hungry:
				if (StateDone)
				{
					needs.HungerIgnored(Settings.CareLevel);
					EnterState(CapooState.Idle);
				}
				break;

			case CapooState.Eating:
				UpdateEating();
				break;

			case CapooState.Pooing:
				if (StateDone) EnterState(CapooState.WaitCleanPoo);
				break;

			case CapooState.WaitCleanPoo:
				if (StateDone) EnterState(CapooState.PooRemoving); // cleans itself up eventually
				break;

			case CapooState.PooRemoving:
				if (!StateDone) break;
				if (phase == 0)
				{
					phase = 1;
					stateTime = 0;
					stateDuration = 1.5;
					view.SetAnimation("pooRemoveAfter", force: true);
				}
				else
				{
					view.HideLayer(CapooView.Layer.Poo);
					needs.Pooed();
					EnterState(CapooState.Idle);
				}
				break;

			case CapooState.Sleepy:
				if (StateDone) EnterState(CapooState.Sleeping);
				break;

			case CapooState.Sleeping:
				if (wakeDelay >= 0)
				{
					wakeDelay -= delta;
					if (wakeDelay <= 0) Wake();
				}
				else if (!sleepUntilUserReturns && StateDone)
				{
					Wake();
				}
				break;

			case CapooState.Stacked:
				if (phase == 0 && stateTime >= 0.6)
				{
					phase = 1;
					view.SetAnimation(RandomFrom(Prone));
				}
				if (stateTime >= stackHopTime && StackAbove == null) HopOffStack();
				break;

			case CapooState.Recycle:
				if (stateTime >= 0.35) view.SetCharacterVisible(false);
				if (StateDone)
				{
					PetManager.Instance?.RemovePet(this);
					QueueFree();
				}
				break;
		}
	}

	private void UpdateEating()
	{
		if (!StateDone) return;
		switch (phase)
		{
			case 0:
				view.HideLayer(CapooView.Layer.Food);
				view.SetAnimation("chew", force: true);
				stateDuration = 1.6;
				break;
			case 1:
				view.SetAnimation(RandomFrom(AfterEat), force: true);
				stateDuration = 1.6;
				break;
			default:
				needs.Fed();
				EnterState(CapooState.Idle);
				return;
		}
		phase++;
		stateTime = 0;
	}

	/// <summary>Back to whatever fits after a one-shot action.</summary>
	private void Resume()
	{
		if (StackBelow != null)
		{
			EnterState(CapooState.Stacked);
			phase = 1;
			view.SetAnimation(RandomFrom(Prone));
		}
		else
		{
			EnterState(CapooState.Idle);
		}
	}

	// ---- Autonomous decisions ----

	/// <summary>Self-test switch: when false, Capoos only idle unless told otherwise.</summary>
	public static bool AutonomyEnabled = true;

	/// <summary>Self-test/debug: jump straight into a state (hungry, pooing, ...).</summary>
	public void DebugEnter(CapooState next) => EnterState(next);

	private void Decide()
	{
		if (!AutonomyEnabled)
		{
			EnterState(CapooState.Idle);
			return;
		}

		var manager = PetManager.Instance;
		var settings = Settings;
		var p = Parameters;

		if (settings.SleepEnabled && manager != null && manager.UserAway)
		{
			GoToSleep(untilUserReturns: true);
			return;
		}

		bool carrying = StackAbove != null;
		bool night = manager != null && manager.IsNight;
		double wIdle = p.idleProbability;
		double wWalk = p.walkProbability;
		double wRun = carrying ? 0 : p.runProbability;
		double wWild = carrying ? 0 : p.wildProbability;
		double wHungry = needs.CanBeHungry(settings.CareLevel) ? p.hungryProbability * 3 : 0;
		double wPoo = needs.CanPoo(settings.CareLevel) ? p.pooProbability * 3 : 0;
		double wNap = settings.SleepEnabled ? (night ? 2.5 : 0.05) : 0;
		if (night)
		{
			wRun *= 0.3;
			wWild *= 0.2;
		}

		double total = wIdle + wWalk + wRun + wWild + wHungry + wPoo + wNap;
		double roll = Random.Shared.NextDouble() * total;

		if ((roll -= wIdle) < 0) EnterState(CapooState.Idle);
		else if ((roll -= wWalk) < 0) StartMove(CapooState.Walk);
		else if ((roll -= wRun) < 0) StartMove(CapooState.Run);
		else if ((roll -= wWild) < 0) StartMove(CapooState.Wild);
		else if ((roll -= wHungry) < 0) EnterState(CapooState.Hungry);
		else if ((roll -= wPoo) < 0) EnterState(CapooState.Pooing);
		else GoToSleep(untilUserReturns: false, napSeconds: night ? Rand(60, 240) : Rand(20, 60));
	}

	private void StartMove(CapooState kind)
	{
		var p = Parameters;
		float radius, speed;
		string[] pool;
		switch (kind)
		{
			case CapooState.Run:
				radius = p.runRadius;
				speed = p.runSpeed;
				pool = Running;
				break;
			case CapooState.Wild:
				radius = p.wildRadius;
				speed = p.wildSpeed;
				pool = Wild;
				break;
			default:
				radius = p.walkRadius;
				speed = p.walkSpeed;
				pool = Walking;
				break;
		}

		// Radii/speeds are tuned for the original pet size; scale them with ours.
		float sizeFactor = PetScale * 2f;
		radius *= sizeFactor;
		moveSpeed = speed * SpeedUnit * PetScale;

		float width = DesktopWindow.Instance.Size.X;
		float x = GlobalPosition.X;
		int dir = Random.Shared.Next(2) == 0 ? -1 : 1;
		if (x < width * 0.12f) dir = 1;
		else if (x > width * 0.88f) dir = -1;

		float distance = radius * (float)Rand(0.4, 1.0);
		moveTargetX = Mathf.Clamp(x + dir * distance, BodySize.X * 0.45f, width - BodySize.X * 0.45f);
		moveDirection = dir;
		SetFacing(dir > 0);

		EnterState(kind);
		view.SetAnimation(RandomFrom(pool));
		stateDuration = Mathf.Abs(moveTargetX - x) / Math.Max(moveSpeed, 1f) + 2.0;
	}

	private void OnBlocked() => EnterState(CapooState.Idle);

	private void OnPlatformEdge()
	{
		if (Random.Shared.NextDouble() < 0.5)
			HopOff(moveDirection);
		else
			EnterState(CapooState.Idle);
	}

	private void HopOff(int dir)
	{
		float k = Mathf.Sqrt(PetScale * 2f);
		Velocity = new Vector2(dir * 320f * k, -650f * k);
		SetFacing(dir > 0);
		EnterState(CapooState.Hop);
	}

	private void HopOffStack()
	{
		DetachFromBelow();
		HopOff(Random.Shared.Next(2) == 0 ? -1 : 1);
	}

	public void StartFalling()
	{
		if (state.IsAirborne() || state is CapooState.Hop or CapooState.Dragged or CapooState.Recycle) return;
		DetachFromBelow();
		EnterState(CapooState.Air);
	}

	private void OnLanded(Vector2 impact)
	{
		landImpact = impact.Y;
		EnterState(CapooState.Land);
	}

	/// <summary>Landed on <paramref name="below"/>'s head.</summary>
	public void LandOnHead(CapooController below)
	{
		Velocity = Vector2.Zero;
		AttachOnto(below);
		FollowStack();
		EnterState(CapooState.Stacked);
		below.OnSomeoneLanded();
	}

	private void OnSomeoneLanded()
	{
		if (state.IsGroundIdle() || state is CapooState.Land or CapooState.React)
			React(RandomFrom(StackDown));
	}

	private void React(string action)
	{
		reactAction = action;
		EnterState(CapooState.React);
	}

	private void Dizzy(string action)
	{
		reactAction = action;
		EnterState(CapooState.Dizzy);
	}

	/// <summary>Hit by a thrown Capoo.</summary>
	public void Bumped()
	{
		if (state is CapooState.Recycle or CapooState.Appear or CapooState.Dragged || state.IsAirborne()) return;
		if (state == CapooState.WaitCleanPoo) view.HideLayer(CapooView.Layer.Poo);
		Dizzy(RandomFrom(CapooActionPools.Dizzy));
		view.SetLayer(CapooView.Layer.EffectBot, "bump", false);
	}

	public bool CanBeBumped =>
		state.IsGroundIdle() || state is CapooState.Hungry or CapooState.Sleepy or CapooState.Sleeping or CapooState.React or CapooState.Land;

	// ---- Sleep ----

	private void GoToSleep(bool untilUserReturns, double napSeconds = 0)
	{
		sleepUntilUserReturns = untilUserReturns;
		sleepDuration = untilUserReturns ? double.MaxValue : napSeconds;
		wakeDelay = -1;
		EnterState(CapooState.Sleepy);
	}

	public void Wake()
	{
		if (!IsAsleep) return;
		EnterState(CapooState.Waking);
	}

	/// <summary>The user is back at the computer: wake up one by one.</summary>
	public void OnUserReturned()
	{
		if (IsAsleep && sleepUntilUserReturns) wakeDelay = Rand(0.5, 6.0);
	}

	// ---- Commands (context menu / book) ----

	public void Feed()
	{
		if (IsAsleep || state is CapooState.Hungry || state.IsGroundIdle() || state is CapooState.React or CapooState.Pet)
			EnterState(CapooState.Eating);
	}

	public void CleanPoo()
	{
		if (state == CapooState.WaitCleanPoo) EnterState(CapooState.PooRemoving);
	}

	public void ToggleSleep()
	{
		if (IsAsleep) Wake();
		else if (state.IsGroundIdle() || state is CapooState.Hungry or CapooState.React)
			GoToSleep(untilUserReturns: false, napSeconds: 180);
	}

	public void StartRecycle()
	{
		if (state == CapooState.Recycle) return;
		if (StackAbove != null && IsInstanceValid(StackAbove))
		{
			var above = StackAbove;
			above.DetachFromBelow();
			above.StartFalling();
		}
		DetachFromBelow();
		EnterState(CapooState.Recycle);
	}

	private static double Rand(double min, double max) => min + Random.Shared.NextDouble() * (max - min);
}
