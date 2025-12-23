using Godot;
using System;

public partial class GameUI : CanvasLayer
{
    [Signal]
    public delegate void ChangeCapooRequestedEventHandler(int id);

    private TextureButton menuButton;
    private Control customMenu; // 自定义菜单容器
    
    // 拖动相关变量
    private bool isDragging = false;
    private Vector2 dragStartMousePos;
    private Vector2 dragStartButtonPos;
    private bool hasDragged = false;

    public override void _Ready()
    {
        // 1. 创建菜单按钮
        menuButton = new TextureButton();
        // 加载图片
        var texture = GD.Load<Texture2D>("res://assets/images/btn_menu.png");
        if (texture != null)
        {
            menuButton.TextureNormal = texture;
        }
        else
        {
            GD.PrintErr("Failed to load btn_menu.png");
        }
        
        // 设置位置 (左上角，留一点边距)
        menuButton.Position = new Vector2(20, 20);
        // 设置缩放 (0.75倍)
        menuButton.Scale = new Vector2(0.75f, 0.75f);
        // 设置鼠标光标形状为手型
        menuButton.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        
        // 连接输入事件以处理拖动
        menuButton.GuiInput += OnMenuButtonGuiInput;
        menuButton.Pressed += OnMenuButtonPressed;
        AddChild(menuButton);

        // 2. 创建自定义菜单
        CreateCustomMenu();
    }

    private void CreateCustomMenu()
    {
        customMenu = new Control();
        customMenu.Visible = false;
        AddChild(customMenu);

        // 背景图片
        var bgTexture = GD.Load<Texture2D>("res://assets/images/ui_pop_collection_01.png");
        var bgRect = new TextureRect();
        if (bgTexture != null)
        {
            bgRect.Texture = bgTexture;
            // 强制设置背景图大小为 782x542 (如果图片本身不是这个尺寸，可能需要缩放)
            // 但通常 TextureRect 会自动适应 Texture 大小。
            // 如果需要缩放，可以使用 bgRect.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize; bgRect.Size = ...
            customMenu.Size = bgTexture.GetSize(); 
        }
        else
        {
            GD.PrintErr("Failed to load ui_pop_collection_01.png");
            customMenu.Size = new Vector2(782, 542); // 使用指定的默认大小
        }
        customMenu.AddChild(bgRect);

        // 退出按钮 (btn_shutdown_vt.png)
        var shutdownBtn = new TextureButton();
        var shutdownTexture = GD.Load<Texture2D>("res://assets/images/btn_shutdown_vt.png");
        if (shutdownTexture != null)
        {
            shutdownBtn.TextureNormal = shutdownTexture;
        }
        // 放置在右上角 (根据 782x542 调整)
        shutdownBtn.Position = new Vector2(customMenu.Size.X - 80, 30);
        shutdownBtn.Scale = new Vector2(1.0f, 1.0f); 
        shutdownBtn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
        shutdownBtn.Pressed += () => GetTree().Quit();
        customMenu.AddChild(shutdownBtn);

        // 滚动容器 (用于放置角色选择按钮)
        var scrollContainer = new ScrollContainer();
        // 根据 782x542 的背景图调整位置和大小
        // 假设顶部标题栏约 80px，左右边距 50px，底部边距 50px
        scrollContainer.Position = new Vector2(50, 100); 
        scrollContainer.Size = new Vector2(customMenu.Size.X - 100, customMenu.Size.Y - 150);
        customMenu.AddChild(scrollContainer);

        // 网格布局
        var gridContainer = new GridContainer();
        gridContainer.Columns = 3; // 宽度变大了，改为 3 列
        // 开启自动扩展，让列宽均匀分布
        gridContainer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        gridContainer.AddThemeConstantOverride("h_separation", 20);
        gridContainer.AddThemeConstantOverride("v_separation", 20);
        scrollContainer.AddChild(gridContainer);

        // 填充角色按钮
        var config = GetNodeOrNull<CapooConfig>("/root/CapooConfig");
        if (config != null && config.infos != null)
        {
            for (int i = 0; i < config.infos.Count; i++)
            {
                var btn = new Button();
                btn.Text = config.infos[i].name;
                // 设置按钮最小尺寸，并允许水平扩展
                btn.CustomMinimumSize = new Vector2(0, 60); // 高度增加到 60
                btn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
                btn.MouseDefaultCursorShape = Control.CursorShape.PointingHand;
                
                int index = i; // 闭包捕获
                btn.Pressed += () => 
                {
                    EmitSignal(SignalName.ChangeCapooRequested, index);
                    // customMenu.Visible = false; // 选择后是否关闭菜单？暂时保持打开方便切换
                };
                gridContainer.AddChild(btn);
            }
        }
    }

    private void OnMenuButtonGuiInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed)
                {
                    isDragging = true;
                    hasDragged = false;
                    dragStartMousePos = GetViewport().GetMousePosition();
                    dragStartButtonPos = menuButton.Position;
                }
                else
                {
                    isDragging = false;
                }
            }
        }
        else if (@event is InputEventMouseMotion mouseMotion)
        {
            if (isDragging)
            {
                Vector2 currentMousePos = GetViewport().GetMousePosition();
                if (currentMousePos.DistanceTo(dragStartMousePos) > 5) // 移动超过5像素才算拖动
                {
                    hasDragged = true;
                }
                
                // 计算新位置
                Vector2 newPos = dragStartButtonPos + (currentMousePos - dragStartMousePos);
                
                // 限制在屏幕范围内
                Rect2 viewportRect = GetViewport().GetVisibleRect();
                float maxX = viewportRect.Size.X - (menuButton.Size.X * menuButton.Scale.X);
                float maxY = viewportRect.Size.Y - (menuButton.Size.Y * menuButton.Scale.Y);
                
                newPos.X = Mathf.Clamp(newPos.X, 0, maxX);
                newPos.Y = Mathf.Clamp(newPos.Y, 0, maxY);
                
                menuButton.Position = newPos;
                
                /*
                if (customMenu != null && customMenu.Visible)
                {
                    Vector2 menuPos = menuButton.Position;
                    menuPos.Y += menuButton.Size.Y * menuButton.Scale.Y + 5;
                    customMenu.Position = menuPos;
                }
                */
            }
        }
    }

    private void OnMenuButtonPressed()
    {
        // 如果刚刚进行了拖动操作，则不弹出菜单
        if (hasDragged) return;

        // 切换菜单显示状态
        if (customMenu != null)
        {
            customMenu.Visible = !customMenu.Visible;
            if (customMenu.Visible)
            {
                // 将菜单显示在屏幕正中间
                Rect2 viewportRect = GetViewport().GetVisibleRect();
                customMenu.Position = (viewportRect.Size - customMenu.Size) / 2;
            }
        }
    }

}
