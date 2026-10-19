using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using WpfApp3.Models;
using WpfApp3.Services;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfApp3
{
    public partial class ToolsView : UserControl
    {
        private const string AllToolsKey = "__ALL__";

        private Root _root = new();
        private Category? _currentCategory;          // null 表示“全部工具”
        private Tool? _selectedTool;                 // 非 null 表示正在查看详情
        private bool _suppressTreeEvents;
        private readonly Dictionary<object, TreeViewItem> _treeItems = new();

        private const double MinCardWidth = 170, CardGap = 10, ScrollBarReserve = 12;
        private List<object> _cardItems = new();
        private int _cardColumns = 3;

        private const string TerminalSettingKey = "CliTerminal";
        private const string BuiltinTerminal = "Builtin";

        /// <summary>请求在内置终端中运行：标题、工作目录、命令。</summary>
        public event Action<string, string, string?>? RunInTerminal;

        public ToolsView()
        {
            InitializeComponent();
            var mode = AppSettings.Get(TerminalSettingKey, BuiltinTerminal);
            TerminalModeComboBox.SelectedIndex = mode == "System" ? 1 : 0;
            ReloadData();
        }

        private bool UseBuiltinTerminal =>
            (TerminalModeComboBox.SelectedItem as ComboBoxItem)?.Tag as string != "System";

        private void TerminalModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded) return;
            AppSettings.Set(TerminalSettingKey, UseBuiltinTerminal ? BuiltinTerminal : "System");
        }

        /// <summary>
        /// 重新读取 tools.json 并刷新界面，尽量保留当前所在的分类与展开状态。
        /// </summary>
        public void ReloadData()
        {
            var currentPath = _currentCategory != null ? ToolRepository.GetPath(_currentCategory) : null;
            var expanded = new HashSet<string>(_treeItems
                .Where(p => p.Key is Category && p.Value.IsExpanded)
                .Select(p => ToolRepository.GetPath((Category)p.Key)));

            ToolIconProvider.Clear();
            try
            {
                _root = ToolRepository.Load();
            }
            catch (Exception ex)
            {
                _root = new Root();
                MessageBox.Show($"读取 tools.json 失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            BuildTree(expanded);

            _currentCategory = currentPath == null
                ? null
                : ToolRepository.AllCategories(_root.Categories).FirstOrDefault(c => ToolRepository.GetPath(c) == currentPath);
            _selectedTool = null;
            SyncTreeSelection(_currentCategory);
            ShowCurrent();
        }

        #region 树

        private void BuildTree(HashSet<string> expandedPaths)
        {
            _treeItems.Clear();
            ToolTreeView.Items.Clear();

            var allItem = new TreeViewItem { Header = MakeHeader("\uE8A9", "全部工具", "ChipAll"), Tag = AllToolsKey };
            _treeItems[AllToolsKey] = allItem;
            ToolTreeView.Items.Add(allItem);

            foreach (var category in _root.Categories)
                ToolTreeView.Items.Add(CreateCategoryItem(category, expandedPaths));

            TotalText.Text = $"{ToolRepository.AllTools(_root.Categories).Count()} 个工具";
        }

        private TreeViewItem CreateCategoryItem(Category category, HashSet<string> expandedPaths)
        {
            var item = new TreeViewItem
            {
                Header = MakeHeader("\uF12B", category.Name ?? "", "ChipFolder"),
                Tag = category,
                IsExpanded = expandedPaths.Contains(ToolRepository.GetPath(category))
            };
            _treeItems[category] = item;

            foreach (var sub in category.SubCategories ?? new List<Category>())
                item.Items.Add(CreateCategoryItem(sub, expandedPaths));

            foreach (var tool in category.Tools ?? new List<Tool>())
            {
                var toolItem = new TreeViewItem
                {
                    Header = MakeToolHeader(tool),
                    Tag = tool,
                    ToolTip = $"{tool.ModeText} · {tool.Description}".TrimEnd(' ', '·')
                };
                // 图标在后台提取，就绪后再替换表头
                tool.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(Tool.Icon))
                        toolItem.Header = MakeToolHeader(tool);
                };
                _treeItems[tool] = toolItem;
                item.Items.Add(toolItem);
            }
            return item;
        }

        private static StackPanel MakeToolHeader(Tool tool) =>
            tool.Icon is { } icon ? MakeImageHeader(icon, tool.Name)
            : tool.Cmd == 1 ? MakeHeader("\uECAA", tool.Name, "ChipExe")
            : MakeHeader("\uE756", tool.Name, "ChipCmd");

        private static StackPanel MakeImageHeader(ImageSource image, string text)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            var picture = new Image { Source = image, Width = 16, Height = 16 };
            RenderOptions.SetBitmapScalingMode(picture, BitmapScalingMode.HighQuality);
            var border = new Border { Child = picture };
            border.SetResourceReference(StyleProperty, "Chip");
            panel.Children.Add(border);
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            return panel;
        }

        /// <param name="chip">配色资源前缀，如 ChipFolder → ChipFolderBrush / ChipFolderFgBrush，随主题切换</param>
        private static StackPanel MakeHeader(string glyph, string text, string chip)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal };

            var icon = new TextBlock { Text = glyph };
            icon.SetResourceReference(StyleProperty, "ChipGlyph");
            icon.SetResourceReference(TextBlock.ForegroundProperty, chip + "FgBrush");

            var border = new Border { Child = icon };
            border.SetResourceReference(StyleProperty, "Chip");
            border.SetResourceReference(Border.BackgroundProperty, chip + "Brush");

            panel.Children.Add(border);
            panel.Children.Add(new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
            return panel;
        }

        private void SyncTreeSelection(object? target)
        {
            var key = target ?? AllToolsKey;
            if (!_treeItems.TryGetValue(key, out var item)) return;

            _suppressTreeEvents = true;
            try
            {
                for (var parent = item.Parent as TreeViewItem; parent != null; parent = parent.Parent as TreeViewItem)
                    parent.IsExpanded = true;
                item.IsSelected = true;
                item.BringIntoView();
            }
            finally
            {
                _suppressTreeEvents = false;
            }
        }

        private void ToolTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_suppressTreeEvents) return;
            if (e.NewValue is TreeViewItem item)
                NavigateTo(item.Tag);
        }

        // 再次点击已选中的节点时 SelectedItemChanged 不会触发，这里补上
        private void ToolTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source) return;
            if (FindAncestor<ToggleButton>(source) != null) return;
            var item = FindAncestor<TreeViewItem>(source);
            if (item != null && item.IsSelected)
                NavigateTo(item.Tag);
        }

        private void NavigateTo(object? tag)
        {
            switch (tag)
            {
                case Category category:
                    ClearSearchSilently();
                    _currentCategory = category;
                    _selectedTool = null;
                    ShowCurrent();
                    break;
                case Tool tool:
                    ShowDetails(tool);
                    break;
                default:
                    ClearSearchSilently();
                    _currentCategory = null;
                    _selectedTool = null;
                    ShowCurrent();
                    break;
            }
        }

        #endregion

        #region 视图切换

        private void ShowCurrent()
        {
            var search = SearchBox.Text.Trim();
            if (search.Length > 0)
            {
                var results = ToolRepository.AllTools(_root.Categories)
                    .Where(t => Contains(t.Name, search) || Contains(t.Description, search) ||
                                Contains(t.ToolPath, search) || Contains(t.RUN, search))
                    .Cast<object>().ToList();
                ShowItems(results, "搜索结果", $"“{search}” · 找到 {results.Count} 个工具", false);
                return;
            }

            if (_currentCategory == null)
            {
                var all = ToolRepository.AllTools(_root.Categories).Cast<object>().ToList();
                ShowItems(all, "全部工具", $"共 {all.Count} 个工具 · {_root.Categories.Count} 个分类", false);
            }
            else
            {
                var items = new List<object>();
                items.AddRange(_currentCategory.SubCategories ?? new List<Category>());
                items.AddRange(_currentCategory.Tools ?? new List<Tool>());
                ShowItems(items, _currentCategory.Name ?? "", ToolRepository.GetPath(_currentCategory, " › "), true);
            }
        }

        private void ShowItems(List<object> items, string title, string subtitle, bool canGoBack)
        {
            _selectedTool = null;
            DetailsPanel.Visibility = Visibility.Collapsed;
            CardsHost.Visibility = Visibility.Visible;
            _cardItems = items;
            BindCardRows();
            (CardsHost.Template.FindName("PART_Scroll", CardsHost) as ScrollViewer)?.ScrollToTop();

            EmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyText.Text = SearchBox.Text.Trim().Length > 0 ? "没有匹配的工具" : "这里还没有工具";

            PageTitle.Text = title;
            PageSubtitle.Text = subtitle;
            BackButton.Visibility = canGoBack ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BindCardRows()
        {
            CardsHost.ItemsSource = _cardItems.Chunk(_cardColumns).ToList();
        }

        // 按可用宽度计算每行卡片数，并把卡片拉伸到刚好铺满一行
        private void CardsHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            var available = CardsHost.ActualWidth - ScrollBarReserve + CardGap;
            if (available <= 0) return;

            var columns = Math.Max(1, (int)(available / (MinCardWidth + CardGap)));
            Resources["CardWidth"] = Math.Floor(available / columns - CardGap);
            if (columns != _cardColumns)
            {
                _cardColumns = columns;
                BindCardRows();
            }
        }

        private void ShowDetails(Tool tool)
        {
            _selectedTool = tool;
            DetailsPanel.DataContext = tool;
            DetailsPanel.Visibility = Visibility.Visible;
            CardsHost.Visibility = Visibility.Collapsed;
            EmptyState.Visibility = Visibility.Collapsed;
            DetailsPanel.ScrollToTop();

            var path = ToolRepository.GetPath(tool.ParentCategory, " › ");
            DetailPathText.Text = path;
            PageTitle.Text = "工具详情";
            PageSubtitle.Text = path;
            BackButton.Visibility = Visibility.Visible;

            bool isCmd = tool.Cmd != 1;
            CommandLabel.Visibility = CommandComboBox.Visibility = isCmd ? Visibility.Visible : Visibility.Collapsed;
            TerminalLabel.Visibility = TerminalModeComboBox.Visibility = CommandLabel.Visibility;
            RunLabel.Visibility = RunValue.Visibility = isCmd ? Visibility.Collapsed : Visibility.Visible;

            CommandComboBox.Items.Clear();
            foreach (var cmd in SplitCommands(tool.Commond))
                CommandComboBox.Items.Add(cmd);
            if (CommandComboBox.Items.Count > 0)
                CommandComboBox.SelectedIndex = 0;
            else
                CommandComboBox.Text = string.Empty;
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTool != null)
            {
                ShowCurrent();
                return;
            }
            if (_currentCategory != null)
            {
                _currentCategory = _currentCategory.ParentCategory;
                SyncTreeSelection(_currentCategory);
                ShowCurrent();
            }
        }

        private void Card_Click(object sender, RoutedEventArgs e)
        {
            switch ((sender as FrameworkElement)?.DataContext)
            {
                case Category category:
                    _currentCategory = category;
                    SyncTreeSelection(category);
                    ShowCurrent();
                    break;
                case Tool tool:
                    ShowDetails(tool);
                    break;
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!IsLoaded && SearchBox.Text.Length == 0) return;
            ShowCurrent();
        }

        private void SearchBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                SearchBox.Clear();
        }

        private void ClearSearchSilently()
        {
            if (SearchBox.Text.Length == 0) return;
            SearchBox.TextChanged -= SearchBox_TextChanged;
            SearchBox.Clear();
            SearchBox.TextChanged += SearchBox_TextChanged;
        }

        #endregion

        #region 启动 / 目录

        private void StartTool_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTool != null)
                LaunchTool(_selectedTool, CommandComboBox.Text?.Trim());
        }

        private void OpenLocalFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTool != null)
                OpenFolder(_selectedTool);
        }

        private void CopyPath_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedTool != null)
                CopyPath(_selectedTool);
        }

        private void CardLaunch_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Tool tool)
                LaunchTool(tool, SplitCommands(tool.Commond).FirstOrDefault());
        }

        private void CardOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Tool tool)
                OpenFolder(tool);
        }

        private void CardCopyPath_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is Tool tool)
                CopyPath(tool);
        }

        private void LaunchTool(Tool tool, string? command)
        {
            try
            {
                if (tool.Cmd == 1)
                {
                    if (string.IsNullOrWhiteSpace(tool.RUN))
                    {
                        Toast.Warning("该工具未设置 EXE 文件名。");
                        return;
                    }
                    var exePath = Path.IsPathRooted(tool.RUN) ? tool.RUN : Path.Combine(tool.ToolPath ?? "", tool.RUN);
                    if (!File.Exists(exePath))
                    {
                        Toast.Error($"文件不存在：{exePath}");
                        return;
                    }
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = Path.GetDirectoryName(exePath) ?? "",
                        UseShellExecute = true
                    });
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(tool.ToolPath) || !Directory.Exists(tool.ToolPath))
                    {
                        Toast.Error("工具目录未设置或不存在。");
                        return;
                    }
                    if (UseBuiltinTerminal && RunInTerminal != null)
                    {
                        RunInTerminal(tool.Name, tool.ToolPath, string.IsNullOrWhiteSpace(command) ? null : command);
                        return;
                    }
                    // 命令为空时直接在工具目录打开命令行
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = string.IsNullOrWhiteSpace(command) ? "/k" : $"/k \"{command}\"",
                        WorkingDirectory = tool.ToolPath,
                        UseShellExecute = true
                    });
                }
                Toast.Success($"已启动 {tool.Name}");
            }
            catch (Exception ex)
            {
                Toast.Error($"启动失败：{ex.Message}");
            }
        }

        private static void OpenFolder(Tool tool)
        {
            if (string.IsNullOrWhiteSpace(tool.ToolPath) || !Directory.Exists(tool.ToolPath))
            {
                Toast.Error("工具目录未设置或不存在。");
                return;
            }
            try
            {
                Process.Start(new ProcessStartInfo { FileName = tool.ToolPath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Toast.Error($"无法打开目录：{ex.Message}");
            }
        }

        private static void CopyPath(Tool tool)
        {
            var path = tool.Cmd == 1 && !string.IsNullOrWhiteSpace(tool.RUN)
                ? (Path.IsPathRooted(tool.RUN) ? tool.RUN : Path.Combine(tool.ToolPath ?? "", tool.RUN))
                : tool.ToolPath;
            if (string.IsNullOrWhiteSpace(path))
            {
                Toast.Warning("该工具没有设置路径。");
                return;
            }
            try
            {
                Clipboard.SetText(path);
                Toast.Success("路径已复制");
            }
            catch (Exception ex)
            {
                Toast.Error($"复制失败：{ex.Message}");
            }
        }

        #endregion

        #region 辅助

        private static IEnumerable<string> SplitCommands(string? commands) =>
            (commands ?? "").Split(';').Select(c => c.Trim()).Where(c => c.Length > 0);

        private static bool Contains(string? source, string value) =>
            source?.Contains(value, StringComparison.OrdinalIgnoreCase) == true;

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = current is Visual || current is System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }
            return null;
        }

        #endregion
    }
}
