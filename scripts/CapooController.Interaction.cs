using Godot;
using System;
using System.Collections.Generic;
using static CapooActionPools;

/// <summary>
/// Mouse interaction, routed here by PetManager so only the top-most Capoo reacts:
/// click = hit reaction / feed / clean / wake, long press = drag, release = throw,
/// rubbing the cursor over it = petting.
/// </summary>
public partial class CapooController
{
	private const double DragHoldTime = 0.25;
	private const float DragStartDistance = 10f;
	private const double RubDecay = 260;        // px of rubbing forgotten per second
	private const float RubThreshold = 260f;    // px of rubbing to start petting (at scale 0.5)

	private bool pressed;
	private Vector2 pressPosition;
	private double pressTime;
	private Vector2 grabOffset;
	private double hitCooldown;
	private double rubAmount;
	private double lastRubTime;
	private readonly Queue<(double time, Vector2 position)> dragSamples = new Queue<(double, Vector2)>();

	private static double Now => Time.GetTicksMsec() / 1000.0;

	public bool IsPressed => pressed;

	public void OnPressStart(Vector2 world)
	{
		pressed = true;
		pressPosition = world;
		pressTime = Now;
		grabOffset = GlobalPosition - world;
	}

	/// <summary>Called every frame while the button is held.</summary>
	public void OnPressUpdate(Vector2 world)
	{
		if (!pressed || state == CapooState.Dragged) return;
		if (world.DistanceTo(pressPosition) > DragStartDistance || Now - pressTime > DragHoldTime)
			TryStartDrag();
	}

	public void OnPressEnd(Vector2 world)
	{
		if (!pressed) return;
		pressed = false;
		if (state == CapooState.Dragged)
			Release();
		else if (Now - pressTime <= 0.6)
			OnClicked();
	}

	public void OnBubbleClicked()
	{
		if (state == CapooState.Hungry) Feed();
		else if (state == CapooState.WaitCleanPoo) CleanPoo();
	}

	/// <summary>The cursor moved <paramref name="distance"/> px over this Capoo with no button held.</summary>
	public void OnHover(float distance)
	{
		if (distance < 0.5f) return;
		rubAmount += distance;
		lastRubTime = Now;

		if (state == CapooState.Pet) return;
		bool canPet = state.IsGroundIdle() || state == CapooState.Stacked;
		if (canPet && rubAmount > RubThreshold * PetScale * 2f)
		{
			rubAmount = 0;
			EnterState(CapooState.Pet);
		}
	}

	/// <summary>Context-menu "pet": plays the petting reaction for a moment.</summary>
	public void PetOnce()
	{
		if (!(state.IsGroundIdle() || state is CapooState.Stacked or CapooState.React)) return;
		EnterState(CapooState.Pet);
		lastRubTime = Now + 1.5;
	}

	private void OnClicked()
	{
		switch (state)
		{
			case CapooState.Hungry:
				Feed();
				return;
			case CapooState.WaitCleanPoo:
				CleanPoo();
				return;
			case CapooState.Sleepy:
			case CapooState.Sleeping:
				Wake();
				return;
			case CapooState.Appear:
			case CapooState.Recycle:
			case CapooState.Pooing:
			case CapooState.Eating:
			case CapooState.PooRemoving:
			case CapooState.Dragged:
			case CapooState.Air:
			case CapooState.Roll:
			case CapooState.Hop:
			case CapooState.Land:
			case CapooState.Waking:
				return;
		}

		if (hitCooldown > 0) return;
		hitCooldown = 0.4;
		React(RandomFrom(Hit));
	}

	// ---- Dragging ----

	private void TryStartDrag()
	{
		if (!state.CanBeDragged()) return;

		// Picking it up interrupts whatever it was waiting for; nothing is lost.
		if (state == CapooState.WaitCleanPoo)
		{
			view.HideLayer(CapooView.Layer.Poo);
			needs.Pooed();
		}
		else if (state == CapooState.Hungry)
		{
			needs.HungerIgnored(Settings.CareLevel);
		}

		DetachFromBelow();
		dragSamples.Clear();
		EnterState(CapooState.Dragged);
		PetManager.Instance?.BringToFront(this);
	}

	private void UpdateDrag()
	{
		var window = DesktopWindow.Instance;
		Vector2 cursor = window.CursorWorld;
		Vector2 target = cursor + grabOffset;
		target.X = Mathf.Clamp(target.X, 0, window.Size.X);
		target.Y = Mathf.Clamp(target.Y, BodySize.Y * 0.5f, window.Size.Y);

		float dx = target.X - GlobalPosition.X;
		if (Mathf.Abs(dx) > 3f) SetFacing(dx > 0);
		GlobalPosition = target;

		double now = Now;
		dragSamples.Enqueue((now, cursor));
		while (dragSamples.Count > 2 && now - dragSamples.Peek().time > 0.1) dragSamples.Dequeue();
	}

	private void Release()
	{
		Vector2 velocity = Vector2.Zero;
		if (dragSamples.Count >= 2)
		{
			var first = dragSamples.Peek();
			(double time, Vector2 position) last = default;
			foreach (var sample in dragSamples) last = sample;
			double dt = last.time - first.time;
			if (dt > 0.005) velocity = (last.position - first.position) / (float)dt;
		}
		Velocity = velocity.LimitLength(MaxThrowSpeed);
		EnterState(Velocity.Y < -250f ? CapooState.Roll : CapooState.Air);
	}

	/// <summary>Forget a press without reacting (e.g. the pet was recycled meanwhile).</summary>
	public void CancelPress()
	{
		pressed = false;
		if (state == CapooState.Dragged) Release();
	}
}
