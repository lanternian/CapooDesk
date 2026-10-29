using Godot;
using System;
using System.Collections.Generic;
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

	/// <summary>Run the frame-time benchmark instead of the interaction script.</summary>
	public bool PerfMode;

	/// <summary>Window-awareness test against a window titled "CapooTestWindow".</summary>
	public bool WindowMode;

	/// <summary>Build every character once and check that all its parts load.</summary>
	public bool LoadMode;

	/// <summary>Measure the clickable area of every character (126), not only of samples.</summary>
	public bool AllHitAreaMode;

	/// <summary>Save frame-by-frame pictures of the summon and the recycle.</summary>
	public bool CaptureMode;

	public override async void _Ready()
	{
		// The tests clear the desktop and change settings; never do that to the real ones.
		if (!AppSettings.IsTestRun)
		{
			GD.PrintErr("[selftest] refusing to run without --test (it would overwrite the real settings)");
			GetTree().Quit(1);
			return;
		}
		// Every run starts from the defaults, so an earlier run cannot change the outcome.
		AppSettings.Instance.ResetToDefaults();

		// Keep the overlay off the user's desktop; the viewport keeps rendering so the
		// captures below still show what the pets are doing.
		Window.SetTestHidden(true);
		try
		{
			if (PerfMode) await RunPerf();
			else if (WindowMode) await RunWindowTest();
			else if (LoadMode) await RunLoadTest();
			else if (AllHitAreaMode) await RunAllHitAreas();
			else if (CaptureMode) await RunCapture();
			else await Run();
		}
		catch (Exception e)
		{
			GD.PrintErr($"[selftest] crashed: {e}");
			GD.PrintErr(e.StackTrace ?? "");
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
		// Aim below B's head line: a Capoo that falls onto a head would stack instead of bump.
		Vector2 start = new Vector2(b.GlobalPosition.X - 260, b.GlobalPosition.Y - 60) + grab;
		await DragTo(start, 10, 0.02);
		await DragTo(start + new Vector2(200, 0), 4, 0.02);
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

		// The bubble has to mirror with the Capoo, like in the original Unity version.
		b.SetFacing(false);
		await Wait(0.5);
		Rect2 bubbleLeft = b.View.GetTalkBubbleRect();
		b.SetFacing(true);
		await Wait(0.5);
		Rect2 bubbleRight = b.View.GetTalkBubbleRect();
		float offsetLeft = bubbleLeft.GetCenter().X - b.GlobalPosition.X;
		float offsetRight = bubbleRight.GetCenter().X - b.GlobalPosition.X;
		Check("the hungry bubble mirrors with the Capoo",
			Mathf.Abs(offsetLeft + offsetRight) < 12 && Mathf.Abs(offsetLeft) > 20,
			$"left {offsetLeft:0} right {offsetRight:0}");
		// The bubble animates, so its box moves a little between the two measurements.
		Check("the hungry bubble keeps its size",
			Mathf.Abs(bubbleLeft.Size.X - bubbleRight.Size.X) < 12 &&
			Mathf.Abs(bubbleLeft.Size.Y - bubbleRight.Size.Y) < 12,
			$"{bubbleLeft.Size} {bubbleRight.Size}");
		await Capture("hungry_right");

		await Click(bubbleRight.GetCenter());
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
		Vector2 hatBone = c.View.GetBoneGlobalPosition("hat");
		bool hatHit = false;
		for (int dx = -60; dx <= 60 && !hatHit; dx += 6)
		{
			for (int dy = -60; dy <= 40 && !hatHit; dy += 6)
			{
				if (c.View.AccessoryHitTest(hatBone + new Vector2(dx, dy))) hatHit = true;
			}
		}
		Check("hats count as part of the Capoo", hatHit, hatBone.ToString());

		GD.Print($"[selftest] perf: fps={Performance.GetMonitor(Performance.Monitor.TimeFps):0} process={Performance.GetMonitor(Performance.Monitor.TimeProcess) * 1000:0.0}ms physics={Performance.GetMonitor(Performance.Monitor.TimePhysicsProcess) * 1000:0.0}ms nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0}");

		// 8. Sleep -> click wakes.
		d.ToggleSleep();
		await WaitFor(() => d.State == CapooState.Sleeping, 4);
		Check("nap: sleepy then sleeping", d.State == CapooState.Sleeping, d.State.ToString());
		await Capture("sleeping");
		await Click(Center(d));
		Check("click wakes a sleeping Capoo", d.State == CapooState.Waking, d.State.ToString());
		await Wait(2.0);

		// 9. UI: right-click menu and the three book tabs.
		var book = GetNode<MagicBook>("/root/Main/UI");
		book.OnPetRightClicked(b, Center(b));
		await Wait(0.3);
		await Capture("pet_menu");
		book.ShowBook();
		for (int tab = 0; tab < 3; tab++)
		{
			book.ShowTab(tab);
			await Wait(0.5);
			await Capture($"book_tab{tab}");
		}
		CheckBookLayout(book);
		book.HideBook();
		Check("book closes", !book.IsBookOpen);

		// 10. Away from the computer -> everyone falls asleep; back -> they wake up one by one.
		CapooController.AutonomyEnabled = true;
		AppSettings.Instance.Data.SleepEnabled = true;
		Pets.DebugIdleSeconds = 24 * 3600;
		await WaitFor(() => Pets.Pets.All(p => p.IsAsleep || p.StackBelow != null), 20);
		Check("idle user: Capoos fall asleep", Pets.Pets.All(p => p.IsAsleep || p.StackBelow != null), States());
		await Capture("all_asleep");
		Pets.DebugIdleSeconds = 0;
		await WaitFor(() => Pets.Pets.All(p => !p.IsAsleep), 10);
		Check("user back: Capoos wake up", Pets.Pets.All(p => !p.IsAsleep), States());
		Pets.DebugIdleSeconds = null;
		CapooController.AutonomyEnabled = false;

		// 11. Recycle everything.
		Pets.RecycleAll();
		await Wait(1.5);
		Check("recycle removes all", Pets.Pets.Count == 0, Pets.Pets.Count.ToString());

		// 12. Summon and recycle: body, face and accessories come and go together.
		await CheckSummonSync();

		// 13. The book on a 1080p screen (100% zoom) as well.
		Vector2I fullSize = Window.Size;
		Window.SetTestSize(new Vector2I(1920, 1040));
		await Wait(0.4);
		book.ShowBook();
		for (int tab = 0; tab < 3; tab++)
		{
			book.ShowTab(tab);
			await Wait(0.4);
			await Capture($"book_1080p_tab{tab}");
		}
		CheckBookLayout(book);
		book.HideBook();
		Window.SetTestSize(fullSize);
		await Wait(0.3);

		// 14. The area around a Capoo that reacts to clicks.
		await CheckHitArea();
	}

	/// <summary>Colours that identify the three layers of one Capoo: the blue body, the pink
	/// blush of the face and the yellow helmet. Counting them in the viewport tells us which
	/// layers are actually on screen.</summary>
	private static readonly Color BodyBlue = new Color(90f / 255f, 201f / 255f, 232f / 255f);
	private static readonly Color FacePink = new Color(255f / 255f, 116f / 255f, 116f / 255f);
	private static readonly Color HatYellow = new Color(253f / 255f, 218f / 255f, 116f / 255f);

	private async Task<(int blue, int pink, int yellow)> CountColors(Rect2 rect)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var img = GetViewport().GetTexture().GetImage();
		return (CountColor(img, rect, BodyBlue), CountColor(img, rect, FacePink), CountColor(img, rect, HatYellow));
	}

	private static int CountColor(Image image, Rect2 rect, Color target)
	{
		Vector2I size = image.GetSize();
		int x0 = Mathf.Max(0, (int)rect.Position.X);
		int y0 = Mathf.Max(0, (int)rect.Position.Y);
		int x1 = Mathf.Min(size.X, (int)rect.End.X);
		int y1 = Mathf.Min(size.Y, (int)rect.End.Y);
		int count = 0;
		for (int y = y0; y < y1; y++)
		{
			for (int x = x0; x < x1; x++)
			{
				Color c = image.GetPixel(x, y);
				if (c.A < 0.5f) continue;
				if (Mathf.Abs(c.R - target.R) < 0.04f && Mathf.Abs(c.G - target.G) < 0.04f && Mathf.Abs(c.B - target.B) < 0.04f) count++;
			}
		}
		return count;
	}

	/// <summary>Measures how much of the area around a Capoo reacts to clicks although it is
	/// empty space, and checks that clicks next to the Capoo fall through to the desktop.</summary>
	private async Task CheckHitArea()
	{
		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		await Wait(0.3);

		int[] characters = { 0, 1, 7, 20, 40, 48, 60, 90 };
		int worst = 0;
		string worstName = "";
		foreach (int index in characters)
		{
			float x = Window.Size.X * 0.3f;
			var pet = Pets.SpawnPet(index, new Vector2(x, Window.Size.Y));
			await Wait(1.5); // let it appear and settle
			pet.View.SetAnimation("idle_idle1", force: true);
			// The effects (sparkles, mood) hang above the Capoo, they are not part of it.
			pet.View.SetDecorationsVisible(false);
			await Wait(0.3);

			var measured = await MeasureHitArea(pet);
			if (measured == null)
			{
				Check($"hit area {pet.DisplayName}: the Capoo is drawn", false, "nothing on screen");
				continue;
			}
			int ratio = measured.Ratio;
			GD.Print($"[selftest] hit area {pet.DisplayName}: picture={measured.Picture * 4}px2 " +
				$"blank={measured.Blank * 4}px2 ({ratio}%) covered={measured.Coverage}% " +
				$"pictureBox={measured.PictureBox.Size} hitBox={measured.HitBox.Size} " +
				$"picPos={measured.PictureBox.Position} hitPos={measured.HitBox.Position} petPos={pet.GlobalPosition} win={Window.Size}");
			if (ratio > worst)
			{
				worst = ratio;
				worstName = pet.DisplayName;
			}
			// The box that reacts to clicks may not stick out much further than the Capoo.
			float grow = measured.Grow;
			// The default Capoo has to fit closely (that is the rule of the issue), the
			// others may differ a little: their accessories move while the idle plays.
			float allowed = index == 0 ? 10 : 20;
			Check($"hit area {pet.DisplayName}: little empty space around it", ratio <= 10, $"{ratio}%");
			Check($"hit area {pet.DisplayName}: the clickable box fits the Capoo", grow <= allowed, $"{grow:0}px");
			if (index == 0)
			{
				// A click next to the Capoo has to reach the desktop.
				var outside = new Vector2(measured.PictureBox.Position.X - 15, measured.PictureBox.GetCenter().Y);
				Window.VirtualCursor = outside;
				await Wait(0.2);
				Check("hit area: a click next to the Capoo falls through", Window.IsClickThrough, outside.ToString());
				var inside = measured.PictureBox.GetCenter();
				Window.VirtualCursor = inside;
				await Wait(0.2);
				Check("hit area: a click on the Capoo does not fall through", !Window.IsClickThrough, inside.ToString());
			}
			Pets.RemovePet(pet);
			pet.QueueFree();
			await Wait(0.3);
		}
		Check("hit area: no Capoo has a large dead zone", worst <= 10, $"{worstName} {worst}%");
	}

	/// <summary>What the measurement of one Capoo found.</summary>
	private class HitArea
	{
		/// <summary>Painted pixels (counted on the scan grid, not in square pixels).</summary>
		public int Picture;

		/// <summary>Pixels that react to clicks although nothing is drawn there.</summary>
		public int Blank;

		/// <summary>Painted pixels that also react to clicks.</summary>
		public int Covered;

		public Rect2 PictureBox;
		public Rect2 HitBox;

		/// <summary>The empty clickable area in percent of the artwork.</summary>
		public int Ratio => Picture > 0 ? Blank * 100 / Picture : 0;

		/// <summary>The part of the artwork that reacts to clicks, in percent.</summary>
		public int Coverage => Picture > 0 ? Covered * 100 / Picture : 0;

		/// <summary>How much the clickable box differs from the box of the artwork.</summary>
		public float Grow => Mathf.Max(Mathf.Abs(HitBox.Size.X - PictureBox.Size.X),
			Mathf.Abs(HitBox.Size.Y - PictureBox.Size.Y));
	}

	/// <summary>Measures the clickable area of a Capoo. The scan covers the artwork plus a
	/// margin, so a clickable shape that sticks out is counted as empty space.</summary>
	private async Task<HitArea> MeasureHitArea(CapooController pet)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = GetViewport().GetTexture().GetImage();
		// The alpha channel tells which pixels show a Capoo and which are empty.
		if (image.GetFormat() != Image.Format.Rgba8)
		{
			GD.PrintErr($"[selftest] the viewport format is {image.GetFormat()}, the hit area cannot be measured");
			return null;
		}
		int width = image.GetWidth();
		int height = image.GetHeight();
		var pixels = image.GetData();
		bool Painted(int x, int y) => pixels[(y * width + x) * 4 + 3] > 127;

		// 1. Where is the artwork? A coarse scan around the Capoo finds it.
		float floor = Window.Size.Y;
		var coarse = new Rect2(pet.GlobalPosition.X - 320, floor - 380, 640, 420);
		int cx0 = Mathf.Max(0, (int)coarse.Position.X);
		int cy0 = Mathf.Max(0, (int)coarse.Position.Y);
		int cx1 = Mathf.Min(width, (int)coarse.End.X);
		int cy1 = Mathf.Min(height, (int)coarse.End.Y);
		var art = new Rect2();
		bool artSeen = false;
		for (int y = cy0; y < cy1; y += 4)
		{
			for (int x = cx0; x < cx1; x += 4)
			{
				if (!Painted(x, y)) continue;
				var point = new Vector2(x, y);
				if (!artSeen) { art = new Rect2(point, Vector2.Zero); artSeen = true; }
				art = art.Expand(point);
			}
		}
		if (!artSeen) return null;

		// 2. The fine scan: every painted pixel and every clickable pixel.
		var region = art.Grow(70).Intersection(new Rect2(0, 0, width, height));
		var result = new HitArea();
		bool picSeen = false;
		bool hitSeen = false;
		for (int y = (int)region.Position.Y; y < region.End.Y; y += 2)
		{
			for (int x = (int)region.Position.X; x < region.End.X; x += 2)
			{
				var point = new Vector2(x, y);
				bool drawn = Painted(x, y);
				bool hit = pet.HitTest(point);
				if (drawn)
				{
					result.Picture++;
					if (hit) result.Covered++;
					if (!picSeen) { result.PictureBox = new Rect2(point, Vector2.Zero); picSeen = true; }
					result.PictureBox = result.PictureBox.Expand(point);
				}
				if (hit)
				{
					if (!hitSeen) { result.HitBox = new Rect2(point, Vector2.Zero); hitSeen = true; }
					result.HitBox = result.HitBox.Expand(point);
					if (!drawn) result.Blank++;
				}
			}
		}
		return result;
	}

	/// <summary>Measures the clickable area of every character and reports the ones that
	/// do not fit: too much empty space, or artwork that cannot be clicked at all.</summary>
	private async Task RunAllHitAreas()
	{
		CapooController.AutonomyEnabled = false;
		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		await Wait(0.3);

		var config = GetNode<CapooConfig>("/root/CapooConfig");
		float x = Window.Size.X * 0.3f;
		var blanks = new List<string>();
		var loose = new List<string>();
		var dark = new List<string>();
		var table = new System.Text.StringBuilder();
		table.AppendLine("index,id,name,picture_px2,blank_px2,blank_percent,covered_percent,grow_px");
		int measured = 0;
		int worst = 0;
		string worstName = "";
		int worstCovered = 100;
		string worstCoveredName = "";
		float worstGrow = 0;
		string worstGrowName = "";
		float defaultGrow = 0;

		for (int index = 0; index < config.infos.Count; index++)
		{
			var info = config.infos[index];
			var pet = Pets.SpawnPet(index, new Vector2(x, Window.Size.Y));
			await Wait(1.4); // let it appear and settle
			pet.View.SetAnimation("idle_idle1", force: true);
			pet.View.SetDecorationsVisible(false);
			await Wait(0.25);

			var area = await MeasureHitArea(pet);
			if (area == null)
			{
				GD.PrintErr($"[selftest] hitarea {index:000} {info.id} {info.name}: nothing on screen");
				blanks.Add($"{info.name}(nothing drawn)");
				Pets.RemovePet(pet);
				pet.QueueFree();
				await Wait(0.2);
				continue;
			}
			measured++;
			GD.Print($"[selftest] hitarea {index:000} {info.id} {info.name}: picture={area.Picture * 4}px2 " +
				$"blank={area.Blank * 4}px2 ({area.Ratio}%) covered={area.Coverage}% " +
				$"pictureBox={area.PictureBox.Size} hitBox={area.HitBox.Size} grow={area.Grow:0}px");
			table.AppendLine($"{index},{info.id},{info.name},{area.Picture * 4},{area.Blank * 4}," +
				$"{area.Ratio},{area.Coverage},{area.Grow:0}");
			if (area.Ratio > 10) blanks.Add($"{info.name} {area.Ratio}%");
			if (area.Coverage < 85) dark.Add($"{info.name} {area.Coverage}%");
			// The shape of an attachment is built for one pose. Artwork that moves while
			// the Capoo idles (a tail, an ear) is a little off, that is a known limit.
			if (area.Grow > 16) loose.Add($"{info.name} {area.Grow:0}px");
			if (index == 0) defaultGrow = area.Grow;
			if (area.Ratio > worst)
			{
				worst = area.Ratio;
				worstName = info.name;
			}
			if (area.Coverage < worstCovered)
			{
				worstCovered = area.Coverage;
				worstCoveredName = info.name;
			}
			if (area.Grow > worstGrow)
			{
				worstGrow = area.Grow;
				worstGrowName = info.name;
			}
			Pets.RemovePet(pet);
			pet.QueueFree();
			await Wait(0.2);
		}

		GD.Print($"[selftest] hitarea summary: {measured} characters measured, " +
			$"{blanks.Count} with too much empty space, {dark.Count} not fully clickable, " +
			$"{loose.Count} with artwork that moves away from the shape");
		GD.Print($"[selftest] hitarea worst values: empty space {worstName} {worst}%, " +
			$"clickable artwork {worstCoveredName} {worstCovered}%, box {worstGrowName} {worstGrow:0}px");
		if (blanks.Count > 0) GD.Print("[selftest] hitarea empty space: " + string.Join(", ", blanks));
		if (dark.Count > 0) GD.Print("[selftest] hitarea not clickable: " + string.Join(", ", dark));
		if (loose.Count > 0)
			GD.Print("[selftest] hitarea known limit (the shape is built for one pose): " + string.Join(", ", loose));

		// The whole table, so the numbers can be looked at after the run.
		using (var csv = FileAccess.Open("user://hit_area_sweep.csv", FileAccess.ModeFlags.Write))
		{
			csv?.StoreString(table.ToString());
		}
		GD.Print($"[selftest] hitarea table: {ProjectSettings.GlobalizePath("user://hit_area_sweep.csv")}");

		Check("hit area: every character is measured", measured == config.infos.Count, $"{measured}/{config.infos.Count}");
		Check("hit area: every character can be clicked", dark.Count == 0, string.Join(", ", dark));
		Check("hit area: no character has a large dead zone", blanks.Count == 0, $"{blanks.Count} characters, worst {worstName} {worst}%");
		Check("hit area: the clickable box fits the default Capoo", defaultGrow <= 10, $"{defaultGrow:0}px");
	}

	/// <summary>Nothing on the book pages may overlap, and nothing may leave the paper.
	/// Also checks the book on a 1080p screen, where it has to fit as well.</summary>
	private void CheckBookLayout(MagicBook book)
	{
		var overlaps = new List<string>();
		var outside = new List<string>();
		for (int tab = 0; tab < 3; tab++)
		{
			var rects = book.GetLayoutRects(tab);
			// The parts of a page must not sit on top of each other.
			for (int i = 0; i < rects.Count; i++)
			{
				for (int j = i + 1; j < rects.Count; j++)
				{
					if (!rects[i].rect.Intersects(rects[j].rect)) continue;
					overlaps.Add($"tab{tab} {rects[i].name}/{rects[j].name}");
				}
			}
			// Everything that belongs to a page has to stay on the paper.
			foreach (var (name, rect) in rects)
			{
				if (name.StartsWith("tab") || name == "close") continue; // the tabs stick out on purpose
				bool onLeft = MagicBook.LeftPageArea.Grow(2).Encloses(rect);
				bool onRight = MagicBook.RightPageArea.Grow(2).Encloses(rect);
				if (!onLeft && !onRight) outside.Add($"tab{tab} {name} {rect}");
			}
		}
		Check("book: nothing overlaps", overlaps.Count == 0, string.Join(", ", overlaps));
		Check("book: everything stays on the paper", outside.Count == 0, string.Join(", ", outside));

		// The tabs sit behind the cover, but their icons must stay in sight.
		var cover = UiTheme.Tex("ui_pop_collection_01").GetImage();
		var hidden = new List<string>();
		var tabRects = book.GetLayoutRects(0).Where(r => r.name.StartsWith("tab")).ToList();
		string[] tabImages = { "01", "03", "04" };
		for (int i = 0; i < tabRects.Count; i++)
		{
			var tabImage = UiTheme.Tex($"tab_menu_{tabImages[i]}_h").GetImage();
			int icon = 0, covered = 0;
			for (int y = 0; y < tabImage.GetHeight(); y++)
			{
				for (int x = 0; x < tabImage.GetWidth(); x++)
				{
					Color c = tabImage.GetPixel(x, y);
					if (c.A < 0.8f || c.R < 0.92f || c.G < 0.92f || c.B < 0.92f) continue; // the white icon
					icon++;
					int bx = (int)(tabRects[i].rect.Position.X + x), by = (int)(tabRects[i].rect.Position.Y + y);
					if (bx < cover.GetWidth() && by < cover.GetHeight() && cover.GetPixel(bx, by).A > 0.5f) covered++;
				}
			}
			if (icon == 0 || covered > icon * 0.02) hidden.Add($"tab{i} {covered}/{icon} icon pixels under the cover");
		}
		Check("book: the tab icons are not hidden by the cover", hidden.Count == 0, string.Join(", ", hidden));

		// The book has to fit on a 1080p screen as well.
		Vector2 size = book.BookRect.Size;
		Check("book: fits on a 1080p screen", size.X <= 1920 && size.Y <= 1040, size.ToString());

		// The default tooltip is a dark box, which is unreadable with the brown text.
		var theme = UiTheme.Get();
		var tooltipBox = theme.GetStylebox("panel", "TooltipPanel") as StyleBoxFlat;
		var tooltipInk = theme.GetColor("font_color", "TooltipLabel");
		Check("book: tooltips are readable",
			tooltipBox != null && tooltipBox.BgColor.R > 0.8f && tooltipInk.R < 0.6f,
			$"{tooltipBox?.BgColor} {tooltipInk}");
	}

	/// <summary>Waits until the blush of the face is visible. The blush blinks, so a
	/// measurement has to start while it shows.</summary>
	private async Task<bool> WaitForBlush(Rect2 rect, float timeout)
	{
		float waited = 0;
		while (waited < timeout)
		{
			var colors = await CountColors(rect);
			if (colors.pink > 0) return true;
			await Wait(0.05);
			waited += 0.05f;
		}
		return false;
	}

	/// <summary>Summons a Capoo with a blue body, a pink face and a yellow helmet and watches
	/// the layers frame by frame: they must all appear (and vanish) in the same frame.</summary>
	private async Task CheckSummonSync()
	{
		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		await Wait(0.3);

		const int character = 7; // 工地咖波: blue body, pink blush, yellow helmet
		float x = Window.Size.X * 0.3f;
		var pet = Pets.SpawnPet(character, new Vector2(x, Window.Size.Y));
		// The idle action is picked at random and only some faces have the blush:
		// use the one that always does, so the pink pixels mean "the face is there".
		pet.View.SetAnimation("idle_idle1", force: true);
		var rect = new Rect2(x - 140, Window.Size.Y - 230, 280, 250);

		var frames = new List<(int blue, int pink, int yellow)>();
		var smoke = new List<bool>();
		for (int i = 0; i < 14; i++)
		{
			frames.Add(await CountColors(rect));
			smoke.Add(pet.AppearSprite != null && pet.AppearSprite.Visible);
			if (i == 3) await Capture("summon_smoke");
			await Wait(0.04);
		}
		GD.Print("[selftest] summon frames: " + string.Join(" | ",
			frames.Select(f => $"{f.blue}/{f.pink}/{f.yellow}")));
		Check("summon: the character is hidden while the smoke plays",
			frames.Take(6).All(f => f.blue == 0 && f.pink == 0 && f.yellow == 0),
			string.Join(" ", frames.Take(6).Select(f => $"{f.blue}/{f.pink}/{f.yellow}")));
		// The blush of the face blinks, so only the direction that never lies is checked:
		// the face and the helmet may not show up without the body.
		Check("summon: body, face and helmet appear together",
			frames.Any(f => f.blue > 0 && f.pink > 0 && f.yellow > 0) &&
			frames.All(f => (f.pink > 0) == (f.blue > 0) || f.pink == 0 && f.blue > 0) &&
			frames.All(f => f.yellow == 0 || f.blue > 0),
			string.Join(" ", frames.Select(f => $"{f.blue}/{f.pink}/{f.yellow}")));
		Check("summon: the smoke plays while the character is hidden", smoke.Take(6).Any(v => v));
		await Capture("summon");

		await Wait(0.6);
		pet.View.SetAnimation("idle_idle1", force: true);
		// The blush blinks, wait until it shows before the Capoo is recycled.
		await WaitForBlush(rect, 4.0f);
		pet.StartRecycle();
		Check("recycle: the smoke plays", pet.AppearSprite != null && pet.AppearSprite.Visible);
		// The smoke would hide the Capoo behind it; take it away to watch the layers.
		if (pet.AppearSprite != null) pet.AppearSprite.Visible = false;
		var recycle = new List<(int blue, int pink, int yellow)>();
		for (int i = 0; i < 16; i++)
		{
			recycle.Add(await CountColors(rect));
			await Wait(0.04);
		}
		GD.Print("[selftest] recycle frames: " + string.Join(" | ",
			recycle.Select(f => $"{f.blue}/{f.pink}/{f.yellow}")));
		Check("recycle: body, face and helmet vanish together",
			recycle.Any(f => f.blue == 0 && f.pink == 0 && f.yellow == 0) &&
			recycle.All(f => f.pink == 0 || f.blue > 0) &&
			recycle.All(f => f.yellow == 0 || f.blue > 0),
			string.Join(" ", recycle.Select(f => $"{f.blue}/{f.pink}/{f.yellow}")));
		Check("recycle: no body left behind", recycle.Skip(Math.Max(0, recycle.Count - 4)).All(f => f.blue == 0 && f.pink == 0 && f.yellow == 0),
			string.Join(" ", recycle.Select(f => $"{f.blue}/{f.pink}/{f.yellow}")));

		// A second Capoo shows what the recycle looks like with the smoke in front of it.
		float x2 = Window.Size.X * 0.65f;
		var smokePet = Pets.SpawnPet(character, new Vector2(x2, Window.Size.Y));
		await Wait(1.4);
		smokePet.StartRecycle();
		await Wait(0.12);
		await Capture("recycle_smoke");
		await Wait(0.3);
		await Capture("recycle_smoke_late");
		await Wait(0.6);
		Check("recycle: nothing is left of the recycled Capoo",
			CountColor(GetViewport().GetTexture().GetImage(), new Rect2(x2 - 140, Window.Size.Y - 230, 280, 250), BodyBlue) == 0);
	}

	private async Task RunPerf()
	{
		Window.VirtualCursor = new Vector2(5, 5);
		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		AppSettings.Instance.Data.MaxPets = 50;
		foreach (string variant in new[] { "normal", "no-physics", "no-spine", "neither" })
		{
			while (Pets.Pets.Count < 5) Pets.SpawnRandom();
			await Wait(2.0);
			foreach (var p in Pets.Pets)
			{
				p.SetPhysicsProcess(variant is "normal" or "no-spine");
				p.View.ProcessMode = variant is "normal" or "no-physics" ? ProcessModeEnum.Inherit : ProcessModeEnum.Disabled;
			}
			await Wait(1.0);
			var proc0 = System.Diagnostics.Process.GetCurrentProcess();
			var cpu0 = proc0.TotalProcessorTime;
			ulong wall0 = Time.GetTicksUsec();
			await Wait(4.0);
			proc0.Refresh();
			double ms = (proc0.TotalProcessorTime - cpu0).TotalMilliseconds;
			GD.Print($"[selftest] perf variant={variant} pets=5 cpu={ms / ((Time.GetTicksUsec() - wall0) / 1000.0) * 100:0}% of one core");
		}
		foreach (var p in Pets.Pets)
		{
			p.SetPhysicsProcess(true);
			p.View.ProcessMode = ProcessModeEnum.Inherit;
		}
		foreach (int target in new[] { 0, 1, 5, 10, 20 })
		{
			if (target == 0)
			{
				foreach (var p in Pets.Pets.ToArray())
				{
					Pets.RemovePet(p);
					p.QueueFree();
				}
			}
			while (Pets.Pets.Count < target) Pets.SpawnRandom();
			await Wait(3.0);
			var proc = System.Diagnostics.Process.GetCurrentProcess();
			var cpuStart = proc.TotalProcessorTime;
			ulong wallStart = Time.GetTicksUsec();
			double fps = 0;
			for (int i = 0; i < 10; i++)
			{
				await Wait(0.4);
				fps += Performance.GetMonitor(Performance.Monitor.TimeFps) / 10;
			}
			proc.Refresh();
			double cpuMs = (proc.TotalProcessorTime - cpuStart).TotalMilliseconds;
			double wallMs = (Time.GetTicksUsec() - wallStart) / 1000.0;
			GD.Print($"[selftest] perf pets={target} fps={fps:0} cpu={cpuMs / wallMs * 100:0}% of one core ({cpuMs / wallMs * 100 / System.Environment.ProcessorCount:0.0}% total) cpu/frame={cpuMs / (wallMs / 1000 * fps):0.0}ms nodes={Performance.GetMonitor(Performance.Monitor.ObjectNodeCount):0} mem={OS.GetStaticMemoryUsage() / 1048576.0:0}MB");
		}
		passed++;
	}

	private async Task RunWindowTest()
	{
		CapooController.AutonomyEnabled = false;
		Window.VirtualCursor = new Vector2(5, 5);

		IntPtr hwnd = IntPtr.Zero;
		for (int i = 0; i < 100 && hwnd == IntPtr.Zero; i++)
		{
			hwnd = Win32.FindWindow(null, "CapooTestWindow");
			if (hwnd == IntPtr.Zero) await Wait(0.1);
		}
		Check("test window found", hwnd != IntPtr.Zero);
		if (hwnd == IntPtr.Zero) return;

		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		await Wait(0.6);

		Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT rect, System.Runtime.InteropServices.Marshal.SizeOf<Win32.RECT>());
		Vector2 top = Window.ScreenToWorld(new Vector2I((rect.Left + rect.Right) / 2, rect.Top));
		bool hasPlatform = DesktopWorld.Instance.GetPlatformSegments().Any(s => Mathf.Abs(s.from.Y - top.Y) < 2 && s.from.X <= top.X && s.to.X >= top.X);
		Check("window top edge becomes a platform", hasPlatform, $"top={top}");

		var pet = Pets.SpawnPet(0, new Vector2(top.X, Window.Size.Y));
		await Wait(1.5);
		await Press(Center(pet));
		await Wait(0.35);
		await DragTo(new Vector2(top.X, top.Y - 260) + (Center(pet) - pet.GlobalPosition), 15, 0.02);
		await Wait(0.25);
		Release();
		await WaitFor(() => pet.State == CapooState.Idle, 4);
		Check("dropped Capoo lands on the window", pet.FloorHwnd == hwnd.ToInt64() && Mathf.Abs(pet.GlobalPosition.Y - top.Y) < 3,
			$"floor={pet.FloorHwnd} y={pet.GlobalPosition.Y} top={top.Y} state={pet.State}");
		await Capture("on_window");

		// Walking towards the edge: it either stops/turns at the edge or hops down, never floats.
		var walker = Pets.SpawnPet(1, new Vector2(top.X - 150, top.Y - 200));
		await WaitFor(() => walker.State == CapooState.Idle && walker.FloorHwnd == hwnd.ToInt64(), 4);
		walker.DebugWalkTo(Window.ScreenToWorld(new Vector2I(rect.Right, 0)).X + 300);
		await Wait(8.0);
		await WaitFor(() => walker.State == CapooState.Idle, 4);
		bool onWindow = walker.FloorHwnd == hwnd.ToInt64() && Mathf.Abs(walker.GlobalPosition.Y - top.Y) < 3;
		bool onFloor = Mathf.Abs(walker.GlobalPosition.Y - Window.Size.Y) < 3;
		Check("walking to the window edge stops or hops down", onWindow || onFloor, $"{walker.State} {walker.GlobalPosition}");
		walker.StartRecycle();
		await Wait(1.0);

		Vector2 before = pet.GlobalPosition;
		Win32.SetWindowPos(hwnd, IntPtr.Zero, rect.Left + 200, rect.Top - 150, 0, 0,
			Win32.SWP_NOSIZE | Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
		await Wait(0.6);
		Vector2 moved = pet.GlobalPosition - before;
		Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out Win32.RECT after, System.Runtime.InteropServices.Marshal.SizeOf<Win32.RECT>());
		var windowMoved = new Vector2(after.Left - rect.Left, after.Top - rect.Top);
		Check("Capoo rides along when the window moves", moved.DistanceTo(windowMoved) < 2, $"pet {moved} window {windowMoved}");
		await Capture("carried");

		Win32.PostMessage(hwnd, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
		await WaitFor(() => pet.State.IsAirborne(), 2);
		Check("Capoo falls when the window closes", pet.State.IsAirborne(), pet.State.ToString());
		await WaitFor(() => pet.State == CapooState.Idle, 4);
		Check("...and lands on the taskbar", Mathf.Abs(pet.GlobalPosition.Y - Window.Size.Y) < 3, pet.GlobalPosition.ToString());
	}

	/// <summary>Frame-by-frame pictures of a summon and a recycle, for the issue reports.</summary>
	private async Task RunCapture()
	{
		CapooController.AutonomyEnabled = false;
		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		await Wait(0.3);

		float x = Window.Size.X * 0.3f;
		var pet = Pets.SpawnPet(7, new Vector2(x, Window.Size.Y));
		pet.View.SetAnimation("idle_idle1", force: true);
		for (int i = 0; i < 12; i++)
		{
			await Capture($"appear_{i * 5:000}");
			await Wait(0.05);
		}
		await Wait(0.5);
		pet.StartRecycle();
		for (int i = 0; i < 12; i++)
		{
			await Capture($"recycle_{i * 5:000}");
			await Wait(0.05);
		}

		// The book on its very first frame.
		var book = GetNode<MagicBook>("/root/Main/UI");
		book.ShowBook();
		await Capture("book_first_frame");
		await Wait(0.1);
		await Capture("book_0.1s");
		await Wait(0.5);
		await Capture("book_0.6s");

		// The tooltip of a page tab (the real mouse never moves, the event is injected).
		Vector2 tab = book.FirstTab.GetGlobalRect().GetCenter();
		Input.ParseInputEvent(new InputEventMouseMotion { Position = tab, GlobalPosition = tab });
		await Wait(0.9);
		await Capture("book_tooltip");
		book.HideBook();

		// The area that reacts to clicks, painted over the picture of the Capoo.
		var plain = Pets.SpawnPet(7, new Vector2(x, Window.Size.Y));
		plain.View.SetAnimation("idle_idle1", force: true);
		await Wait(1.5);
		var hitArea = new Rect2I((int)x - 150, (int)Window.Size.Y - 250, 300, 240);
		await Capture("hit_area", hitArea, plain);
		Pets.RemovePet(plain);
		plain.QueueFree();
		await Wait(0.5);

		// The characters whose clickable box the sweep found too far from the artwork.
		foreach (int character in new[] { 40, 47, 50, 69, 73, 86, 120 })
		{
			var odd = Pets.SpawnPet(character, new Vector2(x, Window.Size.Y));
			odd.View.SetAnimation("idle_idle1", force: true);
			await Wait(1.5);
			odd.View.SetDecorationsVisible(false);
			await Wait(0.3);
			await Capture($"hitarea_{character}", new Rect2I((int)x - 200, (int)Window.Size.Y - 300, 400, 290), odd);
			Pets.RemovePet(odd);
			odd.QueueFree();
			await Wait(0.3);
		}

		// The bubble of the poo, looking left and looking right.
		var poo = Pets.SpawnPet(7, new Vector2(x, Window.Size.Y));
		poo.View.SetAnimation("idle_idle1", force: true);
		await Wait(1.0);
		poo.DebugEnter(CapooState.Pooing);
		await WaitFor(() => poo.View.GetTalkBubbleRect().Size != Vector2.Zero, 5);
		var bubbleArea = new Rect2I((int)x - 150, (int)Window.Size.Y - 250, 300, 240);
		poo.SetFacing(false);
		await Wait(0.8);
		await Capture("bubble_left", bubbleArea);
		poo.SetFacing(true);
		await Wait(0.8);
		await Capture("bubble_right", bubbleArea);
	}

	private async Task RunLoadTest()
	{
		foreach (var p in Pets.Pets.ToArray())
		{
			Pets.RemovePet(p);
			p.QueueFree();
		}
		var config = GetNode<CapooConfig>("/root/CapooConfig");
		Check("126 characters configured", config.infos.Count == 126, config.infos.Count.ToString());
		int broken = 0;
		for (int i = 0; i < config.infos.Count; i++)
		{
			var view = new CapooView(i) { Position = new Vector2(200 + (i % 10) * 180, 300 + (i / 10 % 4) * 220) };
			AddChild(view);
			bool ok = view.SetAnimation("idle_idle1") && view.GetActionDuration("hit1") > 0;
			if (!ok)
			{
				broken++;
				GD.PrintErr($"[selftest] character {i} ({config.infos[i].id} {config.infos[i].name}) failed to animate");
			}
			if (i % 40 == 39)
			{
				await Wait(0.3);
				await Capture($"characters_{i / 40}");
			}
			if (i % 10 == 9)
			{
				await Wait(0.05);
				foreach (var child in GetChildren()) if (child is CapooView v) v.QueueFree();
			}
		}
		Check("every character loads and animates", broken == 0, $"{broken} broken");
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

	private async Task Capture(string name, Rect2I? region = null, CapooController pet = null)
	{
		await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
		var image = GetViewport().GetTexture().GetImage();
		if (pet != null && region.HasValue)
		{
			// Paint the area that reacts to clicks over the picture: the artwork that reacts
			// stays as it is, artwork that does not react turns green, and empty space that
			// reacts turns magenta.
			var area = region.Value;
			for (int y = area.Position.Y; y < area.End.Y; y++)
			{
				for (int x = area.Position.X; x < area.End.X; x++)
				{
					Color c = image.GetPixel(x, y);
					bool painted = c.A > 0.5f;
					bool hit = pet.HitTest(new Vector2(x, y));
					if (painted && !hit) image.SetPixel(x, y, new Color(0f, 0.9f, 0.2f, 1f));
					else if (!painted && hit) image.SetPixel(x, y, new Color(0.9f, 0.1f, 0.9f, 1f));
				}
			}
		}
		if (region.HasValue) image = image.GetRegion(region.Value);
		string path = $"user://selftest_{++shot:00}_{name}.png";
		image.SavePng(path);
		GD.Print($"[selftest] captured {ProjectSettings.GlobalizePath(path)}");
	}
}
