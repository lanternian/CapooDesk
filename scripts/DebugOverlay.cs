using Godot;
using System.Text;

/// <summary>
/// Debug info (toggled from the tray): FPS/timings, pet outlines with their state,
/// window platforms and the click-through state.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
	private Label label;
	private Node2D drawer;
	private double labelTimer;

	public override void _Ready()
	{
		drawer = new Node2D();
		drawer.Draw += OnDraw;
		AddChild(drawer);

		label = new Label { Position = new Vector2(24, 24), MouseFilter = Control.MouseFilterEnum.Ignore };
		label.AddThemeFontSizeOverride("font_size", 18);
		label.AddThemeColorOverride("font_color", Colors.White);
		label.AddThemeColorOverride("font_outline_color", Colors.Black);
		label.AddThemeConstantOverride("outline_size", 6);
		AddChild(label);
	}

	public override void _Process(double delta)
	{
		if (!Visible) return;
		drawer.QueueRedraw();

		labelTimer -= delta;
		if (labelTimer > 0) return;
		labelTimer = 0.5;

		var window = DesktopWindow.Instance;
		var manager = PetManager.Instance;
		var sb = new StringBuilder();
		sb.AppendLine($"FPS {Performance.GetMonitor(Performance.Monitor.TimeFps):0}  process {Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0}ms  physics {Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0}ms");
		sb.AppendLine($"draw calls {Performance.GetMonitor(Performance.Monitor.RenderTotalDrawCallsInFrame):0}  nodes {Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}  mem {OS.GetStaticMemoryUsage() / 1048576.0:0}MB");
		sb.AppendLine($"window {window.Mode} {window.Size}  click-through {window.IsClickThrough}  cursor {window.CursorWorld}");
		if (manager != null)
			sb.AppendLine($"pets {manager.Pets.Count}  away {manager.UserAway}  night {manager.IsNight}  idle {Win32.GetSystemIdleSeconds():0}s  fullscreen {DesktopWorld.Instance?.FullscreenAppActive}");
		label.Text = sb.ToString();
	}

	private void OnDraw()
	{
		var font = ThemeDB.FallbackFont;
		if (DesktopWorld.Instance != null)
		{
			foreach (var (from, to) in DesktopWorld.Instance.GetPlatformSegments())
				drawer.DrawLine(from, to, Colors.Orange, 4);
		}
		if (PetManager.Instance == null) return;
		foreach (var pet in PetManager.Instance.Pets)
		{
			var outline = pet.GetWorldOutline();
			var closed = new Vector2[outline.Length + 1];
			outline.CopyTo(closed, 0);
			closed[^1] = outline[0];
			drawer.DrawPolyline(closed, pet.IsPressed ? Colors.Red : Colors.Cyan, 2);
			drawer.DrawRect(pet.BodyRect, new Color(1, 1, 0, 0.6f), false, 1);
			drawer.DrawLine(new Vector2(pet.GlobalPosition.X - 30, pet.HeadY), new Vector2(pet.GlobalPosition.X + 30, pet.HeadY), Colors.Magenta, 2);
			var bubble = pet.View.GetTalkBubbleRect();
			if (pet.HasBubble && bubble.Size != Vector2.Zero) drawer.DrawRect(bubble, Colors.Green, false, 2);
			string text = $"{pet.State} {(pet.FloorHwnd != 0 ? "win" : "")}";
			drawer.DrawString(font, pet.GlobalPosition + new Vector2(-40, 18), text, HorizontalAlignment.Left, -1, 16, Colors.Yellow);
		}
	}
}
