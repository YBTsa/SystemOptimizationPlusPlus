using System.Diagnostics;
using System.Management;
using System.Net.NetworkInformation;

namespace SOPP.Helpers
{
    public class SystemMonitor
    {
        private readonly PerformanceCounter cpuCounter;
        private readonly PerformanceCounter diskCounter;
        public SystemMonitor()
        {
            Logger.WriteLog(LogLevel.Info, "[SystemMonitor] System Monitor initializing...");
            cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            diskCounter = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total");
        }
        public int GetCpuUsage()
        {
            return (int)cpuCounter.NextValue();
        }

        public int GetRamUsage()
        {
            var wql = new ObjectQuery("SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
            var searcher = new ManagementObjectSearcher(wql);
            foreach (ManagementObject queryObj in searcher.Get().Cast<ManagementObject>())
            {
                var freeMemory = Convert.ToUInt64(queryObj["FreePhysicalMemory"]);
                var totalMemory = Convert.ToUInt64(queryObj["TotalVisibleMemorySize"]);
                var usedMemory = totalMemory - freeMemory;
                return (int)((usedMemory * 100) / totalMemory);
            }
            return 0;
        }

        public int GetDiskUsage()
        {
            return (int)Math.Min(diskCounter.NextValue(), 100);
        }

        public async Task<int> GetNetworkDownloadUsage()
        {
            var firstBytes = GetTotalBytesReceived(); // Get total bytes received at a point in time
            await Task.Delay(250); // Non-blocking delay
            var secondBytes = GetTotalBytesReceived(); // Get total bytes received after the 500ms
            return (int)(secondBytes - firstBytes); // Convert Bytes to KB
        }

        // Similar to GetNetworkDownloadUsage but for upload
        public async Task<int> GetNetworkUploadUsage()
        {
            var firstBytes = GetTotalBytesSent();
            await Task.Delay(250); // Non-blocking delay
            var secondBytes = GetTotalBytesSent();
            return (int)(secondBytes - firstBytes);
        }

        // Get total bytes received by all network interfaces
        private long GetTotalBytesReceived()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Sum(ni => ni.GetIPv4Statistics().BytesReceived);
        }

        // Same as GetTotalBytesReceived but for sent bytes
        private long GetTotalBytesSent()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
                .Sum(ni => ni.GetIPv4Statistics().BytesSent);
        }

        public int GetProcessesCount()
        {
            return Process.GetProcesses().Length;
        }
        public async Task<int> GetGpuUsageAsync()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                var counterNames = category.GetInstanceNames();
                var gpuCounters = new List<PerformanceCounter>();
                var result = 0f;

                foreach (var counterName in counterNames)
                {
                    if (counterName.EndsWith("engtype_3D"))
                    {
                        foreach (var counter in category.GetCounters(counterName))
                        {
                            if (counter.CounterName == "Utilization Percentage")
                            {
                                gpuCounters.Add(counter);
                            }
                        }
                    }
                }

                gpuCounters.ForEach(x =>
                {
                    _ = x.NextValue();
                });
                await Task.Delay(1000);
                gpuCounters.ForEach(x =>
                {
                    result += x.NextValue();
                });
                return (int)result;
            }
            catch
            {
                return 0;
            }
        }
    }
}
