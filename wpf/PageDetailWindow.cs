using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace wpf;

/// <summary>Temporarily hosts the page's existing editor, retaining drafts and event handlers.</summary>
public sealed class PageDetailWindow : Window
{
    private readonly Border host;
    private readonly UIElement body;
    private readonly Grid layout;

    public PageDetailWindow(Page page, Border host, string title, FrameworkElement? enabledSource = null,
        TextBlock? status = null, Action? commit = null)
    {
        this.host = host;
        body = host.Child ?? throw new InvalidOperationException("이미 열려 있는 창입니다.");
        Title = title;
        Owner = Window.GetWindow(page);
        WindowStartupLocation = Owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner;
        FontFamily = page.FontFamily; FontSize = 14;
        Foreground = page.Foreground; Background = Brushes.White;
        Resources.MergedDictionaries.Add(page.Resources);
        DataContext = page.DataContext;
        EditorWindowSizing.Apply(this, 1220, 760);
        MinWidth = Math.Min(720, Width); MinHeight = Math.Min(480, Height);
        layout = new Grid { Margin = new Thickness(20) };
        NameScope.SetNameScope(layout, NameScope.GetNameScope(page));
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };
        var close = new Button { Content = "닫기", Padding = new Thickness(24, 8, 24, 8), Margin = new Thickness(16, 0, 0, 0), IsCancel = false };
        DockPanel.SetDock(close, Dock.Right); footer.Children.Add(close);
        close.Click += (_, _) => Close();
        if (status != null)
        {
            var message = new TextBlock { TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            message.SetBinding(TextBlock.TextProperty, new Binding(nameof(TextBlock.Text)) { Source = status });
            footer.Children.Add(message);
        }
        Grid.SetRow(footer, 1); layout.Children.Add(footer); Content = layout;
        if (enabledSource != null)
            layout.SetBinding(IsEnabledProperty, new Binding(nameof(IsEnabled)) { Source = enabledSource });
        Closing += (_, e) =>
        {
            if (enabledSource?.IsEnabled == false) { e.Cancel = true; return; }
            try { commit?.Invoke(); }
            catch (Exception ex) { e.Cancel = true; MessageBox.Show(this, ex.Message, "입력값 확인", MessageBoxButton.OK, MessageBoxImage.Warning); }
        };
    }

    public void ShowAttachedDialog()
    {
        host.Child = null;
        try { layout.Children.Insert(0, body); ShowDialog(); }
        finally { layout.Children.Remove(body); host.Child = body; }
    }
}
