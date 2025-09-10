using System.Windows;

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

            if (DatabaseViewControl != null)
            {
                DatabaseViewControl.ItemUpdated += OnItemAdded; // 新增
            }
        }

        /// <summary>
        /// 当 AddToolView 添加新工具或分类时，此方法会被调用以刷新 ToolsView。
        /// </summary>
        private void OnItemAdded()
        {
            // 在这里调用 ToolsView 的公共方法来重新加载数据
            ToolsViewControl?.ReloadData();
        }
    }
}