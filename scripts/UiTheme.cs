using Godot;
using System.Collections.Generic;

/// <summary>
/// Shared look for the book and menus: a system CJK font, warm brown text and the original
/// CapooPals checkbox / slider / button art.
/// </summary>
public static class UiTheme
{
	public static readonly Color Ink = new Color("6b4f2c");
	public static readonly Color InkLight = new Color("9c8160");
	public static readonly Color Paper = new Color("f7f3e8");
	public static readonly Color PaperEdge = new Color("c9b48f");
	public static readonly Color Accent = new Color("f6c445");

	private static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
	private static Theme theme;

	public static Texture2D Tex(string name)
	{
		string path = $"res://assets/images/{name}.png";
		if (!cache.TryGetValue(path, out var texture))
		{
			texture = GD.Load<Texture2D>(path);
			cache[path] = texture;
		}
		return texture;
	}

	/// <summary>A resized copy of an image (theme icons are drawn at their native size).</summary>
	public static Texture2D Scaled(string name, int width, int height)
	{
		string key = $"{name}@{width}x{height}";
		if (cache.TryGetValue(key, out var texture)) return texture;
		var image = Tex(name).GetImage();
		image.Resize(width, height, Image.Interpolation.Lanczos);
		texture = ImageTexture.CreateFromImage(image);
		cache[key] = texture;
		return texture;
	}

	public static Theme Get()
	{
		if (theme != null) return theme;
		theme = new Theme();

		var font = new SystemFont
		{
			FontNames = new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Noto Sans CJK SC", "SimHei" },
			FontWeight = 600,
			Antialiasing = TextServer.FontAntialiasing.Gray
		};
		theme.DefaultFont = font;
		theme.DefaultFontSize = 18;

		foreach (string type in new[] { "Label", "Button", "CheckBox" })
		{
			theme.SetColor("font_color", type, Ink);
			theme.SetColor("font_hover_color", type, Ink);
			theme.SetColor("font_pressed_color", type, Ink);
			theme.SetColor("font_hover_pressed_color", type, Ink);
			theme.SetColor("font_focus_color", type, Ink);
			theme.SetColor("font_disabled_color", type, InkLight);
		}

		// Buttons: rounded yellow pills.
		theme.SetStylebox("normal", "Button", Pill(new Color("fbe7a1")));
		theme.SetStylebox("hover", "Button", Pill(new Color("fff0b8")));
		theme.SetStylebox("pressed", "Button", Pill(Accent));
		theme.SetStylebox("hover_pressed", "Button", Pill(Accent));
		theme.SetStylebox("disabled", "Button", Pill(new Color("eee6d4")));
		theme.SetStylebox("focus", "Button", new StyleBoxEmpty());

		// Check boxes: the original tick, and a plain paper box when off.
		var tick = Scaled("btn_checkbox_02", 30, 30);
		var empty = RoundedBox(30, 7, new Color("fffaf0"), PaperEdge, 3);
		theme.SetIcon("checked", "CheckBox", tick);
		theme.SetIcon("unchecked", "CheckBox", empty);
		theme.SetIcon("checked_disabled", "CheckBox", tick);
		theme.SetIcon("unchecked_disabled", "CheckBox", empty);
		foreach (string state in new[] { "normal", "hover", "pressed", "hover_pressed", "focus", "disabled" })
			theme.SetStylebox(state, "CheckBox", new StyleBoxEmpty());
		theme.SetConstant("h_separation", "CheckBox", 8);

		// Sliders: grey track, yellow fill, yellow knob.
		theme.SetStylebox("slider", "HSlider", Bar(new Color("d9d2c3")));
		theme.SetStylebox("grabber_area", "HSlider", Bar(Accent));
		theme.SetStylebox("grabber_area_highlight", "HSlider", Bar(new Color("ffd760")));
		theme.SetIcon("grabber", "HSlider", Scaled("btn_slide_ye", 24, 28));
		theme.SetIcon("grabber_highlight", "HSlider", Scaled("btn_slide_ye", 26, 30));

		// Popup panels.
		theme.SetStylebox("panel", "PanelContainer", PanelBox());

		// Tooltips: the default dark box with brown text on it is unreadable.
		theme.SetStylebox("panel", "TooltipPanel", TooltipBox());
		theme.SetColor("font_color", "TooltipLabel", Ink);
		theme.SetFontSize("font_size", "TooltipLabel", 16);

		theme.SetConstant("separation", "VBoxContainer", 8);
		theme.SetConstant("separation", "HBoxContainer", 8);
		return theme;
	}

	/// <summary>The style of a chosen option: a deeper gold with a strong border, so it is
	/// clearly different from the buttons next to it.</summary>
	public static StyleBoxFlat SelectedBox()
	{
		var box = new StyleBoxFlat
		{
			BgColor = new Color("e0a52c"),
			BorderColor = new Color("8a6216"),
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 4,
			ContentMarginBottom = 4
		};
		box.SetBorderWidthAll(2);
		box.SetCornerRadiusAll(16);
		return box;
	}

	/// <summary>A light paper box for the tooltips.</summary>
	private static StyleBoxFlat TooltipBox()
	{
		var box = new StyleBoxFlat
		{
			BgColor = Paper,
			BorderColor = PaperEdge,
			ContentMarginLeft = 10,
			ContentMarginRight = 10,
			ContentMarginTop = 4,
			ContentMarginBottom = 4
		};
		box.SetBorderWidthAll(2);
		box.SetCornerRadiusAll(8);
		return box;
	}

	/// <summary>A small rounded square icon drawn in code.</summary>
	private static Texture2D RoundedBox(int size, float radius, Color fill, Color border, float borderWidth)
	{
		var image = Image.Create(size, size, false, Image.Format.Rgba8);
		float half = size / 2f;
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				// Signed distance to a rounded square, with 1px of anti-aliasing.
				float qx = Mathf.Abs(x + 0.5f - half) - (half - radius - 1);
				float qy = Mathf.Abs(y + 0.5f - half) - (half - radius - 1);
				float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).Length() + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
				float alpha = Mathf.Clamp(0.5f - outside, 0, 1);
				if (alpha <= 0) continue;
				Color c = outside > -borderWidth ? border : fill;
				c.A *= alpha;
				image.SetPixel(x, y, c);
			}
		}
		return ImageTexture.CreateFromImage(image);
	}

	private static StyleBoxFlat Pill(Color color)
	{
		var box = new StyleBoxFlat
		{
			BgColor = color,
			BorderColor = new Color("c9a24a"),
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 4,
			ContentMarginBottom = 4
		};
		box.SetBorderWidthAll(2);
		box.SetCornerRadiusAll(16);
		return box;
	}

	private static StyleBoxFlat Bar(Color color)
	{
		var box = new StyleBoxFlat { BgColor = color, ContentMarginTop = 5, ContentMarginBottom = 5 };
		box.SetCornerRadiusAll(6);
		return box;
	}

	public static StyleBoxFlat PanelBox()
	{
		var box = new StyleBoxFlat
		{
			BgColor = Paper,
			BorderColor = PaperEdge,
			ShadowColor = new Color(0, 0, 0, 0.25f),
			ShadowSize = 6,
			ShadowOffset = new Vector2(0, 3),
			ContentMarginLeft = 14,
			ContentMarginRight = 14,
			ContentMarginTop = 10,
			ContentMarginBottom = 12
		};
		box.SetBorderWidthAll(3);
		box.SetCornerRadiusAll(16);
		return box;
	}

	public static Label MakeLabel(string text, int size = 18, Color? color = null)
	{
		var label = new Label { Text = text, MouseFilter = Control.MouseFilterEnum.Ignore };
		if (size != 18) label.AddThemeFontSizeOverride("font_size", size);
		if (color.HasValue) label.AddThemeColorOverride("font_color", color.Value);
		return label;
	}
}
