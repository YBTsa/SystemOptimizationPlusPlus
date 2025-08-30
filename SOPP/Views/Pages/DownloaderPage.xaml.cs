using SOPP.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace SOPP.Views.Pages
{
    /// <summary>
    /// DownloaderPage.xaml 的交互逻辑
    /// </summary>
    public partial class DownloaderPage : INavigableView<DownloaderViewModel>
    {
        public DownloaderViewModel ViewModel{ get; set; }
        public DownloaderPage(DownloaderViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
        }
    }
}
