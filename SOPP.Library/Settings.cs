using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SOPP.Library
{
    public class Settings
    {
        public string LogFilePath { get; set; } = ".//logs//log.txt";
        public bool IsLoggingEnabled { get; set; } = true;
        public bool IsAutoSaveEnabled { get; set; } = false;
        public bool IsAutoSaveOnExitEnabled { get; set; } = false;
        public Settings()
        {
        }
        public Settings(string logFilePath, bool isLoggingEnabled, bool isAutoSaveEnabled, bool isAutoSaveOnExitEnabled)
        {
            LogFilePath = logFilePath;
            IsLoggingEnabled = isLoggingEnabled;
            IsAutoSaveEnabled = isAutoSaveEnabled;
            IsAutoSaveOnExitEnabled = isAutoSaveOnExitEnabled;
        }
        public void SetLogFilePath(string logFilePath) => LogFilePath = logFilePath;
        public void SetIsLoggingEnabled(bool isLoggingEnabled) => IsLoggingEnabled = isLoggingEnabled;
        public void SetIsAutoSaveEnabled(bool isAutoSaveEnabled) => IsAutoSaveEnabled = isAutoSaveEnabled;
        public void SetIsAutoSaveOnExitEnabled(bool isAutoSaveOnExitEnabled) => IsAutoSaveOnExitEnabled = isAutoSaveOnExitEnabled;
        public void Save()
        {
            if (!File.Exists(GetDefaultSettingsFilePath()))
            {
                try
                {
                    File.Create(GetDefaultSettingsFilePath()).Dispose();
                }
                catch (Exception ex)
                {
                    throw new Exception("Error while creating settings file: " + ex.Message);
                }
            }
            string settingsJson = JsonConvert.SerializeObject(this, Formatting.Indented, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            File.WriteAllText(GetDefaultSettingsFilePath(), settingsJson);
        }
        public static string GetDefaultSettingsFilePath() => ".//settings.json";
        public static string Create()
        {
            Settings settings = new();
            settings.Save();
            return GetDefaultSettingsFilePath();
        }
        public static Settings Load()
        {
            if (!File.Exists(GetDefaultSettingsFilePath())) Create();
            try
            {
                string settingsJson = File.ReadAllText(GetDefaultSettingsFilePath());
                Settings settings = JsonConvert.DeserializeObject<Settings>(settingsJson)!;
                return settings;
            }
            catch (Exception ex)
            {
                throw new Exception("Error while loading settings file: " + ex.Message);
            }
        }
    }
}
