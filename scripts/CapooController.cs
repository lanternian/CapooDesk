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
    //public float gravity = ProjectSettings.GetSetting("physics/2d/default_gravity").AsSingle(); --重复定义？

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
		talkPolygon.Polygon = new Vector2[] 	// 对话触发区域
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
		collisionPolygon.Polygon = new Vector2[]	// 主体碰撞体积
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
			ClampPosition(false);
		}
	}

	private void ClampPosition(bool bounce)
	{
		Rect2 viewportRect = GetViewportRect();
		Vector2 pos = Position;
		Vector2 vel = Velocity;
		
		// Capoo 的大致尺寸 (基于 Scale 0.796)
		// 宽度的一半：约 190 * 0.8 = 152
		// 高度（从锚点向上）：约 285 * 0.8 = 228
		float halfWidth = 152f;
		float height = 228f;
		
		float bounceFactor = 0.6f; // 反弹系数

		// 左边界
		if (pos.X < halfWidth)
		{
			pos.X = halfWidth;
			if (bounce) 
			{
				vel.X = -vel.X * bounceFactor;
				UpdateFaceDirection(vel.X); // 反弹时改变朝向
			}
		}
		// 右边界
		else if (pos.X > viewportRect.Size.X - halfWidth)
		{
			pos.X = viewportRect.Size.X - halfWidth;
			if (bounce) 
			{
				vel.X = -vel.X * bounceFactor;
				UpdateFaceDirection(vel.X);
			}
		}

		// 上边界
		if (pos.Y < height)
		{
			pos.Y = height;
			if (bounce) vel.Y = -vel.Y * bounceFactor;
		}
		// 下边界 (由落地逻辑处理，这里只做限制)
		else if (pos.Y > viewportRect.Size.Y)
		{
			pos.Y = viewportRect.Size.Y;
		}

		Position = pos;
		if (bounce) Velocity = vel;
	}

	private double fallingDelta = 0;
	private const double fallingTime = 0.8;		// 下落时间0.8s
	public override void _PhysicsProcess(double delta)
	{
		var velocity = Velocity;
		if (isFalling)
		{
			fallingDelta += delta;
			velocity.Y += gravity * (float)delta;

			// 获取视口大小
			float viewportHeight = GetViewportRect().Size.Y;
			// 判断是否触底（允许一点点误差，比如 1.0 像素）
			bool isTouchingBottom = Position.Y >= (viewportHeight - 1.0f);

			if (fallingDelta >= fallingTime || IsOnFloor() || isTouchingBottom) // Landing
			{
				fallingDelta = 0;
				velocity = new Vector2(0, 0);
				isFalling = false;
				capooView.ClearAnimation();
				capooView.AddAnimation("fall");
				capooView.AddAnimation($"idle_idle{Random.Shared.Next(0, 6)+1}");
			}
		}
		
		Velocity = velocity;
		MoveAndSlide();
		ClampPosition(true);
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
