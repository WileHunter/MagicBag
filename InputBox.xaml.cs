using System;
using System.Windows;
using System.Windows.Input;

namespace WpfApp3
{
    public partial class InputBox : Window
    {
        private Func<string, string?>? _validator;

        public string InputValue
        {
            get => Boxx.Text;
            set => Boxx.Text = value;
        }

        public InputBox(string title, string? message = null)
        {
            InitializeComponent();
            TitleTextBlock.Text = title;
            if (!string.IsNullOrEmpty(message))
            {
                MessageTextBlock.Text = message;
                MessageTextBlock.Visibility = Visibility.Visible;
            }
            Loaded += (_, _) =>
            {
                Boxx.Focus();
                Boxx.SelectAll();
            };
        }

        /// <summary>
        /// 弹出模态输入框。确定时返回去除首尾空白后的文本，取消时返回 null。
        /// validator 返回非空字符串时表示校验失败，并在对话框内显示该提示。
        /// </summary>
        public static string? Prompt(DependencyObject? ownerSource, string title, string? initial = null,
                                     Func<string, string?>? validator = null, string? message = null)
        {
            var box = new InputBox(title, message)
            {
                _validator = validator
            };
            box.InputValue = initial ?? string.Empty;

            var owner = ownerSource != null ? GetWindow(ownerSource) : Application.Current?.MainWindow;
            if (owner != null && owner.IsVisible)
                box.Owner = owner;
            else
                box.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            return box.ShowDialog() == true ? box.InputValue.Trim() : null;
        }

        private void Done_Button_Click(object sender, RoutedEventArgs e)
        {
            var value = Boxx.Text.Trim();
            var error = string.IsNullOrEmpty(value) ? "名称不能为空。" : _validator?.Invoke(value);
            if (!string.IsNullOrEmpty(error))
            {
                ErrorTextBlock.Text = error;
                ErrorTextBlock.Visibility = Visibility.Visible;
                Boxx.Focus();
                return;
            }
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void Boxx_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ErrorTextBlock.Visibility = Visibility.Collapsed;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }
    }
}
