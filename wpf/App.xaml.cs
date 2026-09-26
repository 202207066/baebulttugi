using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

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
            DispatcherUnhandledException += (_, error) =>
            {
                MessageBox.Show("작업 중 오류가 발생했지만 프로그램은 계속 사용할 수 있습니다.\n\n" + error.Exception.Message,
                    "작업 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                error.Handled = true;
            };

            try
            {
                // 1. 설정 파일 확인
                if (!AppConfig.HasSpreadsheetId)
                {
                    MessageBox.Show(
                        AppConfig.LoadError ?? "앱의 구글 시트 연결 설정이 빠져 있습니다. 배포 담당자에게 문의해 주세요.",
                        "앱 설정 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }

                if (!AppConfig.HasCredentials)
                {
                    MessageBox.Show(
                        "앱에 구글 로그인 설정이 포함되어 있지 않습니다. 배포 담당자에게 문의해 주세요.",
                        "앱 설정 확인", MessageBoxButton.OK, MessageBoxImage.Warning);
                    Shutdown();
                    return;
                }

                // 2. 이 PC에서 처음 실행하는 경우, 로그인 전에 이유부터 설명합니다.
                //    "왜 구글 로그인을 해야 하는지" 모른 채 동의 화면을 마주하지 않도록
                //    로그인보다 먼저 띄웁니다.
                if (UserPrefs.IsFirstRun)
                {
                    new WelcomeWindow().ShowDialog();

                    // 창을 X로 닫았더라도 다시 띄우지 않습니다.
                    // (필요하면 메인 화면의 [도움말]에서 언제든 다시 볼 수 있습니다.)
                    UserPrefs.HasSeenTutorial = true;
                }

                // 3. 저장된 로그인 토큰이 있으면 로그인 창을 거치지 않고 바로 엽니다.
                // 토큰이 만료된 경우에는 아래의 일반 로그인 흐름으로 자연스럽게 돌아갑니다.
                GoogleSheetsService? sheetsService = null;
                if (LoginWindow.HasSavedCredential())
                {
                    try
                    {
                        var credential = await LoginWindow.AuthorizeAsync(System.Threading.CancellationToken.None);
                        sheetsService = new GoogleSheetsService(credential);
                        await sheetsService.PreparePersonalDatabaseAsync(new Progress<string>(_ => { }));
                        AppServices.Sheets = sheetsService;
                    }
                    catch { sheetsService = null; AppServices.Sheets = null; }
                }

                if (sheetsService == null)
                {
                    var login = new LoginWindow
                    {
                        PrepareDataAsync = async (credential, progress) =>
                        {
                            sheetsService = new GoogleSheetsService(credential);
                            await sheetsService.PreparePersonalDatabaseAsync(progress);
                            AppServices.Sheets = sheetsService;
                        }
                    };
                    if (login.ShowDialog() != true || sheetsService == null)
                    {
                        Shutdown();
                        return;
                    }
                }

                // 5. 메인 창
                //
                //    예전에는 여기서 나이대 선택 창을 한 번 더 띄웠습니다. 로그인 직후
                //    대시보드까지 가는 길에 모달이 하나 더 끼어 흐름이 끊겼고, 고른 값도
                //    결국 «식단 자동 조합» 화면에서만 쓰였습니다.
                //    이제 나이대는 그 화면 안의 콤보박스에서 바로 고릅니다.
                var main = new Main(sheetsService, AgeSelectionWindow.SelectedAgeGroup);
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

    }
}
