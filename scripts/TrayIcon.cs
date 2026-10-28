using Godot;
using System;
using System.Runtime.InteropServices;

/// <summary>
/// Notification-area icon with a context menu. Uses a hidden Win32 window on the main thread;
/// Godot's message pump dispatches its messages. Commands are raised deferred, outside WndProc.
/// </summary>
public partial class TrayIcon : Node
{
	public enum Command
	{
		OpenBook = 1,
		SummonRandom,
		RecycleAll,
		ToggleHidden,
		ToggleTopmost,
		ToggleDebug,
		Quit = 99
	}

	public event Action<Command> CommandInvoked;

	/// <summary>Asked when building the menu, to show check marks.</summary>
	public Func<Command, bool> IsChecked;

	private const string ClassName = "CapooDeskTrayWindow";
	private const uint CallbackMessage = Win32.WM_APP + 1;
	private const string Tooltip = "CapooDesk 咖波桌宠";

	private Win32.WndProc wndProc; // kept alive: native code holds a pointer to it
	private IntPtr hwnd;
	private IntPtr hIcon;
	private IntPtr hInstance;
	private uint taskbarCreatedMessage;
	private bool iconAdded;

	public override void _Ready()
	{
		hInstance = Win32.GetModuleHandle(null);
		wndProc = WndProc;
		var wc = new Win32.WNDCLASSEX
		{
			cbSize = (uint)Marshal.SizeOf<Win32.WNDCLASSEX>(),
			lpfnWndProc = Marshal.GetFunctionPointerForDelegate(wndProc),
			hInstance = hInstance,
			lpszClassName = ClassName
		};
		Win32.RegisterClassEx(ref wc);
		// A plain (never shown) top-level window: it can become foreground, which
		// TrackPopupMenu needs to close the menu when clicking elsewhere.
		hwnd = Win32.CreateWindowEx((uint)Win32.WS_EX_TOOLWINDOW, ClassName, "CapooDeskTray", 0,
			0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);
		if (hwnd == IntPtr.Zero)
		{
			GD.PrintErr($"TrayIcon: CreateWindowEx failed ({Marshal.GetLastWin32Error()})");
			return;
		}

		taskbarCreatedMessage = Win32.RegisterWindowMessage("TaskbarCreated");
		hIcon = LoadIcon();
		AddIcon();
	}

	public override void _ExitTree()
	{
		RemoveIcon();
		if (hwnd != IntPtr.Zero) Win32.DestroyWindow(hwnd);
		hwnd = IntPtr.Zero;
		if (hIcon != IntPtr.Zero) Win32.DestroyIcon(hIcon);
		hIcon = IntPtr.Zero;
		Win32.UnregisterClass(ClassName, hInstance);
	}

	private IntPtr LoadIcon()
	{
		// LoadImage needs a real file; res:// lives inside the pck once exported.
		const string source = "res://assets/icons/capoo.ico";
		const string target = "user://tray.ico";
		var bytes = FileAccess.GetFileAsBytes(source);
		if (bytes.Length > 0)
		{
			var file = FileAccess.Open(target, FileAccess.ModeFlags.Write);
			if (file != null)
			{
				file.StoreBuffer(bytes);
				file.Close();
				var icon = Win32.LoadImage(IntPtr.Zero, ProjectSettings.GlobalizePath(target), Win32.IMAGE_ICON,
					0, 0, Win32.LR_LOADFROMFILE | Win32.LR_DEFAULTSIZE);
				if (icon != IntPtr.Zero) return icon;
			}
		}
		GD.PrintErr("TrayIcon: failed to load icon, using the default application icon.");
		return Win32.LoadIcon(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
	}

	private Win32.NOTIFYICONDATA MakeData()
	{
		return new Win32.NOTIFYICONDATA
		{
			cbSize = (uint)Marshal.SizeOf<Win32.NOTIFYICONDATA>(),
			hWnd = hwnd,
			uID = 1,
			uFlags = Win32.NIF_MESSAGE | Win32.NIF_ICON | Win32.NIF_TIP,
			uCallbackMessage = CallbackMessage,
			hIcon = hIcon,
			szTip = Tooltip
		};
	}

	private void AddIcon()
	{
		var data = MakeData();
		iconAdded = Win32.Shell_NotifyIcon(Win32.NIM_ADD, ref data);
		if (!iconAdded) GD.PrintErr("TrayIcon: Shell_NotifyIcon(NIM_ADD) failed");
	}

	private void RemoveIcon()
	{
		if (!iconAdded) return;
		var data = MakeData();
		Win32.Shell_NotifyIcon(Win32.NIM_DELETE, ref data);
		iconAdded = false;
	}

	private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
	{
		try
		{
			if (msg == CallbackMessage)
			{
				uint mouseMessage = (uint)(lParam.ToInt64() & 0xFFFF);
				if (mouseMessage == Win32.WM_RBUTTONUP)
					ShowMenu();
				else if (mouseMessage == Win32.WM_LBUTTONUP)
					Raise(Command.OpenBook);
				return IntPtr.Zero;
			}
			if (msg == taskbarCreatedMessage && taskbarCreatedMessage != 0)
			{
				// Explorer restarted: the icon is gone, add it again.
				iconAdded = false;
				AddIcon();
				return IntPtr.Zero;
			}
		}
		catch (Exception e)
		{
			GD.PrintErr($"TrayIcon: {e}");
		}
		return Win32.DefWindowProc(hWnd, msg, wParam, lParam);
	}

	private void ShowMenu()
	{
		IntPtr menu = Win32.CreatePopupMenu();
		Append(menu, Command.OpenBook, "打开魔法书");
		Append(menu, Command.SummonRandom, "召唤一只随机咖波");
		Append(menu, Command.RecycleAll, "回收全部咖波");
		Win32.AppendMenu(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);
		Append(menu, Command.ToggleHidden, "隐藏咖波");
		Append(menu, Command.ToggleTopmost, "总在最前");
		Append(menu, Command.ToggleDebug, "调试信息");
		Win32.AppendMenu(menu, Win32.MF_SEPARATOR, UIntPtr.Zero, null);
		Append(menu, Command.Quit, "退出");

		Win32.GetCursorPos(out var p);
		Win32.SetForegroundWindow(hwnd);
		int chosen = Win32.TrackPopupMenu(menu, Win32.TPM_RETURNCMD | Win32.TPM_RIGHTBUTTON | Win32.TPM_NONOTIFY,
			p.X, p.Y, 0, hwnd, IntPtr.Zero);
		Win32.PostMessage(hwnd, Win32.WM_NULL, IntPtr.Zero, IntPtr.Zero);
		Win32.DestroyMenu(menu);

		if (chosen != 0) Raise((Command)chosen);
	}

	private void Append(IntPtr menu, Command command, string text)
	{
		uint flags = Win32.MF_STRING;
		if (IsChecked != null && IsChecked(command)) flags |= Win32.MF_CHECKED;
		Win32.AppendMenu(menu, flags, (UIntPtr)(uint)command, text);
	}

	private void Raise(Command command)
	{
		Callable.From(() => CommandInvoked?.Invoke(command)).CallDeferred();
	}
}
