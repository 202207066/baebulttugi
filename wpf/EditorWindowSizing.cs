using System.Windows;
namespace wpf;
internal static class EditorWindowSizing
{
    // Keep the left/top edges and the user's current width fixed while expanding.
    public static void SetHeight(Window window, double height)
    {
        var area=SystemParameters.WorkArea;
        double available=double.IsFinite(window.Top) ? Math.Max(1,area.Bottom-window.Top) : area.Height;
        window.Height=Math.Min(height,Math.Min(area.Height,available));
    }
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
