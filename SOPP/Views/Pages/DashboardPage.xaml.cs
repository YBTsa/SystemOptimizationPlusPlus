using SOPP.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace SOPP.Views.Pages
{
    public partial class DashboardPage : INavigableView<DashboardViewModel>
    {
        public DashboardViewModel ViewModel { get; }

        public DashboardPage()
        {
            ViewModel = new DashboardViewModel();
            DataContext = ViewModel;

            InitializeComponent();
            Loaded += GetUsage;
            Unloaded += OnUnloaded;
        }
        private void GetUsage(object sender, RoutedEventArgs e)
        {
            (DataContext as DashboardViewModel)?.StartUpdating();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            (DataContext as DashboardViewModel)?.StopUpdating();
        }
    }
}
