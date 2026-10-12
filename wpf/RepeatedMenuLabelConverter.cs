using System.Globalization;
using System.Windows.Data;

namespace wpf;

public sealed class RepeatedMenuLabelConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string current = values.ElementAtOrDefault(0) as string ?? "";
        return values.ElementAtOrDefault(1) is string previous && current == previous ? "" : current;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
