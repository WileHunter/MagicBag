using System.Windows;
using System.Windows.Threading;

namespace WpfApp3
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            Services.ThemeManager.LoadSaved();
            base.OnStartup(e);
        }

        // 防止单个 UI 异常直接导致程序崩溃
        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            HandyControl.Controls.MessageBox.Show($"发生未处理的错误：\n{e.Exception.Message}", "错误",
                MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}
