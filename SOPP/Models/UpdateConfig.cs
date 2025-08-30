using System.IO;
using System.Text.Json;

namespace SOPP.Models
{
    public record class UpdateConfig
    {
        public string UpdateUrl { get; set; }
        public string Version { get; set; }
        public string Description { get; set; }
        public string UpdateConfigUrl { get; set; }
        public string ReleaseDate { get; set; }
        public bool IsPreRelease { get; set; }

        public static UpdateConfig? Instance { get; set; } = _instance?.Value;
        private static readonly Lazy<UpdateConfig?> _instance = new(() => Load().GetAwaiter().GetResult());
        private static string ConfigFilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "update_config.json");
        private static async Task<UpdateConfig?> Load(bool throwIfNotFound = false)
        {
            if (!File.Exists(ConfigFilePath))
            {
                if (throwIfNotFound)
                {
                    await Logger.WriteLogAsync(Helpers.LogLevel.Error, "[Updater] update_config.json not found");
                    throw new FileNotFoundException("update_config.json not found");
                }
                else
                {
                    await Logger.WriteLogAsync(Helpers.LogLevel.Error, "[Updater] update_config.json not found");
                    return null;
                }
            }
            string json = await File.ReadAllTextAsync(ConfigFilePath);
            return JsonSerializer.Deserialize<UpdateConfig>(json);
        }
    }
}
