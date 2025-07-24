using SOPP.Library.SimpleTools;
using System.Collections.ObjectModel;
using System.Windows;
using Wpf.Ui.Controls;
using Wpf.Ui.Input;

namespace SOPP
{
    /// <summary>
    /// StartupItemsManager.xaml 的交互逻辑
    /// </summary>
    public partial class StartupItemsManager : FluentWindow
    {
        private readonly StartupItemManager _startupManager = new();

        // 启动项集合
        public ObservableCollection<StartupItem> StartupItems { get; set; } = [];

        // 删除命令
        public RelayCommand<StartupItem?> DeleteCommand { get; set; }

        public StartupItemsManager()
        {
            InitializeComponent();

            // 初始化命令
            DeleteCommand = new RelayCommand<StartupItem?>(DeleteStartupItem);

            // 设置数据上下文
            DataContext = this;

            // 加载启动项
            LoadStartupItems();
        }

        // 加载启动项
        private void LoadStartupItems()
        {
            try
            {
                StatusText.Text = "正在加载启动项...";

                // 清空现有数据
                StartupItems.Clear();

                // 获取所有启动项
                var items = _startupManager.GetAllStartupItems();

                // 添加到集合
                foreach (var item in items)
                {
                    StartupItems.Add(item);
                }

                StatusText.Text = $"已加载 {StartupItems.Count} 个启动项";
            }
            catch (UnauthorizedAccessException)
            {
                StatusText.Text = "权限不足，无法获取系统启动项";
                System.Windows.MessageBox.Show("权限错误",
                    "获取系统启动项需要管理员权限。\n\n请以管理员身份运行此程序。",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "加载启动项失败";
                System.Windows.MessageBox.Show("错误",
                    $"加载启动项时发生错误: {ex.Message}",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 删除启动项
        private void DeleteStartupItem(StartupItem? item)
        {
            if (!item.HasValue)
                return;

            try
            {
                // 确认删除
                var result = System.Windows.MessageBox.Show(
                    $"确定要删除启动项 \"{item?.Name}\" 吗?\n\n位置: {item?.LocationName}\n路径: {item?.Path}",
                    "确认删除",
                    System.Windows.MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    StatusText.Text = $"正在删除 \"{item?.Name}\"...";

                    // 删除启动项
                    _startupManager.RemoveStartupItem(item?.Name!, (StartupLocation)(item?.Location!));

                    // 从列表中移除
                    StartupItems.Remove((StartupItem)item);

                    StatusText.Text = $"已成功删除启动项 \"{item?.Name}\"";
                }
            }
            catch (UnauthorizedAccessException)
            {
                StatusText.Text = "权限不足，无法删除启动项";
                System.Windows.MessageBox.Show("权限错误",
                    "删除系统启动项需要管理员权限。\n\n请以管理员身份运行此程序。",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                StatusText.Text = "删除启动项失败";
                System.Windows.MessageBox.Show("错误",
                    $"删除启动项时发生错误: {ex.Message}",
                    System.Windows.MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 刷新按钮点击事件
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            LoadStartupItems();
        }
    }
}
