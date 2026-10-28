using System;
using System.Runtime.InteropServices;
using System.Text;

/// <summary>
/// Win32 P/Invoke declarations shared by DesktopWindow, TrayIcon, WindowTracker and SystemIdle.
/// </summary>
public static class Win32
{
	// ---- Window styles ----
	public const int GWL_STYLE = -16;
	public const int GWL_EXSTYLE = -20;

	public const long WS_VISIBLE = 0x10000000L;
	public const long WS_MINIMIZE = 0x20000000L;
	public const long WS_MAXIMIZE = 0x01000000L;
	public const long WS_CHILD = 0x40000000L;

	public const long WS_EX_TOPMOST = 0x00000008L;
	public const long WS_EX_TRANSPARENT = 0x00000020L;
	public const long WS_EX_TOOLWINDOW = 0x00000080L;
	public const long WS_EX_APPWINDOW = 0x00040000L;
	public const long WS_EX_LAYERED = 0x00080000L;
	public const long WS_EX_NOACTIVATE = 0x08000000L;

	public const uint LWA_COLORKEY = 0x1;
	public const uint LWA_ALPHA = 0x2;

	public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
	public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
	public const uint SWP_NOSIZE = 0x0001;
	public const uint SWP_NOMOVE = 0x0002;
	public const uint SWP_NOZORDER = 0x0004;
	public const uint SWP_NOACTIVATE = 0x0010;
	public const uint SWP_FRAMECHANGED = 0x0020;
	public const uint SWP_SHOWWINDOW = 0x0040;

	public const uint GW_HWNDPREV = 3;
	public const uint GA_ROOT = 2;
	public const uint GA_ROOTOWNER = 3;

	[StructLayout(LayoutKind.Sequential)]
	public struct POINT
	{
		public int X;
		public int Y;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct RECT
	{
		public int Left;
		public int Top;
		public int Right;
		public int Bottom;
		public int Width => Right - Left;
		public int Height => Bottom - Top;
	}

	[StructLayout(LayoutKind.Sequential)]
	public struct MARGINS
	{
		public int Left;
		public int Right;
		public int Top;
		public int Bottom;
	}

	[DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
	public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

	[DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
	public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

	public static long GetStyle(IntPtr hWnd, int index) => GetWindowLongPtr(hWnd, index).ToInt64();
	public static void SetStyle(IntPtr hWnd, int index, long value) => SetWindowLongPtr(hWnd, index, new IntPtr(value));

	[DllImport("user32.dll")]
	public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

	[DllImport("user32.dll", SetLastError = true)]
	public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

	[DllImport("user32.dll")]
	public static extern bool GetCursorPos(out POINT lpPoint);

	public const int SW_HIDE = 0;
	public const int SW_SHOWNOACTIVATE = 4;

	[DllImport("user32.dll")]
	public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

	[DllImport("user32.dll")]
	public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

	[DllImport("user32.dll")]
	public static extern bool IsWindowVisible(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern bool IsIconic(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern bool IsZoomed(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

	[DllImport("user32.dll")]
	public static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern int GetWindowTextLength(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

	public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

	[DllImport("user32.dll")]
	public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

	public const int VK_LBUTTON = 0x01;
	public const int VK_RBUTTON = 0x02;
	public const int VK_MBUTTON = 0x04;

	[DllImport("user32.dll")]
	public static extern short GetAsyncKeyState(int vKey);

	public static bool IsKeyDown(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

	public static bool IsAnyMouseButtonDown() => IsKeyDown(VK_LBUTTON) || IsKeyDown(VK_RBUTTON) || IsKeyDown(VK_MBUTTON);

	// ---- DWM ----
	public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
	public const int DWMWA_CLOAKED = 14;

	[DllImport("dwmapi.dll")]
	public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out RECT pvAttribute, int cbAttribute);

	[DllImport("dwmapi.dll")]
	public static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out int pvAttribute, int cbAttribute);

	[DllImport("dwmapi.dll")]
	public static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref MARGINS margins);

	// ---- Idle time ----
	[StructLayout(LayoutKind.Sequential)]
	public struct LASTINPUTINFO
	{
		public uint cbSize;
		public uint dwTime;
	}

	[DllImport("user32.dll")]
	public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

	[DllImport("kernel32.dll")]
	public static extern uint GetTickCount();

	/// <summary>Seconds since the last keyboard/mouse input anywhere in the system.</summary>
	public static double GetSystemIdleSeconds()
	{
		var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
		if (!GetLastInputInfo(ref info)) return 0;
		return unchecked(GetTickCount() - info.dwTime) / 1000.0;
	}

	// ---- Message-only window + tray icon ----
	public const uint WM_DESTROY = 0x0002;
	public const uint WM_COMMAND = 0x0111;
	public const uint WM_NULL = 0x0000;
	public const uint WM_LBUTTONUP = 0x0202;
	public const uint WM_LBUTTONDBLCLK = 0x0203;
	public const uint WM_RBUTTONUP = 0x0205;
	public const uint WM_APP = 0x8000;
	public static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

	public delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct WNDCLASSEX
	{
		public uint cbSize;
		public uint style;
		public IntPtr lpfnWndProc;
		public int cbClsExtra;
		public int cbWndExtra;
		public IntPtr hInstance;
		public IntPtr hIcon;
		public IntPtr hCursor;
		public IntPtr hbrBackground;
		public string lpszMenuName;
		public string lpszClassName;
		public IntPtr hIconSm;
	}

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);

	[DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
	public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
		int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

	[DllImport("user32.dll")]
	public static extern bool DestroyWindow(IntPtr hWnd);

	[DllImport("user32.dll")]
	public static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("user32.dll")]
	public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr GetModuleHandle(string lpModuleName);

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern uint RegisterWindowMessage(string lpString);

	public const uint NIM_ADD = 0x0;
	public const uint NIM_MODIFY = 0x1;
	public const uint NIM_DELETE = 0x2;
	public const uint NIF_MESSAGE = 0x1;
	public const uint NIF_ICON = 0x2;
	public const uint NIF_TIP = 0x4;

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	public struct NOTIFYICONDATA
	{
		public uint cbSize;
		public IntPtr hWnd;
		public uint uID;
		public uint uFlags;
		public uint uCallbackMessage;
		public IntPtr hIcon;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
		public string szTip;
		public uint dwState;
		public uint dwStateMask;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
		public string szInfo;
		public uint uVersion;
		[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
		public string szInfoTitle;
		public uint dwInfoFlags;
		public Guid guidItem;
		public IntPtr hBalloonIcon;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
	public static extern bool Shell_NotifyIcon(uint dwMessage, ref NOTIFYICONDATA lpData);

	public const uint IMAGE_ICON = 1;
	public const uint LR_LOADFROMFILE = 0x10;
	public const uint LR_DEFAULTSIZE = 0x40;

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern IntPtr LoadImage(IntPtr hInst, string name, uint type, int cx, int cy, uint fuLoad);

	[DllImport("user32.dll")]
	public static extern IntPtr LoadIcon(IntPtr hInstance, IntPtr lpIconName);

	[DllImport("user32.dll")]
	public static extern bool DestroyIcon(IntPtr hIcon);

	public const uint MF_STRING = 0x0;
	public const uint MF_SEPARATOR = 0x800;
	public const uint MF_CHECKED = 0x8;
	public const uint MF_GRAYED = 0x1;
	public const uint TPM_RIGHTBUTTON = 0x2;
	public const uint TPM_RETURNCMD = 0x100;
	public const uint TPM_NONOTIFY = 0x80;

	[DllImport("user32.dll")]
	public static extern IntPtr CreatePopupMenu();

	[DllImport("user32.dll", CharSet = CharSet.Unicode)]
	public static extern bool AppendMenu(IntPtr hMenu, uint uFlags, UIntPtr uIDNewItem, string lpNewItem);

	[DllImport("user32.dll")]
	public static extern bool DestroyMenu(IntPtr hMenu);

	[DllImport("user32.dll")]
	public static extern int TrackPopupMenu(IntPtr hMenu, uint uFlags, int x, int y, int nReserved, IntPtr hWnd, IntPtr prcRect);
}
