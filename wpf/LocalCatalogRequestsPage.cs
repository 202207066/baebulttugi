using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace wpf;

public sealed class LocalCatalogRequestsPage : Page
{
    public LocalCatalogRequestsPage()
    {
        var root = new StackPanel { Margin = new Thickness(28), MaxWidth = 1000 };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = "메뉴·식재료 요청서", FontSize = 26, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "로컬 버전은 요청을 자동 전송하지 않습니다. 요청서를 파일로 저장하고 인터넷을 사용할 수 있을 때 baebulttugi03@gmail.com으로 보내 주세요. 개인 명단·건강정보는 적지 마세요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,20) });
        var draft = CatalogRequestDraftStore.Read("local") ?? new();
        var kind = new ComboBox { ItemsSource = CatalogRequestDraft.Kinds, SelectedItem = draft.Kind };
        var track = new ComboBox { ItemsSource = new[] { "공통", "3-5", "6-18" }, SelectedItem = draft.Track };
        void Field(string title, Control input) { root.Children.Add(new TextBlock { Text = title, Margin = new Thickness(0,12,0,6) }); root.Children.Add(input); }
        TextBox Input(string title, string value, int max, int height = 40) { var box = new TextBox { Text = value, MaxLength = max, Height = height, AcceptsReturn = height > 40, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Field(title, box); return box; }
        Field("요청 종류", kind); Field("연령대", track);
        var name = Input("메뉴·식재료 이름", draft.Name, 120);
        var reference = Input("기존 메뉴 ID (선택)", draft.ReferenceId, 160);
        var details = Input("추가·수정할 내용", draft.Details, 4000, 150);
        var reason = Input("요청 이유", draft.Reason, 1500, 80);
        var source = Input("자료 출처 (선택)", draft.Source, 800);
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkGreen, Margin = new Thickness(0,14,0,0) };
        void Collect() { draft.Kind = kind.SelectedItem?.ToString() ?? ""; draft.Track = track.SelectedItem?.ToString() ?? ""; draft.Name = name.Text; draft.ReferenceId = reference.Text; draft.Details = details.Text; draft.Reason = reason.Text; draft.Source = source.Text; }
        var actions = new WrapPanel { Margin = new Thickness(0,20,0,0) }; root.Children.Add(actions);
        var save = new Button { Content = "이 PC에 임시 저장", Margin = new Thickness(0,0,12,0) }; actions.Children.Add(save);
        save.Click += (_, _) => { try { Collect(); CatalogRequestDraftStore.Save("local", draft); status.Text = "이 PC에 임시 저장했습니다. 아직 전송되지 않았습니다."; } catch (Exception ex) { status.Text = ex.Message; } };
        var export = new Button { Content = "요청서 파일로 저장" }; actions.Children.Add(export);
        export.Click += (_, _) =>
        {
            try
            {
                Collect(); draft.Validate();
                var dialog = new SaveFileDialog { Filter = "텍스트 요청서|*.txt", FileName = "배불뚝이_자료반영요청.txt" };
                if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
                File.WriteAllText(dialog.FileName, $"배불뚝이 공식 자료 반영 요청\n받는 곳: baebulttugi03@gmail.com\n요청 번호: {draft.Id}\n종류: {draft.Kind}\n연령대: {draft.Track}\n이름: {draft.Name}\n참조 ID: {draft.ReferenceId}\n\n요청 내용\n{draft.Details}\n\n요청 이유\n{draft.Reason}\n\n참고 자료\n{draft.Source}\n");
                CatalogRequestDraftStore.Save("local", draft);
                status.Text = "요청서를 저장했습니다. 파일을 이메일에 첨부해 보내 주세요. 앱에서 전송하지 않았습니다.";
            }
            catch (Exception ex) { status.Text = ex.Message; }
        };
        root.Children.Add(status);
    }
}
