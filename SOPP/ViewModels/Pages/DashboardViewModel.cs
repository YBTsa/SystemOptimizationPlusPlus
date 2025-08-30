using SOPP.Helpers;

namespace SOPP.ViewModels.Pages;

public partial class DashboardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _cpuUsage;
    [ObservableProperty]
    private int _memoryUsage;
    [ObservableProperty]
    private int _networkUploadUsage;
    [ObservableProperty]
    private int _networkDownloadUsage;
    [ObservableProperty]
    private int _diskUsage;
    [ObservableProperty]
    private int _totalProcessCount;
    [ObservableProperty]
    private int gpuUsage;
    private readonly SystemMonitor systemMonitor = new();
    // 标记是否已启动，避免重复执行
    private bool _isUpdating;

    /// <summary>
    /// 启动数据更新（在UI线程调用，确保首次执行不阻塞）
    /// </summary>
    public void StartUpdating()
    {
        if (_isUpdating) return;
        _isUpdating = true;
        // 用Task.Run将整个循环移至后台线程，避免阻塞UI
        _ = Task.Run(UpdateUsage).ConfigureAwait(false);
    }

    private async Task UpdateUsage()
    {
        // 首次执行前短暂延迟，让UI先完成加载
        await Task.Delay(250).ConfigureAwait(false);

        while (_isUpdating) // 用_isUpdating控制循环退出
        {
            await Task.Delay(500).ConfigureAwait(false);
            try
            {
                // 1. 将同步耗时操作包装到Task.Run，移至线程池执行
                var (cpu, memory, disk, processCount) = await Task.Run(() => (
                    systemMonitor.GetCpuUsage(),
                    systemMonitor.GetRamUsage(),
                    systemMonitor.GetDiskUsage(),
                    systemMonitor.GetProcessesCount()
                )).ConfigureAwait(false);

                // 2. 并行执行异步指标（保持不变）
                int[] all = await Task.WhenAll(
                    systemMonitor.GetNetworkUploadUsage(),
                    systemMonitor.GetNetworkDownloadUsage(),
                    systemMonitor.GetGpuUsageAsync()
                ).ConfigureAwait(false);

                // 3. 回到UI线程更新属性（确保绑定刷新在UI线程）
                await App.Current.Dispatcher.InvokeAsync(() =>
                {
                    CpuUsage = cpu;
                    MemoryUsage = memory;
                    DiskUsage = disk;
                    TotalProcessCount = processCount;
                    NetworkUploadUsage = all[0];
                    NetworkDownloadUsage = all[1];
                    GpuUsage = all[2];
                });
            }
            catch (Exception ex)
            {
                Logger.WriteLog(LogLevel.Error, $"[DashboardViewModel] Update usage failed: {ex.Message}");
                await Task.Delay(1000).ConfigureAwait(false);
            }
        }
    }

    // 页面卸载时调用，停止更新循环
    public void StopUpdating()
    {
        _isUpdating = false;
    }
}