using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Requests;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using wpf;

internal static class LoginRecoveryChecks
{
    private static int count;
    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception(name);
        count++; Console.WriteLine("PASS " + name);
    }
    internal static GoogleApiException ScopeError() => new("drive", "Request had insufficient authentication scopes.") { HttpStatusCode = HttpStatusCode.Forbidden };
    private static TokenResponse GoodToken(string access = "fake-access") => new()
    {
        AccessToken = access, RefreshToken = "fake-refresh", Scope = DriveService.Scope.DriveFile + " openid email profile", ExpiresInSeconds = 3600, IssuedUtc = DateTime.UtcNow
    };
    private static UserCredential Credential(string access = "fake-account") => new(new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
    { ClientSecrets = new ClientSecrets { ClientId = "fake-client", ClientSecret = "fake-secret" }, Scopes = GoogleLogin.Scopes }), "user", GoodToken(access));

    public static void Run()
    {
        ProtocolChecks().GetAwaiter().GetResult();
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml", UriKind.Relative));
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        UiChecks();
        Console.WriteLine($"{count} login recovery checks passed. No live Google API calls or real saved credentials were used.");
    }

    private static async Task ProtocolChecks()
    {
        var store = new MemoryStore(); var receiver = new Receiver(); var http = new TokenHttp();
        GoogleLogin NewLogin() => new(store, () => new ClientSecrets { ClientId = "fake-client", ClientSecret = "fake-secret" }, receiver, http);
        await store.StoreAsync("user", GoodToken());
        var saved = await NewLogin().RestoreAsync(CancellationToken.None);
        Check(saved?.Token.AccessToken == "fake-access" && receiver.Calls == 0 && http.Calls == 0, "Valid saved login reuses the token without browser or network");
        store.Token!.Scope = "openid email profile";
        try { await NewLogin().RestoreAsync(CancellationToken.None); throw new Exception("accepted incomplete grant"); }
        catch (MissingDrivePermissionException) { Check(receiver.Calls == 0 && http.Calls == 0, "Partial saved grant stops before any Drive or browser call"); }
        store.Token = GoodToken(); store.Token.Scope = "https://www.googleapis.com/auth/drive.file.other openid";
        try { GoogleLogin.CheckGrantedScopes(store.Token); throw new Exception("accepted substring"); }
        catch (MissingDrivePermissionException) { Check(true, "Scope matching requires the entire case-sensitive scope"); }
        store.Token = GoodToken(); store.Token.Scope = null;
        Check(await NewLogin().RestoreAsync(CancellationToken.None) != null, "Older tokens without scope remain subject to the subsequent real API permission check");
        var credential = await NewLogin().AuthorizeAsync(true, false, CancellationToken.None);
        Check(store.DeletedKeys.SequenceEqual(new[] { "user" }) && !store.Cleared, "Account switch deletes only the local user token");
        Check(receiver.LastRequest?.Prompt == "select_account consent", "Account switch requests Google's account chooser and consent");
        Check(receiver.LastRequest?.Scope?.Split(' ').Order().SequenceEqual(GoogleLogin.Scopes.Order()) == true, "Fresh login retains only the existing four OAuth scopes");
        Check(receiver.LastRequest?.CodeChallengeMethod == "S256" && http.LastBody.Contains("code_verifier="), "Browser login keeps PKCE protection");
        Check(credential.Token.AccessToken == "new-fake-account" && store.Token?.AccessToken == "new-fake-account", "New account token replaces the old cached token");
        int before = receiver.Calls;
        await NewLogin().AuthorizeAsync(false, false, CancellationToken.None);
        Check(receiver.Calls == before, "Ordinary retry can reuse a valid login after a temporary failure");
        await NewLogin().AuthorizeAsync(false, true, CancellationToken.None);
        Check(receiver.Calls == before + 1 && receiver.LastRequest?.Prompt?.Contains("consent") == true, "Permission recovery cannot silently reuse the incomplete saved login");
        http.Scope = "openid email profile";
        try { await NewLogin().AuthorizeAsync(false, true, CancellationToken.None); throw new Exception("accepted declined permission"); }
        catch (MissingDrivePermissionException) { Check(true, "Declined Drive permission is rejected after Google's token response"); }
        Check(LoginFailure.From(ScopeError()).RenewConsent, "The exact reported Drive 403 offers fresh consent");
        Check(!LoginFailure.From(new GoogleApiException("drive", "insufficientFilePermissions") { HttpStatusCode = HttpStatusCode.Forbidden }).RenewConsent, "File permission denial is not misclassified as a scope failure");
        Check(LoginFailure.From(new TokenResponseException(new TokenErrorResponse { Error = "invalid_grant" })).RenewConsent, "Expired or revoked login offers fresh authentication");
        Check(LoginFailure.From(new TokenResponseException(new TokenErrorResponse { Error = "access_denied" })).Message.Contains("다른 계정"), "User denial offers account switching");
        Check(!LoginFailure.From(new HttpRequestException("offline")).RenewConsent, "Network failure does not discard a valid account");
        var redacted = LoginFailure.From(new Exception("access_token=should-not-be-shown"));
        Check(!redacted.Message.Contains("should-not-be-shown") && !redacted.Details.Contains("should-not-be-shown"), "Failure UI never dumps raw exception payloads or token values");

        var pendingHttp = new PendingDriveHttp();
        var service = new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_driveService", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.SetValue(service,
            new DriveService(new BaseClientService.Initializer { HttpClientFactory = pendingHttp }));
        using var cancel = new CancellationTokenSource();
        var pending = service.PreparePersonalDatabaseAsync(new Progress<string>(), cancel.Token);
        await pendingHttp.Started.Task;
        cancel.Cancel();
        try { await pending; throw new Exception("uncancelled Drive request"); }
        catch (OperationCanceledException) { Check(pendingHttp.Cancelled && pendingHttp.Writes == 0, "Cancelling preparation reaches the Drive HTTP request before any data creation"); }
    }

    private static void Pump(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(new Action(() => frame.Continue = false)), TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    private static Button Button(LoginWindow window, string name) => (Button)window.FindName(name);
    private static string Text(LoginWindow window, string name) => ((TextBlock)window.FindName(name)).Text;
    private static bool Ready(LoginWindow window) => Button(window, "LoginButton").IsEnabled && Button(window, "SwitchAccountButton").IsEnabled && Button(window, "CancelLogin").IsEnabled;
    private static void UiChecks()
    {
        var fake = new FakeLogin(); var window = new LoginWindow(fake); window.Show();
        IProgress<string>? staleProgress = null;
        window.PrepareDataAsync = (_, progress, _) => { staleProgress = progress; return Task.FromException(ScopeError()); };
        Pump(window.TryLoginAsync(restoreOnly: true));
        Check(window.IsVisible && Ready(window) && Text(window, "LoginStatus").Contains("권한"), "Saved-login Drive failure leaves a visible retry/account-switch screen");
        Check(window.UserCredential == null && window.DialogResult == null, "Failed preparation cannot count as a completed login");
        Pump(window.TryLoginAsync());
        Check(fake.Attempts.Last().Renew && !fake.Attempts.Last().Choose, "Retry after a scope error explicitly renews consent");
        string status = Text(window, "LoginStatus"); staleProgress!.Report("stale progress");
        Pump(Dispatcher.CurrentDispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle).Task);
        Check(Text(window, "LoginStatus") == status, "Late progress from a failed attempt cannot overwrite recovery instructions");
        Pump(window.TryLoginAsync(chooseAccount: true));
        Check(fake.Attempts.Last().Choose && window.IsVisible && Ready(window), "Switch-account failure still permits further retries");
        window.Close();

        var partial = new FakeLogin { Value = Credential() }; partial.Value.Token.Scope = "openid";
        var partialWindow = new LoginWindow(partial); int prepares = 0;
        partialWindow.PrepareDataAsync = (_, _, _) => { prepares++; return Task.CompletedTask; };
        Pump(partialWindow.TryLoginAsync());
        Check(prepares == 0 && Ready(partialWindow), "Partial consent never creates or opens a user database"); partialWindow.Close();

        var waiting = new FakeLogin { Wait = true }; var waitWindow = new LoginWindow(waiting);
        var attempt = waitWindow.TryLoginAsync();
        Check(!Button(waitWindow, "LoginButton").IsEnabled && !Button(waitWindow, "SwitchAccountButton").IsEnabled && Button(waitWindow, "CancelLogin").IsEnabled, "Pending login prevents duplicate attempts while allowing cancellation");
        Pump(waitWindow.TryLoginAsync(chooseAccount: true));
        Check(waiting.Attempts.Count == 1, "Double-click does not open another login flow");
        Button(waitWindow, "CancelLogin").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump(attempt);
        Check(Ready(waitWindow) && Text(waitWindow, "LoginHeading").Contains("중단"), "Stopping browser login returns to retry/account controls"); waitWindow.Close();

        var timed = new LoginWindow(new FakeLogin { Wait = true }) { LoginTimeout = TimeSpan.FromMilliseconds(30) };
        Pump(timed.TryLoginAsync());
        Check(Ready(timed) && Text(timed, "LoginHeading").Contains("초과"), "Login timeout restores controls rather than closing the app"); timed.Close();

        var prep = new LoginWindow(new FakeLogin()); bool prepCancelled = false;
        prep.PrepareDataAsync = async (_, _, token) => { try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { prepCancelled = true; throw; } };
        var prepAttempt = prep.TryLoginAsync();
        Button(prep, "CancelLogin").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)); Pump(prepAttempt);
        Check(prepCancelled && Ready(prep) && prep.UserCredential == null, "Database preparation can be cancelled without publishing a partial session"); prep.Close();

        var account = new FakeLogin(); var recovery = new LoginWindow(account) { TrySavedLoginOnOpen = true }; int calls = 0; string? preparedAccount = null;
        recovery.PrepareDataAsync = (credential, _, _) =>
        {
            if (++calls == 1) throw ScopeError();
            preparedAccount = credential.Token.AccessToken; return Task.CompletedTask;
        };
        recovery.ContentRendered += async (_, _) =>
        {
            Check(recovery.IsVisible && Ready(recovery), "Automatic saved login failure keeps its original window open");
            account.Value = Credential("different-account"); await recovery.TryLoginAsync(chooseAccount: true);
        };
        bool? result = recovery.ShowDialog();
        Check(result == true && preparedAccount == "different-account" && recovery.UserCredential?.Token.AccessToken == preparedAccount, "Switching accounts after failure prepares and returns only the new account");

        var retryFake = new FakeLogin(); var retryWindow = new LoginWindow(retryFake); int retryCalls = 0;
        retryWindow.PrepareDataAsync = (_, _, _) => ++retryCalls == 1 ? Task.FromException(new HttpRequestException("offline")) : Task.CompletedTask;
        retryWindow.ContentRendered += async (_, _) =>
        {
            await retryWindow.TryLoginAsync(); Check(Ready(retryWindow), "Temporary network failure leaves retry enabled");
            await retryWindow.TryLoginAsync();
        };
        Check(retryWindow.ShowDialog() == true && retryCalls == 2 && !retryFake.Attempts.Last().Renew, "Retry succeeds in the same app without forcing a new account for a network error");
    }

    public static void Preview()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml", UriKind.Relative));
        var window = new LoginWindow(new FakeLogin()) { TrySavedLoginOnOpen = true, Title = "배불뚝이 · 로그인 오류 복구 화면 검증" };
        window.PrepareDataAsync = (_, _, _) => Task.FromException(ScopeError());
        window.ShowDialog();
    }

    private sealed class FakeLogin : IGoogleLogin
    {
        internal UserCredential Value = Credential(); internal bool Wait;
        internal List<(bool Choose, bool Renew)> Attempts = [];
        public Task<UserCredential?> RestoreAsync(CancellationToken token) => Task.FromResult<UserCredential?>(Value);
        public async Task<UserCredential> AuthorizeAsync(bool chooseAccount, bool renewConsent, CancellationToken token)
        {
            Attempts.Add((chooseAccount, renewConsent));
            if (Wait) await Task.Delay(Timeout.Infinite, token);
            return Value;
        }
    }
    private sealed class MemoryStore : IDataStore
    {
        internal TokenResponse? Token; internal List<string> DeletedKeys = []; internal bool Cleared;
        public Task StoreAsync<T>(string key, T value) { Token = value as TokenResponse; return Task.CompletedTask; }
        public Task<T> GetAsync<T>(string key) => Task.FromResult(Token is T value ? value : default!);
        public Task DeleteAsync<T>(string key) { DeletedKeys.Add(key); Token = null; return Task.CompletedTask; }
        public Task ClearAsync() { Cleared = true; throw new InvalidOperationException("May not clear user data"); }
    }
    private sealed class Receiver : ICodeReceiver
    {
        internal int Calls; internal GoogleAuthorizationCodeRequestUrl? LastRequest;
        public string RedirectUri => "http://127.0.0.1:12345/authorize/";
        public Task<AuthorizationCodeResponseUrl> ReceiveCodeAsync(AuthorizationCodeRequestUrl url, CancellationToken cancellationToken)
        {
            Calls++; LastRequest = (GoogleAuthorizationCodeRequestUrl)url;
            return Task.FromResult(new AuthorizationCodeResponseUrl { Code = "fake-code", State = url.State });
        }
    }
    private sealed class TokenHttp : HttpMessageHandler, Google.Apis.Http.IHttpClientFactory
    {
        internal int Calls; internal string Scope = string.Join(' ', GoogleLogin.Scopes); internal string LastBody = "";
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args) => new(new ConfigurableMessageHandler(this));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; LastBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { access_token = "new-fake-account", refresh_token = "new-fake-refresh", expires_in = 3600, token_type = "Bearer", scope = Scope }), Encoding.UTF8, "application/json") };
        }
    }
    private sealed class PendingDriveHttp : HttpMessageHandler, Google.Apis.Http.IHttpClientFactory
    {
        internal TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously); internal bool Cancelled; internal int Writes;
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args) => new(new ConfigurableMessageHandler(this));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method != HttpMethod.Get) Writes++;
            Started.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, cancellationToken); }
            catch (OperationCanceledException) { Cancelled = true; throw; }
            throw new Exception("Unexpected completed request");
        }
    }
}
