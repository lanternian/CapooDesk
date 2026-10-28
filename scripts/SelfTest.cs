using Godot;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Scripted interaction test (run with "-- --test --selftest"). Drives a virtual cursor through
/// PetManager's input path, checks the resulting states, saves viewport captures to user://
/// and quits. Does not touch the real mouse.
/// </summary>
public partial class SelfTest : Node
{
	private int passed;
	private int failed;
	private int shot;

	private static DesktopWindow Window => DesktopWindow.Instance;
	private static PetManager Pets => PetManager.Instance;

	public override async void _Ready()
	{
		try
		{
			await Run();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[selftest] crashed: {e}");
			failed++;
		}
		GD.Print($"[selftest] done: {passed} passed, {failed} failed");
		Window.VirtualCursor = null;
		GetTree().Quit(failed == 0 ? 0 : 1);
	}

	private async Task Run()
	{
		CapooController.AutonomyEnabled = false;
		Window.VirtualCursor = new Vector2(5, 5);
		await Wait(0.5);

		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		await Wait(0.2);

		float floor = Window.Size.Y;
		var a = Pets.SpawnPet(0, new Vector2(500, floor));
		var b = Pets.SpawnPet(1, new Vector2(900, floor));
		var c = Pets.SpawnPet(2, new Vector2(1300, floor));
		var d = Pets.SpawnPet(3, new Vector2(1700, floor));
		await Wait(2.0);
		Check("spawned pets settle to Idle", new[] { a, b, c, d }.All(p => p.State == CapooState.Idle), States());
		await Capture("spawned");

		// Click-through: away from pets the window must let clicks through, over a pet it must not.
		await MoveTo(new Vector2(200, 300));
		await Wait(0.1);
		Check("click-through away from pets", Window.IsClickThrough);
		await MoveTo(Center(a));
		await Wait(0.1);
		Check("hit-testable over a pet", !Window.IsClickThrough);

		// 1. Click -> hit reaction.
		await Click(Center(a));
		Check("click plays a hit reaction", a.State == CapooState.React, a.State.ToString());
		await Wait(1.8);
		Check("reaction returns to Idle", a.State == CapooState.Idle, a.State.ToString());

		// 2. Rub the cursor over B -> petting, then enjoy when it stops.
		Vector2 bc = Center(b);
		for (int i = 0; i < 40; i++)
		{
			Window.VirtualCursor = bc + new Vector2(i % 2 == 0 ? -45 : 45, 0);
			await Wait(0.03);
		}
		Check("rubbing starts petting", b.State == CapooState.Pet, b.State.ToString());
		await Capture("petting");
		await MoveTo(new Vector2(200, 300));
		await Wait(1.3);
		Check("petting ends with Enjoy", b.State == CapooState.Enjoy, b.State.ToString());

		// 3. Long press -> drag, throw upwards -> roll/air -> land.
		await Press(Center(a));
		await Wait(0.35);
		Check("long press starts dragging", a.State == CapooState.Dragged, a.State.ToString());
		await DragTo(Center(a) + new Vector2(150, -500), 12, 0.02);
		await Capture("dragging");
		await DragTo(Center(a) + new Vector2(100, -200), 3, 0.016);
		Release();
		Check("fast upward release rolls", a.State == CapooState.Roll, $"{a.State} v={a.Velocity}");
		await WaitFor(() => a.State == CapooState.Land, 4);
		Check("thrown Capoo lands", a.State is CapooState.Land or CapooState.Dizzy or CapooState.Idle, a.State.ToString());
		await Wait(2.5);

		// 4. Drop C onto D's head -> stacked.
		await Press(Center(c));
		await Wait(0.35);
		Vector2 above = new Vector2(d.GlobalPosition.X, d.HeadY - 250) + (Center(c) - c.GlobalPosition);
		await DragTo(above, 15, 0.02);
		await Wait(0.2);
		Release();
		await WaitFor(() => c.State == CapooState.Stacked, 3);
		Check("dropping onto a head stacks", c.State == CapooState.Stacked && c.StackBelow == d, $"{c.State} below={c.StackBelow?.Name}");
		await Wait(1.0);
		await Capture("stacked");
		Check("stack follows the base", Mathf.Abs(c.GlobalPosition.X - d.GlobalPosition.X) < 1f);

		// 5. Throw A sideways into B -> B gets dizzy.
		await Press(Center(a));
		await Wait(0.35);
		Vector2 grab = Center(a) - a.GlobalPosition;
		Vector2 start = new Vector2(b.GlobalPosition.X - 450, b.GlobalPosition.Y - 120) + grab;
		await DragTo(start, 10, 0.02);
		await DragTo(start + new Vector2(260, 0), 3, 0.016);
		Release();
		await WaitFor(() => b.State == CapooState.Dizzy, 2);
		Check("thrown Capoo bumps another (dizzy)", b.State == CapooState.Dizzy, $"b={b.State} a={a.State}");
		await Capture("bump");
		await Wait(3.0);

		// 6. Hungry bubble -> click bubble -> eat.
		b.DebugEnter(CapooState.Hungry);
		await Wait(0.6);
		Rect2 bubble = b.View.GetTalkBubbleRect();
		Check("hungry shows a bubble", bubble.Size != Vector2.Zero, bubble.ToString());
		await Capture("hungry");
		await Click(bubble.GetCenter());
		Check("clicking the bubble feeds", b.State == CapooState.Eating, b.State.ToString());
		await Wait(1.2);
		await Capture("eating");
		await WaitFor(() => b.State == CapooState.Idle, 6);
		Check("eating finishes", b.State == CapooState.Idle, b.State.ToString());

		// 7. Poo -> wait for cleanup -> click -> removed. (A may sit on someone's head after the bump.)
		var pooer = new[] { a, b, c, d }.First(p => p.StackBelow == null && p.StackAbove == null && p.State == CapooState.Idle);
		pooer.DebugEnter(CapooState.Pooing);
		await WaitFor(() => pooer.State == CapooState.WaitCleanPoo, 4);
		Check("pooing waits for cleanup", pooer.State == CapooState.WaitCleanPoo, pooer.State.ToString());
		await Capture("poo");
		await Click(Center(pooer));
		Check("clicking cleans the poo", pooer.State == CapooState.PooRemoving, pooer.State.ToString());
		await WaitFor(() => pooer.State == CapooState.Idle, 6);
		Check("cleanup finishes", pooer.State == CapooState.Idle, pooer.State.ToString());

		// Accessories are clickable too (C wears a duck hat).
		Vector2 hatPoint = c.View.GetBoneGlobalPosition("hat") + new Vector2(0, -c.BodySize.Y * 0.25f);
		Check("hats count as part of the Capoo", c.HitTest(hatPoint), hatPoint.ToString());

		GD.Print($"[selftest] perf: fps={Performance.GetMonitor(Performance.Monitor.TimeFps):0} process={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0}ms physics={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0}ms nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}");

		// 8. Sleep -> click wakes.
		d.ToggleSleep();
		await WaitFor(() => d.State == CapooState.Sleeping, 4);
		Check("nap: sleepy then sleeping", d.State == CapooState.Sleeping, d.State.ToString());
		await Capture("sleeping");
		await Click(Center(d));
		Check("click wakes a sleeping Capoo", d.State == CapooState.Waking, d.State.ToString());
		await Wait(2.0);

		// 9. Recycle everything.
		Pets.RecycleAll();
		await Wait(1.5);
		Check("recycle removes all", Pets.Pets.Count == 0, Pets.Pets.Count.ToString());
	}

	// ---- helpers ----

	private static Vector2 Center(CapooController pet) => pet.GlobalPosition + new Vector2(0, -pet.BodySize.Y * 0.45f);

	private async Task Wait(double seconds) =>
		await ToSignal(GetTree().CreateTimer(seconds), SceneTreeTimer.SignalName.Timeout);

	private async Task WaitFor(Func<bool> condition, double timeout)
	{
		double t = 0;
		while (!condition() && t < timeout)
		{
			await Wait(0.05);
			t += 0.05;
		}
	}

	private async Task MoveTo(Vector2 position)
	{
		Window.VirtualCursor = position;
		await Wait(0.05);
	}

	private async Task Press(Vector2 position)
	{
		await MoveTo(position);
		Window.VirtualLeftDown = true;
		Pets.HandleLeftPress(position);
	}

	private void Release()
	{
		Window.VirtualLeftDown = false;
		Pets.HandleLeftRelease(Window.CursorWorld);
	}

	private async Task Click(Vector2 position)
	{
		await Press(position);
		await Wait(0.08);
		Release();
		await Wait(0.05);
	}

	private async Task DragTo(Vector2 target, int steps, double stepTime)
	{
		Vector2 from = Window.CursorWorld;
		for (int i = 1; i <= steps; i++)
		{
			Window.VirtualCursor = from.Lerp(target, (float)i / steps);
			await Wait(stepTime);
		}
	}

	private string States() =>
		string.Join(", ", Pets.Pets.Select(p => $"{p.Name}:{p.State}@({p.GlobalPosition.X:0},{p.GlobalPosition.Y:0})"));

	private void Check(string name, bool ok, string detail = "")
	{
		if (ok) passed++;
		else failed++;
		GD.Print($"[selftest] {(ok ? "PASS" : "FAIL")} {name}{(ok || detail == "" ? "" : $"  ({detail})")}");
	}

	private async Task Capture(string name)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = GetViewport().GetTexture().GetImage();
		string path = $"user://selftest_{++shot:00}_{name}.png";
		image.SavePng(path);
		GD.Print($"[selftest] captured {ProjectSettings.GlobalizePath(path)}");
	}
}
