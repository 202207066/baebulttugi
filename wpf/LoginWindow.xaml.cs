using System.IO;
using System.Windows;
using Google.Apis.Auth.OAuth2;

namespace wpf;

public partial class LoginWindow : Window
{
    public UserCredential? UserCredential { get; private set; }
    public Func<UserCredential, IProgress<string>, CancellationToken, Task>? PrepareDataAsync { get; set; }
    public bool TrySavedLoginOnOpen { get; set; }
    public static readonly string[] OAuthScopes = GoogleLogin.Scopes;
    private readonly IGoogleLogin googleLogin;
    private CancellationTokenSource? cancellation;
    private bool closed, renewConsent;
    internal TimeSpan LoginTimeout { get; set; } = TimeSpan.FromSeconds(90);
    internal TimeSpan PreparationTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public LoginWindow() : this(new GoogleLogin()) { }
    internal LoginWindow(IGoogleLogin googleLogin)
    {
        this.googleLogin = googleLogin;
        InitializeComponent();
        ContentRendered += async (_, _) =>
        {
            if (TrySavedLoginOnOpen) { TrySavedLoginOnOpen = false; await TryLoginAsync(restoreOnly: true); }
        };
        Closing += (_, e) =>
        {
            if (cancellation != null) { e.Cancel = true; StopLogin(); return; }
            closed = true;
        };
    }

    public static bool HasSavedCredential()
    {
        try { return Directory.Exists(AppConfig.TokenStorePath) && Directory.EnumerateFiles(AppConfig.TokenStorePath).Any(); }
        catch { return false; }
    }
    public static async Task<UserCredential?> RestoreCredentialAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        return await new GoogleLogin().RestoreAsync(timeout.Token);
    }
    public static Task<UserCredential> AuthorizeAsync(CancellationToken token) => new GoogleLogin().AuthorizeAsync(false, false, token);
    public static void ClearSavedCredential() => GoogleLogin.ClearSavedAsync().GetAwaiter().GetResult();

    private async void OkButton_Click(object sender, RoutedEventArgs e) => await TryLoginAsync();
    private async void SwitchAccount_Click(object sender, RoutedEventArgs e) => await TryLoginAsync(chooseAccount: true);

    internal async Task TryLoginAsync(bool chooseAccount = false, bool restoreOnly = false)
    {
        if (cancellation != null || closed) return;
        using var attempt = new CancellationTokenSource();
        using var timeout = new CancellationTokenSource(LoginTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(attempt.Token, timeout.Token);
        cancellation = attempt;
        UserCredential = null;
        SetBusy(true);
        LoginHeading.Text = restoreOnly ? "저장된 계정으로 연결 중" : "Google 로그인 중";
        LoginDetails.Visibility = Visibility.Collapsed;
        LoginStatus.Text = restoreOnly ? "저장된 로그인 정보를 확인하고 있습니다…" :
            "브라우저에서 계정을 선택하고 배불뚝이가 사용하는 파일의 보기·수정 권한을 허용해 주세요. 브라우저를 닫았다면 ‘로그인 중단’을 누르고 다시 시도할 수 있습니다.";
        bool success = false;
        try
        {
            UserCredential? credential = restoreOnly
                ? await googleLogin.RestoreAsync(linked.Token)
                : await googleLogin.AuthorizeAsync(chooseAccount, renewConsent, linked.Token);
            if (credential == null)
            {
                LoginHeading.Text = "Google 계정 연결";
                LoginStatus.Text = "저장된 로그인이 없습니다. 아래 버튼으로 Google 계정에 로그인해 주세요.";
                return;
            }
            linked.Token.ThrowIfCancellationRequested();
            GoogleLogin.CheckGrantedScopes(credential.Token);
            timeout.CancelAfter(PreparationTimeout);
            LoginHeading.Text = "내 식단 자료 연결 중";
            LoginStatus.Text = "내 Google Drive에 저장된 식단 자료를 확인하고 있습니다…";
            if (PrepareDataAsync != null)
                await PrepareDataAsync(credential, new Progress<string>(message =>
                {
                    if (!closed && ReferenceEquals(cancellation, attempt) && !linked.IsCancellationRequested)
                        LoginStatus.Text = message;
                }), linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            UserCredential = credential;
            renewConsent = false;
            success = true;
        }
        catch (OperationCanceledException)
        {
            LoginHeading.Text = timeout.IsCancellationRequested ? "연결 시간이 초과되었습니다" : "로그인을 중단했습니다";
            LoginStatus.Text = timeout.IsCancellationRequested
                ? "Google 응답을 기다리는 시간이 길어졌습니다. 인터넷 연결이나 브라우저의 안내를 확인한 뒤 다시 시도하거나 다른 계정으로 로그인해 주세요."
                : "아래 버튼으로 다시 시도하거나 다른 계정으로 로그인할 수 있습니다.";
        }
        catch (Exception error)
        {
            var failure = LoginFailure.From(error);
            renewConsent |= failure.RenewConsent;
            LoginHeading.Text = "연결을 완료하지 못했습니다";
            LoginStatus.Text = failure.Message;
            LoginDetails.Text = failure.Details;
            LoginDetails.Visibility = Visibility.Visible;
        }
        finally
        {
            cancellation = null;
            SetBusy(false);
            LoginButton.Content = "다시 시도";
        }
        if (success && !closed) { DialogResult = true; Close(); }
    }

    private void SetBusy(bool busy)
    {
        LoginButton.IsEnabled = !busy;
        SwitchAccountButton.IsEnabled = !busy;
        CancelLogin.IsEnabled = true;
        CancelLogin.Content = busy ? "로그인 중단" : "앱 종료";
        LoginProgress.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
    }
    private void StopLogin()
    {
        cancellation?.Cancel();
        CancelLogin.IsEnabled = false;
        LoginStatus.Text = "연결을 중단하고 있습니다. 잠시만 기다려 주세요…";
    }
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (cancellation != null) { StopLogin(); return; }
        Close();
    }
    private void Privacy_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(CatalogRequestsClient.Website + "/privacy") { UseShellExecute = true }); }
        catch { MessageBox.Show("개인정보 안내: " + CatalogRequestsClient.Website + "/privacy"); }
    }
}
