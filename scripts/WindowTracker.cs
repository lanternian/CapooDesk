using Godot;
using System;
using System.Collections.Generic;
using System.Text;

/// <summary>
/// Enumerates top-level windows (front to back) and finds the visible parts of their top edges,
/// which become platforms Capoos can stand on. All rects are physical screen pixels.
/// </summary>
public class WindowTracker
{
	public struct Edge
	{
		public long Hwnd;
		public Rect2I Bounds;               // screen coordinates
		public List<Vector2I> Spans;        // visible [x1, x2] of the top edge, screen coordinates
	}

	private struct WindowEntry
	{
		public IntPtr Hwnd;
		public Win32.RECT Rect;
		public bool Platform;
	}

	private static readonly HashSet<string> ShellClasses = new HashSet<string>
	{
		"Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "NotifyIconOverflowWindow",
		"Windows.UI.Core.CoreWindow", "TopLevelWindowForOverflowXamlIsland", "XamlExplorerHostIslandWindow",
		"ApplicationManager_ImmersiveShellWindow", "ForegroundStaging", "MultitaskingViewFrame"
	};

	private const int MinPlatformWidth = 150;
	private const int MinSpanWidth = 60;

	private readonly IntPtr ownHwnd;
	private readonly uint ownProcessId;
	private readonly List<WindowEntry> windows = new List<WindowEntry>();
	private readonly Win32.EnumWindowsProc enumProc;
	private readonly StringBuilder classBuffer = new StringBuilder(256);

	public WindowTracker(IntPtr ownHwnd)
	{
		this.ownHwnd = ownHwnd;
		Win32.GetWindowThreadProcessId(ownHwnd, out ownProcessId);
		enumProc = OnEnumWindow;
	}

	/// <summary>Top-edge platforms inside <paramref name="area"/> (screen coordinates).</summary>
	public List<Edge> Scan(Rect2I area)
	{
		windows.Clear();
		Win32.EnumWindows(enumProc, IntPtr.Zero);

		var edges = new List<Edge>();
		for (int i = 0; i < windows.Count; i++)
		{
			var w = windows[i];
			if (!w.Platform) continue;

			int top = w.Rect.Top;
			// Edges at the very top can't hold a Capoo; edges near the floor are pointless.
			if (top <= area.Position.Y + 8 || top >= area.End.Y - 40) continue;

			var spans = new List<Vector2I>
			{
				new Vector2I(Math.Max(w.Rect.Left, area.Position.X), Math.Min(w.Rect.Right, area.End.X))
			};
			for (int j = 0; j < i && spans.Count > 0; j++)
			{
				var o = windows[j].Rect;
				if (o.Top <= top + 2 && o.Bottom > top - 2) Subtract(spans, o.Left, o.Right);
			}
			spans.RemoveAll(s => s.Y - s.X < MinSpanWidth);
			if (spans.Count == 0) continue;

			edges.Add(new Edge
			{
				Hwnd = w.Hwnd.ToInt64(),
				Bounds = ToRect(w.Rect),
				Spans = spans
			});
		}
		return edges;
	}

	/// <summary>Current bounds of a window, or false if it's gone, hidden or minimized.</summary>
	public bool TryGetBounds(long hwnd, out Rect2I bounds)
	{
		var handle = new IntPtr(hwnd);
		bounds = default;
		if (!Win32.IsWindowVisible(handle) || Win32.IsIconic(handle) || IsCloaked(handle)) return false;
		if (!GetFrameBounds(handle, out var rect)) return false;
		bounds = ToRect(rect);
		return true;
	}

	/// <summary>The foreground window covers the whole monitor (game, video, presentation).</summary>
	public bool IsForegroundFullscreen(Rect2I monitor)
	{
		IntPtr fg = Win32.GetForegroundWindow();
		if (fg == IntPtr.Zero || fg == ownHwnd) return false;
		Win32.GetWindowThreadProcessId(fg, out uint pid);
		if (pid == ownProcessId) return false;
		if (ShellClasses.Contains(GetClassName(fg))) return false;
		if (!Win32.GetWindowRect(fg, out var r)) return false;
		return r.Left <= monitor.Position.X && r.Top <= monitor.Position.Y
			&& r.Right >= monitor.End.X && r.Bottom >= monitor.End.Y;
	}

	private bool OnEnumWindow(IntPtr hwnd, IntPtr lParam)
	{
		if (hwnd == ownHwnd || !Win32.IsWindowVisible(hwnd) || Win32.IsIconic(hwnd)) return true;

		long style = Win32.GetStyle(hwnd, Win32.GWL_STYLE);
		long exStyle = Win32.GetStyle(hwnd, Win32.GWL_EXSTYLE);
		if ((style & Win32.WS_CHILD) != 0) return true;
		// Click-through overlays (game/recording HUDs) neither block nor support anything.
		if ((exStyle & Win32.WS_EX_TRANSPARENT) != 0) return true;
		if (IsCloaked(hwnd)) return true;

		Win32.GetWindowThreadProcessId(hwnd, out uint pid);
		if (pid == ownProcessId) return true;

		string className = GetClassName(hwnd);
		if (className == "Progman" || className == "WorkerW") return true;
		if (!GetFrameBounds(hwnd, out var rect) || rect.Width < 2 || rect.Height < 2) return true;

		bool tool = (exStyle & Win32.WS_EX_TOOLWINDOW) != 0;
		bool platform = !tool
			&& !ShellClasses.Contains(className)
			&& !Win32.IsZoomed(hwnd)
			&& Win32.GetWindowTextLength(hwnd) > 0
			&& rect.Width >= MinPlatformWidth && rect.Height >= 80;

		// Tiny tool windows (tooltips) shouldn't punch holes in platforms.
		if (tool && (rect.Width < 120 || rect.Height < 60)) return true;

		windows.Add(new WindowEntry { Hwnd = hwnd, Rect = rect, Platform = platform });
		return true;
	}

	private static bool GetFrameBounds(IntPtr hwnd, out Win32.RECT rect)
	{
		// Extended frame bounds exclude the invisible resize borders of Windows 10/11.
		if (Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_EXTENDED_FRAME_BOUNDS, out rect,
			System.Runtime.InteropServices.Marshal.SizeOf<Win32.RECT>()) == 0)
			return true;
		return Win32.GetWindowRect(hwnd, out rect);
	}

	private static bool IsCloaked(IntPtr hwnd)
	{
		return Win32.DwmGetWindowAttribute(hwnd, Win32.DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0;
	}

	private string GetClassName(IntPtr hwnd)
	{
		classBuffer.Clear();
		Win32.GetClassName(hwnd, classBuffer, classBuffer.Capacity);
		return classBuffer.ToString();
	}

	private static Rect2I ToRect(Win32.RECT r) => new Rect2I(r.Left, r.Top, r.Width, r.Height);

	/// <summary>Remove [left, right] from a list of disjoint spans.</summary>
	private static void Subtract(List<Vector2I> spans, int left, int right)
	{
		for (int i = spans.Count - 1; i >= 0; i--)
		{
			var s = spans[i];
			if (right <= s.X || left >= s.Y) continue;
			spans.RemoveAt(i);
			if (left > s.X) spans.Add(new Vector2I(s.X, left));
			if (right < s.Y) spans.Add(new Vector2I(right, s.Y));
		}
	}
}
