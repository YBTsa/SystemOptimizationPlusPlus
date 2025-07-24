using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SOPP.Library
{
    namespace SimpleTools
    {
        /// <summary>
        /// 硬盘使用情况分析器
        /// </summary>
        public class DiskUsageAnalysis
        {
            /// <summary>
            /// 文件信息类，包含文件名、大小和占父目录百分比
            /// </summary>
            public class FileInfoEx
            {
                public string Name { get; set; }
                public string FullPath { get; set; }
                public long Size { get; set; }
                public double ParentPercentage { get; set; }
                public bool IsDirectory { get; set; }
                public DateTime LastModified { get; set; }
            }

            /// <summary>
            /// 扫描进度更新事件
            /// </summary>
            public event Action<FileInfoEx> FileScanned;

            /// <summary>
            /// 扫描完成事件
            /// </summary>
            public event Action ScanCompleted;

            /// <summary>
            /// 错误发生事件（如无权限访问）
            /// </summary>
            public event Action<string, Exception> ErrorOccurred;

            private CancellationTokenSource _cancellationTokenSource;
            private int _maxDegreeOfParallelism = Environment.ProcessorCount * 8;
            private readonly ConcurrentDictionary<string, long> _directorySizes = new();

            /// <summary>
            /// 最大并行度
            /// </summary>
            public int MaxDegreeOfParallelism
            {
                get => _maxDegreeOfParallelism;
                set => _maxDegreeOfParallelism = Math.Max(1, value);
            }

            /// <summary>
            /// 开始异步扫描指定路径
            /// </summary>
            /// <param name="path">要扫描的路径</param>
            /// <returns>扫描任务</returns>
            public async Task StartScanAsync(string path)
            {
                if (!Directory.Exists(path))
                {
                    throw new DirectoryNotFoundException($"路径不存在: {path}");
                }

                _cancellationTokenSource = new CancellationTokenSource();

                // 首先计算所有目录大小（用于百分比计算）
                await CalculateDirectorySizesAsync(path, _cancellationTokenSource.Token);

                // 然后扫描并报告文件信息
                await ScanDirectoryAsync(path, path, _cancellationTokenSource.Token);

                ScanCompleted?.Invoke();
            }

            /// <summary>
            /// 取消当前扫描
            /// </summary>
            public void CancelScan()
            {
                _cancellationTokenSource?.Cancel();
            }

            /// <summary>
            /// 异步计算目录大小
            /// </summary>
            private async Task CalculateDirectorySizesAsync(string path, CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                long totalSize = 0;

                try
                {
                    // 获取目录中的文件
                    var files = Directory.EnumerateFiles(path);

                    // 并行处理文件
                    totalSize = await Task.Run(() =>
                        files.AsParallel()
                             .WithCancellation(cancellationToken)
                             .WithDegreeOfParallelism(MaxDegreeOfParallelism)
                             .Sum(file =>
                             {
                                 try
                                 {
                                     return new FileInfo(file).Length;
                                 }
                                 catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                                          ex is PathTooLongException ||
                                                          ex is IOException)
                                 {
                                     // 捕获并报告无权限等错误，但不中断扫描
                                     ErrorOccurred?.Invoke(file, ex);
                                     return 0L;
                                 }
                             }), cancellationToken);

                    // 获取子目录并递归处理
                    var subDirectories = Directory.EnumerateDirectories(path);

                    foreach (var subDir in subDirectories)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        // 递归计算子目录大小
                        await CalculateDirectorySizesAsync(subDir, cancellationToken);

                        // 累加子目录大小
                        if (_directorySizes.TryGetValue(subDir, out long subDirSize))
                        {
                            totalSize += subDirSize;
                        }
                    }

                    // 存储当前目录总大小
                    _directorySizes[path] = totalSize;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                         ex is PathTooLongException ||
                                         ex is IOException)
                {
                    ErrorOccurred?.Invoke(path, ex);
                    _directorySizes[path] = totalSize;
                }
            }

            /// <summary>
            /// 异步扫描目录并报告文件信息
            /// </summary>
            private async Task ScanDirectoryAsync(string path, string parentPath, CancellationToken cancellationToken)
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                try
                {
                    // 报告当前目录信息
                    if (_directorySizes.TryGetValue(path, out long dirSize))
                    {
                        if (_directorySizes.TryGetValue(parentPath, out long parentSize) && parentSize > 0)
                        {
                            var dirInfo = new DirectoryInfo(path);
                            FileScanned?.Invoke(new FileInfoEx
                            {
                                Name = dirInfo.Name,
                                FullPath = dirInfo.FullName,
                                Size = dirSize,
                                ParentPercentage = (double)dirSize / parentSize * 100,
                                IsDirectory = true,
                                LastModified = dirInfo.LastWriteTime
                            });
                        }
                    }

                    // 处理文件
                    var files = Directory.EnumerateFiles(path);

                    // 并行处理文件并报告
                    await Task.Run(() =>
                    {
                        Parallel.ForEach(files,
                            new ParallelOptions
                            {
                                CancellationToken = cancellationToken,
                                MaxDegreeOfParallelism = MaxDegreeOfParallelism
                            },
                            file =>
                            {
                                try
                                {
                                    var fileInfo = new FileInfo(file);
                                    long fileSize = fileInfo.Length;

                                    double percentage = 0;
                                    if (_directorySizes.TryGetValue(path, out long dirSize) && dirSize > 0)
                                    {
                                        percentage = (double)fileSize / dirSize * 100;
                                    }

                                    // 触发文件扫描完成事件，用于动态加载
                                    FileScanned?.Invoke(new FileInfoEx
                                    {
                                        Name = fileInfo.Name,
                                        FullPath = fileInfo.FullName,
                                        Size = fileSize,
                                        ParentPercentage = percentage,
                                        IsDirectory = false,
                                        LastModified = fileInfo.LastWriteTime
                                    });
                                }
                                catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                                         ex is PathTooLongException ||
                                                         ex is IOException)
                                {
                                    ErrorOccurred?.Invoke(file, ex);
                                }
                            });
                    }, cancellationToken);

                    // 处理子目录
                    var subDirectories = Directory.EnumerateDirectories(path);

                    // 为了控制内存使用，我们串行处理子目录，但每个子目录的处理是并行的
                    foreach (var subDir in subDirectories)
                    {
                        if (cancellationToken.IsCancellationRequested)
                            return;

                        await ScanDirectoryAsync(subDir, path, cancellationToken);
                    }
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException ||
                                         ex is PathTooLongException ||
                                         ex is IOException)
                {
                    ErrorOccurred?.Invoke(path, ex);
                }
            }

            /// <summary>
            /// 将字节大小转换为易读格式
            /// </summary>
            public static string FormatSize(long bytes)
            {
                string[] sizes = ["B", "KB", "MB", "GB", "TB"];
                int order = 0;

                while (bytes >= 1024 && order < sizes.Length - 1)
                {
                    order++;
                    bytes /= 1024;
                }

                return $"{bytes:0.##} {sizes[order]}";
            }
        }
    }
}
