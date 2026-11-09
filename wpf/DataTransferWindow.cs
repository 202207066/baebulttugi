using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace wpf;

public sealed class DataTransferWindow : Window
{
    public Facility? ImportedFacility { get; private set; }
    public DataTransferWindow(GoogleSheetsService service)
    {
        Title = "내 자료 · 엑셀 · " + (service.IsLocal ? "로컬" : "인터넷");
        Width = 730; SizeToContent = SizeToContent.Height; MaxHeight = 760; MinWidth = 540;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Background = Brushes.White;
        var body = new StackPanel { Margin = new Thickness(28) }; Content = new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        body.Children.Add(new TextBlock { Text = service.IsLocal ? "이 PC에 저장한 내 자료" : "Google Sheets의 내 자료", FontSize = 25, FontWeight = FontWeights.Bold });
        body.Children.Add(new TextBlock { Text = service.IsLocal ? "저장 버튼을 누른 자료는 앱을 닫아도 유지됩니다. 엑셀로 다른 PC에 옮길 수 있습니다." : "현재 급식소의 Google Sheets 자료를 엑셀로 내려받아 로컬 버전에 옮길 수 있습니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,16) });
        body.Children.Add(new TextBlock { Text = "선택 급식소: " + (service.CurrentFacility?.Name ?? "아직 선택하지 않음"), FontWeight = FontWeights.Bold, FontSize = 16, Margin = new Thickness(0,0,0,16) });
        var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkGreen, Margin = new Thickness(0,16,0,0) };
        var buttons = new List<Button>(); bool busy = false;
        void ActionButton(string text, Func<Task> action, bool enabled = true)
        {
            var button = new Button { Content = text, HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(16,10,16,10), Margin = new Thickness(0,0,0,10), IsEnabled = enabled };
            buttons.Add(button); body.Children.Add(button);
            button.Click += async (_, _) =>
            {
                if (busy) return; busy = true; var old = buttons.Select(b => b.IsEnabled).ToArray(); foreach (var b in buttons) b.IsEnabled = false;
                try { await action(); } catch (Exception ex) { status.Text = "완료하지 못했습니다. " + ex.Message; }
                finally { busy = false; for (int i = 0; i < buttons.Count; i++) buttons[i].IsEnabled = old[i]; }
            };
        }
        ActionButton("선택 급식소 엑셀 내보내기", async () =>
        {
            var save = new SaveFileDialog { Filter = "엑셀 통합 문서|*.xlsx", FileName = "배불뚝이_내자료_" + DateTime.Today.ToString("yyyy-MM-dd") + ".xlsx" };
            if (save.ShowDialog(this) != true) return;
            status.Text = "자료를 내보내는 중입니다…";
            var tables = await service.ExportTablesAsync();
            await Task.Run(() => WorkbookExcel.Write(save.FileName, tables));
            status.Text = "엑셀 저장 완료: " + save.FileName + "\n현재 급식소의 개인 자료만 포함됩니다. 공식 메뉴는 앱에 포함되어 있습니다.";
        }, service.CurrentFacility != null);
        if (service.IsLocal)
        {
            ActionButton("엑셀 가져오기 · 새 급식소로 추가", async () =>
            {
                var open = new OpenFileDialog { Filter = "엑셀 통합 문서|*.xlsx" }; if (open.ShowDialog(this) != true) return;
                var tables = await Task.Run(() => WorkbookExcel.Read(open.FileName));
                var info = tables.FirstOrDefault(t => t.Title == "배불뚝이 내보내기 정보");
                string suggested = info?.Rows.Skip(1).FirstOrDefault()?.FirstOrDefault()?.ToString() ?? Path.GetFileNameWithoutExtension(open.FileName);
                string? name = PromptDialog.Show(this, "새 급식소 이름", $"자료 {tables.Count}개 탭을 가져옵니다. 기존 급식소는 그대로 유지됩니다.\n수식은 엑셀에 마지막으로 저장된 계산값을 가져옵니다.", [suggested + " 가져오기"]);
                if (name == null) return;
                string count = info?.Rows.Skip(1).FirstOrDefault()?.Skip(1).FirstOrDefault()?.ToString() ?? "0";
                string? input = PromptDialog.Show(this, "식수 인원", "이 급식소의 배식 인원을 입력해 주세요.", [count]); if (input == null) return;
                if (!int.TryParse(input, out int diners) || diners < 0) throw new InvalidOperationException("식수는 0 이상의 정수로 입력해 주세요.");
                ImportedFacility = await service.ImportLocalTablesAsync(name, diners, tables);
                MessageBox.Show(this, "새 급식소로 가져왔습니다. 화면에서 대상자·식단·원가를 확인해 주세요.", "가져오기 완료");
                busy = false; DialogResult = true;
            });
            ActionButton("전체 로컬 자료 백업", () =>
            {
                var save = new SaveFileDialog { Filter = "배불뚝이 로컬 백업|*.json", FileName = "배불뚝이_전체백업_" + DateTime.Now.ToString("yyyy-MM-dd_HHmm") + ".json" };
                if (save.ShowDialog(this) == true) { service.ExportLocalBackup(save.FileName); status.Text = "모든 급식소 백업 완료: " + save.FileName + "\n복원: 앱을 닫고 백업 파일을 아래 저장 폴더의 data.json으로 복사하세요. 기존 data.json은 별도로 보관하세요."; }
                return Task.CompletedTask;
            });
            ActionButton("PC 저장 폴더 열기", () => { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.GetDirectoryName(service.LocalDataPath!)!) { UseShellExecute = true }); return Task.CompletedTask; });
            body.Children.Add(new TextBlock { Text = "저장 위치\n" + service.LocalDataPath, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0,8,0,8) });
        }
        body.Children.Add(new TextBlock { Text = "엑셀·백업에는 대상자 정보가 포함될 수 있습니다. 공유할 파일에 필요한 자료만 들어 있는지 확인해 주세요.", TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DimGray, Margin = new Thickness(0,8,0,0) });
        body.Children.Add(status);
        Closing += (_, e) => { if (busy) e.Cancel = true; };
    }
}
