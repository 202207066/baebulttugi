using System.IO;
using System.Threading;
using System.Windows;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Sheets.v4;
using Google.Apis.Util.Store;

namespace wpf
{
    public partial class LoginWindow : Window
    {
        // null 허용 처리(?) 완료
        public UserCredential? UserCredential { get; private set; }
        public Func<UserCredential, IProgress<string>, Task>? PrepareDataAsync { get; set; }
        private CancellationTokenSource? _cancellation;
        private bool _preparingData;
        private bool _closed;

        public LoginWindow()
        {
            InitializeComponent();
            Closing += (_, e) => { if (_preparingData) { e.Cancel = true; return; } _closed = true; _cancellation?.Cancel(); };
        }

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellation != null) return;
            _cancellation = new CancellationTokenSource();
            LoginButton.IsEnabled = false;
            LoginProgress.Visibility = Visibility.Visible;
            LoginStatus.Text = "브라우저에서 구글 로그인을 완료해 주세요. 이 창은 열린 상태로 유지됩니다.";
            Topmost = true;
            try
            {
                string[] scopes = { DriveService.Scope.Drive, SheetsService.Scope.Spreadsheets };

                using (var stream = AppConfig.OpenCredentials())
                {
                    string credPath = AppConfig.TokenStorePath;

                    UserCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        scopes,
                        "user",
                        _cancellation.Token,
                        new FileDataStore(credPath, true));
                }

                if (_closed) return;
                _preparingData = true;
                CancelLogin.IsEnabled = false;
                if (PrepareDataAsync != null) await PrepareDataAsync(UserCredential, new Progress<string>(s => LoginStatus.Text = s));
                _preparingData = false;

                this.DialogResult = true;
                this.Close();
            }
            catch (OperationCanceledException) { if (!_closed) LoginStatus.Text = "로그인을 취소했습니다."; }
            catch (System.Exception ex)
            {
                if (!_closed) LoginStatus.Text = "연결을 완료하지 못했습니다. " + ex.Message;
            }
            finally { _preparingData = false; Topmost = false; LoginButton.IsEnabled = true; CancelLogin.IsEnabled = true; LoginProgress.Visibility = Visibility.Collapsed; _cancellation.Dispose(); _cancellation = null; }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
