using System;
using System.IO;
using System.Windows;

namespace wpf
{
    /// <summary>
    /// 애플리케이션 시작 흐름을 한 곳에서 관리합니다.
    ///
    ///   설정 확인 → 구글 로그인(1회) → 개인 DB 준비 → 나이대 선택 → 메인 창
    ///
    /// 예전에는 여기서 로그인한 뒤 Main.Loaded에서 로그인 창을 한 번 더 띄워
    /// 사용자가 구글 인증을 두 번 거쳐야 했습니다. 이제 여기서 받은 자격증명을
    /// AppServices에 담아 모든 화면이 공유합니다.
    /// </summary>
    public partial class App : Application
    {
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 로그인 창을 닫아도 앱이 곧바로 종료되지 않도록 합니다.
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            try
            {
                // 1. 설정 파일 확인
                if (!AppConfig.HasSpreadsheetId)
                {
                    MessageBox.Show(
                        (AppConfig.LoadError ?? "appsettings.json에 SpreadsheetId가 비어 있습니다.") +
                        "\n\n설정 파일 위치: " + AppConfig.ConfigFilePath,
                        "설정 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }

                if (!File.Exists(AppConfig.CredentialsPath))
                {
                    MessageBox.Show(
                        "구글 OAuth 클라이언트 파일을 찾을 수 없습니다.\n\n" +
                        "기대 위치: " + AppConfig.CredentialsPath,
                        "인증 파일 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }

                // 2. 구글 로그인 (앱 전체에서 단 한 번)
                var login = new LoginWindow();
                if (login.ShowDialog() != true || login.UserCredential == null)
                {
                    Shutdown();
                    return;
                }

                var sheetsService = new GoogleSheetsService(login.UserCredential);
                AppServices.Sheets = sheetsService;

                // 3. 사용자 전용 DB 준비 (최초 1회만 템플릿을 복사)
                await PrepareUserDatabaseAsync(sheetsService);

                // 4. 나이대 선택
                var ageWindow = new AgeSelectionWindow();
                if (ageWindow.ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }

                string ageGroup = AgeSelectionWindow.SelectedAgeGroup;

                // 5. 메인 창
                var main = new Main(sheetsService, ageGroup);
                this.MainWindow = main;
                this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "프로그램을 시작하는 중 오류가 발생했습니다:\n\n" + ex.Message,
                    "시작 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 사용자 드라이브에 개인 DB 사본이 있는지 확인하고, 없으면 템플릿을 복사합니다.
        /// 복사에 실패해도 공용 시트로 계속 진행할 수 있게 두고, 사유만 알려 줍니다.
        /// </summary>
        private static async System.Threading.Tasks.Task PrepareUserDatabaseAsync(GoogleSheetsService service)
        {
            string idFile = AppConfig.UserSheetIdFilePath;

            try
            {
                if (File.Exists(idFile))
                {
                    string savedId = File.ReadAllText(idFile).Trim();
                    if (!string.IsNullOrWhiteSpace(savedId))
                    {
                        service.SetUserSpreadsheetId(savedId);
                        return;
                    }
                }

                string newId = await service.SetupUserDatabaseAsync();
                File.WriteAllText(idFile, newId);

                MessageBox.Show(
                    "개인 구글 계정에 전용 DB 배포가 완료되었습니다.",
                    "초기화 성공", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "개인 DB를 준비하지 못했습니다. 설정 파일의 공용 시트로 계속 진행합니다.\n\n" + ex.Message,
                    "초기화 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
