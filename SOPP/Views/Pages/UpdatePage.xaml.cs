using SOPP.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace SOPP.Views.Pages
{
    /// <summary>
    /// UpdatePage.xaml 的交互逻辑
    /// </summary>
    public partial class UpdatePage : INavigableView<UpdateViewModel>
    {
        public UpdateViewModel ViewModel { get; set; }
        public UpdatePage(UpdateViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = ViewModel;
            InitializeComponent();
            Loaded += UpdatePage_Loaded;
        }
        private void UpdatePage_Loaded(object sender, RoutedEventArgs e)
        { 
            ViewModel.CheckForUpdates();
        }
    }
}
