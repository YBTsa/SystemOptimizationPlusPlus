using System.Globalization;
using System.Windows.Data;

namespace SOPP.Helpers
{
    /// <summary>
    /// 转换网络流量数值为合适的单位（B、KB、MB、GB等）
    /// </summary>
    public class NetworkUsageConverter : IValueConverter
    {
        /// <summary>
        /// 将数值转换为带单位的字符串
        /// </summary>
        /// <param name="value">要转换的数值（字节数）</param>
        /// <param name="targetType">目标类型</param>
        /// <param name="parameter">参数（保留小数位数，默认2位）</param>
        /// <param name="culture">文化信息</param>
        /// <returns>格式化后的字符串（如 "2.56 MB"）</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 检查输入值是否有效
            if (value == null || value is not long && value is not double && value is not int)
            {
                return "0 B";
            }

            // 将值转换为双精度浮点数以便计算
            double bytes = System.Convert.ToDouble(value);
            int decimalPlaces = 0;
            if (parameter != null && int.TryParse(parameter.ToString(), out int places))
            {
                decimalPlaces = places;
            }

            // 定义单位和转换因子
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double[] factors = [1, 1024, 1024 * 1024, 1024 * 1024 * 1024, (long)1024 * 1024 * 1024 * 1024];

            // 找到最合适的单位
            int unitIndex = 0;
            while (unitIndex < units.Length - 1 && bytes >= factors[unitIndex + 1])
            {
                unitIndex++;
            }

            // 计算转换后的值
            double convertedValue = bytes / factors[unitIndex];

            // 格式化输出
            return $"{convertedValue.ToString($"F{decimalPlaces}", culture)} {units[unitIndex]}";
        }

        /// <summary>
        /// 转换回方法（未实现，因为不需要双向转换）
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("不支持从字符串转换回数值");
        }
    }
}
