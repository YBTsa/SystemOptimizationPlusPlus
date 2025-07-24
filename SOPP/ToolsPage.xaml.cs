using System.Windows;
using System.Windows.Controls;

namespace SOPP
{
    /// <summary>
    /// ToolsPage.xaml 的交互逻辑
    /// </summary>
    public partial class ToolsPage : Page
    {
        public ToolsPage()
        {
            InitializeComponent();
        }

        private void mtadButton_Click(object sender, RoutedEventArgs e)
        {
            new MtaDownload().ShowDialog();
        }

        private void simButton_Click(object sender, RoutedEventArgs e)
        {
            new StartupItemsManager().ShowDialog();
        }

        private void duaButton_Click(object sender, RoutedEventArgs e)
        {
            new DiskUsageAnalysis().ShowDialog();
        }
    }
}
