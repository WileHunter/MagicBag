using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace WpfApp3.Services
{
    /// <summary>
    /// 浅色主题模板：只需给出底色、文字与一个强调色，其余画刷（徽标、选中态、阴影、终端、纸张等）统一推导，
    /// 保证各浅色主题的层次和观感一致。资源键与 Palette.Tech.xaml / Palette.Mono.xaml 完全对应。
    /// </summary>
    public sealed record LightPalette(
        string Background,      // 窗口底色
        string Region,          // 面板 / 卡片
        string Region2,         // 次级区域（输入框、悬停）
        string Region3,         // 三级区域（图块底、分隔块）
        string Border,
        string Border2,
        string Text1,
        string Text2,
        string Text3,
        string Accent,          // 强调色（主按钮渐变起点）
        string AccentDark,      // 强调色深一级（主按钮渐变终点、徽标文字）
        string Terminal,        // 终端背景
        double GridOpacity = 0.025)
    {
        public ResourceDictionary Build()
        {
            var d = new ResourceDictionary();
            Color bg = C(Background), region = C(Region), region2 = C(Region2), region3 = C(Region3);
            Color border = C(Border), border2 = C(Border2);
            Color text1 = C(Text1), text2 = C(Text2), text3 = C(Text3);
            Color accent = C(Accent), accentDark = C(AccentDark);
            var white = Colors.White;

            // —— HandyControl 颜色 ——
            var colors = new (string Key, Color Value)[]
            {
                ("LightPrimaryColor", Mix(accent, white, 0.90)),
                ("PrimaryColor", accent),
                ("DarkPrimaryColor", accentDark),
                ("LightDangerColor", C("#FEF2F2")), ("DangerColor", C("#D9483B")), ("DarkDangerColor", C("#B83A2F")),
                ("LightWarningColor", C("#FFF8EB")), ("WarningColor", C("#C77D12")), ("DarkWarningColor", C("#A5660C")),
                ("LightInfoColor", Mix(accent, white, 0.90)), ("InfoColor", accent), ("DarkInfoColor", accentDark),
                ("LightSuccessColor", C("#EFF8F1")), ("SuccessColor", C("#2E9457")), ("DarkSuccessColor", C("#237544")),
                ("PrimaryTextColor", text1), ("SecondaryTextColor", text2), ("ThirdlyTextColor", text3),
                ("ReverseTextColor", white), ("TextIconColor", white),
                ("BorderColor", border), ("SecondaryBorderColor", border2),
                ("BackgroundColor", bg), ("RegionColor", region), ("SecondaryRegionColor", region2), ("ThirdlyRegionColor", region3),
                ("TitleColor", text1), ("SecondaryTitleColor", text2),
                ("DefaultColor", region), ("DarkDefaultColor", region2),
                ("AccentColor", accent), ("DarkAccentColor", accentDark),
                ("DarkMaskColor", A(text1, 0x73)), ("DarkOpacityColor", A(text1, 0x33)),
                ("NeonColor", accent), ("VioletColor", accentDark),
            };
            foreach (var (key, value) in colors)
            {
                d[key] = value;
                if (key.EndsWith("Color") && key != "NeonColor" && key != "VioletColor")
                    d[key[..^5] + "Brush"] = B(value);
            }
            // 这两个是渐变画刷，覆盖上面按颜色生成的纯色版本
            d["PrimaryBrush"] = Linear(new Point(0, 0), new Point(0, 1), (accent, 0), (accentDark, 1));
            d["TitleBrush"] = Linear(new Point(0, 0), new Point(1, 0), (text1, 0), (text2, 1));

            // —— 应用画刷 ——
            d["NeonBrush"] = B(accent);
            d["VioletBrush"] = B(accentDark);
            d["OnPrimaryBrush"] = B(white);
            d["SelectedItemBrush"] = B(A(accent, 0x14));
            d["HoverItemBrush"] = B(A(text1, 0x08));
            d["PanelBrush"] = B(region);
            d["InputBrush"] = B(region2);
            d["GhostBrush"] = B(region);
            d["GhostHoverBrush"] = B(region2);
            d["AccentSoftBrush"] = B(A(accent, 0x14));
            d["AccentSoftBorderBrush"] = B(A(accent, 0x40));
            d["NavCheckedBrush"] = B(A(accent, 0x14));
            d["NavCheckedBorderBrush"] = B(A(accent, 0x2E));

            d["BadgeBrush"] = B(A(accent, 0x14));
            d["BadgeBorderBrush"] = B(A(accent, 0x33));
            d["BadgeTextBrush"] = B(accentDark);
            d["Badge2Brush"] = B(A(region, 0x00));
            d["Badge2BorderBrush"] = B(border);
            d["Badge2TextBrush"] = B(text2);

            d["CardBrush"] = B(region);
            d["CardHoverBrush"] = B(region);
            d["CardHoverBorderBrush"] = B(A(accent, 0x66));
            d["CardHoverEffect"] = Shadow(accentDark, 20, 4, 0.12);

            d["HeaderLineBrush"] = Linear(new Point(0, 0), new Point(1, 0),
                (A(text1, 0), 0), (A(text1, 0x1A), 0.3), (A(text1, 0x1A), 0.7), (A(text1, 0), 1));
            d["LogoTileBrush"] = Linear(new Point(0, 0), new Point(0, 1), (Mix(accent, white, 0.12), 0), (accentDark, 1));
            d["LogoTextBrush"] = Linear(new Point(0, 0), new Point(1, 0), (text1, 0), (Mix(text1, accent, 0.5), 0.6), (accent, 1));
            d["LogoRingBrush"] = Linear(new Point(0, 0), new Point(1, 1), (white, 0), (accent, 1));
            d["LogoDiscBrush"] = B(A(accentDark, 0xE6));

            // 图块与小图标：浅色主题统一用柔和的低饱和色
            d["FolderTileBrush"] = B(C("#24D9962B")); d["FolderTileGlyphBrush"] = B(C("#B7791F"));
            d["ToolTileBrush"] = B(C("#1F4A74C9")); d["ToolTileGlyphBrush"] = B(C("#3F66B8"));
            d["CmdTileBrush"] = B(C("#1F2F8F5B")); d["CmdTileGlyphBrush"] = B(C("#2B8152"));

            d["ChipAllBrush"] = B(A(accent, 0x1A)); d["ChipAllFgBrush"] = B(accentDark);
            d["ChipFolderBrush"] = B(C("#24D9962B")); d["ChipFolderFgBrush"] = B(C("#B7791F"));
            d["ChipCmdBrush"] = B(C("#1F2F8F5B")); d["ChipCmdFgBrush"] = B(C("#2B8152"));
            d["ChipExeBrush"] = B(C("#1F4A74C9")); d["ChipExeFgBrush"] = B(C("#3F66B8"));

            var notes = new[] { ("#4A74C9", "#3F66B8"), ("#7C5CC4", "#6A4DB0"), ("#2F8F5B", "#2B8152"),
                                ("#D9962B", "#B7791F"), ("#C25478", "#B04468"), ("#2C8C87", "#247A75") };
            for (var i = 0; i < notes.Length; i++)
            {
                d[$"NoteChip{i}Brush"] = B(A(C(notes[i].Item1), 0x1F));
                d[$"NoteChip{i}FgBrush"] = B(C(notes[i].Item2));
            }

            // —— 终端 ——
            d["TermBackgroundBrush"] = B(C(Terminal));
            d["TermForegroundBrush"] = B(text1);
            d["TermPromptBrush"] = B(accentDark);
            d["TermSelectionBrush"] = B(accent);
            d["TermRunningBrush"] = B(C("#2E9457"));
            d["TermIdleBrush"] = B(text3);

            // —— 笔记纸张 ——
            d["PaperBrush"] = B(region);
            d["PaperBorderBrush"] = B(border);
            d["PaperCaretBrush"] = B(text1);
            d["PaperSelectionBrush"] = B(accent);
            d["PaperIconBrush"] = B(text3);
            d["PaperMutedBrush"] = B(text2);
            d["PaperGlow"] = Shadow(text1, 18, 3, 0.06);

            // —— 窗口背景 ——
            var grid = new DrawingBrush(new GeometryDrawing(null, new Pen(B(text1), 0.6), Geometry.Parse("M0,0 L28,0 M0,0 L0,28")))
            {
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 28, 28),
                ViewportUnits = BrushMappingMode.Absolute,
                Opacity = GridOpacity
            };
            grid.Freeze();
            d["GridPatternBrush"] = grid;
            d["GlowBrush"] = Radial(new Point(0.5, 0), 0.75, 0.7, A(white, 0xFF), A(white, 0));
            d["GlowBrush2"] = Radial(new Point(1, 1), 0.5, 0.6, A(accent, 0x0F), A(accent, 0));

            d["NeonGlow"] = Shadow(accentDark, 14, 2, 0.12);
            d["SoftShadow"] = Shadow(text1, 22, 6, 0.12);
            d["AccentGlow"] = Shadow(accentDark, 10, 3, 0.25);
            d["AccentGlowHover"] = Shadow(accentDark, 16, 4, 0.35);
            return d;
        }

        private static Color C(string hex) => (Color)ColorConverter.ConvertFromString(hex);

        private static Color A(Color c, byte alpha) => Color.FromArgb(alpha, c.R, c.G, c.B);

        private static Color Mix(Color a, Color b, double t) => Color.FromRgb(
            (byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));

        private static SolidColorBrush B(Color c)
        {
            var brush = new SolidColorBrush(c);
            brush.Freeze();
            return brush;
        }

        private static LinearGradientBrush Linear(Point start, Point end, params (Color Color, double Offset)[] stops)
        {
            var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end };
            foreach (var (color, offset) in stops) brush.GradientStops.Add(new GradientStop(color, offset));
            brush.Freeze();
            return brush;
        }

        private static RadialGradientBrush Radial(Point center, double rx, double ry, Color from, Color to)
        {
            var brush = new RadialGradientBrush(from, to) { Center = center, GradientOrigin = center, RadiusX = rx, RadiusY = ry };
            brush.Freeze();
            return brush;
        }

        private static DropShadowEffect Shadow(Color color, double blur, double depth, double opacity)
        {
            var effect = new DropShadowEffect { Color = color, BlurRadius = blur, ShadowDepth = depth, Direction = 270, Opacity = opacity };
            effect.Freeze();
            return effect;
        }
    }
}
