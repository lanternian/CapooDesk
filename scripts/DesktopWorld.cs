using Godot;
using System.Collections.Generic;

public static class PhysicsLayers
{
	public const uint World = 1;     // floor (taskbar), walls, ceiling
	public const uint Platform = 2;  // top edges of other windows
	public const uint Pet = 4;
}

/// <summary>
/// The physical desktop: a floor on top of the taskbar, walls at the screen edges, and
/// one-way platforms along the visible top edges of other windows (window awareness).
/// Capoos standing on a window move with it and fall when it closes or minimizes.
/// </summary>
public partial class DesktopWorld : Node2D
{
	public static DesktopWorld Instance { get; private set; }

	public bool FullscreenAppActive { get; private set; }

	private class Platform
	{
		public long Hwnd;
		public Rect2I Bounds;
		public readonly List<StaticBody2D> Bodies = new List<StaticBody2D>();
		public bool Seen;
	}

	private const double ScanInterval = 0.25;

	private readonly Dictionary<long, Platform> platforms = new Dictionary<long, Platform>();
	private readonly HashSet<long> occupied = new HashSet<long>();
	private StaticBody2D bounds;
	private WindowTracker tracker;
	private double scanTimer;
	private double fullscreenTimer;

	private static SettingsData Settings => AppSettings.Instance.Data;

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		tracker = new WindowTracker(DesktopWindow.Instance.Hwnd);
		BuildBounds();
		DesktopWindow.Instance.ScreenChanged += OnScreenChanged;
	}

	public override void _ExitTree()
	{
		if (DesktopWindow.Instance != null) DesktopWindow.Instance.ScreenChanged -= OnScreenChanged;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Settings.WindowAware)
		{
			TrackOccupiedWindows();
			scanTimer -= delta;
			if (scanTimer <= 0)
			{
				scanTimer = ScanInterval;
				Rescan();
			}
		}
		else if (platforms.Count > 0)
		{
			ClearPlatforms();
		}

		fullscreenTimer -= delta;
		if (fullscreenTimer <= 0)
		{
			fullscreenTimer = 0.5;
			bool fullscreen = Settings.HideOnFullscreen && tracker.IsForegroundFullscreen(DesktopWindow.Instance.MonitorRect);
			if (fullscreen != FullscreenAppActive)
			{
				FullscreenAppActive = fullscreen;
				DesktopWindow.Instance.SetAutoHidden(fullscreen);
			}
		}
	}

	/// <summary>Visible platform segments in world coordinates (debug drawing).</summary>
	public IEnumerable<(Vector2 from, Vector2 to)> GetPlatformSegments()
	{
		foreach (var platform in platforms.Values)
		{
			foreach (var body in platform.Bodies)
			{
				var shape = (SegmentShape2D)body.GetChild<CollisionShape2D>(0).Shape;
				yield return (body.Position + shape.A, body.Position + shape.B);
			}
		}
	}

	// ---- Bounds ----

	private void OnScreenChanged()
	{
		BuildBounds();
		ClearPlatforms();
		scanTimer = 0;
	}

	private void BuildBounds()
	{
		bounds?.QueueFree();
		var size = DesktopWindow.Instance.Size;
		bounds = new StaticBody2D { Name = "Bounds", CollisionLayer = PhysicsLayers.World, CollisionMask = 0 };
		AddBoundary(new Vector2(0, size.Y), Vector2.Up);       // floor = top of the taskbar
		AddBoundary(Vector2.Zero, Vector2.Down);               // ceiling
		AddBoundary(Vector2.Zero, Vector2.Right);              // left wall
		AddBoundary(new Vector2(size.X, 0), Vector2.Left);     // right wall
		AddChild(bounds);
	}

	private void AddBoundary(Vector2 position, Vector2 normal)
	{
		bounds.AddChild(new CollisionShape2D
		{
			Position = position,
			Shape = new WorldBoundaryShape2D { Normal = normal, Distance = 0 }
		});
	}

	// ---- Window platforms ----

	private void TrackOccupiedWindows()
	{
		occupied.Clear();
		var manager = PetManager.Instance;
		if (manager == null) return;
		foreach (var pet in manager.Pets)
		{
			if (pet.FloorHwnd != 0) occupied.Add(pet.FloorHwnd);
		}

		// Follow windows with Capoos on them every frame so they ride along smoothly.
		foreach (long hwnd in occupied)
		{
			if (!platforms.TryGetValue(hwnd, out var platform)) continue;
			if (tracker.TryGetBounds(hwnd, out var current))
				MoveIfChanged(platform, current);
			else
				RemovePlatform(platform);
		}
	}

	private void Rescan()
	{
		var window = DesktopWindow.Instance;
		var area = new Rect2I(window.Origin, window.Size);
		var edges = tracker.Scan(area);

		foreach (var platform in platforms.Values) platform.Seen = false;
		foreach (var edge in edges)
		{
			if (!platforms.TryGetValue(edge.Hwnd, out var platform))
			{
				platform = new Platform { Hwnd = edge.Hwnd, Bounds = edge.Bounds };
				platforms[edge.Hwnd] = platform;
			}
			else
			{
				MoveIfChanged(platform, edge.Bounds);
			}
			platform.Seen = true;
			RebuildBodies(platform, edge, window.Origin);
		}

		var gone = new List<Platform>();
		foreach (var platform in platforms.Values)
		{
			if (!platform.Seen) gone.Add(platform);
		}
		foreach (var platform in gone) RemovePlatform(platform);
	}

	private void MoveIfChanged(Platform platform, Rect2I current)
	{
		Vector2I delta = current.Position - platform.Bounds.Position;
		platform.Bounds = current;
		if (delta == Vector2I.Zero) return;

		var offset = new Vector2(delta.X, delta.Y);
		foreach (var body in platform.Bodies) body.Position += offset;
		PetManager.Instance?.CarryPetsOn(platform.Hwnd, offset);
	}

	private void RebuildBodies(Platform platform, WindowTracker.Edge edge, Vector2I origin)
	{
		while (platform.Bodies.Count > edge.Spans.Count)
		{
			platform.Bodies[^1].QueueFree();
			platform.Bodies.RemoveAt(platform.Bodies.Count - 1);
		}
		while (platform.Bodies.Count < edge.Spans.Count)
		{
			var body = new StaticBody2D { CollisionLayer = PhysicsLayers.Platform, CollisionMask = 0 };
			body.SetMeta("hwnd", platform.Hwnd);
			body.AddChild(new CollisionShape2D
			{
				Shape = new SegmentShape2D(),
				OneWayCollision = true,
				OneWayCollisionMargin = 16
			});
			AddChild(body);
			platform.Bodies.Add(body);
		}

		float y = edge.Bounds.Position.Y - origin.Y;
		for (int i = 0; i < edge.Spans.Count; i++)
		{
			var body = platform.Bodies[i];
			var shape = (SegmentShape2D)body.GetChild<CollisionShape2D>(0).Shape;
			body.Position = new Vector2(0, y);
			shape.A = new Vector2(edge.Spans[i].X - origin.X, 0);
			shape.B = new Vector2(edge.Spans[i].Y - origin.X, 0);
		}
	}

	private void RemovePlatform(Platform platform)
	{
		foreach (var body in platform.Bodies) body.QueueFree();
		platform.Bodies.Clear();
		platforms.Remove(platform.Hwnd);
	}

	private void ClearPlatforms()
	{
		foreach (var platform in new List<Platform>(platforms.Values)) RemovePlatform(platform);
	}
}
