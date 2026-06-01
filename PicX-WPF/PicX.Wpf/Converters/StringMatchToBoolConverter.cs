using System.Globalization;
using System.Windows.Data;

namespace PicXWpf.Converters;

/// <summary>
/// 绑定值与 ConverterParameter 字符串相等时返回 true
/// </summary>
public sealed class StringMatchToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var left = value?.ToString() ?? string.Empty;
        var right = parameter?.ToString() ?? string.Empty;
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true)
        {
            return parameter?.ToString() ?? string.Empty;
        }

        return Binding.DoNothing;
    }
}
