using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace wpf;
public sealed class CatalogRequestDraft
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Kind { get; set; } = "메뉴 추가";
    public string Name { get; set; } = "";
    public string Track { get; set; } = "공통";
    public string ReferenceId { get; set; } = "";
    public string Details { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "";
    public static readonly string[] Kinds = ["메뉴 추가", "메뉴 수정", "식재료 추가", "식재료 수정"];
    public static readonly string[] States = ["접수", "검토 중", "반영 예정", "반영 완료", "보류"];
    public void Validate()
    {
        if (!Guid.TryParse(Id, out var id) || id == Guid.Empty || !Kinds.Contains(Kind) || !new[] { "공통", "3-5", "6-18" }.Contains(Track))
            throw new InvalidOperationException("요청 종류와 연령대를 선택해 주세요.");
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Details) || string.IsNullOrWhiteSpace(Reason))
            throw new InvalidOperationException("메뉴·식재료 이름, 요청 내용, 요청 이유를 입력해 주세요.");
        if (Name.Length > 120 || ReferenceId.Length > 160 || Details.Length > 4000 || Reason.Length > 1500 || Source.Length > 800)
            throw new InvalidOperationException("입력한 내용이 너무 깁니다. 요청 내용을 나누어 작성해 주세요.");
    }
}
public sealed class CatalogRequestItem
{
    public string Id { get; set; } = "";
    public string Email { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    public string Track { get; set; } = "";
    public string ReferenceId { get; set; } = "";
    public string Details { get; set; } = "";
    public string Reason { get; set; } = "";
    public string Source { get; set; } = "";
    public string Status { get; set; } = "";
    public string AdminNote { get; set; } = "";
    public long CreatedAt { get; set; }
    public long UpdatedAt { get; set; }
    public int Revision { get; set; }
    public string DateDisplay => DateTimeOffset.FromUnixTimeMilliseconds(CreatedAt).ToLocalTime().ToString("yyyy-MM-dd HH:mm");
    public string DetailDisplay => $"{Kind} · {Name}\n연령대: {Track} · 원본 번호: {ReferenceId}\n\n요청 내용\n{Details}\n\n요청 이유\n{Reason}\n\n참고 자료\n{Source}\n\n관리자 답변\n{AdminNote}";
}
public sealed class CatalogRequestList { public List<CatalogRequestItem> Items { get; set; } = []; public string? NextBefore { get; set; } public bool IsAdmin { get; set; } }
public sealed class CatalogRequestReceipt { public string Id { get; set; } = ""; public string Status { get; set; } = ""; }
public interface ICatalogRequestsClient
{
    Task<CatalogRequestList> ListAsync(bool all, string? before = null);
    Task<CatalogRequestReceipt> SubmitAsync(CatalogRequestDraft request);
    Task ReviewAsync(CatalogRequestItem item, string status, string note);
}
public sealed class CatalogRequestsClient(GoogleSheetsService service) : ICatalogRequestsClient
{
    public const string AdminEmail = "baebulttugi03@gmail.com";
    public const string Website = "https://baebulttugi.royal-snail-6531.chatgpt.site";
    private static readonly HttpClient http = new(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);
    private async Task<T> SendAsync<T>(HttpMethod method, object? body = null, string query = "")
    {
        using var request = new HttpRequestMessage(method, Website + "/api/requests" + query);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await service.GetIdentityTokenAsync());
        if (body != null) request.Content = JsonContent.Create(body, options: json);
        using var response = await http.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string message = "요청함에 연결하지 못했습니다. 인터넷 연결을 확인하고 다시 시도해 주세요.";
            try { var data = await response.Content.ReadFromJsonAsync<Dictionary<string, JsonElement>>(json); if (data?.TryGetValue("error", out var error) == true && error.ValueKind == JsonValueKind.String) message = error.GetString() ?? message; } catch { }
            throw new InvalidOperationException(message);
        }
        return await response.Content.ReadFromJsonAsync<T>(json) ?? throw new InvalidOperationException("요청함 응답을 확인하지 못했습니다.");
    }
    public Task<CatalogRequestList> ListAsync(bool all, string? before = null) => SendAsync<CatalogRequestList>(HttpMethod.Get, query: "?scope=" + (all ? "all" : "mine") + (before == null ? "" : "&before=" + Uri.EscapeDataString(before)));
    public Task<CatalogRequestReceipt> SubmitAsync(CatalogRequestDraft request) { request.Validate(); return SendAsync<CatalogRequestReceipt>(HttpMethod.Post, request); }
    public async Task ReviewAsync(CatalogRequestItem item, string status, string note)
    {
        if (!CatalogRequestDraft.States.Contains(status) || note.Length > 2000) throw new InvalidOperationException("검토 상태와 답변을 확인해 주세요.");
        await SendAsync<CatalogRequestReceipt>(HttpMethod.Patch, new { item.Id, status, adminNote = note, item.Revision });
    }
}
public static class CatalogRequestDraftStore
{
    private static string directoryPath = Path.Combine(AppConfig.UserDataDirectory, "RequestDrafts");
    private static string PathFor(string account) => Path.Combine(directoryPath, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.Trim().ToLowerInvariant()))) + ".json");
    public static CatalogRequestDraft? Read(string account)
    {
        try { var path = PathFor(account); return File.Exists(path) ? JsonSerializer.Deserialize<CatalogRequestDraft>(File.ReadAllText(path)) : null; } catch { return null; }
    }
    public static void Save(string account, CatalogRequestDraft draft)
    {
        if (string.IsNullOrWhiteSpace(account)) throw new InvalidOperationException("로그인 계정을 확인한 뒤 임시 저장해 주세요.");
        var path = PathFor(account); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(draft)); File.Move(path + ".tmp", path, true);
    }
    public static void Clear(string account) { var path = PathFor(account); if (File.Exists(path)) File.Delete(path); }
}
public partial class GoogleSheetsService
{
    public async Task<string> GetIdentityTokenAsync()
    {
        var credential = FacilityRegistry._credential ?? throw new InvalidOperationException("다시 로그인한 뒤 요청함을 이용해 주세요.");
        // Only the Google identity token goes to the request service. Drive access/refresh tokens stay on this PC.
        string? token = credential.Token.IdToken;
        bool refresh = true;
        if (!string.IsNullOrEmpty(token))
        {
            try { string payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/'); payload = payload.PadRight((payload.Length + 3) / 4 * 4, '='); using var doc = JsonDocument.Parse(Convert.FromBase64String(payload)); refresh = doc.RootElement.GetProperty("exp").GetInt64() < DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 120; } catch { }
        }
        if (refresh) { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)); await credential.RefreshTokenAsync(timeout.Token); token = credential.Token.IdToken; }
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("요청함 계정 연결을 위해 로그아웃한 뒤 다시 로그인해 주세요.");
        return token;
    }
}
