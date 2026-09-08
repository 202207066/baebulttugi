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

        public LoginWindow()
        {
            InitializeComponent();
        }

        private async void OkButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string[] scopes = { DriveService.Scope.Drive, SheetsService.Scope.Spreadsheets };

                using (var stream = new FileStream(AppConfig.CredentialsPath, FileMode.Open, FileAccess.Read))
                {
                    string credPath = AppConfig.TokenStorePath;

                    UserCredential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                        GoogleClientSecrets.FromStream(stream).Secrets,
                        scopes,
                        "user",
                        CancellationToken.None,
                        new FileDataStore(credPath, true));
                }

                this.DialogResult = true;
                this.Close();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"로그인 실패: {ex.Message}");
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}