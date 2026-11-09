using System.IO;
using System.Net;
using System.Net.Http;
using Google;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Drive.v3;
using Google.Apis.Http;
using Google.Apis.Util.Store;

namespace wpf;

internal interface IGoogleLogin
{
    Task<UserCredential?> RestoreAsync(CancellationToken cancellationToken);
    Task<UserCredential> AuthorizeAsync(bool chooseAccount, bool renewConsent, CancellationToken cancellationToken);
}

internal sealed class GoogleLogin : IGoogleLogin
{
    internal static readonly string[] Scopes = [DriveService.Scope.DriveFile, "openid", "email", "profile"];
    private readonly IDataStore store;
    private readonly Func<ClientSecrets> readSecrets;
    private readonly ICodeReceiver receiver;
    private readonly Google.Apis.Http.IHttpClientFactory? httpClientFactory;

    internal GoogleLogin(IDataStore? store = null, Func<ClientSecrets>? readSecrets = null,
        ICodeReceiver? receiver = null, Google.Apis.Http.IHttpClientFactory? httpClientFactory = null)
    {
        this.store = store ?? new FileDataStore(AppConfig.TokenStorePath, true);
        this.readSecrets = readSecrets ?? (() =>
        {
            using var stream = AppConfig.OpenCredentials();
            return GoogleClientSecrets.FromStream(stream).Secrets;
        });
        this.receiver = receiver ?? new LocalServerCodeReceiver();
        this.httpClientFactory = httpClientFactory;
    }

    private PkceGoogleAuthorizationCodeFlow CreateFlow(string? prompt = null)
    {
        var settings = new GoogleAuthorizationCodeFlow.Initializer
        {
            ClientSecrets = readSecrets(), Scopes = Scopes, DataStore = store, Prompt = prompt
        };
        if (httpClientFactory != null) settings.HttpClientFactory = httpClientFactory;
        return new PkceGoogleAuthorizationCodeFlow(settings);
    }

    private async Task<TokenResponse?> ReadTokenAsync()
    {
        try { return await store.GetAsync<TokenResponse>("user"); }
        catch (Newtonsoft.Json.JsonException) { throw new InvalidSavedLoginException(); }
    }

    internal static void CheckGrantedScopes(TokenResponse token)
    {
        // Refresh/older responses may omit scope; the subsequent Drive request verifies access.
        if (!string.IsNullOrWhiteSpace(token.Scope) &&
            !token.Scope.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Contains(DriveService.Scope.DriveFile, StringComparer.Ordinal))
            throw new MissingDrivePermissionException();
    }

    public async Task<UserCredential?> RestoreAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var token = await ReadTokenAsync();
        if (token == null) return null;
        CheckGrantedScopes(token);
        var flow = CreateFlow();
        try
        {
            var credential = new UserCredential(flow, "user", token);
            await credential.GetAccessTokenForRequestAsync(null, cancellationToken);
            CheckGrantedScopes(credential.Token);
            return credential;
        }
        catch { flow.Dispose(); throw; }
    }

    public async Task<UserCredential> AuthorizeAsync(bool chooseAccount, bool renewConsent, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        bool fresh = chooseAccount || renewConsent;
        if (fresh) await store.DeleteAsync<TokenResponse>("user");
        else
        {
            var saved = await ReadTokenAsync();
            if (saved != null) CheckGrantedScopes(saved);
            else fresh = true;
        }
        // Delete only this PC's token, never revoke Google access or delete Drive data.
        var flow = CreateFlow(fresh ? "select_account consent" : null);
        try
        {
            var credential = await new AuthorizationCodeInstalledApp(flow, receiver).AuthorizeAsync("user", cancellationToken);
            CheckGrantedScopes(credential.Token);
            return credential;
        }
        catch { flow.Dispose(); throw; }
    }

    internal static Task ClearSavedAsync() => new FileDataStore(AppConfig.TokenStorePath, true).DeleteAsync<TokenResponse>("user");
}

internal sealed class MissingDrivePermissionException : Exception;
internal sealed class InvalidSavedLoginException : Exception;

internal sealed record LoginFailure(string Message, bool RenewConsent, string Details)
{
    internal static LoginFailure From(Exception error)
    {
        if (error is MissingDrivePermissionException || IsInsufficientScope(error))
            return new("식단 자료를 읽고 저장하는 Google Drive 권한이 허용되지 않았습니다.\n\n‘다시 시도’를 누른 뒤 Google 동의 화면에서 배불뚝이가 사용하는 파일의 보기·수정 권한을 허용해 주세요. 다른 계정으로도 로그인할 수 있습니다.", true, "Google Drive 권한 부족 (403)");
        if (error is InvalidSavedLoginException)
            return new("이 PC에 저장된 로그인 정보를 읽을 수 없습니다. ‘다시 시도’를 눌러 새로 로그인하거나 다른 계정을 선택해 주세요.", true, "저장된 로그인 정보 확인 필요");
        if (error is TokenResponseException token)
            return token.Error?.Error switch
            {
                "invalid_grant" => new("저장된 로그인이 만료되었거나 취소되었습니다. ‘다시 시도’를 눌러 로그인해 주세요.", true, "Google 로그인 만료 (invalid_grant)"),
                "access_denied" => new("Google 로그인이 취소되었거나 계정에서 접근을 허용하지 않았습니다. 다시 시도하거나 다른 계정으로 로그인해 주세요. 학교·기관 계정의 접근 제한은 해당 관리자에게 확인해 주세요.", true, "Google 로그인 거부 (access_denied)"),
                "invalid_client" or "unauthorized_client" or "invalid_scope" or "redirect_uri_mismatch" => new("앱의 Google 로그인 설정을 확인해야 합니다. 최신 배포 파일로 실행해 주세요. 계속 실패하면 baebulttugi03@gmail.com으로 문의해 주세요.", false, "앱 로그인 설정 확인 필요"),
                _ => new("Google 연결을 완료하지 못했습니다. 잠시 후 다시 시도하거나 다른 계정으로 로그인해 주세요.", false, "Google 로그인 응답 오류")
            };
        if (error is GoogleApiException api)
        {
            if (api.HttpStatusCode == HttpStatusCode.Unauthorized)
                return new("Google 로그인 정보를 다시 확인해야 합니다. ‘다시 시도’를 눌러 로그인해 주세요.", true, "Google 인증 필요 (401)");
            if (api.HttpStatusCode == HttpStatusCode.Forbidden)
                return new("Google에서 자료 접근을 허용하지 않았습니다. 다른 계정으로 로그인하거나 계정·자료의 접근 권한을 확인한 뒤 다시 시도해 주세요. 계속 실패하면 baebulttugi03@gmail.com으로 문의해 주세요.", false, "Google 자료 접근 제한 (403)");
            if (api.HttpStatusCode == HttpStatusCode.TooManyRequests || (int)api.HttpStatusCode >= 500)
                return new("Google 서버가 일시적으로 응답하지 않습니다. 잠시 후 ‘다시 시도’를 눌러 주세요.", false, $"Google 서버 응답 ({(int)api.HttpStatusCode})");
        }
        if (error is HttpRequestException || error is OperationCanceledException || error is TimeoutException)
            return new("연결을 완료하지 못했습니다. 인터넷 연결을 확인한 뒤 다시 시도하거나 다른 계정으로 로그인해 주세요.", false, "연결 중단 또는 응답 시간 초과");
        return new("시작에 필요한 자료를 준비하지 못했습니다. 다시 시도하거나 다른 계정으로 로그인해 주세요. 계속 실패하면 최신 배포 파일로 실행하거나 baebulttugi03@gmail.com으로 문의해 주세요.", false, $"오류 종류: {error.GetType().Name}");
    }

    internal static bool IsInsufficientScope(Exception error) => error is GoogleApiException api &&
        api.HttpStatusCode == HttpStatusCode.Forbidden &&
        (api.Error?.Errors?.Any(e => e.Reason is "insufficientPermissions" or "ACCESS_TOKEN_SCOPE_INSUFFICIENT") == true ||
         api.Message.Contains("insufficient authentication scopes", StringComparison.OrdinalIgnoreCase) ||
         api.Message.Contains("ACCESS_TOKEN_SCOPE_INSUFFICIENT", StringComparison.Ordinal));
}
