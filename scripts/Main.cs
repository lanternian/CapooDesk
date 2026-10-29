using Godot;

/// <summary>
/// Root of the app: wires the tray menu to the pets, the magic book and the debug overlay.
/// </summary>
public partial class Main : Node2D
{
	private TrayIcon tray;
	private MagicBook book;
	private DebugOverlay debug;

	private static SettingsData Settings => AppSettings.Instance.Data;

	public override void _Ready()
	{
		tray = GetNode<TrayIcon>("TrayIcon");
		book = GetNode<MagicBook>("UI");
		debug = GetNode<DebugOverlay>("DebugOverlay");

		tray.IsChecked = command => command switch
		{
			TrayIcon.Command.ToggleHidden => Settings.PetsHidden,
			TrayIcon.Command.ToggleTopmost => Settings.AlwaysOnTop,
			TrayIcon.Command.ToggleDebug => debug.Visible,
			_ => false
		};
		tray.CommandInvoked += OnTrayCommand;

		foreach (string arg in OS.GetCmdlineUserArgs())
		{
			if (arg == "--debug-overlay") debug.Visible = true;
			else if (arg == "--selftest") AddChild(new SelfTest { Name = "SelfTest" });
			else if (arg == "--perftest") AddChild(new SelfTest { Name = "SelfTest", PerfMode = true });
			else if (arg == "--windowtest") AddChild(new SelfTest { Name = "SelfTest", WindowMode = true });
			else if (arg == "--loadtest") AddChild(new SelfTest { Name = "SelfTest", LoadMode = true });
			else if (arg == "--hitareas") AddChild(new SelfTest { Name = "SelfTest", AllHitAreaMode = true });
			else if (arg == "--capture") AddChild(new SelfTest { Name = "SelfTest", CaptureMode = true });

			else if (arg.StartsWith("--quit-after="))
				GetTree().CreateTimer(arg.Substring("--quit-after=".Length).ToFloat()).Timeout += QuitApp;
			else if (arg.StartsWith("--spawn="))
			{
				int count = arg.Substring("--spawn=".Length).ToInt();
				for (int i = 0; i < count; i++) PetManager.Instance.SpawnRandom();
			}
		}
	}

	private void OnTrayCommand(TrayIcon.Command command)
	{
		switch (command)
		{
			case TrayIcon.Command.OpenBook:
				book.ToggleBook();
				break;
			case TrayIcon.Command.SummonRandom:
				PetManager.Instance.SpawnRandom();
				break;
			case TrayIcon.Command.RecycleAll:
				PetManager.Instance.RecycleAll();
				break;
			case TrayIcon.Command.ToggleHidden:
				AppSettings.Instance.Apply(s => s.PetsHidden = !s.PetsHidden);
				break;
			case TrayIcon.Command.ToggleTopmost:
				AppSettings.Instance.Apply(s => s.AlwaysOnTop = !s.AlwaysOnTop);
				break;
			case TrayIcon.Command.ToggleDebug:
				debug.Visible = !debug.Visible;
				break;
			case TrayIcon.Command.Quit:
				QuitApp();
				break;
		}
	}

	public void QuitApp()
	{
		PetManager.Instance?.SaveRoster();
		GetTree().Quit();
	}
}
