using Godot;
using System;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Autoload: persistent settings and the roster of Capoos on the desktop (user://settings.json).
/// </summary>
public partial class AppSettings : Node
{
	public static AppSettings Instance { get; private set; }

	public SettingsData Data { get; private set; } = new SettingsData();

	/// <summary>Raised after any setting changed through <see cref="Apply"/>.</summary>
	public event Action Changed;

	// "--test" on the command line keeps automated runs away from the real settings.
	private static readonly string SettingsPath =
		System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--test") >= 0 ? "user://settings_test.json" : "user://settings.json";

	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		IncludeFields = true,
		WriteIndented = true
	};

	public override void _EnterTree()
	{
		Instance = this;
		Load();
	}

	public void Load()
	{
		if (!FileAccess.FileExists(SettingsPath)) return;

		var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Read);
		if (file == null) return;
		string jsonText = file.GetAsText();
		file.Close();

		try
		{
			Data = JsonSerializer.Deserialize<SettingsData>(jsonText, JsonOptions) ?? new SettingsData();
		}
		catch (Exception e)
		{
			GD.PrintErr($"Failed to parse settings.json, using defaults: {e.Message}");
			Data = new SettingsData();
		}
		Data.Clamp();
	}

	public void Save()
	{
		var file = FileAccess.Open(SettingsPath, FileAccess.ModeFlags.Write);
		if (file == null)
		{
			GD.PrintErr($"Failed to write settings.json: {FileAccess.GetOpenError()}");
			return;
		}
		file.StoreString(JsonSerializer.Serialize(Data, JsonOptions));
		file.Close();
	}

	/// <summary>Modify settings, then save and notify listeners.</summary>
	public void Apply(Action<SettingsData> change)
	{
		change(Data);
		Data.Clamp();
		Save();
		Changed?.Invoke();
	}
}

public class SettingsData
{
	// Appearance
	public float PetScale = 0.5f;
	public int MaxPets = 20;
	public bool ShowBookButton = true;
	public float BookButtonX = -1;
	public float BookButtonY = -1;

	// Window
	public string TransparencyMode = "alpha"; // "alpha" | "colorkey"
	public int ScreenIndex = -1;               // -1 = primary screen
	public bool AlwaysOnTop = true;
	public int MaxFps = 60;
	public bool LowPower = true;
	public bool PetsHidden = false;

	// Behaviour
	public int CareLevel = 2;                  // 0 off, 1 low, 2 normal, 3 often
	public bool SleepEnabled = true;
	public int SleepIdleMinutes = 10;
	public bool NightSleep = true;
	public int NightStartHour = 23;
	public int NightEndHour = 7;
	public bool WindowAware = true;
	public bool HideOnFullscreen = true;
	public bool Stacking = true;

	// Roster
	public List<PetSave> Pets = new List<PetSave>();

	public void Clamp()
	{
		PetScale = Mathf.Clamp(PetScale, 0.25f, 1.2f);
		MaxPets = Math.Clamp(MaxPets, 1, 50);
		MaxFps = Math.Clamp(MaxFps, 20, 144);
		CareLevel = Math.Clamp(CareLevel, 0, 3);
		SleepIdleMinutes = Math.Clamp(SleepIdleMinutes, 1, 120);
		NightStartHour = Math.Clamp(NightStartHour, 0, 23);
		NightEndHour = Math.Clamp(NightEndHour, 0, 23);
		if (TransparencyMode != "alpha" && TransparencyMode != "colorkey") TransparencyMode = "alpha";
		Pets ??= new List<PetSave>();
	}
}

public class PetSave
{
	public int Index;
	public float X;
	public float Y;
	public bool FacingRight;
}
