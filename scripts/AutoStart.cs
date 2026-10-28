using Godot;
using Microsoft.Win32;
using System;

/// <summary>
/// "Start with Windows" through the per-user Run key. Only offered in the exported app:
/// from the editor the executable would be the Godot editor itself.
/// </summary>
public static class AutoStart
{
	private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
	private const string ValueName = "CapooDesk";

	public static bool Supported => OperatingSystem.IsWindows() && !OS.HasFeature("editor");

	public static bool IsEnabled()
	{
		if (!OperatingSystem.IsWindows()) return false;
		using var key = Registry.CurrentUser.OpenSubKey(RunKey);
		return key?.GetValue(ValueName) is string;
	}

	public static void SetEnabled(bool enabled)
	{
		if (!Supported || !OperatingSystem.IsWindows()) return;
		try
		{
			using var key = Registry.CurrentUser.CreateSubKey(RunKey);
			if (enabled)
				key.SetValue(ValueName, $"\"{OS.GetExecutablePath().Replace('/', '\\')}\"");
			else
				key.DeleteValue(ValueName, false);
		}
		catch (Exception e)
		{
			GD.PrintErr($"AutoStart: {e.Message}");
		}
	}
}
