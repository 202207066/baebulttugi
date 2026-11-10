using wpf;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Http;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
internal static class FacilityChecks
{
    public static void Run()
    {
        var handler = new Handler();
        var root = new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(root, new SheetsService(new BaseClientService.Initializer { HttpClientFactory = new ClientFactory(handler) }));
        root.SetUserSpreadsheetId("registry");
        var a = root.ForFacility(new Facility("a","첫 급식소","book-a",25,true));
        var b = root.ForFacility(new Facility("b","둘째 급식소","book-b",80,false));
        if(a.SpreadsheetId != "book-a" || b.SpreadsheetId != "book-b" || root.SpreadsheetId != "registry") throw new Exception("Independent data scope failed");
        if(a.CurrentFacility?.Diners != 25 || b.CurrentFacility?.Diners != 80) throw new Exception("Count separation failed");
        a.GetValuesAsync("'메뉴'!A:Z").GetAwaiter().GetResult();
        b.GetValuesAsync("'알러지 인원'!A:E").GetAwaiter().GetResult();
        a.AppendRowAsync("'이벤트'!A:B",new object[]{"2026-09-26","행사"}).GetAwaiter().GetResult();
        if(!handler.Paths[0].Contains("book-a") || !handler.Paths[1].Contains("book-b") || !handler.Paths[2].Contains("book-a")) throw new Exception("Read/write crossed facilities");
        if(b.FacilityRegistry != root || a.FacilityRegistry != root) throw new Exception("Registry not shared");
        Console.WriteLine("PASS facility service isolation, per-facility counts, read/write routing and shared registry");
        CheckBootstrap("[[\"급식소 이름\",\"급식소 식수 인원\"]]", 0, false);
        CheckBootstrap("[[\"급식소 이름\",\"급식소 식수 인원\"],[\"유치원\",\"120\"]]", 120, false);
        CheckBootstrap("[[\"이름\",\"나이\",\"성별\",\"특이사항\"],[\"홍길동\",\"5\",\"남\",\"\"]]", 1, true);
        CheckBootstrap("[]", 0, false);
        Console.WriteLine("PASS header-only summary, populated summary, legacy roster and empty sheet bootstrap");
        CheckDeletion(false);
        CheckDeletion(true);
    }
    private static void CheckDeletion(bool fail)
    {
        var handler = new Handler { DinerRows = "[]", FailClear = fail,
            RegistryRows = "[[\"급식소ID\",\"급식소 이름\",\"자료ID\",\"급식소 식수 인원\",\"선택\",\"삭제\"],[\"one\",\"유한대\",\"fixture\",150,\"1\",\"0\"]]" };
        var service = new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, new SheetsService(new BaseClientService.Initializer { HttpClientFactory = new ClientFactory(handler) }));
        service.SetUserSpreadsheetId("fixture");
        bool failed = false;
        Facility? result = null;
        try { result = service.SaveFacilityAsync(new Facility("one","유한대","fixture",150,true), "유한대",150,true).GetAwaiter().GetResult(); }
        catch (Google.GoogleApiException) { failed = true; }
        if (failed != fail || result != null) throw new Exception("Last facility deletion state failed");
        var clear = handler.Bodies.Single(b => b.Path.EndsWith(":batchClear"));
        using var body = JsonDocument.Parse(clear.Body);
        var ranges = body.RootElement.GetProperty("ranges").EnumerateArray().Select(x => x.GetString()).ToList();
        if (ranges.Contains("'급식소'") || !ranges.Contains("'식수인원'")) throw new Exception("Deletion touched registry or missed facility data");
        if (fail && handler.Writes.Any(x => x.Contains("/values/"))) throw new Exception("Registry changed after failed data deletion");
        if (!fail) {
            var saved = handler.Bodies.Last().Body;
            if (saved.Contains("유한대") || saved.Contains("fixture")) throw new Exception("Deleted facility retained personal values");
        }
        Console.WriteLine("PASS " + (fail ? "failed deletion preserves registry" : "last facility deletion clears data and preserves registry sheet"));
    }
    private static void CheckBootstrap(string rows, int expected, bool roster)
    {
        var handler = new Handler { DinerRows = rows };
        var service = new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, new SheetsService(new BaseClientService.Initializer { HttpClientFactory = new ClientFactory(handler) }));
        service.SetUserSpreadsheetId("fixture");
        var facilities = service.GetFacilitiesAsync().GetAwaiter().GetResult();
        if (facilities.Count != 1 || facilities[0].Diners != expected || !facilities[0].Selected) throw new Exception("Bootstrap count/selection failed: " + rows);
        if (service.GetDinersAsync().GetAwaiter().GetResult().Count != (roster ? 1 : 0)) throw new Exception("Summary interpreted as roster");
        if (handler.Writes.Count != 1 || !handler.Writes[0].Contains("'급식소'!A2")) throw new Exception("Unexpected bootstrap write");
    }
    private sealed class ClientFactory(Handler handler) : IHttpClientFactory
    {
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args) => new(new ConfigurableMessageHandler(handler));
    }
    private sealed class Handler : HttpMessageHandler
    {
        public List<string> Paths = [];
        public List<string> Writes = [];
        public string? DinerRows;
        public string? RegistryRows;
        public bool FailClear;
        public List<(string Path, string Body)> Bodies = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            string path = Uri.UnescapeDataString(request.RequestUri.AbsolutePath);
            string json = "{}";
            if (request.Method != HttpMethod.Get) Writes.Add(path);
            if (request.Content != null) {
                var bytes = request.Content.ReadAsByteArrayAsync(cancellationToken).GetAwaiter().GetResult();
                if (bytes.Length > 2 && bytes[0] == 0x1f && bytes[1] == 0x8b) {
                    using var stream = new System.IO.Compression.GZipStream(new System.IO.MemoryStream(bytes), System.IO.Compression.CompressionMode.Decompress);
                    using var reader = new System.IO.StreamReader(stream);
                    Bodies.Add((path, reader.ReadToEnd()));
                } else Bodies.Add((path, Encoding.UTF8.GetString(bytes)));
            }
            if (FailClear && path.EndsWith(":batchClear")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent("{\"error\":{\"code\":400,\"message\":\"fixture failure\"}}",Encoding.UTF8,"application/json") });
            if (DinerRows != null && request.Method == HttpMethod.Get)
            {
                if (!path.Contains("/values/")) json = "{\"sheets\":[{\"properties\":{\"title\":\"급식소\"}},{\"properties\":{\"title\":\"식수인원\"}}]}";
                else if (path.Contains("식수인원")) json = "{\"values\":" + DinerRows + "}";
                else json = "{\"values\":" + (RegistryRows ?? "[[\"급식소ID\",\"급식소 이름\",\"자료ID\",\"급식소 식수 인원\",\"선택\",\"삭제\"]]") + "}";
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json,Encoding.UTF8,"application/json") });
        }
    }
}
