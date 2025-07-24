using SOPP.Library;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Controls;

namespace SOPP
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : FluentWindow
    {
        public Settings currentAppSettings = Settings.Load();
        public MainWindow()
        {
            InitializeComponent();
            Init();
        }
        private async void Init()
        {
            await Task.Delay(100);
            LoadPage(new HomePage());
            ProgressShower.Visibility = Visibility.Collapsed;
            MainDock.IsEnabled = true;
        }
        private void LoadPage(Page page)
        {
            if (PFrame.CanGoBack)
            {
                PFrame.RemoveBackEntry();
            }
            PFrame.Navigate(page);
        }

        private void HomeButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(new HomePage());
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(new AboutPage());
        }

        private void ToolsButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(new ToolsPage());
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {
            LoadPage(new SettingsPage());
        }
    }
}
