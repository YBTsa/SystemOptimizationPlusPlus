using Serilog;
using Serilog.Core;

namespace SOPP.Library
{
    public static class AppResources
    {
        public static IReadOnlyList<string> ConfigFiles { get; } = new List<string>
        {
            ".//App//appsettings.json",
            ".//App//appsettings.Development.json"
        }.AsReadOnly();
        private static string LogFilePath { get; } = ".//Logs";
        internal static readonly Logger Logger = new LoggerConfiguration()
           .WriteTo.Console()
           .WriteTo.File(Path.Combine(LogFilePath, "SOPP.log"), rollingInterval: RollingInterval.Day, rollOnFileSizeLimit: true, fileSizeLimitBytes: 1000000).CreateLogger();

    }
}
