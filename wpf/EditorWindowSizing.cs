using System.Windows;
namespace wpf;
internal static class EditorWindowSizing
{
    public static void Apply(Window window, double width, double height)
    {
        var area=SystemParameters.WorkArea;
        window.Width=Math.Min(width,area.Width);
        window.Height=Math.Min(height,area.Height);
        if(window.IsLoaded) {
            window.Left=Math.Clamp(window.Left,area.Left,Math.Max(area.Left,area.Right-window.Width));
            window.Top=Math.Clamp(window.Top,area.Top,Math.Max(area.Top,area.Bottom-window.Height));
        }
    }
}
