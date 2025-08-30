using System;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using SOPP.Helpers;
using SOPP.Models;

namespace SOPP.ViewModels.Pages
{
    public class UpdateViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly UpdateData _updateData;
        private readonly HttpClient _httpClient;
        private bool _isDisposed;
        private bool _isCheckingForUpdates;
        private string _statusMessage = "Checking for updates...";
        private bool _isMainContentVisible;
        private string _currentVersion;

        // Current application version
        public string CurrentVersion
        {
            get => _currentVersion;
            private set
            {
                _currentVersion = value;
                OnPropertyChanged();
            }
        }

        // Whether update checking is in progress
        public bool IsCheckingForUpdates
        {
            get => _isCheckingForUpdates;
            set
            {
                _isCheckingForUpdates = value;
                OnPropertyChanged();
                IsMainContentVisible = !value; // Sync main content visibility
            }
        }

        // Status message displayed to user
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                _statusMessage = value;
                OnPropertyChanged();
            }
        }

        // Main content visibility status
        public bool IsMainContentVisible
        {
            get => _isMainContentVisible;
            set
            {
                _isMainContentVisible = value;
                OnPropertyChanged();
            }
        }

        // Command for checking updates manually
        public ICommand CheckForUpdatesCommand { get; }

        public UpdateViewModel()
        {
            CheckForUpdatesCommand = new RelayCommand(CheckForUpdates);
            _httpClient = new HttpClient();
            _updateData = UpdateData.Load();

            // Initialize current version from assembly
            
            _currentVersion = GetCurrentAppVersion();
            _ = Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Current application version: {CurrentVersion}");

            // Auto-check updates on first load
            if (_updateData.IsFirstLoad)
            {
                _ = Logger.WriteLogAsync(LogLevel.Info, "[Updater] First load detected, initiating auto-update check");
                _ = InitializeAsync();
            }
            else
            {
                _ = Logger.WriteLogAsync(LogLevel.Info, "[Updater] Not first load, showing main content directly");
                IsCheckingForUpdates = false;
                IsMainContentVisible = true;
            }
        }

        // Initialize async update check
        private async Task InitializeAsync()
        {
            try
            {
                await Logger.WriteLogAsync(LogLevel.Info, "[Updater] Starting initialization update check");
                IsCheckingForUpdates = true;
                await PerformUpdateCheck();
                _updateData.IsFirstLoad = false;
                _updateData.Save();
                await Logger.WriteLogAsync(LogLevel.Info, "[Updater] Initialization update check completed");
            }
            catch (Exception ex)
            {
                await Logger.WriteLogAsync(LogLevel.Error, $"[Updater] Failed to initialize update check: {ex.Message}");
                StatusMessage = "Failed to initialize update check. Please try again later.";
                IsCheckingForUpdates = false;
            }
        }

        // Manually trigger update check
        public async void CheckForUpdates()
        {
            if (IsCheckingForUpdates)
            {
                await Logger.WriteLogAsync(LogLevel.Warning, "[Updater] Update check already in progress, ignoring duplicate request");
                return;
            }

            await Logger.WriteLogAsync(LogLevel.Info, "[Updater] Manual update check triggered by user");
            IsCheckingForUpdates = true;
            StatusMessage = "Checking for updates...";
            await PerformUpdateCheck();
        }

        // Core logic for update checking
        private async Task PerformUpdateCheck()
        {
            await Logger.WriteLogAsync(LogLevel.Info, "[Updater] Starting update check process");
            try
            {
                if (UpdateConfig.Instance is null)
                {
                    await Logger.WriteLogAsync(LogLevel.Error, "[Updater] Update configuration is not loaded");
                    throw new InvalidOperationException("Update configuration not loaded");
                }

                await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Fetching latest update config from: {UpdateConfig.Instance.UpdateConfigUrl}");
                UpdateConfig? updateConfig = await _httpClient.GetFromJsonAsync<UpdateConfig>(
                    UpdateConfig.Instance.UpdateConfigUrl);

                if (updateConfig is null)
                {
                    await Logger.WriteLogAsync(LogLevel.Error, "[Updater] Received null update config from server");
                    throw new NullReferenceException("Server returned null update configuration");
                }

                await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Latest version from server: {updateConfig.Version}");
                // Version comparison
                if (HasNewVersion(CurrentVersion, updateConfig.Version))
                {
                    await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] New version available: {updateConfig.Version} (Current: {CurrentVersion})");
                    await HandleNewVersion(updateConfig);
                }
                else
                {
                    await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] No new version available. Current version is latest: {CurrentVersion}");
                    StatusMessage = $"You're already using the latest version (v{CurrentVersion})";
                    await Task.Delay(1500);
                }
            }
            catch (HttpRequestException)
            {
                await Logger.WriteLogAsync(LogLevel.Error, "[Updater] Network request failed during update check");
                StatusMessage = "Failed to check for updates. Please check your internet connection.";
            }
            catch (Exception ex)
            {
                await Logger.WriteLogAsync(LogLevel.Error, $"[Updater] Unexpected error during update check: {ex.Message}");
                StatusMessage = "Failed to check for updates. Please try again later.";
            }
            finally
            {
                await Logger.WriteLogAsync(LogLevel.Info, "[Updater] Update check process completed");
                IsCheckingForUpdates = false;
            }
        }

        // Handle new version found scenario
        private async Task HandleNewVersion(UpdateConfig config)
        {
            await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Starting processing for new version: {config.Version}");
            StatusMessage = $"New version v{config.Version} found\n{config.Description}";
            await Task.Delay(2000);

            // Simulate download progress (replace with actual download logic)
            await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Starting download for version {config.Version}");
            for (int i = 0; i <= 100; i++)
            {
                StatusMessage = $"Downloading update... {i}%";
                if (i % 10 == 0) // Log every 10% to reduce noise
                {
                    await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Download progress: {i}%");
                }
                await Task.Delay(50);
            }

            // Simulate installation process
            await Logger.WriteLogAsync(LogLevel.Info, "[Updater] Download completed, starting file extraction");
            StatusMessage = "Update downloaded! Extracting files...";
            await Task.Delay(2000);

            await Logger.WriteLogAsync(LogLevel.Info, $"[Updater] Update {config.Version} installed successfully");
            StatusMessage = $"Update installed! Please restart the application to use v{config.Version}";
            await Task.Delay(3000);
        }

        // Version comparison: check if new version is available
        private static bool HasNewVersion(string currentVersion, string latestVersion)
        {
            if (!Version.TryParse(currentVersion, out var current))
            {
                throw new ArgumentException($"Invalid current version format: {currentVersion}");
            }

            if (!Version.TryParse(latestVersion, out var latest))
            {
                throw new ArgumentException($"Invalid latest version format: {latestVersion}");
            }

            return latest > current;
        }

        // Get current application version from assembly
        private string GetCurrentAppVersion()
        {
            try
            {
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown version";
                return version;
            }
            catch (Exception ex)
            {
                _ = Logger.WriteLogAsync(LogLevel.Error, $"[Updater] Failed to get current version: {ex.Message}");
                return "Unknown version";
            }
        }

        // Implement IDisposable to release resources
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;
            if (disposing)
            {
                _httpClient?.Dispose();
                _ = Logger.WriteLogAsync(LogLevel.Info, "[Updater] HttpClient disposed");
            }
            _isDisposed = true;
        }

        ~UpdateViewModel() => Dispose(false);

        // INotifyPropertyChanged implementation
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}