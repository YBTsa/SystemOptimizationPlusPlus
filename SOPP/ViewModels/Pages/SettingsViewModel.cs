using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance;

namespace SOPP.ViewModels.Pages
{
    public partial class SettingsViewModel : ObservableObject, INavigationAware
    {
        private bool _isInitialized = false;

        [ObservableProperty]
        private string _appVersion = String.Empty;

        [ObservableProperty]
        private ApplicationTheme _currentTheme = ApplicationTheme.Unknown;

        public Task OnNavigatedToAsync()
        {
            if (!_isInitialized)
                InitializeViewModel();

            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void InitializeViewModel()
        {
            Logger.WriteLog(Helpers.LogLevel.Info, "Initializing Settings...Getting App Version and Current Theme");
            CurrentTheme = ApplicationThemeManager.GetAppTheme();
            AppVersion = $"SOPP - {GetAssemblyVersion()}";

            _isInitialized = true;
            Logger.WriteLog(Helpers.LogLevel.Info, "Settings Initialized");
        }

        private string GetAssemblyVersion()
        {
            Logger.WriteLog(Helpers.LogLevel.Info, "Getting Assembly Version");
            return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString()
                ?? String.Empty;
        }

        [RelayCommand]
        private void OnChangeTheme(string parameter)
        {
            Logger.WriteLog(Helpers.LogLevel.Info, "Changing Theme");
            switch (parameter)
            {
                case "theme_light":
                    if (CurrentTheme == ApplicationTheme.Light)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Light);
                    CurrentTheme = ApplicationTheme.Light;

                    break;

                default:
                    if (CurrentTheme == ApplicationTheme.Dark)
                        break;

                    ApplicationThemeManager.Apply(ApplicationTheme.Dark);
                    CurrentTheme = ApplicationTheme.Dark;

                    break;
            }
        }
    }
}
