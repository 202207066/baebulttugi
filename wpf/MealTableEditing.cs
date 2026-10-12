using System.Globalization;
using System.Windows;
using System.Windows.Data;
namespace wpf;
public static class MealTableEditing
{
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.RegisterAttached("IsInteractive", typeof(bool), typeof(MealTableEditing), new FrameworkPropertyMetadata(false));
    public static bool GetIsInteractive(DependencyObject element) => (bool)element.GetValue(IsInteractiveProperty);
    public static void SetIsInteractive(DependencyObject element, bool value) => element.SetValue(IsInteractiveProperty, value);
    public static readonly DependencyProperty IsEditableProperty = DependencyProperty.RegisterAttached("IsEditable", typeof(bool), typeof(MealTableEditing), new FrameworkPropertyMetadata(false));
    public static bool GetIsEditable(DependencyObject element) => (bool)element.GetValue(IsEditableProperty);
    public static void SetIsEditable(DependencyObject element, bool value) => element.SetValue(IsEditableProperty, value);
    public static bool CanEdit(object row, int displayIndex, bool interactive)
        => interactive && row is WeekMealRow week && week.MenuIndex is >= 0 and < 6 &&
           displayIndex > 0 && displayIndex <= week.Meals.Length && week.Meals[displayIndex - 1] != null;
}
public sealed class MealCellEditableConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length == 3 && values[1] is int index && values[2] is bool interactive && MealTableEditing.CanEdit(values[0], index, interactive);
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
