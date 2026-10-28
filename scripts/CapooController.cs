using Godot;
using System;

public partial class CapooController : CharacterBody2D
{
	// Config
	public Vector2 scale = new Vector2(0.796f, 0.796f);
	public Vector2 position = new Vector2(506, 306);
	public const float WalkSpeed = 80.0f;
	public const float RunSpeed = 160.0f;
	public const float WildSpeed = 240.0f;

	// Components
	private CapooView capooView;
	private CapooStateMachine stateMachine;
	private CapooNeeds needs;

	// Identity
	public int InfoIndex { get; private set; } = 1;
	public void SetInfoIndex(int index) => InfoIndex = index;

	// Mouse interaction
	private bool isMouseOver = false;
	private bool isDragging = false;
	private bool isFalling = false;
	private Vector2 dragOffset = Vector2.Zero;
	private double clickHoldTime = 0.0;
	private bool isClickHeld = false;
	private bool wasClickOnBody = false;

	// Interaction cooldowns
	private double hitCooldown = 0.0;
	private double talkHungryCooldown = 0.0;
	private const double HitCooldownTime = 0.5;
	private const double TalkHungryCooldownTime = 10.0;
	private const double PetThreshold = 0.3;  // seconds held = pet vs drag

	// Pooping state
	private bool isPooping = false;
	private double poopTimer = 0.0;
	private const double PoopDuration = 2.5;

	// Physics
	public new float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();
	private double fallingDelta = 0;
	private const double FallingTime = 0.9;

	// Autonomous movement
	private int moveDirection = 0; // -1 left, 0 none, 1 right
	private double idleDecisionTimer = 0.0;
	private double walkDuration = 0.0;

	public override void _Ready()
	{
		DisplayServer.WindowSetMousePassthrough(new Vector2[] { new Vector2(-1, -1) });
		Scale = scale;
		Position = position;
		InputPickable = true;

		// State machine
		stateMachine = new CapooStateMachine();
		stateMachine.Name = "StateMachine";
		AddChild(stateMachine);

		// Needs system
		needs = new CapooNeeds();
		needs.Name = "Needs";
		AddChild(needs);

		// View
		capooView = new CapooView(InfoIndex);
		AddChild(capooView);

		// Collisions
		var talkPolygon = new CollisionPolygon2D();
		talkPolygon.Name = "talk_collision";
		talkPolygon.Polygon = new Vector2[]
		{
			new Vector2(-194, -244),
			new Vector2(-219, -250),
			new Vector2(-259, -238),
			new Vector2(-301, -241),
			new Vector2(-342, -279),
			new Vector2(-343, -329),
			new Vector2(-332, -357),
			new Vector2(-303, -386),
			new Vector2(-241, -397),
			new Vector2(-194, -371),
			new Vector2(-172, -312),
			new Vector2(-192, -273)
		};
		AddChild(talkPolygon);

		var collisionPolygon = new CollisionPolygon2D();
		collisionPolygon.Name = "capoo_collision";
		collisionPolygon.Polygon = new Vector2[]
		{
			new Vector2(-90, -283.75f),
			new Vector2(-149, -220),
			new Vector2(-189, -125),
			new Vector2(-164, -62),
			new Vector2(-103, -12),
			new Vector2(-26, -6),
			new Vector2(94, -8),
			new Vector2(155, -26),
			new Vector2(184, -115),
			new Vector2(172, -169),
			new Vector2(147.5f, -201.25f),
			new Vector2(85, -278.75f),
			new Vector2(0, -262.5f)
		};
		AddChild(collisionPolygon);

		Connect("mouse_entered", Callable.From(() => { isMouseOver = true; }));
		Connect("mouse_exited", Callable.From(() => { isMouseOver = false; }));

		SpineBone dragBone = capooView.GetSkeleton().FindBone("center");
		dragOffset = new Vector2(dragBone.GetWorldX(), dragBone.GetWorldY()) * Scale;

		// Start with appear animation, then enter idle
		capooView.AddAnimation("summonAppear");
		capooView.AddAnimation($"idle_idle{Random.Shared.Next(1, 7)}", 1, false);
		idleDecisionTimer = Random.Shared.Next(3, 8);
	}

	public override void _Process(double delta)
	{
		// Update cooldowns
		hitCooldown -= delta;
		talkHungryCooldown -= delta;

		// --- Right-click: toggle magic book (GameUI) ---
		if (Input.IsActionJustPressed("right_click") && isMouseOver)
		{
			HandleRightClick();
		}

		// --- Left click held: track time for pet vs drag distinction ---
		if (isClickHeld && isMouseOver)
		{
			clickHoldTime += delta;
		}

		// --- Left click pressed: mark potential body click ---
		if (Input.IsActionJustPressed("left_click") && isMouseOver && !isDragging)
		{
			isClickHeld = true;
			clickHoldTime = 0.0;
			wasClickOnBody = true;
		}

		// If mouse exits while held, cancel click intent (becomes drag only)
		if (isClickHeld && !isMouseOver && !isDragging)
		{
			wasClickOnBody = false;
		}

		// --- Transition to drag mode if held long enough ---
		if (isClickHeld && clickHoldTime >= PetThreshold && !isDragging && wasClickOnBody)
		{
			isDragging = true;
			wasClickOnBody = false;
			stateMachine.ForceState(CapooState.Dragged);
			capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.CatchUp));
			return;
		}

		// --- Left click released: determine pet or hit ---
		if (Input.IsActionJustReleased("left_click"))
		{
			if (isDragging)
			{
				// Drag release -> throw
				isDragging = false;
				isFalling = true;
				isClickHeld = false;
				Velocity = Input.GetLastMouseVelocity();
				stateMachine.ForceState(CapooState.Falling);
				capooView.SetAnimation("air");
				return;
			}

			if (isClickHeld && wasClickOnBody && !isFalling)
			{
				HandleBodyClick();
			}

			isClickHeld = false;
			wasClickOnBody = false;
		}

		// While dragging, follow mouse
		if (isDragging)
		{
			var nPos = GetGlobalMousePosition() - dragOffset;
			UpdateFaceDirection(nPos.X - Position.X);
			Position = nPos;
			return;
		}

		// Falling handled in _PhysicsProcess
		if (isFalling) return;

		// Pooping cycle
		if (isPooping)
		{
			ProcessPooping(delta);
			return;
		}

		// Autonomous behavior
		ProcessAutonomousBehavior(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (isFalling)
		{
			fallingDelta += delta;
			Velocity = new Vector2(Velocity.X, Velocity.Y + gravity * (float)delta);

			if (fallingDelta >= FallingTime)
			{
				fallingDelta = 0;
				Velocity = Vector2.Zero;
				isFalling = false;
				capooView.ClearAnimation();
				capooView.AddAnimation("fall");
				capooView.AddAnimation($"idle_idle{Random.Shared.Next(1, 7)}");
				stateMachine.ForceState(CapooState.Idle);
				idleDecisionTimer = Random.Shared.Next(3, 8);
			}
		}
		else if (isPooping)
		{
			// Pooping: stay still with gravity
			Velocity = new Vector2(0, Velocity.Y + gravity * (float)delta);
		}
		else if (stateMachine.CurrentState is CapooState.Walking or CapooState.Running)
		{
			float speed = stateMachine.CurrentState == CapooState.Running ? RunSpeed : WalkSpeed;
			Velocity = new Vector2(moveDirection * speed, Velocity.Y + gravity * (float)delta);
		}
		else
		{
			// Idle or other states: apply gravity but no horizontal movement
			Velocity = new Vector2(0, Velocity.Y + gravity * (float)delta);
		}

		MoveAndSlide();

		// Screen floor: clamp so Capoo doesn't fall off screen
		var screen = DisplayServer.ScreenGetSize(0);
		float floorY = screen.Y - 50f;
		if (Position.Y > floorY)
		{
			Position = new Vector2(Position.X, floorY);
			Velocity = new Vector2(Velocity.X, 0);
		}
	}

	// ---- Interaction Handlers ----

	private void HandleRightClick()
	{
		// Toggle the magic book via GameUI CanvasLayer
		var gameUI = GetNodeOrNull<CanvasLayer>("/root/GameUI");
		if (gameUI != null)
		{
			var anchorScreenPos = GetGlobalMousePosition();
			gameUI.Call("ToggleMagicBook", anchorScreenPos);
		}
	}

	private void HandleBodyClick()
	{
		// Short click = hit reaction (quick tap)
		// Longer hold (but under drag threshold) = could be double-click context
		if (stateMachine.CurrentState is CapooState.Dragged or CapooState.Falling or CapooState.Pooping)
			return;

		if (clickHoldTime < PetThreshold)
		{
			// Quick tap -> hit reaction
			TriggerHitReaction();
		}
	}

	/// <summary>
	/// Called externally or from double-click detection to trigger pet reaction.
	/// </summary>
	public void TriggerPetReaction()
	{
		if (stateMachine.CurrentState is CapooState.Dragged or CapooState.Falling or CapooState.Pooping)
			return;

		if (stateMachine.TransitionTo(CapooState.Petting))
		{
			needs.Pet();
			moveDirection = 0;
			capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Pet));
			stateMachine.SetStateDuration(1.5);

			// After petting, return to idle
			GetTree().CreateTimer(1.5).Timeout += () =>
			{
				if (stateMachine.CurrentState == CapooState.Petting)
				{
					stateMachine.TransitionTo(CapooState.Idle);
					capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Enjoy));
					idleDecisionTimer = Random.Shared.Next(3, 8);
				}
			};
		}
	}

	private void TriggerHitReaction()
	{
		if (hitCooldown > 0) return;

		hitCooldown = HitCooldownTime;
		needs.Hit();

		// Force to idle first if walking/running so animation plays cleanly
		moveDirection = 0;
		stateMachine.ForceState(CapooState.Idle);
		capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Hit));

		// Return to idle after hit animation
		idleDecisionTimer = Random.Shared.Next(2, 5);
	}

	// ---- Needs-Driven Behavior ----

	private void ProcessAutonomousBehavior(double delta)
	{
		// Tick needs
		needs.Tick(delta);

		// Check hunger -> show talk_hungry bubble
		if (needs.IsHungry() && talkHungryCooldown <= 0 && stateMachine.CurrentState == CapooState.Idle)
		{
			ShowHungerBubble();
			talkHungryCooldown = TalkHungryCooldownTime;
		}

		// Check poop urgency -> start pooping
		if (needs.NeedsPoop() && !isPooping && stateMachine.CurrentState is CapooState.Idle or CapooState.Walking)
		{
			StartPooping();
			return;
		}

		switch (stateMachine.CurrentState)
		{
			case CapooState.Idle:
				idleDecisionTimer -= delta;
				if (idleDecisionTimer <= 0)
				{
					DecideNextAction();
				}
				break;

			case CapooState.Walking:
			case CapooState.Running:
				walkDuration -= delta;
				if (walkDuration <= 0 || IsNearScreenEdge())
				{
					TransitionToIdle();
				}
				break;

			case CapooState.Sleeping:
				// Sleep until user interacts (handled in drag/click logic)
				break;
		}
	}

	private void ShowHungerBubble()
	{
		// Set the talk layer to hungry animation without changing body/face
		capooView.SetAnimation("talk_hungry", faceAniChange: false);
		// The talk animation will be cleared when the next idle action plays
	}

	// ---- Pooping Cycle ----

	private void StartPooping()
	{
		isPooping = true;
		poopTimer = 0.0;
		moveDirection = 0;
		Velocity = new Vector2(0, Velocity.Y);
		stateMachine.ForceState(CapooState.Pooping);
		capooView.SetAnimation("pooing");
	}

	private void ProcessPooping(double delta)
	{
		poopTimer += delta;

		if (poopTimer >= PoopDuration)
		{
			EndPooping();
		}
	}

	private void EndPooping()
	{
		isPooping = false;
		poopTimer = 0.0;
		needs.CleanPoop();
		stateMachine.ForceState(CapooState.Idle);
		capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Idle));
		idleDecisionTimer = Random.Shared.Next(3, 8);
	}

	// ---- Autonomous Movement ----

	private void DecideNextAction()
	{
		double roll = Random.Shared.NextDouble();

		if (roll < 0.15)
		{
			// Start running
			moveDirection = Random.Shared.Next(0, 2) * 2 - 1; // -1 or 1
			if (moveDirection == 0) moveDirection = 1;
			walkDuration = Random.Shared.Next(1, 4);
			stateMachine.TransitionTo(CapooState.Running);
			capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Running));
			UpdateFaceDirection(moveDirection);
		}
		else if (roll < 0.45)
		{
			// Start walking
			moveDirection = Random.Shared.Next(0, 2) * 2 - 1;
			if (moveDirection == 0) moveDirection = 1;
			walkDuration = Random.Shared.Next(2, 7);
			stateMachine.TransitionTo(CapooState.Walking);
			capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Walking));
			UpdateFaceDirection(moveDirection);
		}
		else
		{
			// Stay idle, pick a new idle animation
			capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Idle));
			idleDecisionTimer = Random.Shared.Next(3, 10);
		}
	}

	private void TransitionToIdle()
	{
		moveDirection = 0;
		Velocity = new Vector2(0, Velocity.Y);
		stateMachine.TransitionTo(CapooState.Idle);
		capooView.SetAnimation(CapooActionPools.RandomFrom(CapooActionPools.Idle));
		idleDecisionTimer = Random.Shared.Next(3, 10);
	}

	private bool IsNearScreenEdge()
	{
		var screen = DisplayServer.ScreenGetSize(0);
		var margin = 100f;
		float x = Position.X;

		if (moveDirection < 0 && x < margin)
		{
			moveDirection = 1;
			UpdateFaceDirection(1);
			return false; // Turn around instead of stopping
		}
		if (moveDirection > 0 && x > screen.X - margin)
		{
			moveDirection = -1;
			UpdateFaceDirection(-1);
			return false;
		}
		return false;
	}

	private void UpdateFaceDirection(float moveX)
	{
		if (Mathf.IsZeroApprox(moveX)) return;

		Vector2 currentScale = capooView.Scale;
		if (moveX > 0)
		{
			currentScale.X = -Mathf.Abs(currentScale.X);
		}
		else
		{
			currentScale.X = Mathf.Abs(currentScale.X);
		}
		capooView.Scale = currentScale;
	}
}
