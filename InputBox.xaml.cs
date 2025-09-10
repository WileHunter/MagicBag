using System;
using System.Windows;
using System.Windows.Controls;

namespace WpfApp3
{
    // 定义一个枚举来区分弹窗的用途
    public enum InputBoxMode
    {
        NewNote,    // 新建笔记：需要输入文件名
        DeleteNote  // 删除笔记：需要确认，无需输入文件名
    }

    public partial class InputBox : Window
    {
        public string InputValue
        {
            get { return Boxx.Text; }
            set { this.Boxx.Text = value; }
        }

        public event EventHandler accept;

        private InputBoxMode _currentMode;

        // 构造函数用于新建笔记 (需要输入)
        public InputBox(InputBoxMode mode, string title)
        {
            InitializeComponent();
            _currentMode = mode;
            TitleTextBlock.Text = title;

            // 根据模式调整UI
            if (mode == InputBoxMode.DeleteNote)
            {
                Boxx.Visibility = Visibility.Collapsed; // 隐藏输入框
                MessageTextBlock.Visibility = Visibility.Visible; // 显示消息
            }
            else // NewNote 模式
            {
                Boxx.Visibility = Visibility.Visible;
                MessageTextBlock.Visibility = Visibility.Collapsed;
            }
        }

        // 当需要额外传递自定义消息时（例如删除时要显示具体笔记名称）
        public InputBox(InputBoxMode mode, string title, string message) : this(mode, title)
        {
            if (mode == InputBoxMode.DeleteNote && !string.IsNullOrEmpty(message))
            {
                MessageTextBlock.Text = message; // 使用自定义消息
            }
        }

        private void Done_Button_Click(object sender, RoutedEventArgs e)
        {
            if (_currentMode == InputBoxMode.NewNote)
            {
                if (string.IsNullOrWhiteSpace(Boxx.Text))
                {
                    MessageBox.Show("文件名不能为空。", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }

            if (accept != null)
            {
                accept(this, EventArgs.Empty);
            }
            this.Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}