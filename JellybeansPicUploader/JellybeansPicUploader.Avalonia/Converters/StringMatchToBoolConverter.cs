using System.Globalization;
using Avalonia.Data.Converters;

namespace JellybeansPicUploader.Converters;

public sealed class StringMatchToBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is null)
        {
            return false;
        }

        return string.Equals(value?.ToString(), parameter.ToString(), StringComparison.Ordinal);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is true && parameter is not null)
        {
            return parameter.ToString();
        }

        return Avalonia.Data.BindingOperations.DoNothing;
    }
}
