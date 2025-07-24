namespace SOPP.Library
{
    namespace SimpleTools
    {
        public class MultiThreadDowloader : IDisposable
        {
            private readonly int _maxThreads;
            private readonly HttpClient _httpClient = new();
            private readonly SemaphoreSlim _threadSemaphore;
            private bool _isDisposed;

            public struct DownloadItem
            {
                public string Url;
                public string FileName;
                public string SavePath;
                public DownloadStatus Status;
                public DateTime DownloadedTime;
                public string? ErrorMessage;
            }

            public enum DownloadStatus
            {
                Downloading,
                Downloaded,
                Failed
            }

            public MultiThreadDowloader(int maxThreads)
            {
                if (maxThreads <= 0)
                {
                    _maxThreads = Environment.ProcessorCount;
                }
                else
                {
                    _maxThreads = maxThreads == 1 ? 2 : maxThreads;
                }
                _threadSemaphore = new SemaphoreSlim(_maxThreads);
            }

            public async Task DownloadAsync(DownloadItem item, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
            {
                try
                {
                    // 检查URL是否支持分块下载
                    var supportsRange = await CheckRangeSupportAsync(item.Url, cancellationToken);

                    if (supportsRange)
                    {
                        await DownloadWithMultipleThreadsAsync(item, progress, cancellationToken);
                    }
                    else
                    {
                        await DownloadWithSingleThreadAsync(item, progress, cancellationToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage = "Download canceled";
                    throw;
                }
                catch (Exception ex)
                {
                    item.Status = DownloadStatus.Failed;
                    item.ErrorMessage = ex.Message;
                    throw new InvalidOperationException($"Download failed: {ex.Message}", ex);
                }
            }

            private async Task<bool> CheckRangeSupportAsync(string url, CancellationToken cancellationToken)
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                return response.Headers.AcceptRanges.Contains("bytes");
            }

            private async Task DownloadWithSingleThreadAsync(DownloadItem item, IProgress<int>? progress, CancellationToken cancellationToken)
            {
                using var response = await _httpClient.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
                var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var buffer = new byte[8192];
                var totalBytesRead = 0L;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    totalBytesRead += bytesRead;

                    if (progress != null && totalBytes > 0)
                    {
                        progress.Report((int)(totalBytesRead * 100 / totalBytes));
                    }
                }

                MoveTempFileToFinalPath(tempFilePath, Path.Combine(item.SavePath, item.FileName));
                item.Status = DownloadStatus.Downloaded;
                item.DownloadedTime = DateTime.Now;
            }

            private async Task DownloadWithMultipleThreadsAsync(DownloadItem item, IProgress<int>? progress, CancellationToken cancellationToken)
            {
                using var response = await _httpClient.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength.GetValueOrDefault();
                var segmentSize = (long)Math.Ceiling((double)totalBytes / _maxThreads);

                var tasks = new List<Task>();
                var tempFiles = new List<string>();
                var progressValues = new double[_maxThreads];
                var progressTimer = new System.Timers.Timer(500); // 每500ms更新一次进度

                if (progress != null)
                {
                    progressTimer.Elapsed += (sender, e) =>
                    {
                        var totalProgress = (int)(progressValues.Sum() / _maxThreads);
                        progress.Report(totalProgress);
                    };
                    progressTimer.Start();
                }

                try
                {
                    for (var i = 0; i < _maxThreads; i++)
                    {
                        var start = i * segmentSize;
                        var end = Math.Min(start + segmentSize - 1, totalBytes - 1);

                        if (start > totalBytes)
                        {
                            break;
                        }

                        var segmentNumber = i;
                        var tempFilePath = Path.Combine(Path.GetTempPath(), $"{Path.GetFileNameWithoutExtension(item.FileName)}.part{segmentNumber}");
                        tempFiles.Add(tempFilePath);

                        tasks.Add(DownloadSegmentAsync(item.Url, start, end, tempFilePath,
                            p => progressValues[segmentNumber] = p, cancellationToken));
                    }

                    await Task.WhenAll(tasks);

                    // 确保最后一次进度更新是100%
                    if (progress != null)
                    {
                        progress.Report(100);
                        progressTimer.Stop();
                        progressTimer.Dispose();
                    }

                    await MergeTempFilesAsync(tempFiles, Path.Combine(item.SavePath, item.FileName), cancellationToken);
                    item.Status = DownloadStatus.Downloaded;
                    item.DownloadedTime = DateTime.Now;
                }
                catch
                {
                    // 清理临时文件
                    foreach (var tempFile in tempFiles)
                    {
                        try { File.Delete(tempFile); } catch { /* 忽略删除错误 */ }
                    }

                    throw;
                }
                finally
                {
                    if (progress != null)
                    {
                        progressTimer.Stop();
                        progressTimer.Dispose();
                    }
                }
            }

            private async Task DownloadSegmentAsync(string url, long start, long end, string tempFilePath, Action<double> progressCallback, CancellationToken cancellationToken)
            {
                await _threadSemaphore.WaitAsync(cancellationToken);

                try
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    response.EnsureSuccessStatusCode();

                    var segmentSize = end - start + 1;

                    await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                    await using var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                    var buffer = new byte[8192];
                    var totalBytesRead = 0L;
                    int bytesRead;

                    while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        totalBytesRead += bytesRead;

                        if (progressCallback != null)
                        {
                            progressCallback((double)totalBytesRead * 100 / segmentSize);
                        }
                    }
                }
                finally
                {
                    _threadSemaphore.Release();
                }
            }

            private async Task MergeTempFilesAsync(List<string> tempFiles, string destinationPath, CancellationToken cancellationToken)
            {
                var directory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                await using var outputStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                foreach (var tempFile in tempFiles)
                {
                    // 修复：先关闭文件流再删除文件
                    await using (var inputStream = new FileStream(tempFile, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true))
                    {
                        await inputStream.CopyToAsync(outputStream, 8192, cancellationToken);
                    }

                    // 现在文件流已经关闭，可以安全删除文件
                    try { File.Delete(tempFile); } catch { /* 忽略删除错误 */ }
                }
            }

            private void MoveTempFileToFinalPath(string tempFilePath, string destinationPath)
            {
                var directory = Path.GetDirectoryName(destinationPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                File.Move(tempFilePath, destinationPath, true);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                if (_isDisposed)
                {
                    return;
                }

                if (disposing)
                {
                    _threadSemaphore.Dispose();
                    _httpClient.Dispose(); // 确保HttpClient也被释放
                }

                _isDisposed = true;
            }
        }
    }
}