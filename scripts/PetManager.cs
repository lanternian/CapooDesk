using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Owns all Capoos on the desktop: summon/recycle, the saved roster, mouse routing to the
/// top-most Capoo, Capoo-to-Capoo collisions and stacking, and the shared sleep schedule.
/// Runs after the pets each frame (priority 10) so stacks follow the Capoo below them.
/// </summary>
public partial class PetManager : Node2D
{
	public static PetManager Instance { get; private set; }

	public const int MaxStackHeight = 12;

	public IReadOnlyList<CapooController> Pets => pets;

	/// <summary>No keyboard/mouse input for the configured time: Capoos fall asleep.</summary>
	public bool UserAway { get; private set; }

	/// <summary>Inside the configured night hours: Capoos nap a lot more.</summary>
	public bool IsNight { get; private set; }

	public event Action PetsChanged;
	public event Action<CapooController, Vector2> PetRightClicked;

	private readonly List<CapooController> pets = new List<CapooController>();
	private CapooController pressedPet;
	private CapooController hoveredPet;
	private Vector2 lastCursor;
	private double scheduleTimer;
	private double saveTimer;
	private int serial;

	private static SettingsData Settings => AppSettings.Instance.Data;

	public override void _EnterTree()
	{
		Instance = this;
		ProcessPriority = 10;
		ProcessPhysicsPriority = 10;
	}

	public override void _Ready()
	{
		DesktopWindow.Instance.AddHitTester(IsInteractiveAt);
		DesktopWindow.Instance.ScreenChanged += OnScreenChanged;
		AppSettings.Instance.Changed += OnSettingsChanged;
		RestoreRoster();
	}

	public override void _ExitTree()
	{
		SaveRoster();
		DesktopWindow.Instance?.RemoveHitTester(IsInteractiveAt);
		if (DesktopWindow.Instance != null) DesktopWindow.Instance.ScreenChanged -= OnScreenChanged;
		if (AppSettings.Instance != null) AppSettings.Instance.Changed -= OnSettingsChanged;
	}

	public override void _Notification(int what)
	{
		if (what == NotificationWMCloseRequest) SaveRoster();
	}

	// ---- Summon / recycle ----

	public CapooController SpawnPet(int infoIndex, Vector2? position = null)
	{
		if (pets.Count >= Settings.MaxPets) return null;

		var config = GetNode<CapooConfig>("/root/CapooConfig");
		infoIndex = Math.Clamp(infoIndex, 0, config.infos.Count - 1);

		var pet = new CapooController(infoIndex, Settings.PetScale)
		{
			Name = $"Capoo_{config.infos[infoIndex].id}_{serial++}",
			Position = position ?? RandomFloorPosition()
		};
		AddChild(pet);
		pets.Add(pet);
		PetsChanged?.Invoke();
		return pet;
	}

	public CapooController SpawnRandom(Vector2? position = null)
	{
		var config = GetNode<CapooConfig>("/root/CapooConfig");
		return SpawnPet(Random.Shared.Next(config.infos.Count), position);
	}

	public void RecycleAll()
	{
		foreach (var pet in pets.ToArray()) pet.StartRecycle();
	}

	/// <summary>Called by a Capoo when its recycle animation has finished.</summary>
	public void RemovePet(CapooController pet)
	{
		if (pressedPet == pet) ReleasePress();
		if (hoveredPet == pet) hoveredPet = null;
		if (pets.Remove(pet)) PetsChanged?.Invoke();
	}

	public void BringToFront(CapooController pet)
	{
		MoveChild(pet, GetChildCount() - 1);
		// Keep whatever rides on it drawn above it.
		for (var c = pet.StackAbove; c != null; c = c.StackAbove) MoveChild(c, GetChildCount() - 1);
	}

	private Vector2 RandomFloorPosition()
	{
		var size = DesktopWindow.Instance.Size;
		return new Vector2((float)(size.X * (0.15 + Random.Shared.NextDouble() * 0.7)), size.Y);
	}

	// ---- Roster ----

	private void RestoreRoster()
	{
		var saved = Settings.Pets;
		if (saved == null || saved.Count == 0)
		{
			SpawnPet(0);
			return;
		}

		var size = DesktopWindow.Instance.Size;
		foreach (var entry in saved)
		{
			var position = new Vector2(Mathf.Clamp(entry.X, 0, size.X), Mathf.Clamp(entry.Y, 0, size.Y));
			var pet = SpawnPet(entry.Index, position);
			pet?.SetFacing(entry.FacingRight);
		}
	}

	public void SaveRoster()
	{
		if (AppSettings.Instance == null) return;
		var roster = new List<PetSave>();
		foreach (var pet in pets)
		{
			if (!IsInstanceValid(pet) || pet.State == CapooState.Recycle) continue;
			roster.Add(new PetSave
			{
				Index = pet.InfoIndex,
				X = pet.GlobalPosition.X,
				Y = pet.GlobalPosition.Y,
				FacingRight = pet.FacingRight
			});
		}
		Settings.Pets = roster;
		AppSettings.Instance.Save();
	}

	// ---- Mouse routing ----

	/// <summary>Top-most Capoo under a point. Speech bubbles win over bodies, since they
	/// often overlap a neighbour (or the Capoo stacked on top).</summary>
	public CapooController PickPet(Vector2 world)
	{
		for (int i = GetChildCount() - 1; i >= 0; i--)
		{
			if (GetChild(i) is CapooController pet && pet.BubbleHitTest(world)) return pet;
		}
		for (int i = GetChildCount() - 1; i >= 0; i--)
		{
			if (GetChild(i) is CapooController pet && pet.HitTest(world)) return pet;
		}
		return null;
	}

	private bool IsInteractiveAt(Vector2 world) => pressedPet != null || PickPet(world) != null;

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton mouse) return;

		bool handled = mouse.ButtonIndex switch
		{
			MouseButton.Left when mouse.Pressed => HandleLeftPress(mouse.Position),
			MouseButton.Left => HandleLeftRelease(mouse.Position),
			MouseButton.Right when mouse.Pressed => HandleRightPress(mouse.Position),
			_ => false
		};
		if (handled) GetViewport().SetInputAsHandled();
	}

	public bool HandleLeftPress(Vector2 world)
	{
		var pet = PickPet(world);
		if (pet == null) return false;
		if (pet.BubbleHitTest(world))
		{
			pet.OnBubbleClicked();
		}
		else
		{
			pressedPet = pet;
			pet.OnPressStart(world);
			DesktopWindow.Instance.ForceOpaque = true;
		}
		return true;
	}

	public bool HandleLeftRelease(Vector2 world)
	{
		if (pressedPet == null) return false;
		pressedPet.OnPressEnd(world);
		ReleasePress();
		return true;
	}

	public bool HandleRightPress(Vector2 world)
	{
		var pet = PickPet(world);
		if (pet == null) return false;
		PetRightClicked?.Invoke(pet, world);
		return true;
	}

	private void ReleasePress()
	{
		pressedPet = null;
		DesktopWindow.Instance.ForceOpaque = false;
	}

	public override void _Process(double delta)
	{
		Vector2 cursor = DesktopWindow.Instance.CursorWorld;

		if (pressedPet != null)
		{
			if (!IsInstanceValid(pressedPet))
			{
				ReleasePress();
			}
			else if (!DesktopWindow.Instance.IsLeftButtonDown)
			{
				// Missed the release event (e.g. it happened outside the window).
				pressedPet.OnPressEnd(cursor);
				ReleasePress();
			}
			else
			{
				pressedPet.OnPressUpdate(cursor);
			}
		}
		else if (!DesktopWindow.Instance.IsAnyButtonDown)
		{
			var hovered = PickPet(cursor);
			if (hovered != null && hovered == hoveredPet) hovered.OnHover(cursor.DistanceTo(lastCursor));
			hoveredPet = hovered;
		}
		lastCursor = cursor;

		UpdateStacks();

		bool busy = pressedPet != null;
		foreach (var pet in pets)
		{
			if (busy) break;
			busy = pet.State.IsAirborne() || pet.State is CapooState.Dragged or CapooState.Hop or CapooState.Pet;
		}
		DesktopWindow.Instance.PetsBusy = busy;

		scheduleTimer -= delta;
		if (scheduleTimer <= 0)
		{
			scheduleTimer = 1.0;
			UpdateSchedule();
		}

		saveTimer += delta;
		if (saveTimer >= 60)
		{
			saveTimer = 0;
			SaveRoster();
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		UpdateStacks();
		UpdateCollisions((float)delta);
	}

	// ---- Stacks & collisions ----

	private void UpdateStacks()
	{
		foreach (var pet in pets)
		{
			if (pet.StackBelow != null || pet.StackAbove == null) continue;
			for (var c = pet.StackAbove; c != null; c = c.StackAbove) c.FollowStack();
		}
	}

	/// <summary>A falling Capoo crossed another one's head line this frame: stack it there.</summary>
	public bool TryStackOnto(CapooController faller, Vector2 previousPosition)
	{
		if (!Settings.Stacking) return false;

		float before = previousPosition.Y;
		float now = faller.GlobalPosition.Y;
		int fallerHeight = 1;
		for (var c = faller.StackAbove; c != null; c = c.StackAbove) fallerHeight++;

		for (int i = GetChildCount() - 1; i >= 0; i--)
		{
			if (GetChild(i) is not CapooController target || target == faller) continue;
			if (target.StackAbove != null || !target.State.CanCarryStack() || faller.IsCarrying(target)) continue;
			if (target.StackSize() + fallerHeight > MaxStackHeight) continue;

			float head = target.HeadY;
			if (before > head + 6 || now < head - 6) continue;
			if (Mathf.Abs(faller.GlobalPosition.X - target.GlobalPosition.X) > target.BodySize.X * 0.3f) continue;

			faller.LandOnHead(target);
			BringToFront(target.StackBelow ?? target);
			return true;
		}
		return false;
	}

	private void UpdateCollisions(float delta)
	{
		for (int i = 0; i < pets.Count; i++)
		{
			var a = pets[i];
			for (int j = i + 1; j < pets.Count; j++)
			{
				var b = pets[j];
				if (a.IsCarrying(b) || b.IsCarrying(a)) continue;

				if (a.State.IsAirborne() && b.CanBeBumped) TryBump(a, b);
				else if (b.State.IsAirborne() && a.CanBeBumped) TryBump(b, a);
				else Separate(a, b, delta);
			}
		}
	}

	private static void TryBump(CapooController thrown, CapooController target)
	{
		if (thrown.Velocity.Length() < 1100f) return;
		if (!thrown.BodyRect.Intersects(target.BodyRect)) return;
		target.Bumped();
		thrown.Velocity = new Vector2(-thrown.Velocity.X * 0.4f, thrown.Velocity.Y * 0.5f);
	}

	/// <summary>Resting Capoos that overlap a lot slowly shuffle apart.</summary>
	private static void Separate(CapooController a, CapooController b, float delta)
	{
		if (!IsResting(a) || !IsResting(b)) return;
		if (a.FloorHwnd != b.FloorHwnd || Mathf.Abs(a.GlobalPosition.Y - b.GlobalPosition.Y) > 12f) return;

		float dx = b.GlobalPosition.X - a.GlobalPosition.X;
		float minDistance = (a.BodySize.X + b.BodySize.X) * 0.25f;
		float overlap = minDistance - Mathf.Abs(dx);
		if (overlap <= 0) return;

		float push = Mathf.Min(overlap * 0.5f, 30f * delta);
		float dir = dx >= 0 ? 1f : -1f;
		if (Mathf.IsZeroApprox(dx)) dir = a.GetInstanceId() < b.GetInstanceId() ? 1f : -1f;
		a.Carry(new Vector2(-dir * push, 0));
		b.Carry(new Vector2(dir * push, 0));
	}

	private static bool IsResting(CapooController pet) =>
		pet.StackBelow == null && pet.State is CapooState.Idle or CapooState.Enjoy or CapooState.Sleeping
			or CapooState.Sleepy or CapooState.Hungry or CapooState.WaitCleanPoo;

	/// <summary>Move the Capoos standing on a window that moved.</summary>
	public void CarryPetsOn(long hwnd, Vector2 delta)
	{
		foreach (var pet in pets)
		{
			if (pet.FloorHwnd != hwnd || pet.StackBelow != null) continue;
			if (pet.State is CapooState.Dragged or CapooState.Recycle || pet.State.IsAirborne()) continue;
			pet.Carry(delta);
		}
	}

	// ---- Sleep schedule ----

	private void UpdateSchedule()
	{
		var s = Settings;
		bool away = s.SleepEnabled && Win32.GetSystemIdleSeconds() >= s.SleepIdleMinutes * 60.0;
		if (UserAway && !away)
		{
			foreach (var pet in pets) pet.OnUserReturned();
		}
		UserAway = away;

		int hour = DateTime.Now.Hour;
		IsNight = s.SleepEnabled && s.NightSleep && (s.NightStartHour > s.NightEndHour
			? hour >= s.NightStartHour || hour < s.NightEndHour
			: hour >= s.NightStartHour && hour < s.NightEndHour);
	}

	// ---- Settings / screen ----

	private void OnSettingsChanged()
	{
		float scale = Settings.PetScale;
		foreach (var pet in pets)
		{
			if (!Mathf.IsEqualApprox(pet.PetScale, scale)) pet.ApplyScale(scale);
		}
	}

	private void OnScreenChanged()
	{
		var size = DesktopWindow.Instance.Size;
		foreach (var pet in pets)
		{
			Vector2 p = pet.GlobalPosition;
			pet.GlobalPosition = new Vector2(Mathf.Clamp(p.X, 0, size.X), Mathf.Min(p.Y, size.Y));
		}
	}
}
