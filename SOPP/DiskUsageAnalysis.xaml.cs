using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Threading;
using Wpf.Ui.Controls;
using MessageBoxButton = System.Windows.MessageBoxButton;
using Path = System.IO.Path;

namespace SOPP
{
    public class FileItem
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public long Size { get; set; }
        public double ParentPercentage { get; set; }
        public bool IsDirectory { get; set; }
        public ObservableCollection<FileItem> Children { get; set; } = [];
    }
    /// <summary>
    /// DiskUsageAnalysis.xaml 的交互逻辑
    /// </summary>
    public partial class DiskUsageAnalysis : FluentWindow
    {
        private readonly Library.SimpleTools.DiskUsageAnalysis _diskAnalyzer;
        private readonly Dictionary<string, FileItem> _fileItems = [];
        private bool _isScanning = false;

        // 树形结构数据项

        public DiskUsageAnalysis()
        {
            InitializeComponent();
            _diskAnalyzer = new Library.SimpleTools.DiskUsageAnalysis();
            _diskAnalyzer.FileScanned += OnFileScanned;
            _diskAnalyzer.ScanCompleted += OnScanCompleted;
            _diskAnalyzer.ErrorOccurred += OnErrorOccurred;

            Closing += (sender, e) =>
            {
                if (_isScanning)
                    _diskAnalyzer.CancelScan();
            };
        }

        private void StartScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isScanning) return;

            string path = PathTextBox.Text.Trim();
            if (!Directory.Exists(path))
            {
                System.Windows.MessageBox.Show("请输入有效的目录路径", "路径无效", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // 重置状态
            ResetScanState();

            // 更新状态
            _isScanning = true;
            UpdateScanControls(true);
            StatusTextBlock.Text = $"正在扫描: {path}";

            // 开始扫描
            _ = _diskAnalyzer.StartScanAsync(path);
        }

        private void CancelScanButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isScanning) return;

            _diskAnalyzer.CancelScan();
            StatusTextBlock.Text = "扫描已取消";
            UpdateScanControls(false);
        }

        private void OnFileScanned(Library.SimpleTools.DiskUsageAnalysis.FileInfoEx fileInfo)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // 创建文件项
                    var fileItem = new FileItem
                    {
                        Name = fileInfo.Name,
                        FullPath = fileInfo.FullPath,
                        Size = fileInfo.Size,
                        ParentPercentage = fileInfo.ParentPercentage,
                        IsDirectory = fileInfo.IsDirectory
                    };

                    _fileItems[fileInfo.FullPath] = fileItem;

                    // 获取父路径
                    string parentPath = Path.GetDirectoryName(fileInfo.FullPath)!;

                    if (!string.IsNullOrEmpty(parentPath) && _fileItems.TryGetValue(parentPath, out var parentItem))
                    {
                        // 添加到父节点
                        parentItem.Children.Add(fileItem);
                    }
                    else if (fileInfo.IsDirectory)
                    {
                        // 添加到根节点
                        FileTreeViewControl.Items.Add(fileItem);
                    }
                }
                catch (Exception ex)
                {
                    ErrorTextBlock.Text = $"处理错误: {ex.Message}";
                }
            });
        }

        private void OnScanCompleted()
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.Text = "扫描完成";
                UpdateScanControls(false);
            });
        }

        private void OnErrorOccurred(string path, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                ErrorTextBlock.Text = ex is UnauthorizedAccessException
                    ? $"无权限: {Path.GetFileName(path)}"
                    : $"错误: {Path.GetFileName(path)}";
            });
        }

        // 重置扫描状态
        private void ResetScanState()
        {
            FileTreeViewControl.Items.Clear();
            _fileItems.Clear();
            ErrorTextBlock.Text = "";
        }

        // 更新扫描控件状态
        private void UpdateScanControls(bool isScanning)
        {
            _isScanning = isScanning;
            StartScanButton.IsEnabled = !isScanning;
            CancelScanButton.IsEnabled = isScanning;
            ScanProgressBar.Visibility = isScanning ? Visibility.Visible : Visibility.Collapsed;
            ScanProgressBar.IsIndeterminate = isScanning;
        }

        private void FluentWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_isScanning) return;

            _diskAnalyzer.CancelScan();
            StatusTextBlock.Text = "扫描已取消";
            UpdateScanControls(false);
        }
    }

    // 文件大小转换器
    public class FileSizeConverter : MarkupExtension, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is long size)
            {
                return Library.SimpleTools.DiskUsageAnalysis.FormatSize(size);
            }
            return "0 B";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            return this;
        }
    }
}
