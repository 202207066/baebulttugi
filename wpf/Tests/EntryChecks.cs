using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;

internal static class EntryChecks
{
    public static void Run(Action<bool,string> check, string? fixture)
    {
        var handler = new EntryHttp();
        var service = new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new SheetsService(new BaseClientService.Initializer { HttpClientFactory=new EntryFactory(handler),ApplicationName="Entry tests" }));
        service.SetUserSpreadsheetId("personal-test");
        check(service.GetDinersAsync().GetAwaiter().GetResult().Count==1,"Diner count reads row-2 header and actual people");
        var add=service.AddDinersAsync([new("테스트", "5", "", "우유, 난류"),new("테스트", "5", "", "우유, 난류")]).GetAwaiter().GetResult();
        check(add==1 && service.GetDinersAsync().GetAwaiter().GetResult().Count==2,"CSV/direct entry saves one person per distinct row");
        check(handler.Diners[2][4].ToString()=="=1+1" && handler.LastMode=="RAW","Diner append preserves existing formula and uses literal input");
        var first=service.GetTrackCatalogAsync("3-5").GetAwaiter().GetResult(); int reads=handler.Calls;
        service.GetTrackCatalogAsync("3-5").GetAwaiter().GetResult();
        check(handler.Calls==reads,"Repeated catalog load uses five-minute cache without API calls");
        service.GetTrackCatalogAsync("3-5",true).GetAwaiter().GetResult();
        check(handler.Calls>reads,"Explicit refresh bypasses catalog cache");
        var menu=first.Menus[0] with { Carb=30,Protein=5,Fat=2,Calories=null,Source="test recipe" };
        service.SavePersonalRecipeAsync("3-5",menu,"쌀 40g").GetAwaiter().GetResult();
        var merged=service.GetTrackCatalogAsync("3-5").GetAwaiter().GetResult();
        check(merged.Menus.Count==first.Menus.Count && merged.Menus[0].Carb==30 && merged.Menus[0].Calories==158,"Personal recipe overrides shared key and derives missing kcal");
        menu=menu with { Carb=31 };
        service.SavePersonalRecipeAsync("3-5",menu,"쌀 41g").GetAwaiter().GetResult();
        check(handler.OwnA.Count==2 && handler.OwnA[1][11].ToString()=="31","Editing same recipe updates its row");
        service.SavePersonalRecipeAsync("6-18",menu,"다른 연령 레시피").GetAwaiter().GetResult();
        check(handler.OwnA.Count==2 && handler.OwnB.Count==2,"Tracks A and B retain separate personal recipe tables");
        check(handler.WriteBooks.All(x=>x=="personal-test"),"Entry writes never target shared source database");
        if(fixture!=null && File.Exists(fixture)) {
            var tracks=JsonSerializer.Deserialize<List<List<List<JsonElement>>>>(File.ReadAllText(fixture))!;
            foreach(var (rows,index) in tracks.Select((r,i)=>(r,i))) {
                var parsed=WeeklyMealPlanner.Parse(rows.Select(r=>(IList<object>)r.Select(c=>(object)c.ToString()).ToList()).ToList());
                var stopwatch=System.Diagnostics.Stopwatch.StartNew();
                var week=WeeklyMealPlanner.Generate(parsed,new DateTime(2026,9,21),[DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday],["조식","중식","석식"],index==0?"3-5":"6-18",index==0?"A":"B",new(60,20,15,15),seed:42);
                check(week.Count==15 && week.All(m=>m.Items.All(x=>x.HasNutrition)), $"Actual track {index} generates 15 complete meals ({stopwatch.ElapsedMilliseconds} ms)");
            }
        }
    }
}
internal sealed class EntryFactory(EntryHttp handler):IHttpClientFactory
{
    public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)=>new(new ConfigurableMessageHandler(handler));
}
internal sealed class EntryHttp:HttpMessageHandler
{
    public int Calls;
    public string LastMode="";
    public List<string> WriteBooks=[];
    public List<List<object>> Diners=[[],["이름","나이","성별","특이사항","등록일자"],["기존",4,"여","","=1+1"]];
    public List<List<object>> OwnA=[GoogleSheetsService.PersonalTrackHeaders.Cast<object>().ToList()];
    public List<List<object>> OwnB=[GoogleSheetsService.PersonalTrackHeaders.Cast<object>().ToList()];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
    {
        Calls++;string path=Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);object body;
        var table=path.Contains("식수인원")?Diners:path.Contains("내_트랙A")?OwnA:OwnB;
        if(request.Method==HttpMethod.Put) {
            LastMode=request.RequestUri.Query.Contains("RAW")?"RAW":"OTHER";
            WriteBooks.Add(path.Split('/')[3]);
            byte[] bytes=await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            string raw;
            if(bytes.Length>1&&bytes[0]==0x1f&&bytes[1]==0x8b){using var zip=new System.IO.Compression.GZipStream(new MemoryStream(bytes),System.IO.Compression.CompressionMode.Decompress);using var reader=new StreamReader(zip);raw=await reader.ReadToEndAsync();}else raw=Encoding.UTF8.GetString(bytes);
            using var doc=JsonDocument.Parse(raw);
            int row=int.Parse(System.Text.RegularExpressions.Regex.Match(path,@"!A(\d+)$").Groups[1].Value)-1;
            foreach(var values in doc.RootElement.GetProperty("values").EnumerateArray()) {var entry=values.EnumerateArray().Select(x=>x.ValueKind==JsonValueKind.Number?(object)x.GetDouble():x.ToString()).ToList();while(table.Count<=row)table.Add([]);table[row++]=entry;}
            body=new {};
        } else if(path.Contains("/values/")) {
            if(path.Contains("최종사용자값반영"))body=new{values=new List<List<object>> { new(){"메뉴명","열량(kcal)","탄수화물(g)","단백질(g)","지방(g)"}, new(){"테스트밥",57,10,2,1} }};
            else if(path.Contains("내_트랙")||path.Contains("식수인원"))body=new{values=table};
            else body=new{values=new List<List<object>>{GoogleSheetsService.PersonalTrackHeaders.Cast<object>().ToList(),new(){"","A-1","테스트밥","밥류","","","","","","",10,2,1,57}}};
        } else body=new {sheets=new[]{"식수인원","내_트랙A_레시피","내_트랙B_레시피",AppConfig.TrackASheetName,AppConfig.TrackBSheetName}.Select((title,i)=>new{properties=new{title,sheetId=i}})};
        return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(body),Encoding.UTF8,"application/json")};
    }
}
