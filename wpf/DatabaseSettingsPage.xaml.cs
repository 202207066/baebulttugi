using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace wpf
{
    /// <summary>
    /// 데이터베이스(구글 스프레드시트) 설정 화면.
    ///
    /// 배포하면 급식소마다 자기 시트를 쓰게 됩니다. 예전에는 시트를 바꾸려면
    /// appsettings.json을 직접 편집해야 했는데, 일반 사용자가 할 수 있는 일이
    /// 아니므로 앱 안에서 만들고 연결할 수 있게 합니다.
    /// </summary>
    public partial class DatabaseSettingsPage : Page
    {
        private readonly GoogleSheetsService _service;

        /// <summary>시트(탭) 상태 한 줄.</summary>
        public class SheetStatusRow
        {
            public string Icon { get; set; } = "";
            public string Title { get; set; } = "";
            public string Purpose { get; set; } = "";
            public string Status { get; set; } = "";
            public Brush StatusColor { get; set; } = Brushes.Gray;
        }

        public DatabaseSettingsPage()
        {
            InitializeComponent();

            _service = AppServices.Require();

            Loaded += DatabaseSettingsPage_Loaded;
        }

        private async void DatabaseSettingsPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshAsync();
            ShowRecent();
        }

        // ── 현재 연결 상태 ──────────────────────────────────────────

        private async Task RefreshAsync()
        {
            string id = _service.SpreadsheetId;

            TxtCurrentId.Text = string.IsNullOrWhiteSpace(id) ? "" : $"ID: {id}";
            TxtCurrentNote.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(id))
            {
                TxtCurrentTitle.Text = "연결된 데이터베이스가 없습니다";
                DotStatus.Fill = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E));
                ShowNote("아래에서 새로 만들거나, 이미 쓰던 시트에 연결해 주세요.");
                ListSheets.ItemsSource = null;
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                string title = await _service.GetSpreadsheetTitleAsync();
                TxtCurrentTitle.Text = string.IsNullOrWhiteSpace(title) ? "(이름 없음)" : title;
                DotStatus.Fill = (Brush)Application.Current.Resources["Lime"];

                // 목록에도 최신 이름으로 남겨 둡니다.
                UserPrefs.RememberDatabase(id, TxtCurrentTitle.Text);

                await LoadSheetStatusAsync();
            }
            catch (Exception ex)
            {
                TxtCurrentTitle.Text = "열 수 없습니다";
                DotStatus.Fill = new SolidColorBrush(Color.FromRgb(0xE5, 0x3E, 0x3E));
                ShowNote("이 시트를 열지 못했습니다: " + ex.Message);
                ListSheets.ItemsSource = null;
            }
            finally
            {
                Mouse.OverrideCursor = null;
                ShowRecent();
            }
        }

        private void ShowNote(string text)
        {
            TxtCurrentNote.Text = text;
            TxtCurrentNote.Visibility = Visibility.Visible;
        }

        /// <summary>앱이 쓰는 탭 5개가 있는지 확인해 표시합니다(만들지는 않습니다).</summary>
        private async Task LoadSheetStatusAsync()
        {
            var needed = new (string Title, string Purpose, string Icon)[]
            {
                (AppConfig.MenuSheetName,      "메뉴 · 레시피와 영양성분",        "🍳"),
                (AppConfig.PatientSheetName,   "피급식자 명단과 알러지 성분",     "👥"),
                (AppConfig.DietSheetName,      "자동 조합으로 만든 식단 기록",    "🍽️"),
                (AppConfig.EventSheetName,     "급식소 일정",                    "📅"),
                (AppConfig.CostSheetName,      "식재료 단가 (없으면 데모 데이터)", "💵"),
            };

            List<string> existing = await _service.GetSheetTitlesAsync();

            var rows = new List<SheetStatusRow>();
            foreach (var item in needed)
            {
                bool found = existing.Exists(t =>
                    string.Equals(t.Replace(" ", ""), item.Title.Replace(" ", ""),
                                  StringComparison.OrdinalIgnoreCase));

                rows.Add(new SheetStatusRow
                {
                    Icon = item.Icon,
                    Title = item.Title,
                    Purpose = item.Purpose,
                    Status = found ? "있음" : "없음",
                    StatusColor = found
                        ? (Brush)Application.Current.Resources["Brand"]
                        : (Brush)Application.Current.Resources["Warning"]
                });
            }

            ListSheets.ItemsSource = rows;
        }

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

        private void BtnOpenInBrowser_Click(object sender, RoutedEventArgs e)
        {
            string url = _service.SpreadsheetUrl;
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("연결된 데이터베이스가 없습니다.", "안내",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("브라우저를 열지 못했습니다:\n" + ex.Message, "오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnEnsureSheets_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_service.SpreadsheetId))
            {
                MessageBox.Show("먼저 데이터베이스를 연결해 주세요.", "안내",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var result = await _service.EnsureAllSheetsAsync();

                var created = new List<string>();
                foreach (var (title, wasCreated) in result)
                {
                    if (wasCreated) created.Add(title);
                }

                await LoadSheetStatusAsync();

                MessageBox.Show(
                    created.Count == 0
                        ? "필요한 탭이 모두 준비되어 있습니다."
                        : "다음 탭을 새로 만들었습니다:\n\n· " + string.Join("\n· ", created),
                    "확인 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("탭을 준비하지 못했습니다:\n" + ex.Message, "오류",
                                MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── 기존 시트 연결 ──────────────────────────────────────────

        private async void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            string id = GoogleSheetsService.ParseSpreadsheetId(TxtSheetUrl.Text);

            if (string.IsNullOrWhiteSpace(id))
            {
                ShowResult(TxtConnectResult, false,
                           "주소를 알아볼 수 없습니다. 구글 시트 주소 전체를 붙여넣어 주세요.");
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var (ok, title, error) = await _service.CheckSpreadsheetAsync(id);

                if (!ok)
                {
                    ShowResult(TxtConnectResult, false, error);
                    return;
                }

                ApplyDatabase(id, title);
                ShowResult(TxtConnectResult, true, $"«{title}» 에 연결했습니다.");

                TxtSheetUrl.Clear();
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ShowResult(TxtConnectResult, false, ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── 새로 만들기 ─────────────────────────────────────────────

        private async void BtnCreate_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtNewName.Text.Trim();
            if (name.Length == 0)
            {
                ShowResult(TxtCreateResult, false, "파일 이름을 입력해 주세요.");
                return;
            }

            if (MessageBox.Show(
                    $"내 구글 드라이브에 «{name}» 스프레드시트를 새로 만들고,\n" +
                    "앱을 그 시트에 연결합니다. 계속할까요?",
                    "새 데이터베이스", MessageBoxButton.YesNo, MessageBoxImage.Question)
                != MessageBoxResult.Yes)
            {
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                string id = await _service.CreateDatabaseAsync(name);

                PersistCurrentId(id);
                UserPrefs.RememberDatabase(id, name);

                // 새로 만든 사본에 필요한 탭이 모두 있는지 확인합니다.
                await _service.EnsureAllSheetsAsync();

                ShowResult(TxtCreateResult, true, $"«{name}» 을 만들고 연결했습니다.");
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                ShowResult(TxtCreateResult, false, ex.Message);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── 최근 목록 ───────────────────────────────────────────────

        private void ShowRecent()
        {
            var list = UserPrefs.RecentDatabases;

            // 지금 쓰고 있는 것은 목록에서 빼서 헷갈리지 않게 합니다.
            string current = _service.SpreadsheetId;
            list.RemoveAll(d => string.Equals(d.Id, current, StringComparison.Ordinal));

            ListRecent.ItemsSource = list;
            TxtNoRecent.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void BtnSwitchRecent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button) return;

            string id = button.Tag?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(id)) return;

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var (ok, title, error) = await _service.CheckSpreadsheetAsync(id);
                if (!ok)
                {
                    MessageBox.Show(error, "전환 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                ApplyDatabase(id, title);
                await RefreshAsync();

                MessageBox.Show($"«{title}» 로 전환했습니다.\n다른 화면을 다시 열면 새 데이터가 보입니다.",
                                "전환 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── 공통 ────────────────────────────────────────────────────

        /// <summary>앱이 이 시트를 쓰도록 바꾸고, 다음 실행에도 기억되게 저장합니다.</summary>
        private void ApplyDatabase(string id, string title)
        {
            _service.SetUserSpreadsheetId(id);
            PersistCurrentId(id);
            UserPrefs.RememberDatabase(id, string.IsNullOrWhiteSpace(title) ? "(이름 없음)" : title);
        }

        /// <summary>다음 실행 때도 같은 시트를 쓰도록 파일에 적어 둡니다.</summary>
        private static void PersistCurrentId(string id)
        {
            try
            {
                File.WriteAllText(AppConfig.UserSheetIdFilePath, id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[시트 ID 저장 실패] {ex.Message}");
            }
        }

        private void ShowResult(TextBlock target, bool ok, string message)
        {
            target.Text = message;
            target.Foreground = ok
                ? (Brush)Application.Current.Resources["Brand"]
                : (Brush)Application.Current.Resources["Danger"];
            target.Visibility = Visibility.Visible;
        }
    }
}
