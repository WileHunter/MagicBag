using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WpfApp3.Services;

namespace WpfApp3
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{AppPaths.BaseDir}\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Toast.Error(ex.Message);
            }
        }
    }
}
