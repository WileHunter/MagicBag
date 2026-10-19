using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using WpfApp3.Models;
using WpfApp3.Services;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfApp3
{
    public partial class DatabaseView : UserControl
    {
        private Root _root = new() { Categories = new List<Category>() };
        private Category? _currentCategory;
        private ObservableCollection<Tool>? _gridItems;
        private bool _dirty;

        /// <summary>数据保存后通知工具页刷新。</summary>
        public event Action? ItemUpdated;

        public DatabaseView()
        {
            InitializeComponent();
            PreviewKeyDown += View_PreviewKeyDown;
            LoadData();
        }

        #region 加载 / 保存

        private void LoadData()
        {
            try
            {
                _root = ToolRepository.Load();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"读取 tools.json 失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _root = new Root { Categories = new List<Category>() };
            }

            foreach (var tool in ToolRepository.AllTools(_root.Categories))
                Track(tool);

            _currentCategory = null;
            RefreshTree();
            ShowCategory(null);
            SetDirty(false);
        }

        private void Track(Tool tool)
        {
            tool.PropertyChanged -= Tool_PropertyChanged;
            tool.PropertyChanged += Tool_PropertyChanged;
        }

        private void Tool_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(Tool.ModeText) && e.PropertyName != nameof(Tool.Icon))
                SetDirty(true);
        }

        private void SetDirty(bool dirty)
        {
            _dirty = dirty;
            DirtyBadge.Visibility = dirty ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool Save()
        {
            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            try
            {
                ToolRepository.Save(_root);
                SetDirty(false);
                ItemUpdated?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Toast.Error($"保存失败：{ex.Message}");
                return false;
            }
        }

        private void SaveJson_Click(object sender, RoutedEventArgs e)
        {
            if (Save()) Toast.Success("工具数据已保存");
        }

        private void Reload_Click(object sender, RoutedEventArgs e)
        {
            ToolsGrid.CancelEdit(DataGridEditingUnit.Row);
            if (_dirty)
            {
                var r = MessageBox.Show("放弃所有未保存的修改，并从磁盘重新加载？", "重新加载",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return;
            }
            LoadData();
            Toast.Info("已从磁盘重新加载");
        }

        /// <summary>离开前检查未保存的修改。返回 false 表示用户取消。</summary>
        public bool ConfirmLeave()
        {
            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            if (!_dirty) return true;

            var r = MessageBox.Show("工具数据有未保存的修改，是否保存？", "未保存的修改",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            return r switch
            {
                MessageBoxResult.Yes => Save(),
                MessageBoxResult.No => true,
                _ => false
            };
        }

        private void View_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
            {
                SaveJson_Click(this, e);
                e.Handled = true;
            }
        }

        #endregion

        #region 分类树

        private void RefreshTree()
        {
            CategoryTreeView.ItemsSource = null;
            CategoryTreeView.ItemsSource = _root.Categories;
        }

        private void SelectCategory(Category category)
        {
            foreach (var c in ToolRepository.AllCategories(_root.Categories))
                c.IsSelected = false;
            for (var p = category.ParentCategory; p != null; p = p.ParentCategory)
                p.IsExpanded = true;
            category.IsSelected = true;
            ShowCategory(category);
        }

        private void CategoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            // 重新绑定树时 NewValue 会短暂为 null，此时保持当前分类不变
            if (e.NewValue is Category category)
                ShowCategory(category);
        }

        private void ShowCategory(Category? category)
        {
            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            _currentCategory = category;

            if (category == null)
            {
                _gridItems = null;
                ToolsGrid.ItemsSource = null;
                TitleText.Text = "工具数据";
                var total = ToolRepository.AllTools(_root.Categories).Count();
                var cats = ToolRepository.AllCategories(_root.Categories).Count();
                SubtitleText.Text = $"{cats} 个分类 · {total} 个工具";
            }
            else
            {
                category.Tools ??= new List<Tool>();
                _gridItems = new ObservableCollection<Tool>(category.Tools);
                ToolsGrid.ItemsSource = _gridItems;
                TitleText.Text = category.Name ?? "";
                UpdateSubtitle();
            }
            UpdateEmptyState();
        }

        private void UpdateSubtitle()
        {
            if (_currentCategory == null) return;
            var path = ToolRepository.GetPath(_currentCategory);
            SubtitleText.Text = $"{path}  ·  {_currentCategory.Tools?.Count ?? 0} 个工具";
        }

        private void UpdateEmptyState()
        {
            var empty = _currentCategory == null || (_gridItems?.Count ?? 0) == 0;
            EmptyState.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
            if (_currentCategory == null)
            {
                EmptyIcon.Text = "\uF12B";
                EmptyText.Text = (_root.Categories?.Count ?? 0) == 0
                    ? "还没有分类，点击左上角 + 新建"
                    : "在左侧选择一个分类";
            }
            else
            {
                EmptyIcon.Text = "\uE710";
                EmptyText.Text = "这个分类还没有工具，点击“添加”";
            }
        }

        private void CategoryTreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 右键时先选中该节点，保证菜单操作的对象正确
            if (FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) is { DataContext: Category c } item)
            {
                item.IsSelected = true;
                item.Focus();
                c.IsSelected = true;
            }
        }

        private void CategoryTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<TreeViewItem>(e.OriginalSource as DependencyObject) is { DataContext: Category c } &&
                FindAncestor<System.Windows.Controls.Primitives.ToggleButton>(e.OriginalSource as DependencyObject) == null)
            {
                RenameCategory(c);
                e.Handled = true;
            }
        }

        private void CategoryTreeView_KeyDown(object sender, KeyEventArgs e)
        {
            if (CategoryTreeView.SelectedItem is not Category c) return;
            if (e.Key == Key.F2)
            {
                RenameCategory(c);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete)
            {
                DeleteCategory(c);
                e.Handled = true;
            }
        }

        private static IList<Category> Siblings(Root root, Category? parent) =>
            parent != null ? (parent.SubCategories ??= new List<Category>()) : (root.Categories ??= new List<Category>());

        private Func<string, string?> NameValidator(Category? parent, Category? self) => name =>
            Siblings(_root, parent).Any(c => c != self && string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase))
                ? "同一级下已存在同名分类。"
                : null;

        private void AddCategory(Category? parent)
        {
            var name = InputBox.Prompt(this, parent == null ? "新建顶层分类" : "新建子分类", null, NameValidator(parent, null),
                parent == null ? null : $"位于：{ToolRepository.GetPath(parent)}");
            if (name == null) return;

            var category = new Category
            {
                Name = name,
                SubCategories = new List<Category>(),
                Tools = new List<Tool>(),
                ParentCategory = parent
            };
            Siblings(_root, parent).Add(category);
            if (parent != null) parent.IsExpanded = true;

            RefreshTree();
            SelectCategory(category);
            SetDirty(true);
            Toast.Success($"已创建分类“{name}”");
        }

        private void AddTopLevelCategory_Click(object sender, RoutedEventArgs e) => AddCategory(null);

        private void AddSubCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryTreeView.SelectedItem is Category parent)
                AddCategory(parent);
        }

        private void RenameCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryTreeView.SelectedItem is Category c)
                RenameCategory(c);
        }

        private void RenameCategory(Category category)
        {
            var name = InputBox.Prompt(this, "重命名分类", category.Name, NameValidator(category.ParentCategory, category));
            if (name == null || name == category.Name) return;

            category.Name = name;
            if (category == _currentCategory)
            {
                TitleText.Text = name;
                UpdateSubtitle();
            }
            SetDirty(true);
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryTreeView.SelectedItem is Category c)
                DeleteCategory(c);
        }

        private void DeleteCategory(Category category)
        {
            var subCount = ToolRepository.AllCategories(category.SubCategories).Count();
            var toolCount = ToolRepository.AllTools(new[] { category }).Count();
            var detail = subCount + toolCount > 0
                ? $"\n\n其中包含 {subCount} 个子分类、{toolCount} 个工具，将一并删除。"
                : string.Empty;

            var r = MessageBox.Show($"确定删除分类“{category.Name}”吗？{detail}\n（保存前可通过“重新加载”撤销）", "删除分类",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;

            var parent = category.ParentCategory;
            Siblings(_root, parent).Remove(category);

            var currentRemoved = false;
            for (var c = _currentCategory; c != null; c = c.ParentCategory)
                if (c == category) currentRemoved = true;

            RefreshTree();
            if (currentRemoved)
            {
                if (parent != null) SelectCategory(parent);
                else ShowCategory(null);
            }
            SetDirty(true);
            Toast.Success($"已删除分类“{category.Name}”");
        }

        #endregion

        #region 工具表格

        private void AddTool_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCategory == null || _gridItems == null)
            {
                Toast.Info("请先在左侧选择一个分类");
                return;
            }

            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            var tool = new Tool
            {
                Id = ToolRepository.NextId(_root.Categories),
                Name = "新工具",
                Cmd = 1,
                ParentCategory = _currentCategory
            };
            Track(tool);
            _currentCategory.Tools ??= new List<Tool>();
            _currentCategory.Tools.Add(tool);
            _gridItems.Add(tool);
            SetDirty(true);
            UpdateSubtitle();
            UpdateEmptyState();

            ToolsGrid.SelectedItem = tool;
            ToolsGrid.ScrollIntoView(tool);
            ToolsGrid.Focus();
            ToolsGrid.CurrentCell = new DataGridCellInfo(tool, ToolsGrid.Columns[1]);
            ToolsGrid.BeginEdit();
        }

        private List<Tool> SelectedTools() => ToolsGrid.SelectedItems.OfType<Tool>().ToList();

        private void DeleteTool_Click(object sender, RoutedEventArgs e)
        {
            var tools = SelectedTools();
            if (_currentCategory == null || _gridItems == null || tools.Count == 0)
            {
                Toast.Info("请先在表格中选择要删除的工具");
                return;
            }

            ToolsGrid.CancelEdit(DataGridEditingUnit.Row);
            var text = tools.Count == 1 ? $"确定删除工具“{tools[0].Name}”吗？" : $"确定删除选中的 {tools.Count} 个工具吗？";
            var r = MessageBox.Show(text, "删除工具", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;

            foreach (var t in tools)
            {
                _currentCategory.Tools?.Remove(t);
                _gridItems.Remove(t);
                t.PropertyChanged -= Tool_PropertyChanged;
            }
            SetDirty(true);
            UpdateSubtitle();
            UpdateEmptyState();
        }

        private void ToolsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 仅在非编辑状态下响应 Delete，避免删除单元格文字时误删整行
            if (e.Key == Key.Delete && e.OriginalSource is DataGridCell or DataGridRow or DataGrid)
            {
                DeleteTool_Click(sender, e);
                e.Handled = true;
            }
        }

        private void ToolsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // 右键所在行设为选中
            if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is { Item: Tool tool } row && !row.IsSelected)
            {
                ToolsGrid.SelectedItems.Clear();
                ToolsGrid.SelectedItem = tool;
            }

            MoveToMenu.Items.Clear();
            var hasSelection = ToolsGrid.SelectedItems.Count > 0;
            MoveToMenu.IsEnabled = hasSelection;
            if (!hasSelection) return;

            foreach (var c in ToolRepository.AllCategories(_root.Categories))
            {
                var item = new MenuItem
                {
                    Header = ToolRepository.GetPath(c, " › "),
                    IsEnabled = c != _currentCategory,
                    Tag = c
                };
                item.Click += MoveTo_Click;
                MoveToMenu.Items.Add(item);
            }
        }

        private void MoveTo_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem { Tag: Category target } || _currentCategory == null || _gridItems == null) return;
            var tools = SelectedTools();
            if (tools.Count == 0) return;

            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            target.Tools ??= new List<Tool>();
            foreach (var t in tools)
            {
                _currentCategory.Tools?.Remove(t);
                _gridItems.Remove(t);
                target.Tools.Add(t);
                t.ParentCategory = target;
            }
            SetDirty(true);
            UpdateSubtitle();
            UpdateEmptyState();
            Toast.Success($"已移动 {tools.Count} 个工具到“{target.Name}”");
        }

        private Tool? ContextTool()
        {
            if (ToolsGrid.SelectedItem is Tool t) return t;
            Toast.Info("请先选择一个工具");
            return null;
        }

        private void BrowseProgram_Click(object sender, RoutedEventArgs e)
        {
            if (ContextTool() is not { } tool) return;
            var dlg = new OpenFileDialog
            {
                Title = $"为“{tool.Name}”选择程序",
                Filter = "可执行文件|*.exe;*.bat;*.cmd;*.ps1;*.jar;*.py;*.lnk|所有文件|*.*"
            };
            if (Directory.Exists(tool.ToolPath)) dlg.InitialDirectory = tool.ToolPath;
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            tool.ToolPath = Path.GetDirectoryName(dlg.FileName) ?? "";
            tool.RUN = Path.GetFileName(dlg.FileName);
            if (tool.Name is "" or "新工具")
                tool.Name = Path.GetFileNameWithoutExtension(dlg.FileName);
            if (string.IsNullOrWhiteSpace(tool.Commond))
                tool.Cmd = 1;
        }

        private void BrowseFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ContextTool() is not { } tool) return;
            var dlg = new OpenFolderDialog { Title = $"为“{tool.Name}”选择工作目录" };
            if (Directory.Exists(tool.ToolPath)) dlg.InitialDirectory = tool.ToolPath;
            if (dlg.ShowDialog(Window.GetWindow(this)) != true) return;

            ToolsGrid.CommitEdit(DataGridEditingUnit.Row, true);
            tool.ToolPath = dlg.FolderName;
        }

        #endregion

        private void SidebarToggle_Click(object sender, RoutedEventArgs e)
        {
            var show = SidebarToggle.IsChecked == true;
            SidebarColumn.Width = new GridLength(show ? 196 : 0);
            SidebarGapColumn.Width = new GridLength(show ? 10 : 0);
            Sidebar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = current is Visual or System.Windows.Media.Media3D.Visual3D
                    ? VisualTreeHelper.GetParent(current)
                    : LogicalTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
