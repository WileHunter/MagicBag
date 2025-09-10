using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfApp3.Models;

namespace WpfApp3
{
    public partial class ToolsView : UserControl
    {
        private ObservableCollection<Category>? allCategories;
        private Tool? selectedTool;

        public ToolsView()
        {
            InitializeComponent();
            this.Unloaded += ToolsView_Unloaded;
            ReloadData(); // 初始加载数据
        }

        private void ToolsView_Unloaded(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("ToolsView is unloading.");
        }

        /// <summary>
        /// 重新加载 tools.json 文件并刷新 TreeView。
        /// </summary>
        public void ReloadData()
        {
            try
            {
                string basePath = AppDomain.CurrentDomain.BaseDirectory;
                string jsonPath = Path.Combine(basePath, "tools.json");
                if (!File.Exists(jsonPath))
                {
                    System.Windows.MessageBox.Show($"文件未找到: {jsonPath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                string jsonContent = File.ReadAllText(jsonPath);
                var data = JsonConvert.DeserializeObject<Root>(jsonContent);
                allCategories = data?.Categories != null ? new ObservableCollection<Category>(data.Categories) : new ObservableCollection<Category>();

                ToolTreeView.Items.Clear();
                LoadTreeViewItems(allCategories, ToolTreeView.Items);

                // 默认显示所有工具
                DisplayAllTools();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"加载数据失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadTreeViewItems(ObservableCollection<Category>? categories, ItemCollection parentItems)
        {
            if (categories == null) return;

            foreach (var category in categories)
            {
                var categoryHeader = new StackPanel { Orientation = Orientation.Horizontal };
                categoryHeader.Children.Add(new TextBlock { Text = "📁", FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
                categoryHeader.Children.Add(new TextBlock { Text = category.Name, VerticalAlignment = VerticalAlignment.Center });

                var item = new TreeViewItem
                {
                    Header = categoryHeader,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Tag = category
                };
                if (category.SubCategories != null && category.SubCategories.Count > 0)
                {
                    LoadTreeViewItems(new ObservableCollection<Category>(category.SubCategories), item.Items);
                }
                if (category.Tools != null && category.Tools.Count > 0)
                {
                    foreach (var tool in category.Tools)
                    {
                        var toolHeader = new StackPanel { Orientation = Orientation.Horizontal };
                        toolHeader.Children.Add(new TextBlock { Text = "🛠️", FontFamily = new System.Windows.Media.FontFamily("Segoe UI Symbol"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 5, 0) });
                        toolHeader.Children.Add(new TextBlock { Text = tool.Name, VerticalAlignment = VerticalAlignment.Center });

                        item.Items.Add(new TreeViewItem
                        {
                            Header = toolHeader,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Tag = tool
                        });
                    }
                }
                parentItems.Add(item);
            }
        }

        private void DisplayAllTools()
        {
            SearchBox.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
            BottomButtonPanel.Visibility = Visibility.Collapsed;

            var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            if (allCategories != null)
            {
                var allTools = GetAllTools(allCategories);
                foreach (var tool in allTools)
                {
                    var border = CreateToolBorder(tool);
                    wrapPanel.Children.Add(border);
                }
            }
            RightContent.Content = wrapPanel;
        }

        private void DisplayCategoryItems(Category category)
        {
            SearchBox.Visibility = Visibility.Visible;
            BackButton.Visibility = Visibility.Collapsed;
            BottomButtonPanel.Visibility = Visibility.Collapsed;

            var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            if (category != null)
            {
                if (category.SubCategories != null)
                {
                    foreach (var subCat in category.SubCategories)
                    {
                        var border = CreateFolderBorder(subCat);
                        wrapPanel.Children.Add(border);
                    }
                }
                if (category.Tools != null)
                {
                    foreach (var tool in category.Tools)
                    {
                        var border = CreateToolBorder(tool);
                        wrapPanel.Children.Add(border);
                    }
                }
            }
            RightContent.Content = wrapPanel;
        }

        private void DisplayToolDetails(Tool tool)
        {
            // 隐藏搜索框，显示返回按钮和底部按钮面板
            SearchBox.Visibility = Visibility.Collapsed;
            BackButton.Visibility = Visibility.Visible;
            BottomButtonPanel.Visibility = Visibility.Visible;

            selectedTool = tool;

            var mainContentGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0)
            };

            var detailsGrid = new Grid { Margin = new Thickness(20) };
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120, GridUnitType.Auto) });
            detailsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            detailsGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // 图片
            var image = new Image { Width = 130, Height = 130, Margin = new Thickness(0, 0, 20, 20) };
            try
            {
                System.Drawing.Bitmap? bmp = Properties.Resources.model;
                if (bmp != null)
                {
                    image.Source = BitmapToBitmapSource(bmp);
                }
            }
            catch { }
            Grid.SetRow(image, 0);
            Grid.SetColumn(image, 0);
            Grid.SetRowSpan(image, 2);
            detailsGrid.Children.Add(image);

            // 标题
            var title = new TextBlock { Text = tool?.Name ?? "未知工具", FontSize = 24, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 5, 0, 5) };
            Grid.SetRow(title, 0);
            Grid.SetColumn(title, 1);
            detailsGrid.Children.Add(title);

            // 描述
            var description = new TextBlock { Text = tool?.Description ?? "无描述", Margin = new Thickness(0, 0, 0, 5), TextWrapping = TextWrapping.Wrap, Foreground = Brushes.Gray, FontSize = 12 };
            Grid.SetRow(description, 1);
            Grid.SetColumn(description, 1);
            detailsGrid.Children.Add(description);

            // 下拉输入框
            var commandComboBox = new ComboBox
            {
                Margin = new Thickness(0, 10, 0, 0),
                Width = 350,
                Height = 36,
                FontSize = 14,
                IsEditable = true   // ✅ 允许自定义输入
            };

            if (tool?.Cmd == 0 && !string.IsNullOrEmpty(tool.Commond))
            {
                var commands = tool.Commond.Split(';');
                foreach (var cmd in commands)
                {
                    if (!string.IsNullOrWhiteSpace(cmd))
                        commandComboBox.Items.Add(cmd.Trim());
                }

                if (commandComboBox.Items.Count > 0)
                    commandComboBox.SelectedIndex = 0;
            }
            else if (tool?.Cmd == 1)
            {
                commandComboBox.Visibility = Visibility.Collapsed;
            }

            Grid.SetRow(commandComboBox, 2);
            Grid.SetColumnSpan(commandComboBox, 2);
            detailsGrid.Children.Add(commandComboBox);

            mainContentGrid.Children.Add(detailsGrid);

            StartButton.CommandParameter = commandComboBox;
            OpenFolderButton.CommandParameter = tool;

            RightContent.Content = mainContentGrid;
        }

        private Border CreateFolderBorder(Category subCat)
        {
            var card = new Border
            {
                Width = 120,
                Height = 90,
                Margin = new Thickness(8),
                Background = new SolidColorBrush(Color.FromRgb(240, 240, 240)),
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Gray,
                    Direction = 270,
                    BlurRadius = 5,
                    ShadowDepth = 2,
                    Opacity = 0.5
                }
            };

            var stackPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            stackPanel.Children.Add(new TextBlock { Text = "📁", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 40, HorizontalAlignment = HorizontalAlignment.Center });
            stackPanel.Children.Add(new TextBlock { Text = subCat.Name, FontWeight = FontWeights.Bold, FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap });

            card.Child = stackPanel;
            card.PreviewMouseLeftButtonDown += (s, e) => { if (subCat != null) { DisplayCategoryItems(subCat); } };
            return card;
        }

        private Border CreateToolBorder(Tool tool)
        {
            var card = new Border
            {
                Width = 120,
                Height = 90,
                Margin = new Thickness(8),
                Background = Brushes.White,
                CornerRadius = new CornerRadius(8),
                Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Gray,
                    Direction = 270,
                    BlurRadius = 5,
                    ShadowDepth = 2,
                    Opacity = 0.5
                }
            };

            var stackPanel = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10) };
            stackPanel.Children.Add(new TextBlock { Text = "🛠️", FontFamily = new FontFamily("Segoe UI Symbol"), FontSize = 20, HorizontalAlignment = HorizontalAlignment.Left });
            var textBlock = new StackPanel { Orientation = Orientation.Vertical };
            textBlock.Children.Add(new TextBlock { Text = tool.Name, FontWeight = FontWeights.Bold, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 5, 0, 0), TextWrapping = TextWrapping.Wrap });
            textBlock.Children.Add(new TextBlock { Text = tool.Description, FontStyle = FontStyles.Italic, Foreground = Brushes.Gray, FontSize = 10, TextWrapping = TextWrapping.Wrap });
            stackPanel.Children.Add(textBlock);

            card.Child = stackPanel;
            card.PreviewMouseLeftButtonDown += (s, e) => { if (tool != null) { DisplayToolDetails(tool); } };
            return card;
        }

        private List<Tool> GetAllTools(ObservableCollection<Category>? categories)
        {
            var tools = new List<Tool>();
            if (categories == null) return tools;

            foreach (var category in categories)
            {
                if (category.Tools != null)
                {
                    tools.AddRange(category.Tools);
                }
                if (category.SubCategories != null)
                {
                    tools.AddRange(GetAllTools(new ObservableCollection<Category>(category.SubCategories)));
                }
            }
            return tools;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = (sender as TextBox)?.Text?.ToLower() ?? "";
            var wrapPanel = new WrapPanel { Orientation = Orientation.Horizontal };
            if (allCategories != null && !string.IsNullOrEmpty(searchText))
            {
                var allTools = GetAllTools(allCategories);
                var filteredTools = allTools.Where(t => t.Name?.ToLower().Contains(searchText) == true).ToList();
                foreach (var tool in filteredTools)
                {
                    var border = CreateToolBorder(tool);
                    wrapPanel.Children.Add(border);
                }
                RightContent.Content = wrapPanel;
            }
            else if (allCategories != null)
            {
                DisplayAllTools();
            }
        }

        private void ToolTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (ToolTreeView.SelectedItem is TreeViewItem selectedItem)
            {
                if (selectedItem.Tag is Category category)
                {
                    DisplayCategoryItems(category);
                }
                else if (selectedItem.Tag is Tool tool)
                {
                    DisplayToolDetails(tool);
                }
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            DisplayAllTools();
            selectedTool = null;
        }

        private void StartTool_Click(object sender, RoutedEventArgs e)
        {
            if (selectedTool == null || string.IsNullOrEmpty(selectedTool.ToolPath))
            {
                MessageBox.Show("工具路径未设置。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var commandComboBox = (sender as Button)?.CommandParameter as ComboBox;
            string commandToRun = commandComboBox?.Text?.Trim() ?? string.Empty;  // ✅ 使用 Text，而不是 SelectedItem

            try
            {
                if (selectedTool.Cmd == 0) // 命令行启动方式
                {
                    if (string.IsNullOrEmpty(commandToRun))
                    {
                        MessageBox.Show("请输入或选择一条命令。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var process = new Process();
                    process.StartInfo.FileName = "cmd.exe";
                    process.StartInfo.WorkingDirectory = selectedTool.ToolPath;
                    process.StartInfo.Arguments = $"/k \"{commandToRun}\"";
                    process.Start();
                }
                else if (selectedTool.Cmd == 1) // exe启动方式
                {
                    if (string.IsNullOrEmpty(selectedTool.RUN))
                    {
                        MessageBox.Show("EXE文件名未设置。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    string exePath = Path.Combine(selectedTool.ToolPath, selectedTool.RUN);
                    if (!File.Exists(exePath))
                    {
                        MessageBox.Show($"文件未找到: {exePath}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = exePath,
                        WorkingDirectory = selectedTool.ToolPath,
                        UseShellExecute = true
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"启动工具失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenLocalFolder_Click(object sender, RoutedEventArgs e)
        {
            Tool? tool = (sender as Button)?.CommandParameter as Tool;
            if (tool != null && !string.IsNullOrEmpty(tool.ToolPath) && Directory.Exists(tool.ToolPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tool.ToolPath,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                MessageBox.Show("工具路径无效或不存在。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 辅助方法：将 System.Drawing.Bitmap 转换为 System.Windows.Media.Imaging.BitmapSource
        public static BitmapSource BitmapToBitmapSource(System.Drawing.Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgra32, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }
    }
}
