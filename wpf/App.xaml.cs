using System;
using System.Windows;

namespace wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += (_, error) =>
        {
            MessageBox.Show("작업 중 오류가 발생했습니다. 다시 시도해 주세요.\n\n" + LoginFailure.From(error.Exception).Details,
                "작업 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
            error.Handled = true;
        };

        while (true)
        {
            try
            {
                _ = OfficialCatalog.Current;
                if (!AppConfig.HasCredentials)
                    throw new InvalidOperationException("앱의 Google 로그인 설정이 없습니다.");
                if (UserPrefs.IsFirstRun)
                {
                    new WelcomeWindow().ShowDialog();
                    UserPrefs.HasSeenTutorial = true;
                }

                AppServices.Sheets = null;
                GoogleSheetsService? preparedService = null;
                var login = new LoginWindow
                {
                    TrySavedLoginOnOpen = LoginWindow.HasSavedCredential(),
                    PrepareDataAsync = async (credential, progress, cancellationToken) =>
                    {
                        var candidate = new GoogleSheetsService(credential);
                        await candidate.PreparePersonalDatabaseAsync(progress, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        preparedService = candidate;
                    }
                };
                if (login.ShowDialog() != true || preparedService == null)
                {
                    Shutdown();
                    return;
                }
                // Publish only a completely prepared account. Failed/cancelled attempts cannot leak into the next account.
                AppServices.Sheets = preparedService;
                var main = new Main(preparedService, AgeSelectionWindow.SelectedAgeGroup);
                MainWindow = main;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                main.Show();
                return;
            }
            catch (Exception error)
            {
                AppServices.Sheets = null;
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                if (new StartupRecoveryWindow(error).ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }
        }
    }
}
