using System.Configuration;
using System.Data;
using System.Windows;

namespace wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 앱이 로그인 창을 닫을 때 자동으로 종료되는 것을 방지합니다.
            // (로그인 창을 닫아도 애플리케이션이 곧바로 Shutdown되지 않도록 설정)
            this.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 설정 파일이 준비되지 않았으면 먼저 안내합니다.
            if (!AppConfig.HasSpreadsheetId)
            {
                MessageBox.Show(
                    (AppConfig.LoadError ?? "appsettings.json에 SpreadsheetId가 비어 있습니다.") +
                    "\n\n설정 파일 위치: " + AppConfig.ConfigFilePath,
                    "설정 필요", MessageBoxButton.OK, MessageBoxImage.Warning);
                Shutdown();
                return;
            }

            // 먼저 로그인 창을 모달로 띄웁니다.
            var login = new LoginWindow();
            bool? result = login.ShowDialog();

            if (result == true)
            {
                // 로그인 성공 시 메인 창을 엽니다.
                // 로그인 성공 후 나이대 선택 창을 표시합니다.
                var ageWindow = new AgeSelectionWindow();
                bool? ageResult = ageWindow.ShowDialog();

                if (ageResult != true)
                {
                    // 나이대 선택 취소 시 종료
                    Shutdown();
                    return;
                }
                try
                {
                    var main = new Main();
                    // 메인 창을 애플리케이션의 MainWindow로 지정
                    this.MainWindow = main;
                    // 로그인 창을 닫은 뒤에는 MainWindow가 닫힐 때 앱을 종료하도록 변경
                    this.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    main.Show();
                }
                catch (System.Exception ex)
                {
                    MessageBox.Show("메인 창을 여는 중 오류가 발생했습니다:\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                }
            }
            else
            {
                // 로그인 취소 또는 실패 시 애플리케이션 종료
                Shutdown();
            }
        }
    }

}
