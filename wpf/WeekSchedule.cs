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
    private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(8)};
    public static async Task<Dictionary<DateTime,string>> HolidaysAsync(int year,int month)
    {
        var result=CalendarDates.Holidays(year).Where(p=>p.Key.Month==month).ToDictionary(p=>p.Key,p=>p.Value);
        if(!AppConfig.HasHolidayApiKey)return result;
        try
        {
            var json=await Client.GetStringAsync("https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo"+
                $"?serviceKey={Uri.EscapeDataString(AppConfig.HolidayApiKey)}&solYear={year}&solMonth={month:D2}&_type=json&numOfRows=100");
            var item=JObject.Parse(json)["response"]?["body"]?["items"]?["item"];
            IEnumerable<JToken> items=item is JArray array?array:item is JObject one?new[]{one}:Array.Empty<JToken>();
            foreach(var row in items)
                if(row["isHoliday"]?.ToString()=="Y" && DateTime.TryParseExact(row["locdate"]?.ToString(),"yyyyMMdd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date))
                    result[date]=row["dateName"]?.ToString()??"공휴일";
        }
        catch(Exception ex) when(ex is HttpRequestException or TaskCanceledException or Newtonsoft.Json.JsonException)
        { System.Diagnostics.Debug.WriteLine("공휴일 기본 자료 사용: "+ex.Message); }
        return result;
    }
    public static IReadOnlyList<WeekDayNotice> Build(DateTime selected,IList<IList<object>> rows,IReadOnlyDictionary<DateTime,string> holidays)
    {
        var start=WeeklyMealPlanner.Sunday(selected);
        var events=new Dictionary<DateTime,List<string>>();
        foreach(var row in rows)
            if(row.Count>1 && DateTime.TryParse(row[0]?.ToString(),out var date) && !string.IsNullOrWhiteSpace(row[1]?.ToString()))
            {
                if(!events.TryGetValue(date.Date,out var list))events[date.Date]=list=[];
                list.Add(row[1].ToString()!);
            }
        return Enumerable.Range(0,7).Select(i=>start.AddDays(i)).Select(d=>new WeekDayNotice(d,holidays.GetValueOrDefault(d,""),string.Join("\n",events.GetValueOrDefault(d,[])))).ToArray();
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
