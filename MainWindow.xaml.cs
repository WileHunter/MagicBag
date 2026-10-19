using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WpfApp3
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 数据库页保存后，刷新工具集页
            DatabaseViewControl.ItemUpdated += () => ToolsViewControl.ReloadData();

            // 命令行工具在内置终端中运行
            ToolsViewControl.RunInTerminal += (title, directory, command) =>
            {
                TerminalViewControl.OpenSession(title, directory, command);
                NavTerm.IsChecked = true;
            };
        }

        private void Nav_Checked(object sender, RoutedEventArgs e)
        {
            if (PageHost == null) return;

            FrameworkElement target = sender == NavTerm ? TerminalViewControl
                : sender == NavNotes ? NotebookViewControl
                : sender == NavData ? DatabaseViewControl
                : sender == NavAbout ? AboutViewControl
                : ToolsViewControl;

            foreach (UIElement child in PageHost.Children)
                child.Visibility = child == target ? Visibility.Visible : Visibility.Collapsed;

            AnimateIn(target);
        }

        private static void AnimateIn(FrameworkElement element)
        {
            var translate = new TranslateTransform(0, 8);
            element.RenderTransform = translate;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            var duration = TimeSpan.FromMilliseconds(220);
            element.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
            translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, duration) { EasingFunction = ease });
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            NotebookViewControl.SaveIfDirty();
            if (!DatabaseViewControl.ConfirmLeave())
            {
                e.Cancel = true;
                return;
            }

            var running = TerminalViewControl.RunningCount;
            if (running > 0)
            {
                var result = HandyControl.Controls.MessageBox.Show($"终端中还有 {running} 个命令在运行，退出将结束它们。\n确定退出？",
                    "退出", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (result != MessageBoxResult.OK)
                {
                    e.Cancel = true;
                    return;
                }
            }
            TerminalViewControl.StopAll();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // 无边框窗口最大化时会超出屏幕边缘，这里补偿一圈边距
            if (WindowState == WindowState.Maximized)
            {
                var t = SystemParameters.WindowResizeBorderThickness;
                RootGrid.Margin = new Thickness(t.Left + 4, t.Top + 4, t.Right + 4, t.Bottom + 4);
                MaxButton.Content = "\uE923";
                MaxButton.ToolTip = "还原";
            }
            else
            {
                RootGrid.Margin = new Thickness(0);
                MaxButton.Content = "\uE922";
                MaxButton.ToolTip = "最大化";
            }
        }

        private void ThemeButton_Click(object sender, RoutedEventArgs e)
        {
            var menu = new ContextMenu
            {
                PlacementTarget = ThemeButton,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom
            };
            foreach (var theme in Services.ThemeManager.Themes)
            {
                // 深色与浅色主题之间加分隔线
                if (theme.Light != null && menu.Items.Count > 0 && ((MenuItem)menu.Items[^1]).Tag is Services.ThemeOption { Light: null })
                    menu.Items.Add(new Separator());

                var header = new StackPanel { Orientation = Orientation.Horizontal };
                header.Children.Add(MakeSwatch(theme.Swatch[0], theme.Swatch[1]));
                header.Children.Add(new TextBlock { Text = theme.Name, Width = 52, VerticalAlignment = VerticalAlignment.Center });
                var note = new TextBlock { Text = theme.Description, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                note.SetResourceReference(TextBlock.ForegroundProperty, "SecondaryTextBrush");
                header.Children.Add(note);

                var item = new MenuItem
                {
                    Header = header,
                    IsCheckable = true,
                    IsChecked = theme.Key == Services.ThemeManager.Current,
                    Tag = theme
                };
                var key = theme.Key;
                item.Click += (_, _) => Services.ThemeManager.Apply(key);
                menu.Items.Add(item);
            }
            menu.IsOpen = true;
        }

        /// <summary>主题色块：左半为底色，右下角圆点为强调色。</summary>
        private static FrameworkElement MakeSwatch(string background, string accent)
        {
            Brush ToBrush(string hex) => (Brush)new BrushConverter().ConvertFromString(hex)!;
            var grid = new Grid { Width = 22, Height = 16, Margin = new Thickness(0, 0, 10, 0), VerticalAlignment = VerticalAlignment.Center };
            grid.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(4),
                Background = ToBrush(background),
                BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
                BorderThickness = new Thickness(1)
            });
            grid.Children.Add(new System.Windows.Shapes.Ellipse
            {
                Width = 8, Height = 8, Fill = ToBrush(accent),
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, 3, 3)
            });
            return grid;
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

        private void Maximize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
