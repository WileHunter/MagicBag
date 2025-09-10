using HandyControl.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WpfApp3.Models;
using MessageBox = HandyControl.Controls.MessageBox;

namespace WpfApp3
{
    public partial class DatabaseView : UserControl
    {
        private readonly string _jsonFilePath = "tools.json";
        private Root _rootData = new Root { Categories = new List<Category>() };
        private Category? _currentCategory;

        // 对外通知（与 AddToolView 的语义一致）
        public event Action? ItemUpdated;

        public DatabaseView()
        {
            InitializeComponent();
            LoadData();

            // 动态挂接右键菜单，避免在 XAML 中声明 ContextMenu 引发 connectionId 冲突
            CategoryTreeView.Loaded += CategoryTreeView_Loaded;
            CategoryTreeView.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
        }

        private void LoadData()
        {
            try
            {
                if (File.Exists(_jsonFilePath))
                {
                    var json = File.ReadAllText(_jsonFilePath);
                    _rootData = JsonConvert.DeserializeObject<Root>(json) ?? new Root();
                }
                else
                {
                    _rootData = new Root { Categories = new List<Category>() };
                }

                _rootData.Categories ??= new List<Category>();
                foreach (var c in _rootData.Categories)
                    EnsureListsRecursive(c);

                CategoryTreeView.ItemsSource = _rootData.Categories;
                ToolsGrid.ItemsSource = null;
                _currentCategory = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载数据失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                _rootData = new Root { Categories = new List<Category>() };
                CategoryTreeView.ItemsSource = _rootData.Categories;
            }
        }

        private static void EnsureListsRecursive(Category cat)
        {
            cat.SubCategories ??= new List<Category>();
            cat.Tools ??= new List<Tool>();

            foreach (var sub in cat.SubCategories)
            {
                sub.ParentCategory = cat;
                EnsureListsRecursive(sub);
            }

            foreach (var t in cat.Tools)
            {
                t.ParentCategory = cat;
            }
        }

        private void SaveData()
        {
            var json = JsonConvert.SerializeObject(_rootData, Formatting.Indented);
            File.WriteAllText(_jsonFilePath, json);
        }

        private void CategoryTreeView_Loaded(object sender, RoutedEventArgs e)
        {
            AttachContextMenusToTree(CategoryTreeView.Items);
        }

        private void ItemContainerGenerator_StatusChanged(object? sender, EventArgs e)
        {
            // 容器生成完成后再次尝试附加右键菜单（应对后续动态展开/加载）
            if (CategoryTreeView.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                AttachContextMenusToTree(CategoryTreeView.Items);
            }
        }

        private void AttachContextMenusToTree(ItemCollection items)
        {
            foreach (var item in items)
            {
                if (CategoryTreeView.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                {
                    if (tvi.ContextMenu == null)
                    {
                        var menu = new ContextMenu
                        {
                            MaxWidth = 135
                        };

                        // 新建子文件夹
                        var addSubCategoryItem = new MenuItem
                        {
                            Header = "新建子文件夹",
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(0,1 , 4, 1)
                        };
                        addSubCategoryItem.Click += AddSubCategory_Click;
                        menu.Items.Add(addSubCategoryItem);

                        // 删除文件夹
                        var deleteCategoryItem = new MenuItem
                        {
                            Header = "删除文件夹",
                            HorizontalContentAlignment = HorizontalAlignment.Left,
                            Padding = new Thickness(0, 1, 4, 1)
                        };
                        deleteCategoryItem.Click += DeleteCategory_Click;
                        menu.Items.Add(deleteCategoryItem);

                        tvi.ContextMenu = menu;
                    }

                    AttachContextMenusToTree(tvi.Items);
                }
            }
        }

        private void DeleteCategory_Click(object sender, RoutedEventArgs e)
        {
            if (CategoryTreeView.SelectedItem is Category categoryToDelete)
            {
                var result = MessageBox.Show(
                    $"确定要删除文件夹“{categoryToDelete.Name}”及其所有子内容吗？",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    if (categoryToDelete.ParentCategory != null)
                    {
                        // 从父分类的子分类中移除
                        categoryToDelete.ParentCategory.SubCategories?.Remove(categoryToDelete);
                    }
                    else
                    {
                        // 从顶层分类中移除
                        _rootData.Categories?.Remove(categoryToDelete);
                    }

                    RefreshTreeView();
                    ItemUpdated?.Invoke(); // 通知 ToolsView 刷新
                }
            }
        }

        private void CategoryTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is Category category)
            {
                _currentCategory = category;
                var list = category.Tools ?? new List<Tool>();
                ToolsGrid.ItemsSource = new ObservableCollection<Tool>(list);
            }
            else
            {
                _currentCategory = null;
                ToolsGrid.ItemsSource = null;
            }
        }

        private void AddTopLevelCategory_Click(object sender, RoutedEventArgs e)
        {
            var inputBox = new InputBox(InputBoxMode.NewNote, "新建顶层文件夹");
            inputBox.accept += (s, args) =>
            {
                var name = inputBox.InputValue?.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    MessageBox.Show("名称不能为空。");
                    return;
                }

                var newCategory = new Category
                {
                    Name = name,
                    SubCategories = new List<Category>(),
                    Tools = new List<Tool>()
                };
                _rootData.Categories?.Add(newCategory);
                RefreshTreeView();
            };
            inputBox.ShowDialog();
        }

        private void AddSubCategory_Click(object sender, RoutedEventArgs e)
        {
            // 使用 TreeView 选中的分类作为父级
            if (CategoryTreeView.SelectedItem is Category selectedCategory)
            {
                var inputBox = new InputBox(InputBoxMode.NewNote, "新建子文件夹");
                inputBox.accept += (s, args) =>
                {
                    var name = inputBox.InputValue?.Trim();
                    if (string.IsNullOrEmpty(name))
                    {
                        MessageBox.Show("名称不能为空。");
                        return;
                    }

                    var newCategory = new Category
                    {
                        Name = name,
                        SubCategories = new List<Category>(),
                        Tools = new List<Tool>(),
                        ParentCategory = selectedCategory
                    };
                    selectedCategory.SubCategories ??= new List<Category>();
                    selectedCategory.SubCategories.Add(newCategory);
                    RefreshTreeView();
                };
                inputBox.ShowDialog();
            }
        }

        private void AddTool_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCategory == null)
            {
                MessageBox.Show("请先在左侧选择一个分类。");
                return;
            }

            var newTool = new Tool
            {
                Id = GetNextId(),
                Name = "新工具",
                Description = "",
                ToolPath = "",
                RUN = "",
                Commond = "",
                Cmd = 1,
                ParentCategory = _currentCategory
            };

            _currentCategory.Tools ??= new List<Tool>();
            _currentCategory.Tools.Add(newTool);

            // 刷新右侧表格
            ToolsGrid.ItemsSource = new ObservableCollection<Tool>(_currentCategory.Tools);
        }

        private void DeleteTool_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCategory == null)
            {
                MessageBox.Show("请先在左侧选择一个分类。");
                return;
            }

            if (ToolsGrid.SelectedItem is Tool tool)
            {
                _currentCategory.Tools?.Remove(tool);
                ToolsGrid.ItemsSource = new ObservableCollection<Tool>(_currentCategory.Tools ?? new List<Tool>());
            }
            else
            {
                MessageBox.Show("请先在列表中选择要删除的工具。");
            }
        }

        private void SaveJson_Click(object sender, RoutedEventArgs e)
        {
            if (_currentCategory != null && ToolsGrid.ItemsSource is ObservableCollection<Tool> oc)
            {
                _currentCategory.Tools = oc.ToList();
            }

            try
            {
                SaveData();

                // Growl 成功提示
                Growl.Success("工具数据已成功保存！", "SuccessMsg");

                ItemUpdated?.Invoke();
            }
            catch (Exception ex)
            {
                Growl.Error($"保存失败：{ex.Message}", "SuccessMsg");
            }
        }

        private void Undo_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            MessageBox.Show("已撤销为磁盘上数据。");
        }

        private void RefreshTreeView()
        {
            // 重新绑定刷新 TreeView，并重挂右键菜单
            CategoryTreeView.ItemsSource = null;
            CategoryTreeView.ItemsSource = _rootData.Categories;

            // 等容器生成后挂菜单
            CategoryTreeView.Dispatcher.InvokeAsync(() =>
            {
                AttachContextMenusToTree(CategoryTreeView.Items);
            });
        }

        private int GetNextId()
        {
            var all = GetAllTools(_rootData.Categories ?? Enumerable.Empty<Category>());
            var max = all.Select(t => t?.Id ?? 0).DefaultIfEmpty(0).Max();
            return max + 1;
        }

        private IEnumerable<Tool> GetAllTools(IEnumerable<Category> cats)
        {
            foreach (var c in cats)
            {
                if (c.Tools != null)
                {
                    foreach (var t in c.Tools)
                        yield return t;
                }
                if (c.SubCategories != null)
                {
                    foreach (var sub in GetAllTools(c.SubCategories))
                        yield return sub;
                }
            }
        }

        private void DrawerToggle_Checked(object sender, RoutedEventArgs e)
        {
            DrawerColumn.Width = new GridLength(150);
            DrawerToggle.Content = "❮";
        }

        private void DrawerToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            DrawerColumn.Width = new GridLength(0);
            DrawerToggle.Content = "❯";
        }
    }
}
