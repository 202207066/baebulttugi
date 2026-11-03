using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace wpf;

internal sealed class StartupRecoveryWindow : Window
{
    internal StartupRecoveryWindow(Exception error)
    {
        Title = "배불뚝이 · 시작 오류";
        Width = 520; Height = 430; ResizeMode = ResizeMode.CanMinimize;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(30) };
        var failure = LoginFailure.From(error);
        panel.Children.Add(new TextBlock { Text = "시작을 완료하지 못했습니다", FontSize = 23, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        var status = new TextBlock { Text = failure.Message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 12) };
        panel.Children.Add(status);
        panel.Children.Add(new TextBlock { Text = failure.Details, Foreground = Brushes.SlateGray, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 20) });
        var retry = new Button { Content = "다시 시도", Height = 40, Margin = new Thickness(0, 0, 0, 10) };
        retry.Click += (_, _) => DialogResult = true;
        panel.Children.Add(retry);
        var change = new Button { Content = "다른 Google 계정으로 로그인", Height = 40, Margin = new Thickness(0, 0, 0, 10) };
        change.Click += async (_, _) =>
        {
            retry.IsEnabled = change.IsEnabled = false;
            try { await GoogleLogin.ClearSavedAsync(); DialogResult = true; }
            catch { status.Text = "이 PC의 로그인 정보를 초기화하지 못했습니다. 파일 접근 권한을 확인한 뒤 다시 시도해 주세요."; }
            finally { retry.IsEnabled = change.IsEnabled = true; }
        };
        panel.Children.Add(change);
        var exit = new Button { Content = "앱 종료", Height = 34 };
        exit.Click += (_, _) => Close();
        panel.Children.Add(exit);
        Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }
}
