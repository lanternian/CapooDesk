using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Autoload: turns the main Godot window into a transparent, click-through desktop overlay.
/// The window covers the work area of one screen, so world coordinates = screen pixels - Origin.
/// Clicks only reach Godot while the cursor is over something interactive (pets, UI);
/// everywhere else WS_EX_TRANSPARENT lets them fall through to the desktop.
/// </summary>
public partial class DesktopWindow : Node
{
	public static DesktopWindow Instance { get; private set; }

	public enum TransparencyMode { Alpha, ColorKey }

	// Colour-key fallback: the cleared background is black.
	private const uint ColorKeyRef = 0x00000000;

	public IntPtr Hwnd { get; private set; }
	public Vector2I Origin { get; private set; }
	public Vector2I Size { get; private set; }
	public TransparencyMode Mode { get; private set; }

	/// <summary>Another CapooDesk is already running; this one quits without touching saved data.</summary>
	public bool IsDuplicateInstance { get; private set; }
	public Vector2 CursorWorld { get; private set; }
	public bool IsClickThrough => clickThrough;

	/// <summary>Full bounds of the monitor the overlay is on (screen pixels).</summary>
	public Rect2I MonitorRect { get; private set; }

	/// <summary>Self-test: a scripted cursor replaces the real one (see SelfTest.cs).</summary>
	public Vector2? VirtualCursor { get; set; }
	public bool VirtualLeftDown { get; set; }

	public bool IsLeftButtonDown => VirtualCursor.HasValue ? VirtualLeftDown : Win32.IsKeyDown(Win32.VK_LBUTTON);
	public bool IsAnyButtonDown => VirtualCursor.HasValue ? VirtualLeftDown : Win32.IsAnyMouseButtonDown();

	/// <summary>Something is moving fast or being handled (set by PetManager / UI every frame):
	/// keeps the full frame rate while low-power mode is on.</summary>
	public bool PetsBusy { get; set; }
	public bool UiBusy { get; set; }

	/// <summary>Keep the window hit-testable regardless of the cursor (e.g. while dragging a pet).</summary>
	public bool ForceOpaque { get; set; }

	/// <summary>Raised when the overlay is moved/resized to another screen or work area.</summary>
	public event Action ScreenChanged;

	private readonly List<Func<Vector2, bool>> hitTesters = new List<Func<Vector2, bool>>();
	private readonly HashSet<Control> interactiveControls = new HashSet<Control>();
	private bool clickThrough;
	private bool stylesApplied;
	private bool autoHidden;
	private bool testHidden;
	private bool windowHidden;
	private double maintenanceTimer;
	private int screen;
	private Rect2I lastUsableRect;
	private static System.Threading.Mutex instanceMutex;

	public override void _EnterTree()
	{
		Instance = this;

		// The window of the engine is visible for a moment before the app sets it up.
		// Hide it right away, so the desktop only shows the Capoos.
		Hwnd = new IntPtr(DisplayServer.WindowGetNativeHandle(DisplayServer.HandleType.WindowHandle));
		if (Hwnd != IntPtr.Zero) Win32.ShowWindow(Hwnd, Win32.SW_HIDE);
	}

	public override void _Ready()
	{
		if (!AcquireSingleInstance())
		{
			IsDuplicateInstance = true;
			GD.Print("CapooDesk is already running.");
			GetTree().Quit();
			return;
		}

		Mode = ResolveMode();

		ApplyTransparency();
		FitToScreen(AppSettings.Instance.Data.ScreenIndex);
		ApplyStyles(hideFromTaskbar: true);
		SetClickThrough(true);
		ApplyFrameRate();
		UpdateVisibility();

		AppSettings.Instance.Changed += OnSettingsChanged;
		GD.Print($"DesktopWindow: mode={Mode} origin={Origin} size={Size}");
	}

	public override void _ExitTree()
	{
		if (AppSettings.Instance != null) AppSettings.Instance.Changed -= OnSettingsChanged;
	}

	public override void _Process(double delta)
	{
		if (Hwnd == IntPtr.Zero) return;

		if (VirtualCursor.HasValue)
		{
			CursorWorld = VirtualCursor.Value;
		}
		else
		{
			Win32.GetCursorPos(out var p);
			CursorWorld = new Vector2(p.X - Origin.X, p.Y - Origin.Y);
		}
		SetClickThrough(!IsInteractiveAt(CursorWorld));
		ApplyFrameRate();

		maintenanceTimer += delta;
		if (maintenanceTimer >= 2.0)
		{
			maintenanceTimer = 0;
			Maintain();
		}
	}

	// ---- Public API ----

	public void AddHitTester(Func<Vector2, bool> tester) => hitTesters.Add(tester);
	public void RemoveHitTester(Func<Vector2, bool> tester) => hitTesters.Remove(tester);

	/// <summary>Controls that should receive clicks while visible (book button, panels, menus).</summary>
	public void RegisterInteractive(Control control)
	{
		interactiveControls.Add(control);
		control.TreeExiting += () => interactiveControls.Remove(control);
	}

	public Vector2 ScreenToWorld(Vector2I screenPos) => new Vector2(screenPos.X - Origin.X, screenPos.Y - Origin.Y);
	public Vector2I WorldToScreen(Vector2 worldPos) => new Vector2I(Mathf.RoundToInt(worldPos.X) + Origin.X, Mathf.RoundToInt(worldPos.Y) + Origin.Y);

	/// <summary>Temporarily hide everything (e.g. while a fullscreen game or video is in front).</summary>
	public void SetAutoHidden(bool hidden)
	{
		autoHidden = hidden;
		UpdateVisibility();
	}

	/// <summary>Self-test: hide the overlay so it does not cover the user's desktop.
	/// The viewport keeps rendering (viewport captures still work) and the frame rate stays up,
	/// unlike the automatic hide above which throttles to 5 fps.</summary>
	public void SetTestHidden(bool hidden)
	{
		testHidden = hidden;
		UpdateVisibility();
	}

	/// <summary>Self-test: pretend the overlay is a different size, so the UI lays out again.</summary>
	public void SetTestSize(Vector2I size)
	{
		DisplayServer.WindowSetSize(size);
		Size = size;
		ScreenChanged?.Invoke();
	}

	/// <summary>Apply the user's "hide Capoos" toggle from the settings.</summary>
	public void UpdateVisibility()
	{
		bool hidden = testHidden || autoHidden || AppSettings.Instance.Data.PetsHidden;
		windowHidden = hidden;
		Win32.ShowWindow(Hwnd, hidden ? Win32.SW_HIDE : Win32.SW_SHOWNOACTIVATE);
		if (!hidden) ApplyTopmost();
	}

	// ---- Setup ----

	private static bool AcquireSingleInstance()
	{
		// A test run must not be blocked by (or block) the real app: it gets its own lock.
		string name = AppSettings.IsTestRun ? "CapooDesk.SingleInstance.Test" : "CapooDesk.SingleInstance";
		instanceMutex = new System.Threading.Mutex(true, name, out bool createdNew);
		return createdNew;
	}

	private TransparencyMode ResolveMode()
	{
		string mode = AppSettings.Instance.Data.TransparencyMode;
		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg.StartsWith("--tmode=")) mode = arg.Substring("--tmode=".Length);
		}
		return mode == "colorkey" ? TransparencyMode.ColorKey : TransparencyMode.Alpha;
	}

	private void ApplyTransparency()
	{
		// Both modes clear to transparent black. Alpha relies on DWM per-pixel alpha;
		// ColorKey additionally keys out pure black, which also works on drivers that ignore
		// the alpha channel (pure-black art pixels become click-through holes in that mode).
		GetViewport().TransparentBg = true;
		DisplayServer.WindowSetFlag(DisplayServer.WindowFlags.Transparent, true);
	}

	public void FitToScreen(int screenIndex)
	{
		int count = DisplayServer.GetScreenCount();
		screen = screenIndex >= 0 && screenIndex < count ? screenIndex : DisplayServer.GetPrimaryScreen();

		Rect2I usable = DisplayServer.ScreenGetUsableRect(screen);
		lastUsableRect = usable;
		var full = new Rect2I(DisplayServer.ScreenGetPosition(screen), DisplayServer.ScreenGetSize(screen));
		MonitorRect = full;
		// A window covering the whole monitor gets "fullscreen optimised" by the driver,
		// which drops transparency. Stay one pixel short (only matters with an auto-hide taskbar).
		if (usable.Size == full.Size) usable.Size -= new Vector2I(0, 1);

		DisplayServer.WindowSetPosition(usable.Position);
		DisplayServer.WindowSetSize(usable.Size);
		Origin = usable.Position;
		Size = usable.Size;
		ScreenChanged?.Invoke();
	}

	/// <summary>Hide from taskbar/Alt-Tab and keep the layered style. Godot rewrites
	/// GWL_EXSTYLE whenever it touches window flags, so this is re-applied on demand.</summary>
	private void ApplyStyles(bool hideFromTaskbar = false)
	{
		long ex = Win32.GetStyle(Hwnd, Win32.GWL_EXSTYLE);
		long wanted = (ex | Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_NOACTIVATE) & ~Win32.WS_EX_APPWINDOW;
		if (Mode == TransparencyMode.ColorKey) wanted |= Win32.WS_EX_LAYERED;
		wanted = clickThrough ? wanted | Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT : wanted & ~Win32.WS_EX_TRANSPARENT;

		if (hideFromTaskbar) Win32.ShowWindow(Hwnd, Win32.SW_HIDE);
		Win32.SetStyle(Hwnd, Win32.GWL_EXSTYLE, wanted);
		ApplyLayeredAttributes();
		if (hideFromTaskbar)
		{
			windowHidden = true;
			UpdateVisibility();
		}
		ApplyTopmost();
		stylesApplied = true;
	}

	private void ApplyLayeredAttributes()
	{
		if (Mode == TransparencyMode.ColorKey)
			Win32.SetLayeredWindowAttributes(Hwnd, ColorKeyRef, 0, Win32.LWA_COLORKEY);
		else if ((Win32.GetStyle(Hwnd, Win32.GWL_EXSTYLE) & Win32.WS_EX_LAYERED) != 0)
			Win32.SetLayeredWindowAttributes(Hwnd, 0, 255, Win32.LWA_ALPHA);
	}

	private void ApplyTopmost()
	{
		bool top = AppSettings.Instance.Data.AlwaysOnTop;
		Win32.SetWindowPos(Hwnd, top ? Win32.HWND_TOPMOST : Win32.HWND_NOTOPMOST, 0, 0, 0, 0,
			Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
	}

	/// <summary>Full rate while interacting; 30 fps when just idling (low-power mode);
	/// almost nothing while hidden behind a fullscreen app.</summary>
	private void ApplyFrameRate()
	{
		var data = AppSettings.Instance.Data;
		int fps = data.MaxFps;
		if (windowHidden && !testHidden)
			fps = 5;
		else if (data.LowPower && clickThrough && !ForceOpaque && !PetsBusy && !UiBusy)
			fps = Math.Min(30, data.MaxFps);
		if (Engine.MaxFps != fps) Engine.MaxFps = fps;
	}

	// ---- Click-through ----

	private bool IsInteractiveAt(Vector2 world)
	{
		if (ForceOpaque) return true;
		foreach (var control in interactiveControls)
		{
			if (control.IsVisibleInTree() && control.GetGlobalRect().HasPoint(world)) return true;
		}
		foreach (var tester in hitTesters)
		{
			if (tester(world)) return true;
		}
		return false;
	}

	private void SetClickThrough(bool enabled)
	{
		if (stylesApplied && enabled == clickThrough) return;
		clickThrough = enabled;

		long ex = Win32.GetStyle(Hwnd, Win32.GWL_EXSTYLE);
		if (enabled)
			ex |= Win32.WS_EX_LAYERED | Win32.WS_EX_TRANSPARENT;
		else
			ex &= ~Win32.WS_EX_TRANSPARENT;
		Win32.SetStyle(Hwnd, Win32.GWL_EXSTYLE, ex);
		ApplyLayeredAttributes();
	}

	// ---- Maintenance ----

	private void Maintain()
	{
		// Godot may have rewritten our extended style.
		long ex = Win32.GetStyle(Hwnd, Win32.GWL_EXSTYLE);
		if ((ex & Win32.WS_EX_TOOLWINDOW) == 0) ApplyStyles(hideFromTaskbar: true);
		else ApplyTopmost();

		// Taskbar moved/resized or resolution changed.
		if (DisplayServer.ScreenGetUsableRect(screen) != lastUsableRect)
			FitToScreen(AppSettings.Instance.Data.ScreenIndex);
	}

	private void OnSettingsChanged()
	{
		var data = AppSettings.Instance.Data;
		ApplyFrameRate();
		ApplyTopmost();
		UpdateVisibility();
		var wantedMode = data.TransparencyMode == "colorkey" ? TransparencyMode.ColorKey : TransparencyMode.Alpha;
		if (wantedMode != Mode)
		{
			Mode = wantedMode;
			ApplyTransparency();
			ApplyStyles(hideFromTaskbar: true);
		}
		if (data.ScreenIndex != -1 && data.ScreenIndex != screen) FitToScreen(data.ScreenIndex);
	}
}
