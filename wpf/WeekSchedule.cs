using System.Globalization;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace wpf;

public record WeekDayNotice(DateTime Date,string Holiday,string Events)
{
    public string DateColor => Holiday.Length>0 || Date.DayOfWeek==DayOfWeek.Sunday ? "#C43F3F" : Date.DayOfWeek==DayOfWeek.Saturday ? "#2867B2" : "#28523A";
    public bool IsClosed => Holiday.Length>0 || Events.Split('\n').Any(IsClosure);
    public string Reason => Holiday.Length>0 ? "공휴일 · "+Holiday : string.Join(" · ",Events.Split('\n').Where(IsClosure));
    public string Description => string.Join(" · ",new[]{Holiday,Events.Replace('\n',' ')}.Where(x=>x.Length>0));
    // Explicit markers avoid interpreting an ordinary event such as '휴무 안내 회의' as closure.
    public static bool IsClosure(string text) => text.Contains("[휴무]",StringComparison.Ordinal) ||
        text[EventIconText.LeadingIcon(text).Length..].Trim() is "휴무" or "휴무일" or "휴원" or "휴관";
}

public static class WeekSchedule
{
    public static string HolidayStatus { get; private set; } = "공휴일 기본 자료 사용";
    private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(8)};
    public static async Task<Dictionary<DateTime,string>> HolidaysAsync(int year,int month)
    {
        var result=CalendarDates.Holidays(year).Where(p=>p.Key.Month==month).ToDictionary(p=>p.Key,p=>p.Value);
        string apiKey=AppConfig.HolidayApiKey;
        if(string.IsNullOrWhiteSpace(apiKey))
        {
            HolidayStatus=HolidayApiKeyStore.ReadError??"공휴일 기본 자료 사용 · 실시간 조회는 공휴일 키 등록 후 이용할 수 있습니다.";
            return result;
        }
        try
        {
            var json=await Client.GetStringAsync("https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo"+
                $"?serviceKey={Uri.EscapeDataString(apiKey)}&solYear={year}&solMonth={month:D2}&_type=json&numOfRows=100");
            var response=JObject.Parse(json)["response"];
            string? code=response?["header"]?["resultCode"]?.ToString();
            if(code!=null && code!="00" && code!="0")throw new Newtonsoft.Json.JsonException("공휴일 API 오류 응답");
            if(response?["body"]==null)throw new Newtonsoft.Json.JsonException("공휴일 응답 형식 오류");
            var item=response?["body"]?["items"]?["item"];
            IEnumerable<JToken> items=item is JArray array?array:item is JObject one?new[]{one}:Array.Empty<JToken>();
            foreach(var row in items)
                if(row["isHoliday"]?.ToString()=="Y" && DateTime.TryParseExact(row["locdate"]?.ToString(),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))
                    result[date]=row["dateName"]?.ToString()??"공휴일";
            HolidayStatus="공휴일 API 조회 완료";
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or Newtonsoft.Json.JsonException)
        { HolidayStatus="공휴일 API 조회 실패 · 기본 자료를 표시합니다. 키 등록 상태와 연결을 확인해 주세요."; System.Diagnostics.Debug.WriteLine("공휴일 기본 자료 사용: "+ex.GetType().Name); }
        return result;
    }
    public static IReadOnlyList<WeekDayNotice> Build(DateTime selected,IList<IList<object>> rows,IReadOnlyDictionary<DateTime,string> holidays)
    {
        var start=WeeklyMealPlanner.Sunday(selected);
        var events=AnnualEvent.Parse(rows);
        return Enumerable.Range(0,7).Select(i=>start.AddDays(i)).Select(d=>new WeekDayNotice(d,holidays.GetValueOrDefault(d,""),
            string.Join("\n",events.Where(item=>item.OccursOn(d)).Select(item=>item.Content)))).ToArray();
    }
    public static async Task<IReadOnlyList<WeekDayNotice>> LoadAsync(GoogleSheetsService service,DateTime selected)
    {
        var start=WeeklyMealPlanner.Sunday(selected);
        var holidays=new Dictionary<DateTime,string>();
        foreach(var month in Enumerable.Range(0,7).Select(i=>start.AddDays(i)).Select(d=>(d.Year,d.Month)).Distinct())
            foreach(var pair in await HolidaysAsync(month.Year,month.Month))holidays[pair.Key]=pair.Value;
        var titles=await service.GetSheetTitlesAsync();
        IList<IList<object>> rows=titles.Contains(AppConfig.EventSheetName)?await service.GetValuesAsync("'"+AppConfig.EventSheetName.Replace("'","''")+"'!A:B"):new List<IList<object>>();
        return Build(start,rows,holidays);
    }
}
