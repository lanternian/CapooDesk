using Godot;
using System;

/// <summary>
/// One Capoo on the desktop. Origin is at the feet.
/// This file: setup, physics and geometry. Behaviour states live in CapooController.Behavior.cs,
/// mouse handling in CapooController.Interaction.cs.
/// </summary>
public partial class CapooController : CharacterBody2D
{
	// Outline around the body in view units (unscaled, facing left), feet at the origin.
	private static readonly Vector2[] BodyOutline =
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
	private const float OutlineWidth = 373f;
	private const float OutlineHeight = 284f;

	public const float Gravity = 2200f;
	public const float MaxFallSpeed = 4000f;
	public const float MaxThrowSpeed = 3500f;
	// Pixels per second for one Game_Parameter speed unit at pet scale 1.
	private const float SpeedUnit = 150f;

	public int InfoIndex { get; private set; }
	public CapooInfo Info => view?.Info;
	public string DisplayName => Info?.name ?? "";
	public CapooState State => state;
	public CapooView View => view;

	/// <summary>The summon/recycle smoke, a sibling of the view.</summary>
	public SpineSprite AppearSprite => appearSprite;
	public float PetScale { get; private set; } = 0.5f;
	public bool FacingRight => view.FacingRight;

	/// <summary>Size of the body outline in pixels.</summary>
	public Vector2 BodySize { get; private set; }

	/// <summary>Where a Capoo stacked on this one stands (world Y).</summary>
	public float HeadY => GlobalPosition.Y - BodySize.Y * (state == CapooState.Stacked ? 0.62f : 0.76f);

	/// <summary>Rough body box in world coordinates, used for Capoo-to-Capoo collisions.</summary>
	public Rect2 BodyRect => new Rect2(
		GlobalPosition.X - BodySize.X * 0.35f, GlobalPosition.Y - BodySize.Y * 0.85f,
		BodySize.X * 0.7f, BodySize.Y * 0.85f);

	/// <summary>Window (hwnd) whose top edge this Capoo stands on, 0 for the taskbar floor.</summary>
	public long FloorHwnd { get; private set; }

	public CapooController StackBelow { get; private set; }
	public CapooController StackAbove { get; private set; }

	private static SettingsData Settings => AppSettings.Instance.Data;
	private GameParameter Parameters => config.parameters;

	private CapooConfig config;
	private CapooView view;
	private SpineSprite appearSprite;
	private RectangleShape2D bodyShape;
	private CollisionShape2D bodyCollision;
	private Vector2[] pickPolygon = Array.Empty<Vector2>();
	private double offFloorTime;
	private int restTicks;

	public CapooController()
	{
	}

	public CapooController(int infoIndex, float scale)
	{
		InfoIndex = infoIndex;
		PetScale = scale;
	}

	public override void _Ready()
	{
		CollisionLayer = PhysicsLayers.Pet;
		CollisionMask = PhysicsLayers.World | PhysicsLayers.Platform;
		MotionMode = MotionModeEnum.Grounded;
		UpDirection = Vector2.Up;
		FloorSnapLength = 10f;
		FloorStopOnSlope = true;

		config = GetNode<CapooConfig>("/root/CapooConfig");
		view = new CapooView(InfoIndex);
		AddChild(view);

		// The summon/recycle smoke lives next to the view, not inside it: the character is
		// hidden while the smoke plays, so the smoke must not be hidden with it.
		appearSprite = view.AppearSprite;
		if (appearSprite != null)
		{
			view.RemoveChild(appearSprite);
			AddChild(appearSprite);
			appearSprite.Position = Vector2.Zero;
		}

		bodyShape = new RectangleShape2D();
		bodyCollision = new CollisionShape2D { Name = "capoo_collision", Shape = bodyShape };
		AddChild(bodyCollision);

		ApplyScale(PetScale);
		needs = new CapooNeeds(Settings.CareLevel);
		EnterState(CapooState.Appear);
	}

	public override void _ExitTree()
	{
		if (StackAbove != null && IsInstanceValid(StackAbove))
		{
			var above = StackAbove;
			above.DetachFromBelow();
			above.StartFalling();
		}
		DetachFromBelow();
	}

	public void ApplyScale(float scale)
	{
		PetScale = scale;
		view.SetPetScale(scale);

		float adjust = Info.collider_Adjust > 0 ? Info.collider_Adjust : 1f;
		float s = scale * adjust;
		BodySize = new Vector2(OutlineWidth * s, OutlineHeight * s);
		bodyShape.Size = new Vector2(BodySize.X * 0.55f, BodySize.Y * 0.8f);
		bodyCollision.Position = new Vector2(0, -bodyShape.Size.Y / 2f);

		pickPolygon = new Vector2[BodyOutline.Length];
		for (int i = 0; i < BodyOutline.Length; i++) pickPolygon[i] = BodyOutline[i] * s;
	}

	public void SetFacing(bool right) => view.SetFacing(right);

	private void TurnTo(float directionX)
	{
		if (Mathf.Abs(directionX) > 0.01f) SetFacing(directionX > 0);
	}

	// ---- Hit testing ----

	public bool HitTest(Vector2 world)
	{
		if (state == CapooState.Recycle || view == null) return false;
		Vector2 local = world - GlobalPosition;
		// Cheap reject: accessories never reach further than ~one body size away.
		if (Mathf.Abs(local.X) > BodySize.X * 1.2f || local.Y > BodySize.Y * 0.3f || local.Y < -BodySize.Y * 2.2f) return false;
		if (FacingRight) local.X = -local.X;
		return Geometry2D.IsPointInPolygon(local, pickPolygon) || view.AccessoryHitTest(world);
	}

	public bool BubbleHitTest(Vector2 world)
	{
		if (!HasBubble) return false;
		Rect2 rect = view.GetTalkBubbleRect();
		return rect.Size != Vector2.Zero && rect.Grow(6).HasPoint(world);
	}

	/// <summary>Outline polygon in world coordinates (debug drawing).</summary>
	public Vector2[] GetWorldOutline()
	{
		var result = new Vector2[pickPolygon.Length];
		for (int i = 0; i < pickPolygon.Length; i++)
		{
			Vector2 p = pickPolygon[i];
			if (FacingRight) p.X = -p.X;
			result[i] = GlobalPosition + p;
		}
		return result;
	}

	// ---- Physics ----

	public override void _PhysicsProcess(double delta)
	{
		if (state is CapooState.Dragged or CapooState.Stacked or CapooState.Recycle or CapooState.Appear)
			return;

		float dt = (float)delta;
		Vector2 previous = GlobalPosition;
		bool airborne = state.IsAirborne() || state == CapooState.Hop;

		// Standing still: skip the full physics step and just make sure there's still
		// ground below every few ticks (the window under it may have closed).
		if (!airborne && !state.IsMoving() && offFloorTime == 0 && Velocity == Vector2.Zero)
		{
			if (++restTicks % 8 != 0) return;
			if (TestMove(GlobalTransform, new Vector2(0, 3)))
			{
				ClampToWorld();
				return;
			}
			offFloorTime = 0.001;
		}
		restTicks = 0;

		Vector2 v = Velocity;
		if (!airborne) v.X = state.IsMoving() ? moveDirection * moveSpeed : 0f;
		v.Y = Mathf.Min(v.Y + Gravity * dt, MaxFallSpeed);
		Velocity = v;
		Vector2 impact = v;

		MoveAndSlide();
		UpdateFloorHwnd();

		if (airborne)
		{
			if (IsOnWall())
			{
				Velocity = new Vector2(-impact.X * 0.45f, Velocity.Y);
				TurnTo(Velocity.X);
			}
			if (state == CapooState.Roll && Velocity.Y > 0) EnterState(CapooState.Air);

			if (IsOnFloor() && impact.Y >= 0)
				OnLanded(impact);
			else if (impact.Y > 0)
				PetManager.Instance?.TryStackOnto(this, previous);
		}
		else
		{
			if (IsOnFloor())
			{
				offFloorTime = 0;
				Velocity = Vector2.Zero;
			}
			else if ((offFloorTime += dt) > 0.1)
			{
				StartFalling();
			}

			if (state.IsMoving())
			{
				if (IsOnWall()) OnBlocked();
				else if (FloorHwnd != 0 && IsEdgeAhead()) OnPlatformEdge();
			}
		}

		ClampToWorld();
	}

	private void UpdateFloorHwnd()
	{
		FloorHwnd = 0;
		if (!IsOnFloor()) return;
		for (int i = 0; i < GetSlideCollisionCount(); i++)
		{
			var collision = GetSlideCollision(i);
			if (collision.GetNormal().Y < -0.7f && collision.GetCollider() is Node node && node.HasMeta("hwnd"))
			{
				FloorHwnd = node.GetMeta("hwnd").AsInt64();
				return;
			}
		}
	}

	/// <summary>Standing on something (used before physics has run for this Capoo).</summary>
	private bool IsResting() => TestMove(GlobalTransform, new Vector2(0, 6));

	/// <summary>No ground a little ahead of the feet (end of a window's top edge).</summary>
	private bool IsEdgeAhead()
	{
		float x = GlobalPosition.X + moveDirection * BodySize.X * 0.25f;
		var query = PhysicsRayQueryParameters2D.Create(
			new Vector2(x, GlobalPosition.Y - 6), new Vector2(x, GlobalPosition.Y + 24),
			PhysicsLayers.World | PhysicsLayers.Platform);
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	private void ClampToWorld()
	{
		var size = DesktopWindow.Instance.Size;
		float half = BodySize.X * 0.45f;
		Vector2 p = GlobalPosition;
		p.X = Mathf.Clamp(p.X, half, size.X - half);
		p.Y = Mathf.Min(p.Y, size.Y);
		GlobalPosition = p;
	}

	/// <summary>Move along with the window this Capoo is standing on.</summary>
	public void Carry(Vector2 delta)
	{
		GlobalPosition += delta;
	}

	// ---- Stacking ----

	public void AttachOnto(CapooController below)
	{
		DetachFromBelow();
		StackBelow = below;
		below.StackAbove = this;
	}

	public void DetachFromBelow()
	{
		if (StackBelow == null) return;
		if (IsInstanceValid(StackBelow) && StackBelow.StackAbove == this) StackBelow.StackAbove = null;
		StackBelow = null;
	}

	/// <summary>Number of Capoos in this one's stack, from the bottom up to the top.</summary>
	public int StackSize()
	{
		var bottom = this;
		while (bottom.StackBelow != null) bottom = bottom.StackBelow;
		int count = 1;
		for (var c = bottom.StackAbove; c != null; c = c.StackAbove) count++;
		return count;
	}

	/// <summary>True if <paramref name="other"/> rides somewhere above this Capoo.</summary>
	public bool IsCarrying(CapooController other)
	{
		for (var c = StackAbove; c != null; c = c.StackAbove)
			if (c == other) return true;
		return false;
	}

	/// <summary>Called by PetManager after the Capoo below has moved.</summary>
	public void FollowStack()
	{
		if (StackBelow == null || !IsInstanceValid(StackBelow)) return;
		GlobalPosition = new Vector2(StackBelow.GlobalPosition.X, StackBelow.HeadY);
		if (FacingRight != StackBelow.FacingRight) SetFacing(StackBelow.FacingRight);
	}
}
