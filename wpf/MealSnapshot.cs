using System.Text.Json;
namespace wpf;
public static class MealSnapshot
{
    private static readonly JsonSerializerOptions options=new(){IgnoreReadOnlyProperties=true};
    public static string CategoryAt(int index)=>index<6?WeeklyMealPlanner.Categories[index]:"부찬";
    public static string LabelAt(int index)=>index<6?WeeklyMealPlanner.Categories[index]:"추가 부찬 "+(index-5);
    public static void Validate(IReadOnlyList<TrackMenu> items){if(items.Count is <6 or >8 || items.Where((m,i)=>m==null||m.Category!=CategoryAt(i)||string.IsNullOrWhiteSpace(m.Name)||m.Id=="PENDING").Any())throw new InvalidOperationException("기본 메뉴와 추가 반찬을 선택해 주세요. 반찬은 주찬·부찬·김치를 포함해 최대 5개입니다.");}
    public static string Write(IReadOnlyList<TrackMenu> items){Validate(items);return JsonSerializer.Serialize(items,options);}
    public static TrackMenu[]? Read(string value){if(string.IsNullOrWhiteSpace(value))return null;try{var items=JsonSerializer.Deserialize<TrackMenu[]>(value)??throw new JsonException();Validate(items);return items;}catch(Exception ex){throw new InvalidOperationException("저장된 식단의 메뉴 상세정보를 읽지 못했습니다. 원본 시트를 확인해 주세요. 자료는 변경하지 않았습니다.",ex);}}
}
