using System.Globalization;
namespace wpf;
public record CalendarDateMark(bool IsHoliday,bool HasSpecial,bool HasEvent,string Label) { public bool HasMeal { get; init; } public bool ConnectPrevious { get; init; } public bool ConnectNext { get; init; } public System.Windows.CornerRadius MealCorners => new(ConnectPrevious ? 0 : 14, ConnectNext ? 0 : 14, ConnectNext ? 0 : 14, ConnectPrevious ? 0 : 14); public string EventIcon { get; init; } = ""; }
public static class CalendarDates
{
    private static readonly Dictionary<int,Dictionary<DateTime,string>> cache=new();
    public static IReadOnlyDictionary<DateTime,string> Holidays(int year)
    {
        if(cache.TryGetValue(year,out var found))return found;
        var result=new Dictionary<DateTime,string>();
        void Add(int month,int day,string label)=>result[new DateTime(year,month,day)]=label;
        Add(1,1,"새해 첫날");Add(3,1,"삼일절");Add(5,5,"어린이날");Add(6,6,"현충일");Add(8,15,"광복절");Add(10,3,"개천절");Add(10,9,"한글날");Add(12,25,"성탄절");
        if(year>=2026){Add(5,1,"노동절");Add(7,17,"제헌절");}
        var lunar=new KoreanLunisolarCalendar();
        if(year>=lunar.MinSupportedDateTime.Year && year<lunar.MaxSupportedDateTime.Year){
            DateTime Date(int month,int day){int leap=lunar.GetLeapMonth(year);return lunar.ToDateTime(year,leap>0&&month>=leap?month+1:month,day,0,0,0,0);}
            foreach(var (date,label) in new[]{(Date(1,1),"설날"),(Date(8,15),"추석")}) for(int i=-1;i<=1;i++)result[date.AddDays(i)]=i==0?label:label+" 연휴";
            result[Date(4,8)]="부처님오신날";
        }
        // Verified 2026 dates: KASI calendar data and MPM's 2026-04-29 notice.
        // Future temporary/substitute holidays are supplemented by the official API.
        if(year==2026){Add(3,2,"대체공휴일 · 삼일절");Add(5,25,"대체공휴일 · 부처님오신날");Add(6,3,"전국동시지방선거");Add(8,17,"대체공휴일 · 광복절");Add(10,5,"대체공휴일 · 개천절");}
        cache[year]=result;return result;
    }
    public static string Special(DateTime date) => (date.Year,date.Month,date.Day) switch {
        (2026,7,15)=>"초복 · 닭 요리 등 특식 준비",
        (2026,7,25)=>"중복 · 닭 요리 등 특식 준비",
        (2026,8,14)=>"말복 · 닭 요리 등 특식 준비",
        (2026,3,3)=>"정월대보름 · 오곡밥 등 특식 준비",
        (2026,12,22)=>"동지 · 팥죽 등 특식 준비",
        _=>""
    };
    public static CalendarDateMark Mark(DateTime date,string? holiday,bool hasEvent)
    {
        string special=Special(date);
        return new(!string.IsNullOrEmpty(holiday),special.Length>0,hasEvent,
            string.Join(" · ",new[]{date.ToString("yyyy-MM-dd"),holiday??"",special,hasEvent?"등록한 일정 있음":""}.Where(x=>x.Length>0)));
    }
}
