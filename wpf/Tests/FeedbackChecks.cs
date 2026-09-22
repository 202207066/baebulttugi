using wpf;
using System.Windows;
using System.Windows.Controls;

internal static class FeedbackChecks
{
    public static void Run(Action<bool,string> check, Action<Action,string> reject)
    {
        var kg = new CostRow("고기", "육류",120,"g",28000,1,"kg");
        check(CostCalculator.CostPerPerson(kg)==3360, "120g at 28000 KRW/kg costs 3360 KRW");
        check(new IngredientItem(kg,300).TotalCost==1008000, "Total cost uses full precision for all diners");
        check(CostCalculator.CostPerPerson(new("물","",125,"ml",1000,1,"L"))==125, "ml and L conversion");
        check(CostCalculator.CostPerPerson(new("쌀","",0.5m,"kg",350,100,"g"))==1750, "Price per 100g and reverse conversion");
        var decimalRow = new IngredientItem(new("두부","",0.15m,"모",1.23m),3);
        check(decimalRow.TotalQuantity==0.45m && decimalRow.TotalCost==0.5535m, "Fractional quantity and unrounded per-person cost preserved");
        reject(()=>CostCalculator.CostPerPerson(kg with { PriceUnit="L" }), "Incompatible units rejected");
        reject(()=>CostCalculator.Number("-2", "수량"), "Negative input not changed to positive");
        reject(()=>CostCalculator.Number("", "단가"), "Missing price is not zero");
        reject(()=>CostCalculator.Number("NaN", "단가"), "Nonfinite price rejected");
        reject(()=>CostCalculator.CostPerPerson(kg with { PriceQuantity=0 }), "Zero price basis rejected");

        IList<IList<object>> legacy = new List<IList<object>> {
            new List<object> { "이름","연령","성별","특이사항","대체식품" },
            new List<object> { "테스트A",5,"여","우유","기존 대체식품" },
            new List<object> { "테스트B",8,"남","대두","=1+1" }
        };
        var people = PatientSheetCodec.Parse(legacy);
        check(people[0].Name=="테스트A" && people[0].Allergies=="우유", "Legacy patient headers read correctly");
        people[0].Note="새 메모";
        var merged=PatientSheetCodec.Merge(legacy,people);
        check(merged[1][1].ToString()=="5" && merged[1][2].ToString()=="여" && merged[1][4].ToString()=="기존 대체식품", "Age, gender and existing alternative preserved");
        check(merged[2][4].ToString()=="=1+1" && PatientSheetCodec.Parse(merged)[0].Note=="새 메모", "Unknown formula and new memo preserved");
        var deleted=PatientSheetCodec.Merge(merged,[people[1]]);
        check(deleted.Count==2 && deleted[1][0].ToString()=="테스트B" && deleted[1][1].ToString()=="8", "Deleting a patient keeps remaining row aligned");
        reject(()=>PatientSheetCodec.Parse(new List<IList<object>>{new List<object>{"무관한 표"}}), "Unknown patient schema rejected");
        reject(()=>PatientSheetCodec.Merge(merged,[people[0],people[0]]), "Duplicate patient IDs rejected");
        check(PatientSheetCodec.Parse(new List<IList<object>>()).Count==0, "Empty patient data accepted");

        IList<IList<object>> meals = new List<IList<object>> {
            new List<object>{"식단 ID","날짜","구분","밥","국","메인","사이드1","사이드2","후식"},
            new List<object>{"sample","Day1","중식","예시"},
            new List<object>{"old","2026-09-21","중식","밥"},
            new List<object>{"new","2026-09-21","중식","잡곡밥","국","주찬","부찬","김치","과일"},
            new List<object>{"dinner","2026-09-21","석식","저녁밥"}
        };
        var parsed=GoogleSheetsService.ParseSavedMeals(meals);
        check(parsed.Count==2 && parsed[0].Id=="new" && parsed[0].Menus.Length==6, "Calendar reads latest saved meal, six categories and ignores undated samples");
        check(GoogleSheetsService.ParseSavedMeals(new List<IList<object>>()).Count==0, "Empty menu is unwritten, not an exception");
        using(var outer=AppActivity.Begin("outer"))
        {
            using(var inner=AppActivity.Begin("inner")) check(AppActivity.Message=="inner", "Nested loading displays current operation");
            check(AppActivity.Message=="outer", "Nested loading restores pending operation");
        }
        check(AppActivity.Message==null, "Loading clears on completion");
        check(AppConfig.HasCredentials && AppConfig.HasSpreadsheetId, "Embedded deployment config loads without external JSON");
        IList<IList<object>> source = new List<IList<object>> {
            new List<object>{"식단 ID","날짜","끼니","밥류","국류","주찬","부찬","김치류","후식류"},
            new List<object>{"K001","Day 1","중식","밥","국","주찬","부찬","김치","과일"}
        };
        var personal = source.Select(r => (IList<object>)r.ToList()).ToList();
        personal.Add(new List<object>{"mine","2026-09-21","중식","내 식단"});
        personal.Add(new List<object>{"K001","Day 1","중식","수정한 밥","국","주찬","부찬","김치","과일"});
        personal.Add(new List<object>{"K001","Day 1","중식","밥","국","주찬","부찬","김치","과일","내 메모"});
        check(GoogleSheetsService.FindCopiedExampleRows(personal,source).SequenceEqual(new[]{2}), "Only exact copied examples removed; real dates, edits and metadata retained");
        check(GoogleSheetsService.FindCopiedExampleRows(new List<IList<object>>(),source).Count==0, "Empty personal sheet requires no sample cleanup");
        check(GoogleSheetsService.FindCopiedExampleRows(personal,new List<IList<object>>{new List<object>{"다른 표"}}).Count==0,"Unknown source schema is never used to remove rows");
    }

    public static void Render(FrameworkElement element, string file, int width, int height)
    {
        element.Measure(new Size(width,height)); element.Arrange(new Rect(0,0,width,height)); element.UpdateLayout();
        var bmp=new System.Windows.Media.Imaging.RenderTargetBitmap(width,height,96,96,System.Windows.Media.PixelFormats.Pbgra32); bmp.Render(element);
        var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bmp));
        using var stream=System.IO.File.Create(file); encoder.Save(stream);
    }
}
