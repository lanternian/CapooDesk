using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The magic book UI: a draggable floating book button, and the open book with three tabs —
/// summon (all 126 Capoos, unlocked), the Capoos on the desk, and settings.
/// Also hosts the right-click Capoo menu.
/// </summary>
public partial class MagicBook : CanvasLayer
{
	private const int CardColumns = 3;
	private const int CardRows = 3;
	private const int CardsPerSide = CardColumns * CardRows;
	private const int CardsPerSpread = CardsPerSide * 2;
	private const int DeskRowsPerPage = 8;
	private const float ButtonBaseScale = 0.55f;

	// Writable page areas inside ui_pop_collection_01.png (954x694). The paper runs out at
	// y=500 (below that the pages are rolled up) and the folded corner in the top right of
	// the right page starts at x=815, so the right column has to stop before that.
	private static readonly Rect2 LeftPage = new Rect2(96, 150, 345, 350);
	private static readonly Rect2 RightPage = new Rect2(478, 150, 345, 350);
	private const float FoldLeft = 815f;

	// Card grid inside one page.
	private const float CardPitchX = 112f;
	private const float CardPitchY = 94f;
	private const float CardWidth = 100f;
	private const float CardHeight = 92f;
	private const float CardTop = 28f;

	// The strip at the bottom of a page: the page arrows and the page number.
	private const float BottomBarY = 468f;
	private const float ArrowScale = 0.75f;

	private static SettingsData Settings => AppSettings.Instance.Data;

	private CapooConfig config;
	private Control root;
	private TextureButton bookButton;
	private Control book;
	private readonly Control[] pages = new Control[3];
	private readonly TextureButton[] tabs = new TextureButton[3];
	private static readonly string[] TabImages = { "01", "03", "04" };
	private int currentTab;
	private PetContextMenu petMenu;
	private ulong menuOpenedFrame;
	private bool wasMouseDown;
	private float uiScale = 1f;

	// Summon tab
	private int summonSpread;
	private Control summonCards;
	private Label summonPageLabel;
	private Label summonStatus;
	private readonly TextureButton[] summonArrows = new TextureButton[2];

	// Desk tab
	private int deskPage;
	private VBoxContainer deskList;
	private Label deskPageLabel;
	private Label deskCount;
	private readonly TextureButton[] deskArrows = new TextureButton[2];

	// Settings tab
	private VBoxContainer settingsLeft;
	private VBoxContainer settingsRight;
	private readonly List<Action> settingRefreshers = new List<Action>();
	private bool refreshingSettings;
	private Action pendingApply;
	private double pendingTimer;

	// Book button dragging
	private bool buttonPressed;
	private bool buttonDragged;
	private Vector2 buttonPressMouse;
	private Vector2 buttonPressPosition;

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

		if (pendingApply != null && (pendingTimer -= delta) <= 0)
		{
			var apply = pendingApply;
			pendingApply = null;
			apply();
		}

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
		ClearChildren(summonCards); // free the Spine previews
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

		// The page tabs peek out from behind the cover, so they go in before the paper.
		string[] tips = { "召唤咖波", "桌面上的咖波", "设置" };
		for (int i = 0; i < tabs.Length; i++)
		{
			int index = i;
			tabs[i] = new TextureButton
			{
				TextureNormal = UiTheme.Tex($"tab_menu_{TabImages[i]}_n"),
				Position = new Vector2(836, 96 + i * 66),
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

		book.AddChild(new TextureRect { Texture = background, MouseFilter = Control.MouseFilterEnum.Ignore });

		// The close button sits above the right page, clear of the folded corner.
		var close = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_close_co"),
			Position = new Vector2(742, 58),
			TooltipText = "合上",
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		close.Pressed += HideBook;
		book.AddChild(close);

		pages[0] = BuildSummonPage();
		pages[1] = BuildDeskPage();
		pages[2] = BuildSettingsPage();
		foreach (var page in pages) book.AddChild(page);
	}

	public void ShowTab(int index)
	{
		currentTab = index;
		for (int i = 0; i < pages.Length; i++)
		{
			pages[i].Visible = i == index;
			tabs[i].TextureNormal = UiTheme.Tex($"tab_menu_{TabImages[i]}_{(i == index ? "h" : "n")}");
		}

		if (index == 0) BuildSummonCards();
		else ClearChildren(summonCards);
		if (index == 1) RefreshDesk();
		if (index == 2) RefreshSettings();
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

	private static TextureButton Arrow(bool left, Vector2 position, Action onPressed)
	{
		var arrow = new TextureButton
		{
			TextureNormal = UiTheme.Tex("btn_arrow_ye_s"),
			TextureDisabled = UiTheme.Tex("btn_arrow_gy_s"),
			FlipH = left,
			Position = position,
			Scale = Vector2.One * ArrowScale,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		arrow.Pressed += () =>
		{
			Sfx.Play(Sfx.Sound.Click);
			onPressed();
		};
		return arrow;
	}

	// ---- Summon tab ----

	private Control BuildSummonPage()
	{
		var page = NewPage();

		var title = UiTheme.MakeLabel("咖波图鉴", 25);
		title.Position = LeftPage.Position + new Vector2(8, 0);
		page.AddChild(title);

		var hint = UiTheme.MakeLabel("点一下就能召唤到桌面上", 16, UiTheme.InkLight);
		hint.Position = LeftPage.Position + new Vector2(112, 6);
		page.AddChild(hint);

		// Right aligned, but the folded corner of the page keeps its width.
		summonStatus = UiTheme.MakeLabel("", 17, UiTheme.InkLight);
		summonStatus.Position = RightPage.Position + new Vector2(8, 2);
		summonStatus.Size = new Vector2(FoldLeft - RightPage.Position.X - 20, 24);
		summonStatus.HorizontalAlignment = HorizontalAlignment.Right;
		page.AddChild(summonStatus);

		summonCards = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		page.AddChild(summonCards);

		summonArrows[0] = Arrow(true, new Vector2(LeftPage.Position.X + 4, BottomBarY), () => FlipSummon(-1));
		summonArrows[1] = Arrow(false, new Vector2(RightPage.End.X - 38, BottomBarY), () => FlipSummon(1));
		page.AddChild(summonArrows[0]);
		page.AddChild(summonArrows[1]);

		summonPageLabel = UiTheme.MakeLabel("", 16, UiTheme.InkLight);
		summonPageLabel.Position = new Vector2(LeftPage.Position.X + 124, BottomBarY + 6);
		summonPageLabel.Size = new Vector2(100, 20);
		summonPageLabel.HorizontalAlignment = HorizontalAlignment.Center;
		page.AddChild(summonPageLabel);
		return page;
	}

	private int SpreadCount => Math.Max(1, (config.infos.Count + CardsPerSpread - 1) / CardsPerSpread);

	private void FlipSummon(int direction)
	{
		summonSpread = (summonSpread + direction + SpreadCount) % SpreadCount;
		BuildSummonCards();
	}

	private void BuildSummonCards()
	{
		ClearChildren(summonCards);
		summonPageLabel.Text = $"{summonSpread + 1} / {SpreadCount}";
		UpdateSummonStatus();
		// A single spread has nothing to flip to.
		bool single = SpreadCount <= 1;
		summonArrows[0].Disabled = single;
		summonArrows[1].Disabled = single;

		for (int slot = 0; slot < CardsPerSpread; slot++)
		{
			int index = summonSpread * CardsPerSpread + slot;
			if (index >= config.infos.Count) break;
			Rect2 side = slot < CardsPerSide ? LeftPage : RightPage;
			int local = slot % CardsPerSide;
			var position = side.Position + new Vector2(8 + (local % CardColumns) * CardPitchX, CardTop + (local / CardColumns) * CardPitchY);
			MakeCard(index, position, summonCards);
		}
	}

	private void MakeCard(int index, Vector2 position, Control parent)
	{
		var info = config.infos[index];
		var cell = new Control
		{
			Position = position,
			Size = new Vector2(CardWidth, CardHeight),
			MouseFilter = Control.MouseFilterEnum.Stop,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		parent.AddChild(cell); // in the tree first, so the Spine preview gets _Ready immediately

		var card = new TextureRect
		{
			Texture = UiTheme.Tex("box_pic_wh"),
			Position = new Vector2(14, 0),
			Size = new Vector2(72, 75),
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
			StretchMode = TextureRect.StretchModeEnum.Scale,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		cell.AddChild(card);

		var clip = new Control
		{
			Position = new Vector2(16, 3),
			Size = new Vector2(68, 69),
			ClipContents = true,
			MouseFilter = Control.MouseFilterEnum.Ignore
		};
		cell.AddChild(clip);

		var preview = new CapooView(index) { Position = new Vector2(35, 67) };
		clip.AddChild(preview);
		preview.SetPetScale(0.14f);
		preview.SetAnimation("idle_idle1");
		// Spine only fills the mesh while the sprite is visible, so a fresh preview would
		// show up without face and hat for a frame or two.
		preview.RefreshPose();
		// Static thumbnail until hovered: let the pose settle, then stop animating.
		GetTree().CreateTimer(0.2).Timeout += () =>
		{
			if (IsInstanceValid(preview) && card.Texture == UiTheme.Tex("box_pic_wh"))
				preview.ProcessMode = ProcessModeEnum.Disabled;
		};

		var name = UiTheme.MakeLabel(info.name, 14);
		name.Position = new Vector2(0, 76);
		name.Size = new Vector2(CardWidth, 16);
		name.HorizontalAlignment = HorizontalAlignment.Center;
		name.ClipText = true;
		cell.AddChild(name);

		cell.MouseEntered += () =>
		{
			card.Texture = UiTheme.Tex("box_pic_ye");
			preview.ProcessMode = ProcessModeEnum.Inherit;
			preview.SetAnimation("idle_idle2");
			preview.RefreshPose();
		};
		cell.MouseExited += () =>
		{
			card.Texture = UiTheme.Tex("box_pic_wh");
			preview.SetAnimation("idle_idle1");
			preview.RefreshPose();
			preview.ProcessMode = ProcessModeEnum.Disabled;
		};
		cell.GuiInput += e =>
		{
			if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) Summon(index);
		};
	}

	private void Summon(int index)
	{
		var pet = PetManager.Instance.SpawnPet(index);
		summonStatus.Text = pet == null
			? $"桌面上已经有 {Settings.MaxPets} 只啦"
			: $"召唤了「{config.infos[index].name}」";
	}

	private void UpdateSummonStatus()
	{
		summonStatus.Text = $"桌面上 {PetManager.Instance.Pets.Count} / {Settings.MaxPets} 只";
	}

	// ---- Desk tab ----

	private Control BuildDeskPage()
	{
		var page = NewPage();

		var title = UiTheme.MakeLabel("桌面上的咖波", 25);
		title.Position = LeftPage.Position + new Vector2(8, 0);
		page.AddChild(title);

		deskList = new VBoxContainer { Position = LeftPage.Position + new Vector2(8, 38), Size = new Vector2(LeftPage.Size.X - 16, 268) };
		deskList.AddThemeConstantOverride("separation", 4);
		page.AddChild(deskList);

		deskArrows[0] = Arrow(true, new Vector2(LeftPage.Position.X + 4, BottomBarY), () => FlipDesk(-1));
		deskArrows[1] = Arrow(false, new Vector2(RightPage.End.X - 38, BottomBarY), () => FlipDesk(1));
		page.AddChild(deskArrows[0]);
		page.AddChild(deskArrows[1]);

		deskPageLabel = UiTheme.MakeLabel("", 16, UiTheme.InkLight);
		deskPageLabel.Position = new Vector2(LeftPage.Position.X + 124, BottomBarY + 6);
		deskPageLabel.Size = new Vector2(100, 20);
		deskPageLabel.HorizontalAlignment = HorizontalAlignment.Center;
		page.AddChild(deskPageLabel);

		var right = new VBoxContainer { Position = RightPage.Position + new Vector2(8, 0), Size = new Vector2(FoldLeft - RightPage.Position.X - 20, RightPage.Size.Y) };
		right.AddThemeConstantOverride("separation", 10);
		page.AddChild(right);

		deskCount = UiTheme.MakeLabel("", 22);
		right.AddChild(deskCount);

		var buttons = new HBoxContainer();
		var summonRandom = new Button { Text = "随机召唤一只", MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		summonRandom.Pressed += () => PetManager.Instance.SpawnRandom();
		buttons.AddChild(summonRandom);
		var recycleAll = new Button { Text = "全部回收", MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		recycleAll.Pressed += () => PetManager.Instance.RecycleAll();
		buttons.AddChild(recycleAll);
		right.AddChild(buttons);

		var help = UiTheme.MakeLabel(
			"· 长按咖波可以拖走，松手扔出去\n" +
			"· 鼠标在咖波身上来回蹭，就是摸摸它\n" +
			"· 点头上的气泡：喂食 / 扫便便\n" +
			"· 右键咖波：打开它的小菜单\n" +
			"· 扔到别的咖波头上可以叠罗汉\n" +
			"· 咖波可以站在窗口顶上散步\n" +
			"· 你离开电脑一会儿，它们会睡着", 17, UiTheme.InkLight);
		help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
		help.CustomMinimumSize = new Vector2(FoldLeft - RightPage.Position.X - 20, 0);
		right.AddChild(help);
		return page;
	}

	private void FlipDesk(int direction)
	{
		int pageCount = Math.Max(1, (PetManager.Instance.Pets.Count + DeskRowsPerPage - 1) / DeskRowsPerPage);
		deskPage = (deskPage + direction + pageCount) % pageCount;
		RefreshDesk();
	}

	private void RefreshDesk()
	{
		var pets = PetManager.Instance.Pets;
		int pageCount = Math.Max(1, (pets.Count + DeskRowsPerPage - 1) / DeskRowsPerPage);
		deskPage = Math.Clamp(deskPage, 0, pageCount - 1);
		deskPageLabel.Text = $"{deskPage + 1} / {pageCount}";
		deskCount.Text = $"桌面上 {pets.Count} / {Settings.MaxPets} 只";
		// A single page has nothing to flip to.
		deskArrows[0].Disabled = pageCount <= 1;
		deskArrows[1].Disabled = pageCount <= 1;

		ClearChildren(deskList);
		if (pets.Count == 0)
		{
			deskList.AddChild(UiTheme.MakeLabel("还没有咖波，去图鉴里召唤一只吧", 17, UiTheme.InkLight));
			return;
		}

		for (int i = deskPage * DeskRowsPerPage; i < Math.Min(pets.Count, (deskPage + 1) * DeskRowsPerPage); i++)
		{
			var pet = pets[i];
			var row = new HBoxContainer();
			var name = UiTheme.MakeLabel($"{i + 1}. {pet.DisplayName}", 18);
			name.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
			name.ClipText = true;
			row.AddChild(name);

			var recycle = new TextureButton
			{
				TextureNormal = UiTheme.Tex("btn_recycle_ye"),
				TooltipText = "回收",
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			recycle.Pressed += () =>
			{
				if (IsInstanceValid(pet)) pet.StartRecycle();
			};
			row.AddChild(recycle);
			deskList.AddChild(row);
		}
	}

	private void OnPetsChanged()
	{
		if (!book.Visible) return;
		if (currentTab == 0) UpdateSummonStatus();
		else if (currentTab == 1) Callable.From(RefreshDesk).CallDeferred();
	}

	// ---- Settings tab ----

	private Control BuildSettingsPage()
	{
		var page = NewPage();

		var leftTitle = UiTheme.MakeLabel("外观与窗口", 24);
		leftTitle.Position = LeftPage.Position + new Vector2(8, 0);
		page.AddChild(leftTitle);
		var left = new VBoxContainer { Position = LeftPage.Position + new Vector2(8, 34), Size = new Vector2(LeftPage.Size.X - 16, 300) };
		left.AddThemeConstantOverride("separation", 4);
		page.AddChild(left);
		settingsLeft = left;

		AddSlider(left, "咖波大小", 0.25, 1.0, 0.05, s => s.PetScale, (s, v) => s.PetScale = (float)v, v => $"{v * 100:0}%");
		AddSlider(left, "最多几只", 1, 50, 1, s => s.MaxPets, (s, v) => s.MaxPets = (int)v, v => $"{v:0} 只");
		AddChoice(left, "帧率上限", new[] { "30", "60" }, s => s.MaxFps > 30 ? 1 : 0, (s, i) => s.MaxFps = i == 0 ? 30 : 60);
		AddSlider(left, "音效音量", 0, 1, 0.05, s => s.SoundVolume, (s, v) => s.SoundVolume = (float)v, v => v <= 0 ? "静音" : $"{v * 100:0}%");
		AddCheck(left, "低功耗（没人理时降帧）", s => s.LowPower, (s, v) => s.LowPower = v);
		AddCheck(left, "总在最前", s => s.AlwaysOnTop, (s, v) => s.AlwaysOnTop = v);
		AddCheck(left, "显示书本按钮", s => s.ShowBookButton, (s, v) => s.ShowBookButton = v);
		AddChoice(left, "透明方式", new[] { "逐像素", "颜色键" }, s => s.TransparencyMode == "colorkey" ? 1 : 0,
			(s, i) => s.TransparencyMode = i == 1 ? "colorkey" : "alpha");
		var autoStart = AddCheck(left, "开机自动启动", _ => AutoStart.IsEnabled(), (_, v) => AutoStart.SetEnabled(v));
		if (!AutoStart.Supported)
		{
			autoStart.Disabled = true;
			autoStart.TooltipText = "导出成 exe 后才能设置";
		}

		var rightTitle = UiTheme.MakeLabel("咖波的习惯", 24);
		rightTitle.Position = RightPage.Position + new Vector2(8, 0);
		page.AddChild(rightTitle);
		// The column stops before the folded corner of the page.
		float rightWidth = FoldLeft - RightPage.Position.X - 20;
		var right = new VBoxContainer { Position = RightPage.Position + new Vector2(8, 34), Size = new Vector2(rightWidth, 300) };
		right.AddThemeConstantOverride("separation", 4);
		page.AddChild(right);
		settingsRight = right;

		AddChoice(right, "饿/便便", new[] { "关", "少", "中", "多" }, s => s.CareLevel, (s, i) => s.CareLevel = i);
		AddCheck(right, "会睡觉", s => s.SleepEnabled, (s, v) => s.SleepEnabled = v);
		AddSlider(right, "离开多久后睡", 1, 60, 1, s => s.SleepIdleMinutes, (s, v) => s.SleepIdleMinutes = (int)v, v => $"{v:0} 分钟");
		AddCheck(right, $"夜里多打盹（{Settings.NightStartHour}:00-{Settings.NightEndHour}:00）", s => s.NightSleep, (s, v) => s.NightSleep = v);
		AddCheck(right, "站在窗口顶上", s => s.WindowAware, (s, v) => s.WindowAware = v);
		AddCheck(right, "叠罗汉", s => s.Stacking, (s, v) => s.Stacking = v);
		AddCheck(right, "全屏程序时隐藏", s => s.HideOnFullscreen, (s, v) => s.HideOnFullscreen = v);

		var quit = new Button { Text = "退出咖波桌宠", MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		quit.Pressed += () => GetNode<Main>("/root/Main").QuitApp();
		right.AddChild(quit);
		return page;
	}

	private CheckBox AddCheck(VBoxContainer box, string text, Func<SettingsData, bool> get, Action<SettingsData, bool> set)
	{
		var check = new CheckBox { Text = text, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
		check.Toggled += on =>
		{
			if (!refreshingSettings) AppSettings.Instance.Apply(s => set(s, on));
		};
		settingRefreshers.Add(() => check.SetPressedNoSignal(get(Settings)));
		box.AddChild(check);
		return check;
	}

	private void AddSlider(VBoxContainer box, string text, double min, double max, double step,
		Func<SettingsData, double> get, Action<SettingsData, double> set, Func<double, string> format)
	{
		var row = new HBoxContainer();
		var label = UiTheme.MakeLabel(text, 16);
		label.CustomMinimumSize = new Vector2(96, 0);
		row.AddChild(label);
		var slider = new HSlider
		{
			MinValue = min,
			MaxValue = max,
			Step = step,
			SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
			SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
			MouseDefaultCursorShape = Control.CursorShape.PointingHand
		};
		row.AddChild(slider);
		var value = UiTheme.MakeLabel("", 16, UiTheme.InkLight);
		value.CustomMinimumSize = new Vector2(56, 0);
		value.HorizontalAlignment = HorizontalAlignment.Right;
		row.AddChild(value);

		slider.ValueChanged += v =>
		{
			value.Text = format(v);
			if (refreshingSettings) return;
			// Apply once the slider settles instead of on every step.
			pendingApply = () => AppSettings.Instance.Apply(s => set(s, v));
			pendingTimer = 0.3;
		};
		settingRefreshers.Add(() =>
		{
			slider.Value = get(Settings);
			value.Text = format(slider.Value);
		});
		box.AddChild(row);
	}

	private void AddChoice(VBoxContainer box, string text, string[] options, Func<SettingsData, int> get, Action<SettingsData, int> set)
	{
		var row = new HBoxContainer();
		var label = UiTheme.MakeLabel(text, 16);
		label.CustomMinimumSize = new Vector2(96, 0);
		row.AddChild(label);
		var group = new ButtonGroup();
		var buttons = new List<Button>();
		for (int i = 0; i < options.Length; i++)
		{
			int index = i;
			var button = new Button
			{
				Text = options[i],
				ToggleMode = true,
				ButtonGroup = group,
				CustomMinimumSize = new Vector2(38, 0),
				MouseDefaultCursorShape = Control.CursorShape.PointingHand
			};
			// A picked option has to stand out from the others.
			button.AddThemeStyleboxOverride("pressed", UiTheme.SelectedBox());
			button.Toggled += on =>
			{
				if (on && !refreshingSettings) AppSettings.Instance.Apply(s => set(s, index));
			};
			buttons.Add(button);
			row.AddChild(button);
		}
		settingRefreshers.Add(() =>
		{
			int selected = get(Settings);
			for (int i = 0; i < buttons.Count; i++) buttons[i].SetPressedNoSignal(i == selected);
		});
		box.AddChild(row);
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
		if (currentTab == 2) RefreshSettings();
		else if (currentTab == 0) UpdateSummonStatus();
		else RefreshDesk();
	}

	// ---- Layout check (self-test) ----

	/// <summary>The boxes the book fills on one page, in book coordinates. The self-test
	/// uses them to check that nothing overlaps and that nothing leaves the paper.</summary>
	public List<(string name, Rect2 rect)> GetLayoutRects(int tab)
	{
		var rects = new List<(string, Rect2)>();
		for (int i = 0; i < tabs.Length; i++)
			rects.Add(($"tab{i}", new Rect2(tabs[i].Position, tabs[i].Size * tabs[i].Scale)));
		rects.Add(("close", new Rect2(742, 58, 58, 56)));

		if (tab == 0)
		{
			rects.Add(("status", new Rect2(summonStatus.Position, summonStatus.Size)));
			rects.Add(("page", new Rect2(summonPageLabel.Position, summonPageLabel.Size)));
			for (int i = 0; i < summonArrows.Length; i++)
				rects.Add(($"arrow{i}", new Rect2(summonArrows[i].Position, summonArrows[i].Size * summonArrows[i].Scale)));
			for (int i = 0; i < CardsPerSpread; i++)
			{
				Rect2 side = i < CardsPerSide ? LeftPage : RightPage;
				int local = i % CardsPerSide;
				var position = side.Position + new Vector2(8 + (local % CardColumns) * CardPitchX, CardTop + (local / CardColumns) * CardPitchY);
				rects.Add(($"card{i}", new Rect2(position, new Vector2(CardWidth, CardHeight))));
			}
		}
		else if (tab == 1)
		{
			rects.Add(("page", new Rect2(deskPageLabel.Position, deskPageLabel.Size)));
			rects.Add(("list", new Rect2(deskList.Position, deskList.Size)));
			for (int i = 0; i < deskArrows.Length; i++)
				rects.Add(($"arrow{i}", new Rect2(deskArrows[i].Position, deskArrows[i].Size * deskArrows[i].Scale)));
		}
		else
		{
			rects.Add(("left", new Rect2(settingsLeft.Position, settingsLeft.Size)));
			rects.Add(("right", new Rect2(settingsRight.Position, settingsRight.Size)));
		}
		return rects;
	}

	/// <summary>The paper the pages are printed on.</summary>
	public static Rect2 LeftPageArea => LeftPage;
	public static Rect2 RightPageArea => RightPage;

	/// <summary>How much the book is scaled on the current screen.</summary>
	public float UiScale => uiScale;

	/// <summary>The area the open book covers on the screen.</summary>
	public Rect2 BookRect => book.GetGlobalRect();

	/// <summary>Self-test: a page tab and the first card of the summon page.</summary>
	public TextureButton FirstTab => tabs[0];
	public Control FirstCard => summonCards.GetChildOrNull<Control>(0);

	// ---- Pet menu ----

	public void OnPetRightClicked(CapooController pet, Vector2 position)
	{
		menuOpenedFrame = Engine.GetProcessFrames();
		petMenu.Open(pet, position);
	}
}
