using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;

internal static class CostCalculationChecks
{
    internal static List<CostDraft> Inputs(StoredMeal meal)=>meal.Menus.Select(m=>new CostDraft { Menu=m,Name="양파",Amount="90",Unit="g",Price="2000",PriceQuantity="1",PriceUnit="kg",WastePercent="10" }).ToList();
    internal static GoogleSheetsService Service(CostCalculationSheets http)
    {
        var service=new GoogleSheetsService(null!);service.SetUserSpreadsheetId("cost-calculation-isolated-test");
        typeof(GoogleSheetsService).GetField("_sheetsService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new SheetsService(new BaseClientService.Initializer { HttpClientFactory=new CostCalculationFactory(http) }));
        return service;
    }
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var meal=ManagementChecks.Fixture();var drafts=Inputs(meal);
        var calc=MealCostCalculation.Create(meal,drafts,30,true);var first=calc.Items[0];
        check(CostCalculator.GrossSize(first.Inputs)==100,"원가식 1: 정미량 90g·폐기율 10%의 1인분 구매량은 100g");
        check(CostCalculator.PurchaseBasis(first.Inputs)==(1000m,"g")&&CostCalculator.PricePerBaseUnit(first.Inputs)==2,"원가식 2: 1kg 2000원은 g당 2원");
        check(first.PerPersonCost==200,"원가식 3: 구매량 100g × 2원 = 재료비 200원");
        check(calc.PerPersonCost==1200,"원가식 4: 여섯 메뉴의 재료비 합계는 한 끼 1200원");
        check(calc.TotalCost==36000,"원가식 5: 1200원 × 30명 = 한 끼 총 36000원");
        check(first.TotalQuantity==3000&&first.TotalCost==6000,"원가식 6: 재료별 발주량 3000g 및 재료 총 원가 6000원");
        var pack=new CostRow("달걀","",54,"g",6000,30,"개",10,60);
        check(CostCalculator.PurchaseBasis(pack)==(1800m,"g")&&CostCalculator.CostPerPerson(pack)==200,"30개 × 개당 60g을 1800g으로 환산하고 재료비 계산");
        check(CostCalculator.CostPerPerson(first.Inputs with { UnitSize=.09m,Unit="kg" })==200,"kg 정미량도 g 구매량과 같은 비용");
        check(CostCalculator.PurchaseBasis(pack with { PriceUnit="L",PriceQuantity=2 })==(2000m,"ml"),"액체는 g으로 추측하지 않고 ml 기준 환산");
        reject(()=>CostCalculator.CostPerPerson(pack with { PackageWeightGrams=null }),"개당 중량 없이 g과 개를 환산하지 않음");
        var clone=drafts.Select(MealCostData.Clone).ToList();clone[0].WastePercent="";
        check(!MealCostCalculation.Create(meal,clone,30).Complete,"빈 폐기율은 계산 확정으로 표시하지 않음");
        reject(()=>MealCostCalculation.Create(meal,clone,30,true),"빈 폐기율을 0%로 확정 저장하지 않음");
        clone[0].WastePercent="0";clone[0].Price="";
        reject(()=>MealCostCalculation.Create(meal,clone,30,true),"단가 미입력 소계를 한 끼 확정 원가로 저장하지 않음");
        clone[0].Price="0";
        check(MealCostCalculation.Create(meal,clone,30,true).Complete,"명시적으로 입력한 0원·0%는 유효함");
        clone[0].WastePercent="100";reject(()=>MealCostCalculation.Create(meal,clone,30,true),"100% 폐기율 거부");
        reject(()=>MealCostCalculation.Create(meal,drafts,0,true),"식수 0명 저장 거부");
        reject(()=>MealCostCalculation.Create(meal,drafts.Skip(1).ToList(),30,true),"식단에서 빠진 메뉴 재료가 있으면 한 끼 합계 저장 거부");
        check(calc.Fingerprint!=MealCostCalculation.Create(meal,drafts,31,true).Fingerprint,"식수가 바뀌면 이전 계산 기록과 구별됨");
        clone=drafts.Select(MealCostData.Clone).ToList();clone[0].Amount="45";
        check(calc.Fingerprint!=MealCostCalculation.Create(meal,clone,30,true).Fingerprint,"정미량이 바뀌면 이전 계산 기록과 구별됨");
        var rows=calc.ToSheetRows(meal,DateTimeOffset.UtcNow);
        check(rows.Count==7&&rows[0][1].ToString()=="합계"&&Convert.ToDecimal(rows[0][22])==1200&&Convert.ToDecimal(rows[1][19])==200,"시트에 한 끼 합계와 재료별 재료비를 별도 행으로 저장");

        var http=new CostCalculationSheets(meal);var service=Service(http);
        service.SaveMealCalculationAsync(meal,drafts,30).GetAwaiter().GetResult();
        check(http.Tables.ContainsKey(GoogleSheetsService.MealCalculationSheet),"최초 원가 저장 시 계산 결과 시트를 생성함");
        var saved=service.GetSavedMealCalculationsAsync().GetAwaiter().GetResult().Single();
        check(saved.PerPersonCost==1200&&saved.TotalCost==36000&&saved.HeadCount==30&&saved.Fingerprint==calc.Fingerprint,"저장한 한 끼 합계를 다시 읽어 복원함");
        check(http.AtomicCalculationBatches==1,"구매 기준·레시피 재료와 원가 결과를 한 번의 일괄 저장 요청에 포함함");
        var costs=service.GetMealCostsAsync(true).GetAwaiter().GetResult();
        check(costs.Count(IngredientStandards.IsStandard)==1&&costs.Count(c=>!IngredientStandards.IsStandard(c))==6,"같은 양파 기준은 한 번 저장하고 메뉴별 정미량 여섯 행 유지");
        var reloaded=MealCostData.Build(meal,costs);
        check(MealCostCalculation.Create(meal,reloaded,30,true).Fingerprint==calc.Fingerprint,"저장 후 재조회한 입력값이 계산 기록과 일치함");
        service.SaveMealCalculationAsync(meal,reloaded,30).GetAwaiter().GetResult();
        check(http.Tables[GoogleSheetsService.MealCalculationSheet].Count==8,"같은 식단을 재저장해도 합계·재료 행 중복 없음");
        var other=ManagementChecks.Fixture();other.Raw[1]="2026-09-22";
        var another=new StoredMeal {Raw=other.Raw,Date=new(2026,9,22),RowIndex=2};http.Tables[AppConfig.WeeklyMenuSheetName].Add(another.Raw);
        service.SaveMealCalculationAsync(another,Inputs(another),10).GetAwaiter().GetResult();
        var latest=MealCostData.Build(meal,service.GetMealCostsAsync(true).GetAwaiter().GetResult());
        service.SaveMealCalculationAsync(meal,latest,40).GetAwaiter().GetResult();
        var all=service.GetSavedMealCalculationsAsync().GetAwaiter().GetResult();
        check(all.Count==2&&all.Single(s=>s.Identity==another.Identity).HeadCount==10&&all.Single(s=>s.Identity==meal.Identity).TotalCost==48000,"식수 변경 재저장은 해당 식단만 갱신하고 다른 날짜 원가 보존");
        var before=http.AtomicCalculationBatches;var changed=MealCostData.Build(meal,service.GetMealCostsAsync(true).GetAwaiter().GetResult());
        http.Tables[AppConfig.WeeklyMenuSheetName][1][3]="다른 곳에서 바뀐 메뉴";
        reject(()=>service.SaveMealCalculationAsync(meal,changed,30).GetAwaiter().GetResult(),"메뉴가 다른 곳에서 수정되면 오래된 식단 원가 저장 중단");
        check(http.AtomicCalculationBatches==before,"오래된 식단을 거부할 때 재료·원가 쓰기 없음");
        http.Tables[AppConfig.WeeklyMenuSheetName][1][3]=meal.Rice;
        http.Tables[GoogleSheetsService.MealCalculationSheet][0][7]="다른 용도 열";
        reject(()=>service.SaveMealCalculationAsync(meal,changed,30).GetAwaiter().GetResult(),"기존 결과 시트 구조가 다르면 사용자 자료를 덮어쓰지 않음");
        check(http.AtomicCalculationBatches==before,"결과 시트 검증 실패 시 공통 재료 기준도 함께 쓰지 않음");
        Console.WriteLine("Cost formula and persistence checks use isolated HTTP responses; no live Google requests.");
    }
}

internal sealed class CostCalculationFactory(CostCalculationSheets handler):Google.Apis.Http.IHttpClientFactory
{
    public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args)=>new(new Google.Apis.Http.ConfigurableMessageHandler(handler));
}
internal sealed class CostCalculationSheets:HttpMessageHandler
{
    internal Dictionary<string,List<IList<object>>> Tables {get;}=[];
    internal int AtomicCalculationBatches;
    internal CostCalculationSheets(StoredMeal meal)
    {
        Tables[AppConfig.WeeklyMenuSheetName]=[GoogleSheetsService.WeeklyHeaders.Cast<object>().ToArray(),meal.Raw.ToList()];
        Tables[AppConfig.CostSheetName]=[GoogleSheetsService.CostHeaders.Cast<object>().ToArray()];
        Tables["식수인원"]=[new object[]{"이름","나이","성별","특이사항"}];
    }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        string path=Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        if(!path.StartsWith("/v4/spreadsheets/cost-calculation-isolated-test"))throw new InvalidOperationException("검증에서 외부 접근 차단");
        object response=new{};
        JsonDocument? body=null;
        if(request.Content!=null){var bytes=await request.Content.ReadAsByteArrayAsync(ct);if(bytes.Length>1&&bytes[0]==0x1f&&bytes[1]==0x8b){using var zip=new GZipStream(new MemoryStream(bytes),CompressionMode.Decompress);body=await JsonDocument.ParseAsync(zip,cancellationToken:ct);}else body=JsonDocument.Parse(bytes);}
        using(body)
        {
            if(request.Method==HttpMethod.Post&&path.EndsWith(":batchUpdate"))
            {
                var requests=body!.RootElement.GetProperty("requests").EnumerateArray().ToArray();
                List<IList<object>> Table(int id)=>Tables.ElementAt(id-1).Value;
                IList<object> Values(JsonElement row)=>row.GetProperty("values").EnumerateArray().Select(c=>{if(!c.TryGetProperty("userEnteredValue",out var v))return (object)"";return v.TryGetProperty("numberValue",out var n)?(object)n.GetDecimal():v.GetProperty("stringValue").GetString()!;}).ToList();
                bool costWrite=false,resultWrite=false;
                foreach(var r in requests)
                {
                    if(r.TryGetProperty("addSheet",out var add)){Tables.Add(add.GetProperty("properties").GetProperty("title").GetString()!,[]);}
                    if(r.TryGetProperty("deleteDimension",out var delete)){var range=delete.GetProperty("range");Table(range.GetProperty("sheetId").GetInt32()).RemoveRange(range.GetProperty("startIndex").GetInt32(),range.GetProperty("endIndex").GetInt32()-range.GetProperty("startIndex").GetInt32());}
                    if(r.TryGetProperty("updateCells",out var update)){var start=update.GetProperty("start");var table=Table(start.GetProperty("sheetId").GetInt32());int index=start.GetProperty("rowIndex").GetInt32();foreach(var row in update.GetProperty("rows").EnumerateArray()){while(table.Count<=index)table.Add([]);table[index++]=Values(row);}}
                    if(r.TryGetProperty("appendCells",out var append)){int id=append.GetProperty("sheetId").GetInt32();Table(id).AddRange(append.GetProperty("rows").EnumerateArray().Select(Values));costWrite|=Tables.ElementAt(id-1).Key==AppConfig.CostSheetName;resultWrite|=Tables.ElementAt(id-1).Key==GoogleSheetsService.MealCalculationSheet;}
                }
                if(costWrite&&resultWrite)AtomicCalculationBatches++;
            }
            else if(path.Contains("/values/"))
            {
                string title=path.Split("/values/")[1].Split('!')[0].Trim('\'');
                if(request.Method==HttpMethod.Put)Tables[title]=body!.RootElement.GetProperty("values").EnumerateArray().Select(r=>(IList<object>)r.EnumerateArray().Select(v=>v.ValueKind==JsonValueKind.Number?(object)v.GetDecimal():v.GetString()!).ToList()).ToList();
                else response=new {values=Tables.GetValueOrDefault(title,[])};
            }
            else response=new {sheets=Tables.Keys.Select((title,i)=>new {properties=new {title,sheetId=i+1,gridProperties=new {columnCount=50,rowCount=2000}}})};
        }
        return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(response),Encoding.UTF8,"application/json")};
    }
}
