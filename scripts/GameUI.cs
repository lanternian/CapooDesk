using Godot;
using System;
using System.Collections.Generic;

public partial class GameUI : CanvasLayer
{
	[Signal]
	public delegate void ChangeCapooRequestedEventHandler(int id);

	private const float ScreenMargin = 24.0f;
	private static readonly Vector2 MagicBookOffset = new Vector2(72.0f, -96.0f);
	private static readonly Vector2 MenuOffset = new Vector2(0.0f, 72.0f);

	private TextureButton magicBookButton;
	private Control customMenu;

	private bool isDragging = false;
	private Vector2 dragStartMousePos;
	private Vector2 dragStartButtonPos;
	private bool hasDragged = false;

	public bool IsMagicBookVisible => magicBookButton?.Visible == true || customMenu?.Visible == true;

	public override void _Ready()
	{
		CreateMagicBookButton();
		CreateCustomMenu();
	}

	public void ToggleMagicBook(Vector2 anchorScreenPosition)
	{
		if (IsMagicBookVisible)
		{
			HideMagicBook();
			return;
		}
		ShowMagicBook(anchorScreenPosition);
	}

	public void ShowMagicBook(Vector2 anchorScreenPosition)
	{
		if (magicBookButton == null) return;
		magicBookButton.Position = GetClampedBookPosition(anchorScreenPosition + MagicBookOffset);
		magicBookButton.Visible = true;
	}

	public void HideMagicBook()
	{
		if (magicBookButton != null) magicBookButton.Visible = false;
		if (customMenu != null) customMenu.Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (!IsMagicBookVisible || @event is not InputEventMouseButton mouseButton || !mouseButton.Pressed)
			return;

		if (mouseButton.ButtonIndex != MouseButton.Left && mouseButton.ButtonIndex != MouseButton.Right)
			return;

		bool clickedMagicBook = magicBookButton?.Visible == true && magicBookButton.GetGlobalRect().HasPoint(mouseButton.Position);
		bool clickedMenu = customMenu?.Visible == true && customMenu.GetGlobalRect().HasPoint(mouseButton.Position);
		if (!clickedMagicBook && !clickedMenu)
		{
			HideMagicBook();
		}
	}

	private void CreateMagicBookButton()
	{
		magicBookButton = new TextureButton
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			Scale = new Vector2(0.75f, 0.75f)
		};

		var texture = GD.Load<Texture2D>("res://assets/images/btn_menu.png");
		if (texture != null) magicBookButton.TextureNormal = texture;
		else GD.PrintErr("Failed to load btn_menu.png");

		magicBookButton.Pressed += ToggleCustomMenu;
		magicBookButton.GuiInput += OnMagicBookGuiInput;
		AddChild(magicBookButton);
	}

	private void ToggleCustomMenu()
	{
		if (customMenu == null || magicBookButton == null) return;

		if (hasDragged)
		{
			hasDragged = false;
			return;
		}

		customMenu.Visible = !customMenu.Visible;
		if (customMenu.Visible)
		{
			customMenu.Position = GetClampedMenuPosition(magicBookButton.Position + MenuOffset);
		}
	}

	private void OnMagicBookGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.ButtonIndex == MouseButton.Left)
		{
			if (mouseEvent.Pressed)
			{
				isDragging = true;
				hasDragged = false;
				dragStartMousePos = GetViewport().GetMousePosition();
				dragStartButtonPos = magicBookButton.Position;
			}
			else
			{
				isDragging = false;
			}
			return;
		}

		if (@event is not InputEventMouseMotion || !isDragging) return;

		Vector2 currentMousePos = GetViewport().GetMousePosition();
		if (currentMousePos.DistanceTo(dragStartMousePos) > 5.0f)
			hasDragged = true;

		Vector2 newPosition = GetClampedBookPosition(dragStartButtonPos + (currentMousePos - dragStartMousePos));
		magicBookButton.Position = newPosition;

		if (customMenu != null && customMenu.Visible)
			customMenu.Position = GetClampedMenuPosition(newPosition + MenuOffset);
	}

	private Vector2 GetClampedBookPosition(Vector2 preferredPosition)
	{
		if (magicBookButton == null) return preferredPosition;

		Rect2 viewportRect = GetViewport().GetVisibleRect();
		Vector2 buttonSize = magicBookButton.GetCombinedMinimumSize();
		float width = buttonSize.X * magicBookButton.Scale.X;
		float height = buttonSize.Y * magicBookButton.Scale.Y;
		float maxX = Mathf.Max(ScreenMargin, viewportRect.Size.X - width - ScreenMargin);
		float maxY = Mathf.Max(ScreenMargin, viewportRect.Size.Y - height - ScreenMargin);

		return new Vector2(
			Mathf.Clamp(preferredPosition.X, ScreenMargin, maxX),
			Mathf.Clamp(preferredPosition.Y, ScreenMargin, maxY));
	}

	private Vector2 GetClampedMenuPosition(Vector2 preferredPosition)
	{
		Rect2 viewportRect = GetViewport().GetVisibleRect();
		float maxX = Mathf.Max(ScreenMargin, viewportRect.Size.X - customMenu.Size.X - ScreenMargin);
		float maxY = Mathf.Max(ScreenMargin, viewportRect.Size.Y - customMenu.Size.Y - ScreenMargin);

		return new Vector2(
			Mathf.Clamp(preferredPosition.X, ScreenMargin, maxX),
			Mathf.Clamp(preferredPosition.Y, ScreenMargin, maxY));
	}

	private void CreateCustomMenu()
	{
		customMenu = new Control
		{
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};
		AddChild(customMenu);

		var bgTexture = GD.Load<Texture2D>("res://assets/images/ui_pop_collection_01.png");
		var bgRect = new TextureRect();
		if (bgTexture != null)
		{
			bgRect.Texture = bgTexture;
			customMenu.Size = bgTexture.GetSize();
		}
		else
		{
			customMenu.Size = new Vector2(782, 542);
		}
		bgRect.MouseFilter = Control.MouseFilterEnum.Ignore;
		customMenu.AddChild(bgRect);

		var shutdownBtn = new TextureButton();
		var shutdownTexture = GD.Load<Texture2D>("res://assets/images/btn_shutdown_vt.png");
		if (shutdownTexture != null) shutdownBtn.TextureNormal = shutdownTexture;
		shutdownBtn.Position = new Vector2(customMenu.Size.X - 80, 30);
		shutdownBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
		shutdownBtn.Pressed += () => GetTree().Quit();
		customMenu.AddChild(shutdownBtn);

		var scrollContainer = new ScrollContainer();
		scrollContainer.Position = new Vector2(50, 100);
		scrollContainer.Size = new Vector2(customMenu.Size.X - 100, customMenu.Size.Y - 150);
		customMenu.AddChild(scrollContainer);

		var gridContainer = new GridContainer();
		gridContainer.Columns = 3;
		gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
		gridContainer.AddThemeConstantOverride("h_separation", 20);
		gridContainer.AddThemeConstantOverride("v_separation", 20);
		scrollContainer.AddChild(gridContainer);

		var config = GetNodeOrNull<CapooConfig>("/root/CapooConfig");
		if (config != null && config.infos != null)
		{
			for (int i = 0; i < config.infos.Count; i++)
			{
				var btn = new Button();
				btn.Text = config.infos[i].name;
				btn.CustomMinimumSize = new Vector2(0, 60);
				btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
				btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;

				int index = i;
				btn.Pressed += () =>
				{
					EmitSignal(SignalName.ChangeCapooRequested, index);
					customMenu.Visible = false;
				};
				gridContainer.AddChild(btn);
			}
		}
	}
}
