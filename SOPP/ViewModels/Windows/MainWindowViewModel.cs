using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace SOPP.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "SOPP";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems =
        [
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Home24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem(){
                Content = "Tools",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Toolbox24},
                MenuItemsSource = new object[]{
                    new NavigationViewItem(){
                        Content = "Downloader",
                        Icon = new SymbolIcon { Symbol = SymbolRegular.ArrowDown24 },
                        TargetPageType=typeof(Views.Pages.DownloaderPage) 
                    }
                }
            }
            
        ];

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems =
        [
            new NavigationViewItem(){
                Content = "Update",
                Icon = new SymbolIcon {Symbol = SymbolRegular.DualScreenUpdate24},
                TargetPageType = typeof(Views.Pages.UpdatePage)
            },
            new NavigationViewItemSeparator(),
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        ];

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems =
        [
            new MenuItem { Header = "Home", Tag = "tray_home" }
        ];
    }

}
