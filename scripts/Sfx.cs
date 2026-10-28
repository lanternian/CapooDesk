using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Autoload: short sound effects from the original CapooPals (assets/sounds).
/// Identical sounds closer than <see cref="MinRepeatInterval"/> are dropped so a crowd of
/// Capoos doesn't turn into noise. Volume comes from the settings (0 = muted).
/// </summary>
public partial class Sfx : Node
{
	public enum Sound
	{
		OpenBook,
		Summon,
		Recycle,
		Fall,
		Land,
		Roll,
		Touch,
		Drag,
		Stack,
		Push,
		Feed,
		Click,
		CleanPoo,
		Comfortable
	}

	private static readonly Dictionary<Sound, string> Files = new Dictionary<Sound, string>
	{
		{ Sound.OpenBook, "open_book" },
		{ Sound.Summon, "summon" },
		{ Sound.Recycle, "recycle" },
		{ Sound.Fall, "fall" },
		{ Sound.Land, "land" },
		{ Sound.Roll, "roll" },
		{ Sound.Touch, "touch" },
		{ Sound.Drag, "drag" },
		{ Sound.Stack, "stack" },
		{ Sound.Push, "push" },
		{ Sound.Feed, "feed" },
		{ Sound.Click, "click" },
		{ Sound.CleanPoo, "clean_poo" },
		{ Sound.Comfortable, "comfortable" }
	};

	private const int Voices = 8;
	private const double MinRepeatInterval = 0.12;

	public static Sfx Instance { get; private set; }

	private readonly Dictionary<Sound, AudioStream> streams = new Dictionary<Sound, AudioStream>();
	private readonly Dictionary<Sound, double> lastPlayed = new Dictionary<Sound, double>();
	private readonly List<AudioStreamPlayer> players = new List<AudioStreamPlayer>();

	public override void _EnterTree()
	{
		Instance = this;
	}

	public override void _Ready()
	{
		foreach (var (sound, file) in Files)
		{
			var stream = GD.Load<AudioStream>($"res://assets/sounds/{file}.wav");
			if (stream != null) streams[sound] = stream;
			else GD.PushWarning($"Missing sound {file}.wav");
		}
		for (int i = 0; i < Voices; i++)
		{
			var player = new AudioStreamPlayer();
			AddChild(player);
			players.Add(player);
		}
	}

	public static void Play(Sound sound) => Instance?.PlayInternal(sound);

	private void PlayInternal(Sound sound)
	{
		float volume = AppSettings.Instance.Data.SoundVolume;
		if (volume <= 0.001f || !streams.TryGetValue(sound, out var stream)) return;
		if (AppSettings.Instance.Data.PetsHidden || DesktopWorld.Instance?.FullscreenAppActive == true) return;

		double now = Time.GetTicksMsec() / 1000.0;
		if (lastPlayed.TryGetValue(sound, out double last) && now - last < MinRepeatInterval) return;
		lastPlayed[sound] = now;

		AudioStreamPlayer player = null;
		foreach (var p in players)
		{
			if (!p.Playing)
			{
				player = p;
				break;
			}
		}
		if (player == null) return; // all voices busy: skip rather than cut one off

		player.Stream = stream;
		player.VolumeDb = Mathf.LinearToDb(volume);
		player.PitchScale = (float)(0.95 + Random.Shared.NextDouble() * 0.1);
		player.Play();
	}
}
