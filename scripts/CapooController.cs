using Godot;
using System;

public partial class CapooController : CharacterBody2D
{
	public	Vector2 scale = new Vector2(0.796f, 0.796f);
	public	Vector2 position = new Vector2(506, 306);
	private CapooView capooView;
	private bool isMouseOver = false;
	private bool isDragging = false;
	private bool isFalling = false;
	private Vector2 dragOffset = Vector2.Zero;
	public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle();

	public override void _Ready()
	{
		DisplayServer.WindowSetMousePassthrough(new Vector2[] { new Vector2(-1, -1) });
		Scale = scale;
		Position = position;
		InputPickable = true;

		capooView = new CapooView(1);
		AddChild(capooView);

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
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
		if (Input.IsActionJustPressed("left_click") && isMouseOver)
		{
			isDragging = true;
			capooView.SetAnimation($"catchUp{Random.Shared.Next(0, 4)+1}");
		}

		if (Input.IsActionJustReleased("left_click") && isDragging)
		{
			isDragging = false;
			isFalling = true;
			Velocity = Input.GetLastMouseVelocity();
			capooView.SetAnimation("air");
		}

		if (isDragging)
		{
			var nPos  = GetGlobalMousePosition() - dragOffset;
			UpdateFaceDirection(nPos.X - Position.X);
			Position = nPos;	
		}


	}
	private double fallingDelta = 0;
	private const double fallingTime = 0.9;
	public override void _PhysicsProcess(double delta)
	{
		var velocity = Velocity;
		if (isFalling)
		{
			fallingDelta += delta;
			velocity.Y += gravity * (float)delta;
		}
		if (fallingDelta >= fallingTime) // Landing
		{
			fallingDelta = 0;
			velocity = new Vector2(0, 0);
			isFalling = false;
			capooView.ClearAnimation();
			capooView.AddAnimation("fall");
			capooView.AddAnimation($"idle_idle{Random.Shared.Next(0, 6)+1}");
		}

		// Vector2 direction = Input.GetVector("left", "right", "up", "down");
		// if (direction != Vector2.Zero)
		// {
		// 	velocity.X = direction.X * Speed;
		// 	UpdateFaceDirection(direction.X);
		// 	capooView.SetAnimation("walk1");
		// }
		// else
		// {
		// 	velocity.X = Mathf.MoveToward(Velocity.X, 0, Speed);
		// 	capooView.SetAnimation("idle_idle1");
		// }
		
		Velocity = velocity;
		MoveAndSlide();
	}

	private void UpdateFaceDirection(float moveX)
	{
		// 如果 moveX 为 0，保持当前朝向不变
		if (Mathf.IsZeroApprox(moveX)) return;

		Vector2 currentScale = capooView.Scale;
		if (moveX > 0)
		{
			// 向右移动：Scale.X 设为负值 (假设原始素材朝左)
			currentScale.X = -Mathf.Abs(currentScale.X);
		}
		else
		{
			// 向左移动：Scale.X 设为正值
			currentScale.X = Mathf.Abs(currentScale.X);
		}
		capooView.Scale = currentScale;
	}
}
