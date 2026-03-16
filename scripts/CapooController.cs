using Godot;
using System;

public partial class CapooController : CharacterBody2D
{
	[Export]
	public Vector2 InitialScale = new Vector2(0.4f, 0.4f);

	[Export]
	public Vector2 InitialPosition = new Vector2(506, 306);

	private const float GravityAcceleration = 1000f;
	private const double FallingTime = 0.8;
	private const float BounceFactor = 0.6f;
	private const float BaseHalfWidth = 190.0f;
	private const float BaseHeight = 285.0f;

	private CapooView capooView;
	private GameUI gameUI;
	private CapooState state;
	private CapooMotionController motionController;
	private int currentCapooId = 1;
	private bool isMouseOver = false;
	private bool isDragging = false;
	private bool isFalling = false;
	private Vector2 dragOffset = Vector2.Zero;
	private double fallingDelta;

	public void ChangeCapoo(int id)
	{
		var config = GetNode<CapooConfig>("/root/CapooConfig");
		if (config == null || config.infos == null) return;

		if (id >= config.infos.Count) id = 0;
		if (id < 0) id = config.infos.Count - 1;

		currentCapooId = id;

		if (capooView != null)
		{
			capooView.QueueFree();
		}

		capooView = new CapooView(currentCapooId);
		AddChild(capooView);

		SpineBone dragBone = capooView.GetSkeleton().FindBone("center");
		if (dragBone != null)
		{
			dragOffset = new Vector2(dragBone.GetWorldX(), dragBone.GetWorldY()) * Scale;
		}

		motionController?.Reset();
		state?.Reset(GetIdleHoldDuration());
		capooView.SetAnimation(GetRandomIdleAnimation());
	}

	public override void _Ready()
	{
		motionController = new CapooMotionController(this);
		state = new CapooState(this);
		Scale = InitialScale;
		Position = InitialPosition;
		InputPickable = true;

		EnsureGameUI();
		ChangeCapoo(currentCapooId);
		CreateCollisionPolygons();

		Connect("mouse_entered", Callable.From(() => { isMouseOver = true; }));
		Connect("mouse_exited", Callable.From(() => { isMouseOver = false; }));
	}

	private void EnsureGameUI()
	{
		gameUI = GetNodeOrNull<GameUI>("GameUI");
		if (gameUI == null)
		{
			gameUI = new GameUI();
			gameUI.Name = "GameUI";
			AddChild(gameUI);
		}

		gameUI.ChangeCapooRequested += OnChangeCapooRequested;
	}

	private void OnChangeCapooRequested(int id)
	{
		ChangeCapoo(id);
	}

	private void CreateCollisionPolygons()
	{
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
	}

	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Right && !keyEvent.Echo)
		{
			ChangeCapoo(currentCapooId + 1);
			return;
		}

		if (@event is InputEventMouseButton mouseButton
			&& mouseButton.Pressed
			&& mouseButton.ButtonIndex == MouseButton.Right
			&& isMouseOver
			&& !isDragging)
		{
			ToggleMagicBook();
			GetViewport().SetInputAsHandled();
		}
	}

	public override void _Process(double delta)
	{
		state.Update(delta);

		if (Input.IsActionJustPressed("left_click") && isMouseOver)
		{
			BeginDrag();
		}

		if (Input.IsActionJustReleased("left_click") && isDragging)
		{
			ReleaseDrag();
		}

		if (isDragging)
		{
			var nextPosition = GetGlobalMousePosition() - dragOffset;
			UpdateFaceDirection(nextPosition.X - Position.X);
			Position = nextPosition;
			ClampPosition(false);
		}
	}

	private void BeginDrag()
	{
		isDragging = true;
		isFalling = false;
		motionController.Reset();
		gameUI?.HideMagicBook();
		capooView.SetAnimation($"catchUp{Random.Shared.Next(1, 5)}");
	}

	private void ReleaseDrag()
	{
		isDragging = false;
		isFalling = true;
		fallingDelta = 0;
		Velocity = Input.GetLastMouseVelocity();
		capooView.SetAnimation("air");
	}

	private void ToggleMagicBook()
	{
		if (gameUI == null)
		{
			return;
		}

		gameUI.ToggleMagicBook(GetGlobalTransformWithCanvas().Origin);
		if (gameUI.IsMagicBookVisible)
		{
			motionController.Reset();
			Velocity = Vector2.Zero;
			capooView.SetAnimation(GetRandomIdleAnimation());
			state.Reset(GetIdleHoldDuration());
		}
	}

	private void ClampPosition(bool bounce)
	{
		Rect2 viewportRect = GetViewportRect();
		Vector2 pos = Position;
		Vector2 vel = Velocity;
		float halfWidth = GetHalfWidth();
		float height = GetBodyHeight();

		if (pos.X < halfWidth)
		{
			pos.X = halfWidth;
			if (bounce)
			{
				vel.X = -vel.X * BounceFactor;
				motionController.ReverseDirection();
				UpdateFaceDirection(vel.X);
			}
		}
		else if (pos.X > viewportRect.Size.X - halfWidth)
		{
			pos.X = viewportRect.Size.X - halfWidth;
			if (bounce)
			{
				vel.X = -vel.X * BounceFactor;
				motionController.ReverseDirection();
				UpdateFaceDirection(vel.X);
			}
		}

		if (pos.Y < height)
		{
			pos.Y = height;
			if (bounce) vel.Y = -vel.Y * BounceFactor;
		}
		else if (pos.Y > viewportRect.Size.Y)
		{
			pos.Y = viewportRect.Size.Y;
		}

		Position = pos;
		if (bounce) Velocity = vel;
	}

	public override void _PhysicsProcess(double delta)
	{
		var velocity = Velocity;
		if (isFalling)
		{
			fallingDelta += delta;
			velocity.Y += GravityAcceleration * (float)delta;

			float viewportHeight = GetViewportRect().Size.Y;
			bool isTouchingBottom = Position.Y >= (viewportHeight - 1.0f);

			if (fallingDelta >= FallingTime || IsOnFloor() || isTouchingBottom)
			{
				fallingDelta = 0;
				velocity = new Vector2(0, 0);
				isFalling = false;
				motionController.Reset();
				capooView.ClearAnimation();
				capooView.AddAnimation("fall");
				capooView.AddAnimation(GetRandomIdleAnimation());
				state.Reset(GetIdleHoldDuration());
			}
		}
		else if (!isDragging)
		{
			velocity = motionController.UpdateVelocity((float)delta);
		}

		Velocity = velocity;
		MoveAndSlide();
		ClampPosition(true);
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

	private string GetRandomIdleAnimation()
	{
		return $"idle_idle{Random.Shared.Next(1, 7)}";
	}

	private double GetIdleHoldDuration()
	{
		return 2.0 + Random.Shared.NextDouble() * 2.5;
	}

	private float GetHalfWidth()
	{
		return BaseHalfWidth * Math.Abs(Scale.X);
	}

	private float GetBodyHeight()
	{
		return BaseHeight * Math.Abs(Scale.Y);
	}

	private void ApplyBehavior(CapooBehavior behavior)
	{
		if (capooView == null)
		{
			return;
		}

		capooView.SetAnimation(behavior.ActionName);
		motionController.Play(behavior);
	}

	private readonly struct CapooBehavior
	{
		public CapooBehavior(string actionName, double duration, float speed)
		{
			ActionName = actionName;
			Duration = duration;
			Speed = speed;
		}

		public string ActionName { get; }
		public double Duration { get; }
		public float Speed { get; }
	}

	private sealed class CapooBehaviorTemplate
	{
		public CapooBehaviorTemplate(string prefix, int variantCount, double minDuration, double maxDuration, float minSpeed, float maxSpeed)
		{
			Prefix = prefix;
			VariantCount = variantCount;
			MinDuration = minDuration;
			MaxDuration = maxDuration;
			MinSpeed = minSpeed;
			MaxSpeed = maxSpeed;
		}

		private string Prefix { get; }
		private int VariantCount { get; }
		private double MinDuration { get; }
		private double MaxDuration { get; }
		private float MinSpeed { get; }
		private float MaxSpeed { get; }

		public CapooBehavior CreateBehavior()
		{
			int variant = Random.Shared.Next(1, VariantCount + 1);
			double duration = MinDuration + Random.Shared.NextDouble() * (MaxDuration - MinDuration);
			float speed = 0.0f;

			if (MaxSpeed > 0.0f)
			{
				speed = (float)(MinSpeed + Random.Shared.NextDouble() * (MaxSpeed - MinSpeed));
			}

			return new CapooBehavior($"{Prefix}{variant}", duration, speed);
		}
	}

	private sealed class CapooMotionController
	{
		private readonly CapooController controller;
		private float speed;
		private int direction;
		private double remainingTime;

		public CapooMotionController(CapooController controller)
		{
			this.controller = controller;
		}

		public void Play(CapooBehavior behavior)
		{
			remainingTime = behavior.Duration;
			speed = behavior.Speed;

			if (speed <= 0.0f)
			{
				direction = 0;
				return;
			}

			direction = GetInitialDirection();
			controller.UpdateFaceDirection(direction);
		}

		public Vector2 UpdateVelocity(float delta)
		{
			if (remainingTime <= 0.0 || speed <= 0.0f)
			{
				return Vector2.Zero;
			}

			remainingTime -= delta;
			if (remainingTime <= 0.0)
			{
				Reset();
				return Vector2.Zero;
			}

			AdjustDirectionAtBounds();
			controller.UpdateFaceDirection(direction);
			return new Vector2(direction * speed, 0.0f);
		}

		public void ReverseDirection()
		{
			if (direction == 0)
			{
				return;
			}

			direction = -direction;
		}

		public void Reset()
		{
			speed = 0.0f;
			direction = 0;
			remainingTime = 0.0;
		}

		private int GetInitialDirection()
		{
			float halfWidth = controller.GetHalfWidth();
			float leftEdge = halfWidth + 12.0f;
			float rightEdge = controller.GetViewportRect().Size.X - halfWidth - 12.0f;

			if (controller.Position.X <= leftEdge)
			{
				return 1;
			}

			if (controller.Position.X >= rightEdge)
			{
				return -1;
			}

			return Random.Shared.Next(2) == 0 ? -1 : 1;
		}

		private void AdjustDirectionAtBounds()
		{
			float halfWidth = controller.GetHalfWidth();
			float minX = halfWidth + 8.0f;
			float maxX = controller.GetViewportRect().Size.X - halfWidth - 8.0f;

			if (controller.Position.X <= minX)
			{
				direction = 1;
			}
			else if (controller.Position.X >= maxX)
			{
				direction = -1;
			}
		}
	}

	private class CapooState
	{
		private static readonly CapooBehaviorTemplate[] BehaviorTemplates =
		{
			new CapooBehaviorTemplate("idle_idle", 6, 2.5, 4.5, 0.0f, 0.0f),
			new CapooBehaviorTemplate("idle_full", 2, 2.0, 3.2, 0.0f, 0.0f),
			new CapooBehaviorTemplate("walk", 6, 2.4, 4.0, 70.0f, 110.0f),
			new CapooBehaviorTemplate("run", 9, 1.8, 3.0, 140.0f, 220.0f),
			new CapooBehaviorTemplate("wild", 5, 2.0, 3.0, 100.0f, 160.0f),
			new CapooBehaviorTemplate("roll", 5, 1.6, 2.6, 160.0f, 240.0f)
		};

		private readonly CapooController controller;
		private double remainingTime;

		public CapooState(CapooController controller)
		{
			this.controller = controller;
			Reset(controller.GetIdleHoldDuration());
		}

		public void Reset(double delay)
		{
			remainingTime = delay;
			controller.motionController.Reset();
		}

		public void Update(double delta)
		{
			if (controller.gameUI != null && controller.gameUI.IsMagicBookVisible)
			{
				controller.motionController.Reset();
				return;
			}

			if (controller.isDragging || controller.isFalling)
			{
				return;
			}

			remainingTime -= delta;
			if (remainingTime > 0.0)
			{
				return;
			}

			CapooBehavior behavior = BehaviorTemplates[Random.Shared.Next(BehaviorTemplates.Length)].CreateBehavior();
			remainingTime = behavior.Duration;
			controller.ApplyBehavior(behavior);
		}
	}
}
