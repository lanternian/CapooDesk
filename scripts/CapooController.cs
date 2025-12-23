using Godot;
using System;

public partial class CapooController : CharacterBody2D
{
	public	Vector2 scale = new Vector2(0.4f, 0.4f); // capoo缩放比例
	public	Vector2 position = new Vector2(506, 306); // 初始位置
	private CapooView capooView; // 角色 View
	private int currentCapooId = 1;
	private bool isMouseOver = false;
	private bool isDragging = false;
	private bool isFalling = false;
	private Vector2 dragOffset = Vector2.Zero;
	private CapooState _state; // 状态管理

	public void ChangeCapoo(int id)
	{
		var config = GetNode<CapooConfig>("/root/CapooConfig");
		if (config == null || config.infos == null) return;

		// 循环切换逻辑：如果 ID 超出范围，则回到开头或结尾
		if (id >= config.infos.Count) id = 0;
		if (id < 0) id = config.infos.Count - 1;
		
		currentCapooId = id;

		// 移除旧的 View
		if (capooView != null)
		{
			capooView.QueueFree();
			RemoveChild(capooView);
		}

		// 创建新的 View 并添加到场景
		capooView = new CapooView(currentCapooId);
		AddChild(capooView);

		// 重新获取骨骼数据
		SpineBone dragBone = capooView.GetSkeleton().FindBone("center");
		if (dragBone != null)
		{
			dragOffset = new Vector2(dragBone.GetWorldX(), dragBone.GetWorldY()) * Scale;
		}
		
		// 播放初始动画
		capooView.SetAnimation($"idle_idle{Random.Shared.Next(0, 6)+1}");
	}

    public override void _Ready()
	{
		_state = new CapooState(this);
		DisplayServer.WindowSetMousePassthrough(new Vector2[] { new Vector2(-1, -1) }); 
		Scale = scale;
		Position = position;
		InputPickable = true;

		ChangeCapoo(currentCapooId);

		// 初始化 UI
		// 由Controller直接创建
		var existingUI = GetNodeOrNull<GameUI>("GameUI");
		if (existingUI != null)
		{
			// 如果存在，直接连接信号
			existingUI.ChangeCapooRequested += (id) => ChangeCapoo(id);
		}
		else
		{
			// 如果不存在，再动态创建
			var gameUI = new GameUI();
			gameUI.Name = "GameUI"; // 设置名字以便查找
			AddChild(gameUI);
			gameUI.ChangeCapooRequested += (id) => ChangeCapoo(id);
		}

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
	}

	public override void _Input(InputEvent @event)
	{
		// 键盘右键切换下一个
		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Right && !keyEvent.Echo)
		{
			ChangeCapoo(currentCapooId + 1);
		}
	}

	public override void _Process(double delta)
	{
		_state.Update(delta); // 更新状态逻辑

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
		
		// Capoo 的大致尺寸 (基于 Scale)
		// 原始尺寸：宽度的一半约 190，高度约 285
		float halfWidth = 190f * Math.Abs(Scale.X);
		float height = 285f * Math.Abs(Scale.Y);
		
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

			float viewportHeight = GetViewportRect().Size.Y;
			// 判断是否触底
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

	/* 内部状态类，用于管理待机时的表情变化 */
	private class CapooState
	{
		private CapooController _controller;
		private double state_timer; // 状态计时器 	
		private double _nextChangeTime;

		public CapooState(CapooController controller)
		{
			_controller = controller;
			ResetTimer();
		}

		private void ResetTimer()
		{
			state_timer = 0;
			_nextChangeTime = Random.Shared.Next(3, 8); // 3到8秒随机切换
		}

		public void Update(double delta)
		{
			// 如果正在被拖拽或下落，不进行待机表情切换
			if (_controller.isDragging || _controller.isFalling)
			{
				ResetTimer();
				return;
			}

			state_timer += delta;
			if (state_timer >= _nextChangeTime)
			{
				ResetTimer();
				// 随机播放一个 idle 动画
				string[] types = { "idle_idle", "idle_full", "walk", "run", "wild", "roll" };
				int[] counts = { 6, 2, 6, 9, 5, 5 };
				int idx = Random.Shared.Next(types.Length);
				_controller.capooView.SetAnimation($"{types[idx]}{Random.Shared.Next(1, counts[idx] + 1)}");
			}
		}
	}
}
