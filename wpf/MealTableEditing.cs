using System.Windows;
namespace wpf;
public static class MealTableEditing
{
    public static readonly DependencyProperty IsEditingProperty=DependencyProperty.RegisterAttached("IsEditing",typeof(bool),typeof(MealTableEditing),new FrameworkPropertyMetadata(false));
    public static bool GetIsEditing(DependencyObject element)=>(bool)element.GetValue(IsEditingProperty);
    public static void SetIsEditing(DependencyObject element,bool value)=>element.SetValue(IsEditingProperty,value);
}
