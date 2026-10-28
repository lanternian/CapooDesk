using Godot;
using System;

/// <summary>
/// Right-click menu for one Capoo: its name plus pet / feed / sleep-wake / recycle.
/// Closes on a choice, on a click elsewhere (see MagicBook) or after a while unused.
/// </summary>
public partial class PetContextMenu : PanelContainer
{
	private Label title;
	private Button sleepButton;
	private CapooController pet;
	private double idleTime;

	public bool IsOpen => Visible;

	public override void _Ready()
	{
		Visible = false;
		MouseFilter = MouseFilterEnum.Stop;

		var box = new VBoxContainer();
		box.AddThemeConstantOverride("separation", 6);
		AddChild(box);

		title = UiTheme.MakeLabel("", 20);
		title.HorizontalAlignment = HorizontalAlignment.Center;
		box.AddChild(title);
		box.AddChild(new HSeparator());

		AddItem(box, "摸摸", () => pet.PetOnce());
		AddItem(box, "喂食", () => pet.Feed());
		sleepButton = AddItem(box, "睡觉", () => pet.ToggleSleep());
		AddItem(box, "回收", () => pet.StartRecycle());
	}

	private Button AddItem(VBoxContainer box, string text, Action action)
	{
		var button = new Button { Text = text, CustomMinimumSize = new Vector2(140, 36), MouseDefaultCursorShape = CursorShape.PointingHand };
		button.Pressed += () =>
		{
			if (pet != null && IsInstanceValid(pet)) action();
			Close();
		};
		box.AddChild(button);
		return button;
	}

	public void Open(CapooController target, Vector2 position)
	{
		pet = target;
		title.Text = target.DisplayName;
		sleepButton.Text = target.IsAsleep ? "叫醒" : "睡觉";
		idleTime = 0;
		Visible = true;
		ResetSize();

		// Keep it on screen.
		var screen = GetViewportRect().Size;
		Vector2 size = GetCombinedMinimumSize();
		Position = new Vector2(
			Mathf.Clamp(position.X + 8, 8, screen.X - size.X - 8),
			Mathf.Clamp(position.Y - size.Y * 0.5f, 8, screen.Y - size.Y - 8));
	}

	public void Close()
	{
		Visible = false;
		pet = null;
	}

	public override void _Process(double delta)
	{
		if (!Visible) return;
		if (pet == null || !IsInstanceValid(pet) || pet.State == CapooState.Recycle)
		{
			Close();
			return;
		}

		bool hovered = GetGlobalRect().HasPoint(DesktopWindow.Instance.CursorWorld);
		idleTime = hovered ? 0 : idleTime + delta;
		if (idleTime > 6) Close();
	}
}
