using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// The magic book UI, laid out like the original CapooPals book (img_manual_01 /
/// pc_img_manual_02 of the original app): page tabs on the left that come out from under
/// the page, a collection of beige tiles (green while that Capoo is on the desk), big page
/// arrows in the bottom corners, the page number on the ribbon, a trash can that recycles
/// everything, and a settings page of ◀ value ▶ bars, small check boxes and icon buttons.
/// Also hosts the floating book button and the right-click Capoo menu.
/// </summary>
public partial class MagicBook : CanvasLayer
{
	// ---- Layout, in the coordinates of ui_pop_collection_01.png (954x694) ----
	// Positions follow the original book: its manual screenshots are the same artwork at
	// 0.726 scale. The paper runs out at y=500 (below that the pages are rolled up) and the
	// folded corner of the right page starts at x=815.
	private static readonly Rect2 LeftPage = new Rect2(96, 150, 345, 350);
	private static readonly Rect2 RightPage = new Rect2(478, 150, 345, 350);

	// Collection tiles: 3x3 on each page, 94 px like box_collection_bot_*.
	private const int TileColumns = 3;
	private const int TilesPerSide = TileColumns * 3;
	private const int TilesPerSpread = TilesPerSide * 2;
	private const float TileSize = 94f;
	private static readonly float[] LeftColumnsX = { 126, 237, 347 };
	private static readonly float[] RightColumnsX = { 478, 588, 698 };
	private static readonly float[] RowsY = { 166, 280, 394 };

	// Page tabs on the left. The active tab sticks out further, like in the original.
	private static readonly float[] TabsY = { 169, 256 };
	private const float TabTuck = 10f;
	// The strip where the tabs go under the left page; the page edge is drawn again over them.
	private static readonly Rect2I PageEdgeStrip = new Rect2I(50, 150, 80, 190);

	private static readonly Vector2 ClosePosition = new Vector2(848, 14);
	private const float CloseScale = 0.85f;
	private static readonly Vector2 CounterHeadPosition = new Vector2(208, 24);
	private static readonly Vector2 CounterBarPosition = new Vector2(180, 100);
	private static readonly Vector2 PrevArrowPosition = new Vector2(70, 486);
	private static readonly Vector2 NextArrowPosition = new Vector2(800, 486);
	private static readonly Rect2 RibbonLabelRect = new Rect2(347, 571, 80, 26);
	private static readonly Vector2 TrashPosition = new Vector2(462, 546);
	private const float TrashScale = 0.8f;

	private const float ButtonBaseScale = 0.55f;
	private static readonly Color BubbleInk = new Color("4a3b2a");

	private static SettingsData Settings => AppSettings.Instance.Data;

	private CapooConfig config;
	private Control root;
	private TextureButton bookButton;
	private Control book;
	private readonly Control[] pages = new Control[2];
	private readonly TextureButton[] tabs = new TextureButton[2];
	private static readonly string[] TabImages = { "01", "04" };
	private int currentTab;
	private PetContextMenu petMenu;
	private ulong menuOpenedFrame;
	private bool wasMouseDown;
	private float uiScale = 1f;
	private int[] pageEdgeStart = Array.Empty<int>();

	// Collection page
	private int spread;
	private Control tileLayer;
	private readonly List<Tile> tiles = new List<Tile>();
	private readonly TextureButton[] arrows = new TextureButton[2];
	private Label ribbonLabel;
	private Label counterLabel;
	private Control counter;
	private TextureButton trash;
	private Tile hoveredTile;
	private NinePatchRect nameBubble;
	private Label nameBubbleLabel;
	private double counterFlash;

	// Settings page
	private VBoxContainer settingsValues;
	private GridContainer settingsChecks;
	private HBoxContainer settingsButtons;
	private Control helpPaper;
	private readonly List<Action> settingRefreshers = new List<Action>();
	private bool refreshingSettings;

	// Book button dragging
	private bool buttonPressed;
	private bool buttonDragged;
	private Vector2 buttonPressMouse;
	private Vector2 buttonPressPosition;

	private class Tile
	{
		public int Index;
		public Control Root;
		public TextureRect Box;
		public TextureRect Frame;
		public CapooView Preview;
		public TextureButton Recycle;
		public Label Count;
	}

	public bool IsBookOpen => book != null && book.Visible;

	public override void _Ready()
	{
		config = GetNode<CapooConfig>("/root/CapooConfig");
		Layer = 5;

		root = new Control { Name = "Root", MouseFilter = Control.MouseFilterEnum.Ignore, Theme = UiTheme.Get() };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		BuildBookButton();
		BuildBook();
		petMenu = new PetContextMenu { Name = "PetMenu" };
		root.AddChild(petMenu);

		var window = DesktopWindow.Instance;
		window.RegisterInteractive(bookButton);
		window.RegisterInteractive(book);
		window.RegisterInteractive(petMenu);
		window.ScreenChanged += LayoutForScreen;

		PetManager.Instance.PetRightClicked += OnPetRightClicked;
		PetManager.Instance.PetsChanged += OnPetsChanged;
		AppSettings.Instance.Changed += OnSettingsChanged;

		LayoutForScreen();
		bookButton.Visible = Settings.ShowBookButton;
	}

	public override void _ExitTree()
	{
		if (DesktopWindow.Instance != null) DesktopWindow.Instance.ScreenChanged -= LayoutForScreen;
		if (PetManager.Instance != null)
		{
			PetManager.Instance.PetRightClicked -= OnPetRightClicked;
			PetManager.Instance.PetsChanged -= OnPetsChanged;
		}
		if (AppSettings.Instance != null) AppSettings.Instance.Changed -= OnSettingsChanged;
	}

	public override void _Process(double delta)
	{
		var window = DesktopWindow.Instance;

		// Clicks outside our UI fall through to other apps, so watch the buttons directly.
		bool down = window.IsAnyButtonDown;
		if (down && !wasMouseDown)
		{
			Vector2 cursor = window.CursorWorld;
			if (book.Visible && !book.GetGlobalRect().HasPoint(cursor) && !bookButton.GetGlobalRect().HasPoint(cursor))
				HideBook();
			if (petMenu.IsOpen && Engine.GetProcessFrames() > menuOpenedFrame + 1 && !petMenu.GetGlobalRect().HasPoint(cursor))
				petMenu.Close();
		}
		wasMouseDown = down;

		if (counterFlash > 0 && (counterFlash -= delta) <= 0) UpdateCounter();
		UpdateHoveredTile(window.CursorWorld);

		window.UiBusy = book.Visible || petMenu.IsOpen || buttonPressed;
	}

	// ---- Book button ----

	private void BuildBookButton()
	{
		bookButton = new TextureButton
		{
			Name = "BookButton",
			TextureNormal = UiTheme.Tex("btn_menu"),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand,
			TooltipText = "魔法书"
		};
		bookButton.GuiInput += OnBookButtonInput;
		bookButton.MouseEntered += () => bookButton.Modulate = new Color(1.08f, 1.08f, 1.08f);
		bookButton.MouseExited += () => bookButton.Modulate = Colors.White;
		root.AddChild(bookButton);
	}

	private void OnBookButtonInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouse && mouse.ButtonIndex == MouseButton.Left)
		{
			if (mouse.Pressed)
			{
				buttonPressed = true;
				buttonDragged = false;
				buttonPressMouse = mouse.GlobalPosition;
				buttonPressPosition = bookButton.Position;
				DesktopWindow.Instance.ForceOpaque = true;
			}
			else if (buttonPressed)
			{
				buttonPressed = false;
				DesktopWindow.Instance.ForceOpaque = false;
				if (buttonDragged)
				{
					Vector2 p = bookButton.Position;
					AppSettings.Instance.Apply(s =>
					{
						s.BookButtonX = p.X;
						s.BookButtonY = p.Y;
					});
				}
				else
				{
					ToggleBook();
				}
			}
		}
		else if (@event is InputEventMouseMotion motion && buttonPressed)
		{
			Vector2 offset = motion.GlobalPosition - buttonPressMouse;
			if (!buttonDragged && offset.Length() > 6) buttonDragged = true;
			if (buttonDragged) bookButton.Position = ClampButton(buttonPressPosition + offset);
		}
	}

	private Vector2 ClampButton(Vector2 position)
	{
		Vector2 size = bookButton.Size * bookButton.Scale;
		Vector2 screen = DesktopWindow.Instance.Size;
		// On a screen that is smaller than the button there is nothing to clamp to.
		float maxX = Mathf.Max(0, screen.X - size.X);
		float maxY = Mathf.Max(0, screen.Y - size.Y);
		return new Vector2(Mathf.Clamp(position.X, 0, maxX), Mathf.Clamp(position.Y, 0, maxY));
	}

	private void LayoutForScreen()
	{
		Vector2 screen = DesktopWindow.Instance.Size;
		// Never scale the book up or down on a normal screen: a scaled Control turns the
		// text blurry, and on a 1080p screen the small fonts became unreadable. Only shrink
		// it when the screen is really too small for it.
		uiScale = Mathf.Min(1f, (screen.Y - 12f) / book.Size.Y);
		uiScale = Mathf.Max(uiScale, 0.6f);

		book.Scale = new Vector2(uiScale, uiScale);
		book.Position = ((screen - book.Size * uiScale) / 2).Floor();

		bookButton.Scale = Vector2.One * ButtonBaseScale * uiScale;
		Vector2 buttonSize = bookButton.Size * bookButton.Scale;
		Vector2 saved = new Vector2(Settings.BookButtonX, Settings.BookButtonY);
		Vector2 fallback = new Vector2(screen.X - buttonSize.X - 40, screen.Y - buttonSize.Y - 24);
		bookButton.Position = ClampButton(saved.X >= 0 && saved.Y >= 0 ? saved : fallback);
	}

	// ---- Book ----

	public void ToggleBook()
	{
		if (book.Visible) HideBook();
		else ShowBook();
	}

	public void ShowBook()
	{
		petMenu.Close();
		book.Visible = true;
		Sfx.Play(Sfx.Sound.OpenBook);
		ShowTab(currentTab);
	}

	public void HideBook()
	{
		book.Visible = false;
		helpPaper.Visible = false;
		HideNameBubble();
		ClearTiles(); // free the Spine previews
	}

	private void BuildBook()
	{
		var background = UiTheme.Tex("ui_pop_collection_01");
		book = new Control
		{
			Name = "Book",
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop,
			Size = background.GetSize()
		};
		root.AddChild(book);

		book.AddChild(new TextureRect { Texture = background, MouseFilter = Control.MouseFilterEnum.Ignore });

		// The tabs lie on the cover and go under the left page: the page edge is drawn
		// again on top of them.
		string[] tips = { "咖波图鉴", "设置" };
		for (int i = 0; i < tabs.Length; i++)
		{
			int index = i;
			tabs[i] = new TextureButton
			{
				TextureNormal = UiTheme.Tex($"tab_menu_{TabImages[i]}_n"),
				Position = new Vector2(TabTuck, TabsY[i]),
				TooltipText = tips[i],
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			tabs[i].Pressed += () =>
			{
				Sfx.Play(Sfx.Sound.Click);
				ShowTab(index);
			};
			book.AddChild(tabs[i]);
		}
		book.AddChild(BuildPageEdge(background));

		var close = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_close_ye"),
			Position = ClosePosition,
			Scale = Vector2.One * CloseScale,
			TooltipText = "合上",
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		close.Pressed += () =>
		{
			Sfx.Play(Sfx.Sound.Click);
			HideBook();
		};
		book.AddChild(close);

		pages[0] = BuildCollectionPage();
		pages[1] = BuildSettingsPage();
		foreach (var page in pages) book.AddChild(page);

		helpPaper = BuildHelpPaper();
		book.AddChild(helpPaper);

		nameBubble = BuildNameBubble();
		book.AddChild(nameBubble);
	}

	/// <summary>The left edge of the left page, cut out of the background: everything right
	/// of the page outline in the strip where the tabs are. Drawn over the tabs, it makes
	/// them come out from under the page like in the original.</summary>
	private TextureRect BuildPageEdge(Texture2D background)
	{
		var source = background.GetImage();
		if (source.IsCompressed()) source.Decompress();
		var strip = PageEdgeStrip;
		var edge = Image.Create(strip.Size.X, strip.Size.Y, false, Image.Format.Rgba8);
		pageEdgeStart = new int[strip.Size.Y];
		for (int y = 0; y < strip.Size.Y; y++)
		{
			int sy = strip.Position.Y + y;
			// The first page (cream) pixel, then back over the brown outline of the page.
			int start = strip.Size.X;
			for (int x = 0; x < strip.Size.X; x++)
			{
				if (IsPaper(source.GetPixel(strip.Position.X + x, sy)))
				{
					start = x;
					break;
				}
			}
			while (start > 0 && IsPageOutline(source.GetPixel(strip.Position.X + start - 1, sy))) start--;
			pageEdgeStart[y] = start;
			for (int x = start; x < strip.Size.X; x++)
				edge.SetPixel(x, y, source.GetPixel(strip.Position.X + x, sy));
		}
		return new TextureRect
		{
			Texture = ImageTexture.CreateFromImage(edge),
			Position = strip.Position,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
	}

	private static bool IsPaper(Color c) =>
		c.A > 0.8f && c.R > 0.85f && c.G > 0.82f && c.B > 0.75f && Mathf.Abs(c.R - c.G) < 0.08f;

	// The brown line around the page and its soft edge; the blue cover is not part of it.
	private static bool IsPageOutline(Color c) => c.A > 0.5f && c.B - c.R < 0.1f;

	/// <summary>True if a point of the book (book coordinates) is covered by the left page,
	/// i.e. a tab drawn there would be hidden under the page.</summary>
	public bool IsUnderPage(Vector2 point)
	{
		var strip = PageEdgeStrip;
		int y = (int)point.Y - strip.Position.Y;
		if (y < 0 || y >= pageEdgeStart.Length) return false;
		return (int)point.X - strip.Position.X >= pageEdgeStart[y];
	}

	public void ShowTab(int index)
	{
		currentTab = Math.Clamp(index, 0, pages.Length - 1);
		for (int i = 0; i < pages.Length; i++)
		{
			bool active = i == currentTab;
			pages[i].Visible = active;
			tabs[i].TextureNormal = UiTheme.Tex($"tab_menu_{TabImages[i]}_{(active ? "h" : "n")}");
			tabs[i].Position = new Vector2(active ? 0 : TabTuck, TabsY[i]);
		}
		helpPaper.Visible = false;
		HideNameBubble();

		if (currentTab == 0) BuildTiles();
		else ClearTiles();
		if (currentTab == 1) RefreshSettings();
	}

	private static Control NewPage() => new Control { MouseFilter = Control.MouseFilterEnum.Ignore };

	private static void ClearChildren(Node node)
	{
		if (node == null) return;
		foreach (var child in node.GetChildren())
		{
			node.RemoveChild(child);
			child.QueueFree();
		}
	}

	// ---- Collection page ----

	private Control BuildCollectionPage()
	{
		var page = NewPage();

		// The stitched Capoo holding the "on the desk" bar, where the original shows its timer.
		counter = new Control { Position = CounterHeadPosition, MouseFilter = Control.MouseFilterEnum.Ignore };
		page.AddChild(counter);
		counter.AddChild(new TextureRect { Texture = UiTheme.Tex("ui_pop_collection_03"), MouseFilter = Control.MouseFilterEnum.Ignore });
		var bar = new TextureRect
		{
			Texture = UiTheme.Tex("ui_pop_collection_04"),
			Position = CounterBarPosition - CounterHeadPosition,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		counter.AddChild(bar);
		counterLabel = UiTheme.MakeLabel("", 17);
		counterLabel.Position = new Vector2(0, 8);
		counterLabel.Size = new Vector2(bar.Texture.GetWidth(), 26);
		counterLabel.HorizontalAlignment = HorizontalAlignment.Center;
		bar.AddChild(counterLabel);

		tileLayer = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		page.AddChild(tileLayer);

		arrows[0] = PageArrow(true, PrevArrowPosition, () => Flip(-1));
		arrows[1] = PageArrow(false, NextArrowPosition, () => Flip(1));
		page.AddChild(arrows[0]);
		page.AddChild(arrows[1]);

		// The page number is printed on the red ribbon.
		ribbonLabel = UiTheme.MakeLabel("", 18, Colors.White);
		ribbonLabel.Position = RibbonLabelRect.Position;
		ribbonLabel.Size = RibbonLabelRect.Size;
		ribbonLabel.HorizontalAlignment = HorizontalAlignment.Center;
		page.AddChild(ribbonLabel);

		trash = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_rubbish_n"),
			TexturePressed = UiTheme.Tex("btn_rubbish_d"),
			Position = TrashPosition,
			Scale = Vector2.One * TrashScale,
			TooltipText = "全部回收",
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		trash.Pressed += () =>
		{
			Sfx.Play(Sfx.Sound.Click);
			PetManager.Instance.RecycleAll();
		};
		page.AddChild(trash);
		return page;
	}

	private static TextureButton PageArrow(bool left, Vector2 position, Action onPressed)
	{
		var arrow = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_arrow_ye"),
			TextureDisabled = UiTheme.Tex("btn_arrow_gy"),
			FlipH = left,
			Position = position,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		arrow.Pressed += () =>
		{
			Sfx.Play(Sfx.Sound.Click);
			onPressed();
		};
		return arrow;
	}

	private int SpreadCount => Math.Max(1, (config.infos.Count + TilesPerSpread - 1) / TilesPerSpread);

	private void Flip(int direction)
	{
		spread = (spread + direction + SpreadCount) % SpreadCount;
		BuildTiles();
	}

	private static Vector2 TilePosition(int slot)
	{
		int local = slot % TilesPerSide;
		float x = (slot < TilesPerSide ? LeftColumnsX : RightColumnsX)[local % TileColumns];
		return new Vector2(x, RowsY[local / TileColumns]);
	}

	private void ClearTiles()
	{
		ClearChildren(tileLayer);
		tiles.Clear();
		hoveredTile = null;
	}

	/// <summary>Hover is worked out from the cursor every frame rather than from
	/// mouse_entered/exited: the overlay keeps switching click-through on and off, and the
	/// cursor position is the one thing that is always right.</summary>
	private void UpdateHoveredTile(Vector2 cursor)
	{
		Tile hit = null;
		if (book.Visible && currentTab == 0 && !helpPaper.Visible)
		{
			foreach (var tile in tiles)
			{
				if (tile.Root.GetGlobalRect().HasPoint(cursor))
				{
					hit = tile;
					break;
				}
			}
		}
		if (hit == hoveredTile) return;
		if (hoveredTile != null && IsInstanceValid(hoveredTile.Root)) LeaveTile(hoveredTile);
		hoveredTile = hit;
		if (hit != null) EnterTile(hit);
	}

	private void EnterTile(Tile tile)
	{
		tile.Frame.Visible = true;
		tile.Preview.ProcessMode = ProcessModeEnum.Inherit;
		tile.Preview.SetAnimation("idle_idle2");
		tile.Preview.RefreshPose();
		ShowNameBubble(config.infos[tile.Index].name, tile.Root.Position);
	}

	private void LeaveTile(Tile tile)
	{
		tile.Frame.Visible = false;
		tile.Preview.SetAnimation("idle_idle1");
		tile.Preview.RefreshPose();
		tile.Preview.ProcessMode = ProcessModeEnum.Disabled;
		HideNameBubble();
	}

	private void BuildTiles()
	{
		ClearTiles();
		HideNameBubble();
		ribbonLabel.Text = $"{spread + 1}/{SpreadCount}";
		bool single = SpreadCount <= 1;
		arrows[0].Disabled = single;
		arrows[1].Disabled = single;

		for (int slot = 0; slot < TilesPerSpread; slot++)
		{
			int index = spread * TilesPerSpread + slot;
			if (index >= config.infos.Count) break;
			tiles.Add(MakeTile(index, TilePosition(slot)));
		}
		UpdateTileStates();
	}

	private Tile MakeTile(int index, Vector2 position)
	{
		var tile = new Tile { Index = index };
		tile.Root = new Control
		{
			Position = position,
			Size = new Vector2(TileSize, TileSize),
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		tileLayer.AddChild(tile.Root); // in the tree first, so the Spine preview gets _Ready immediately

		tile.Box = new TextureRect { Texture = UiTheme.Tex("box_collection_bot_01"), MouseFilter = Control.MouseFilterEnum.Ignore };
		tile.Root.AddChild(tile.Box);

		var clip = new Control
		{
			Position = new Vector2(4, 4),
			Size = new Vector2(TileSize - 8, TileSize - 8),
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		tile.Root.AddChild(clip);
		tile.Preview = new CapooView(index) { Position = new Vector2((TileSize - 8) / 2, TileSize - 20) };
		clip.AddChild(tile.Preview);
		tile.Preview.SetPetScale(0.17f);
		tile.Preview.SetAnimation("idle_idle1");
		// Spine only fills the mesh while the sprite is visible, so a fresh preview would
		// show up without face and hat for a frame or two.
		tile.Preview.RefreshPose();
		var preview = tile.Preview;
		GetTree().CreateTimer(0.2).Timeout += () =>
		{
			if (IsInstanceValid(preview) && !tile.Frame.Visible) preview.ProcessMode = ProcessModeEnum.Disabled;
		};

		// The yellow corners of a hovered tile.
		tile.Frame = new TextureRect
		{
			Texture = UiTheme.Tex("box_collection_bot_05"),
			Position = new Vector2(-3, -3),
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		tile.Root.AddChild(tile.Frame);

		// How many of this Capoo are on the desk (only shown from two up).
		tile.Count = UiTheme.MakeLabel("", 14);
		tile.Count.Position = new Vector2(7, 3);
		tile.Count.Visible = false;
		tile.Root.AddChild(tile.Count);

		// Recycle one of them, like the small button of the original.
		tile.Recycle = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_recycle_ye"),
			Position = new Vector2(TileSize - 30, TileSize - 32),
			Visible = false,
			TooltipText = "回收一只",
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		tile.Recycle.Pressed += () => RecycleOne(index);
		tile.Root.AddChild(tile.Recycle);

		tile.Root.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Summon(index);
		};
		return tile;
	}

	/// <summary>Green tile, count and recycle button for the Capoos that are on the desk.</summary>
	private void UpdateTileStates()
	{
		var onDesk = new Dictionary<int, int>();
		foreach (var pet in PetManager.Instance.Pets)
		{
			if (pet.State == CapooState.Recycle) continue;
			onDesk[pet.InfoIndex] = onDesk.GetValueOrDefault(pet.InfoIndex) + 1;
		}
		foreach (var tile in tiles)
		{
			int count = onDesk.GetValueOrDefault(tile.Index);
			tile.Box.Texture = UiTheme.Tex(count > 0 ? "box_collection_bot_02" : "box_collection_bot_01");
			tile.Recycle.Visible = count > 0;
			tile.Count.Visible = count > 1;
			tile.Count.Text = $"×{count}";
		}
		trash.Disabled = onDesk.Count == 0;
		trash.Modulate = onDesk.Count == 0 ? new Color(1, 1, 1, 0.5f) : Colors.White;
		UpdateCounter();
	}

	private void UpdateCounter()
	{
		counterFlash = 0;
		counterLabel.Text = $"桌面上 {PetManager.Instance.Pets.Count(p => p.State != CapooState.Recycle)} / {Settings.MaxPets}";
	}

	private void Summon(int index)
	{
		if (PetManager.Instance.SpawnPet(index) != null) return;
		// Full: say so on the bar for a moment.
		counterLabel.Text = $"满啦，最多 {Settings.MaxPets} 只";
		counterFlash = 1.6;
	}

	private void RecycleOne(int index)
	{
		Sfx.Play(Sfx.Sound.Click);
		var pet = PetManager.Instance.Pets.LastOrDefault(p => p.InfoIndex == index && p.State != CapooState.Recycle);
		pet?.StartRecycle();
	}

	private NinePatchRect BuildNameBubble()
	{
		// tips_name_01: white bubble with its tail in the bottom-left corner, which the
		// patch margins keep from being stretched.
		var bubble = new NinePatchRect
		{
			Texture = UiTheme.Tex("tips_name_01"),
			PatchMarginLeft = 36,
			PatchMarginRight = 16,
			PatchMarginTop = 14,
			PatchMarginBottom = 14,
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		nameBubbleLabel = UiTheme.MakeLabel("", 16, BubbleInk);
		nameBubbleLabel.Position = new Vector2(14, 5);
		bubble.AddChild(nameBubbleLabel);
		return bubble;
	}

	private void ShowNameBubble(string name, Vector2 tilePosition)
	{
		nameBubbleLabel.Text = name;
		Vector2 text = nameBubbleLabel.GetCombinedMinimumSize();
		nameBubble.Size = new Vector2(Mathf.Max(64, text.X + 28), 52);
		nameBubbleLabel.Size = new Vector2(nameBubble.Size.X - 28, 26);
		nameBubbleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		// The tail (23 px from the left edge) points at the middle of the tile's top.
		nameBubble.Position = new Vector2(tilePosition.X + TileSize / 2 - 23, tilePosition.Y - nameBubble.Size.Y + 6);
		nameBubble.Visible = true;
	}

	private void HideNameBubble()
	{
		if (nameBubble != null) nameBubble.Visible = false;
	}

	private void OnPetsChanged()
	{
		if (!book.Visible || currentTab != 0) return;
		// A recycled Capoo leaves the list a moment later; look again once it has.
		Callable.From(UpdateTileStates).CallDeferred();
	}

	// ---- Settings page ----

	private Control BuildSettingsPage()
	{
		var page = NewPage();

		// Left page: one ◀ value ▶ bar per setting, like the volume bars of the original.
		settingsValues = new VBoxContainer { Position = LeftPage.Position + new Vector2(18, 16), MouseFilter = Control.MouseFilterEnum.Ignore };
		settingsValues.AddThemeConstantOverride("separation", 6);
		page.AddChild(settingsValues);

		AddStepper("音效", Enumerable.Range(0, 11).Select(i => i == 0 ? "静音" : $"{i * 10}%").ToArray(),
			s => Mathf.RoundToInt(s.SoundVolume * 10), (s, i) => s.SoundVolume = i / 10f);
		AddStepper("大小", Enumerable.Range(5, 16).Select(i => $"{i * 5}%").ToArray(),
			s => Mathf.RoundToInt(s.PetScale * 20) - 5, (s, i) => s.PetScale = (i + 5) / 20f);
		int[] counts = { 1, 3, 5, 8, 10, 15, 20, 30, 40, 50 };
		AddStepper("数量", counts.Select(c => $"{c} 只").ToArray(),
			s => Nearest(counts, s.MaxPets), (s, i) => s.MaxPets = counts[i]);
		AddStepper("帧率", new[] { "30", "60" }, s => s.MaxFps > 30 ? 1 : 0, (s, i) => s.MaxFps = i == 0 ? 30 : 60);
		AddStepper("透明", new[] { "逐像素", "颜色键" }, s => s.TransparencyMode == "colorkey" ? 1 : 0,
			(s, i) => s.TransparencyMode = i == 1 ? "colorkey" : "alpha");
		AddStepper("饿/便便", new[] { "关", "少", "中", "多" }, s => s.CareLevel, (s, i) => s.CareLevel = i);
		int[] minutes = { 1, 3, 5, 10, 15, 20, 30, 45, 60 };
		AddStepper("离开后睡", minutes.Select(m => $"{m} 分钟").ToArray(),
			s => Nearest(minutes, s.SleepIdleMinutes), (s, i) => s.SleepIdleMinutes = minutes[i]);

		// Right page: small check boxes in two columns...
		settingsChecks = new GridContainer { Columns = 2, Position = RightPage.Position + new Vector2(14, 16), MouseFilter = Control.MouseFilterEnum.Ignore };
		settingsChecks.AddThemeConstantOverride("h_separation", 14);
		settingsChecks.AddThemeConstantOverride("v_separation", 8);
		page.AddChild(settingsChecks);

		AddCheck("会睡觉", s => s.SleepEnabled, (s, v) => s.SleepEnabled = v);
		AddCheck("夜里多打盹", s => s.NightSleep, (s, v) => s.NightSleep = v);
		AddCheck("站在窗口顶上", s => s.WindowAware, (s, v) => s.WindowAware = v);
		AddCheck("叠罗汉", s => s.Stacking, (s, v) => s.Stacking = v);
		AddCheck("全屏时隐藏", s => s.HideOnFullscreen, (s, v) => s.HideOnFullscreen = v);
		AddCheck("低功耗", s => s.LowPower, (s, v) => s.LowPower = v);
		AddCheck("总在最前", s => s.AlwaysOnTop, (s, v) => s.AlwaysOnTop = v);
		AddCheck("书本按钮", s => s.ShowBookButton, (s, v) => s.ShowBookButton = v);
		var autoStart = AddCheck("开机启动", _ => AutoStart.IsEnabled(), (_, v) => AutoStart.SetEnabled(v));
		if (!AutoStart.Supported)
		{
			autoStart.Disabled = true;
			autoStart.TooltipText = "导出成 exe 后才能设置";
		}

		// ...and big icon buttons, like the right page of the original settings.
		settingsButtons = new HBoxContainer { Position = RightPage.Position + new Vector2(66, 216), MouseFilter = Control.MouseFilterEnum.Ignore };
		settingsButtons.AddThemeConstantOverride("separation", 56);
		page.AddChild(settingsButtons);
		AddIconButton("btn_manual_ye", "怎么玩", () => helpPaper.Visible = !helpPaper.Visible);
		AddIconButton("btn_shutdown_vt", "退出", () => GetNode<Main>("/root/Main").QuitApp());
		return page;
	}

	private static int Nearest(int[] values, int value)
	{
		int best = 0;
		for (int i = 1; i < values.Length; i++)
			if (Math.Abs(values[i] - value) < Math.Abs(values[best] - value)) best = i;
		return best;
	}

	/// <summary>A setting with a few values: name, ◀, the value on a dark bar, ▶.</summary>
	private void AddStepper(string text, string[] values, Func<SettingsData, int> get, Action<SettingsData, int> set)
	{
		var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		row.AddThemeConstantOverride("separation", 4);
		var label = UiTheme.MakeLabel(text, 17);
		label.CustomMinimumSize = new Vector2(92, 0);
		label.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
		row.AddChild(label);

		var less = StepArrow(true);
		row.AddChild(less);

		var bar = new NinePatchRect
		{
			Texture = UiTheme.Tex("bar_language_01"),
			PatchMarginLeft = 16,
			PatchMarginRight = 16,
			PatchMarginTop = 12,
			PatchMarginBottom = 12,
			CustomMinimumSize = new Vector2(124, 34),
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		var value = UiTheme.MakeLabel("", 16, Colors.White);
		value.HorizontalAlignment = HorizontalAlignment.Center;
		value.VerticalAlignment = VerticalAlignment.Center;
		value.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bar.AddChild(value);
		row.AddChild(bar);

		var more = StepArrow(false);
		row.AddChild(more);

		void Show(int index)
		{
			index = Math.Clamp(index, 0, values.Length - 1);
			value.Text = values[index];
			less.Disabled = index == 0;
			more.Disabled = index == values.Length - 1;
		}
		void Step(int direction)
		{
			int index = Math.Clamp(get(Settings) + direction, 0, values.Length - 1);
			Sfx.Play(Sfx.Sound.Click);
			AppSettings.Instance.Apply(s => set(s, index));
			Show(index);
		}
		less.Pressed += () => Step(-1);
		more.Pressed += () => Step(1);
		settingRefreshers.Add(() => Show(get(Settings)));
		settingsValues.AddChild(row);
	}

	private static TextureButton StepArrow(bool left)
	{
		return new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_arrow_ye_s"),
			TextureDisabled = UiTheme.Tex("btn_arrow_gy_s"),
			FlipH = left,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
	}

	private CheckBox AddCheck(string text, Func<SettingsData, bool> get, Action<SettingsData, bool> set)
	{
		var check = new CheckBox
		{
			Text = text,
			CustomMinimumSize = new Vector2(140, 0),
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		check.Toggled += on =>
		{
			if (refreshingSettings) return;
			Sfx.Play(Sfx.Sound.Click);
			AppSettings.Instance.Apply(s => set(s, on));
		};
		settingRefreshers.Add(() => check.SetPressedNoSignal(get(Settings)));
		settingsChecks.AddChild(check);
		return check;
	}

	private void AddIconButton(string image, string caption, Action action)
	{
		var column = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
		column.AddThemeConstantOverride("separation", 2);
		var button = new TextureButton
		{
			TextureNormal = UiTheme.Tex(image),
			SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		button.Pressed += () =>
		{
			Sfx.Play(Sfx.Sound.Click);
			action();
		};
		column.AddChild(button);
		var label = UiTheme.MakeLabel(caption, 16);
		label.HorizontalAlignment = HorizontalAlignment.Center;
		column.AddChild(label);
		settingsButtons.AddChild(column);
	}

	/// <summary>A sheet of paper over the right page with the how-to-play notes.</summary>
	private Control BuildHelpPaper()
	{
		var paper = new TextureRect
		{
			Texture = UiTheme.Tex("ui_pop_tooltip_02"),
			Position = new Vector2(RightPage.Position.X - 8, 118),
			Visible = false,
			MouseFilter = Control.MouseFilterEnum.Stop
		};

		var title = UiTheme.MakeLabel("怎么玩", 22);
		title.Position = new Vector2(28, 24);
		paper.AddChild(title);

		var text = UiTheme.MakeLabel(
			"· 点一下咖波：戳戳它\n" +
			"· 鼠标在咖波身上来回蹭：摸摸它\n" +
			"· 长按拖走，松手可以扔出去\n" +
			"· 扔到别的咖波头上：叠罗汉\n" +
			"· 点头上的气泡：喂食 / 扫便便\n" +
			"· 右键咖波：打开它的小菜单\n" +
			"· 咖波会站在窗口顶上散步\n" +
			"· 离开电脑一会儿，它们会睡着", 16, UiTheme.InkLight);
		text.Position = new Vector2(28, 64);
		text.Size = new Vector2(286, 290);
		text.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		text.AddThemeConstantOverride("line_spacing", 6);
		paper.AddChild(text);

		var close = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_close_co"),
			Position = new Vector2(278, 12),
			Scale = Vector2.One * 0.75f,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		close.Pressed += () => paper.Visible = false;
		paper.AddChild(close);
		return paper;
	}

	private void RefreshSettings()
	{
		refreshingSettings = true;
		foreach (var refresh in settingRefreshers) refresh();
		refreshingSettings = false;
	}

	private void OnSettingsChanged()
	{
		bookButton.Visible = Settings.ShowBookButton;
		if (!book.Visible) return;
		if (currentTab == 1) RefreshSettings();
		else UpdateCounter();
	}

	// ---- Layout check (self-test) ----

	/// <summary>The boxes the book fills on one page, in book coordinates. The self-test
	/// uses them to check that nothing overlaps and that nothing leaves the paper.</summary>
	public List<(string name, Rect2 rect)> GetLayoutRects(int tab)
	{
		var rects = new List<(string, Rect2)>();
		for (int i = 0; i < tabs.Length; i++)
			rects.Add(($"tab{i}", new Rect2(tabs[i].Position, tabs[i].Size * tabs[i].Scale)));
		rects.Add(("close", new Rect2(ClosePosition, UiTheme.Tex("btn_close_ye").GetSize() * CloseScale)));

		if (tab == 0)
		{
			rects.Add(("counter", new Rect2(CounterBarPosition, UiTheme.Tex("ui_pop_collection_04").GetSize())));
			rects.Add(("page", RibbonLabelRect));
			rects.Add(("trash", new Rect2(TrashPosition, UiTheme.Tex("btn_rubbish_n").GetSize() * TrashScale)));
			for (int i = 0; i < arrows.Length; i++)
				rects.Add(($"arrow{i}", new Rect2(arrows[i].Position, UiTheme.Tex("btn_arrow_ye").GetSize())));
			for (int i = 0; i < TilesPerSpread; i++)
				rects.Add(($"tile{i}", new Rect2(TilePosition(i), new Vector2(TileSize, TileSize))));
		}
		else
		{
			rects.Add(("values", new Rect2(settingsValues.Position, settingsValues.Size)));
			rects.Add(("checks", new Rect2(settingsChecks.Position, settingsChecks.Size)));
			rects.Add(("buttons", new Rect2(settingsButtons.Position, settingsButtons.Size)));
		}
		return rects;
	}

	/// <summary>Parts that sit off the paper on purpose (on the cover, the ribbon, the corners).</summary>
	public static bool IsOffPaperByDesign(string name) =>
		name.StartsWith("tab") || name.StartsWith("arrow") || name is "close" or "counter" or "page" or "trash";

	/// <summary>The paper the pages are printed on.</summary>
	public static Rect2 LeftPageArea => LeftPage;
	public static Rect2 RightPageArea => RightPage;

	/// <summary>How much the book is scaled on the current screen.</summary>
	public float UiScale => uiScale;

	/// <summary>The area the open book covers on the screen.</summary>
	public Rect2 BookRect => book.GetGlobalRect();

	/// <summary>Self-test: the page tabs, a tile of the collection page and the name bubble.</summary>
	public TextureButton FirstTab => tabs[0];
	public IReadOnlyList<TextureButton> Tabs => tabs;
	public static IReadOnlyList<string> TabImageNames => TabImages;
	public Control FirstCard => tiles.Count > 0 ? tiles[0].Root : null;
	public Control NameBubble => nameBubble;
	public bool IsBookButtonVisible => bookButton.Visible;

	/// <summary>True if the tile of a character on the current spread shows it is on the desk.</summary>
	public bool IsTileOnDesk(int index)
	{
		var tile = tiles.FirstOrDefault(t => t.Index == index);
		return tile != null && tile.Box.Texture == UiTheme.Tex("box_collection_bot_02");
	}
	public int TabCount => pages.Length;

	// ---- Pet menu ----

	public void OnPetRightClicked(CapooController pet, Vector2 position)
	{
		menuOpenedFrame = Engine.GetProcessFrames();
		petMenu.Open(pet, position);
	}
}
