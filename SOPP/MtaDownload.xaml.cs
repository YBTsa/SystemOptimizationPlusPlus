using SOPP.Library.SimpleTools;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using MessageBox = System.Windows.MessageBox;

namespace SOPP
{
    /// <summary>
    /// MtaDownload.xaml 的交互逻辑
    /// </summary>
    public partial class MtaDownload : Wpf.Ui.Controls.FluentWindow
    {
        private readonly Regex UrlRegex = UriRegexBase();
        private MultiThreadDowloader downloader = new(8);
        private string savePath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private string uri = "";
        public MtaDownload()
        {
            InitializeComponent();
            saveTBox.Text = savePath;
        }

        private void NumberBox_ValueChanged(object sender, Wpf.Ui.Controls.NumberBoxValueChangedEventArgs args)
        {
            downloader = new(args.NewValue is null ? 1 : (int)args.NewValue);
        }
        private async void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            DownloadButton.IsEnabled = false;
            pBar.Dispatcher.Invoke(() => pBar.Value = 0);
            uri = uriTBox.Text;
            MultiThreadDowloader.DownloadItem item = new()
            {
                Url = uri,
                SavePath = savePath,
                FileName = System.IO.Path.GetFileName(uri),
            };
            Progress<int> progress = new(value =>
            {
                pBar.Dispatcher.Invoke(() => pBar.Value = value);
                dsTBox.Dispatcher.Invoke(() => dsTBox.Text = value.ToString() + "%");
                if (value == 100)
                {
                    pBar.Dispatcher.Invoke(() =>
                    {
                        pBar.Value = 0;
                        pBar.IsIndeterminate = true;
                    });
                    dsTBox.Dispatcher.Invoke(() => dsTBox.Text = "下载完成,正在合并文件...");
                }
            });
            await downloader.DownloadAsync(item, progress);
            pBar.Dispatcher.Invoke(() => pBar.IsIndeterminate = false);
            dsTBox.Dispatcher.Invoke(() => dsTBox.Text = "下载完成");
            CheckBeforeDownload();
        }
        private void saveButton_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog dialog = new()
            {
                ShowNewFolderButton = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                savePath = dialog.SelectedPath;
                saveTBox.Text = savePath;
            }
        }

        [GeneratedRegex(@"^(?:http|https)://(?:(?:[A-Z0-9](?:[A-Z0-9-]{0,61}[A-Z0-9])?\.)+(?:[A-Z]{2,6}\.?|[A-Z0-9-]{2,}\.?)|localhost|\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3})(?::\d+)?(?:/?|[/?]\S+)$", RegexOptions.IgnoreCase, "zh-CN")]
        private static partial Regex UriRegexBase();

        private void uriTBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            CheckBeforeDownload();
        }
        private void CheckBeforeDownload()
        {
            if (string.IsNullOrWhiteSpace(uriTBox.Text) || string.IsNullOrWhiteSpace(savePath))
            {
                errorTBox.Text = "请填写网址和保存路径";
                return;
            }
            if (!UrlRegex.IsMatch(uriTBox.Text))
            {
                errorTBox.Text = "网址格式错误";
                return;
            }
            if (!System.IO.Directory.Exists(savePath))
            {
                errorTBox.Text = "保存路径不存在";
                return;
            }
            dsTBox.Text = "可以下载...";
            errorTBox.Text = "";
            DownloadButton.IsEnabled = true;
        }
    }
}