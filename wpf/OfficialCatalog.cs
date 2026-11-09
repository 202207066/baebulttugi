using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace wpf;

public sealed class OfficialCatalogData
{
    public int SchemaVersion { get; set; }
    public string Version { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string Source { get; set; } = "";
    public List<OfficialMenu> Menus { get; set; } = [];
    public List<RecipePortion> Recipes { get; set; } = [];
    public Dictionary<string,string> Cooking { get; set; } = [];
}
public sealed class OfficialMenu
{
    public string Track { get; set; } = "";
    public TrackMenu Menu { get; set; } = null!;
    public string Review { get; set; } = "";
}
/// <summary>Official data is read-only; all user edits remain in the user's own spreadsheet.</summary>
public static class OfficialCatalog
{
    private const int MaxBytes = 12 * 1024 * 1024;
    private static readonly SemaphoreSlim updateGate = new(1,1);
    private static readonly object gate = new();
    private static OfficialCatalogData? current;
    public static string? CacheWarning { get; private set; }
    private static string CachePath => Path.Combine(AppConfig.UserDataDirectory,"Catalog","official.json");
    public static OfficialCatalogData Current
    {
        get { lock(gate) {
            if(current != null) return current;
            using var embedded=typeof(OfficialCatalog).Assembly.GetManifestResourceStream("wpf.OfficialCatalog")
                ?? throw new InvalidOperationException("공식 자료가 포함되지 않았습니다. 배포 파일을 다시 받아 주세요.");
            current=Validate(JsonSerializer.Deserialize<OfficialCatalogData>(embedded));
            if(File.Exists(CachePath)) try {
                if(new FileInfo(CachePath).Length > MaxBytes) throw new InvalidDataException();
                var cached=Validate(JsonSerializer.Deserialize<OfficialCatalogData>(File.ReadAllBytes(CachePath)));
                if(System.Version.Parse(cached.Version)>System.Version.Parse(current.Version))current=cached;
            } catch { CacheWarning="저장된 공식 자료를 읽지 못해 앱에 포함된 자료를 사용합니다. 자료 업데이트를 다시 눌러 주세요."; }
            return current;
        } }
    }
    public static OfficialCatalogData Validate(OfficialCatalogData? data)
    {
        if(data==null || data.SchemaVersion!=1 || !Regex.IsMatch(data.Version??"",@"^\d{4}\.\d{1,2}\.\d{1,2}\.\d{1,5}$") || !System.Version.TryParse(data.Version,out _)
            || data.Menus==null || data.Recipes==null || data.Cooking==null || data.Menus.Count is <1 or >50000)
            throw new InvalidDataException("공식 자료 형식 또는 버전을 확인할 수 없습니다.");
        var keys=new HashSet<string>();
        foreach(var entry in data.Menus){
            var m=entry.Menu;
            if(entry.Track is not ("A" or "B") || m==null || string.IsNullOrWhiteSpace(m.Id) || string.IsNullOrWhiteSpace(m.Name)
                || m.Name.Length>300 || !WeeklyMealPlanner.Categories.Contains(m.Category)
                || !keys.Add(entry.Track+":"+m.Key) || new[]{m.Carb,m.Protein,m.Fat,m.Calories,m.ServingGrams}.Any(x=>x.HasValue&&(!double.IsFinite(x.Value)||x<0)))
                throw new InvalidDataException("공식 메뉴의 식별자·분류·영양값을 확인할 수 없습니다.");
        }
        foreach(var entry in data.Menus){var m=entry.Menu;if(m.ProteinReviewed && MenuClassification.IsSide(m) && MenuClassification.Grade(m)=="검토 필요")throw new InvalidDataException("검토 승인된 메뉴의 제공량·단백질 기준을 확인할 수 없습니다.");}
        var recipeKeys=new HashSet<string>();
        foreach(var r in data.Recipes)
            if(r==null || !keys.Contains(r.Track+":"+r.Key) || !recipeKeys.Add(r.Track+":"+r.Key) || r.Ingredients==null || r.Ingredients.Count>200
                || r.Ingredients.Any(i=>i==null||string.IsNullOrWhiteSpace(i.Name)||i.Amount<0||i.Unit is not ("g" or "kg" or "ml" or "l" or "개" or "봉")))
                throw new InvalidDataException("공식 식재료 자료를 확인할 수 없습니다.");
        if(data.Cooking.Count>50000 || data.Cooking.Any(x=>x.Key.Length>500 || x.Value==null || x.Value.Length>50000))
            throw new InvalidDataException("공식 조리방법 자료를 확인할 수 없습니다.");
        return data;
    }
    public static List<TrackMenu> Menus(string age)=>Current.Menus.Where(x=>x.Track==WeeklyMealPlanner.TrackForAge(age)).Select(x=>MenuClassification.Official(x.Menu)).ToList();
    private sealed record UpdateManifest(int SchemaVersion,string Version,string File,string Sha256,int Bytes);
    public static async Task<string> UpdateAsync(CancellationToken cancellationToken=default)
    {
        if (RuntimeEdition.IsLocal) return "로컬 버전의 공식 자료는 새 배포 파일로 업데이트합니다. 기존 PC의 개인 자료는 유지됩니다.";
        await updateGate.WaitAsync(cancellationToken);
        try{
            using var http=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(45)};
            var root=CatalogRequestsClient.Website+"/catalog/";
            var manifest=JsonSerializer.Deserialize<UpdateManifest>(await ReadLimitedAsync(http,root+"latest.json",10000,cancellationToken),new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if(manifest==null || manifest.SchemaVersion!=1 || !System.Version.TryParse(manifest.Version,out var version)
                || !Regex.IsMatch(manifest.File??"",@"^catalog-[0-9.]+\.json$") || !Regex.IsMatch(manifest.Sha256??"",@"^[0-9a-f]{64}$") || manifest.Bytes is <1 or >MaxBytes)
                throw new InvalidDataException("업데이트 안내를 확인할 수 없습니다. 현재 자료는 유지합니다.");
            if(version<=System.Version.Parse(Current.Version))return "현재 공식 자료가 최신입니다. 버전 "+Current.Version;
            var bytes=await ReadLimitedAsync(http,root+manifest.File,MaxBytes,cancellationToken);
            if(bytes.Length!=manifest.Bytes || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=manifest.Sha256)
                throw new InvalidDataException("다운로드한 자료의 확인 값이 다릅니다. 현재 자료는 유지합니다.");
            var next=Validate(JsonSerializer.Deserialize<OfficialCatalogData>(bytes));
            if(next.Version!=manifest.Version)throw new InvalidDataException("자료 버전이 안내와 다릅니다.");
            Directory.CreateDirectory(Path.GetDirectoryName(CachePath)!);
            var temp=CachePath+".tmp";await File.WriteAllBytesAsync(temp,bytes,cancellationToken);File.Move(temp,CachePath,true);
            lock(gate){current=next;CacheWarning=null;}
            return "공식 자료를 "+next.Version+" 버전으로 업데이트했습니다. 내 레시피·식재료·저장 식단은 유지됩니다.";
        } finally {updateGate.Release();}
    }
    private static async Task<byte[]> ReadLimitedAsync(HttpClient http,string url,int limit,CancellationToken ct)
    {
        using var response=await http.GetAsync(url,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>limit)throw new InvalidDataException("자료가 너무 큽니다.");
        await using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();var buffer=new byte[81920];
        int read;while((read=await input.ReadAsync(buffer,ct))>0){if(output.Length+read>limit)throw new InvalidDataException("자료가 너무 큽니다.");output.Write(buffer,0,read);}return output.ToArray();
    }
}
