using System.Globalization;
using System.Text.Json;
using Google.Apis.Sheets.v4.Data;
namespace wpf;

public sealed class StoredMeal
{
    public int RowIndex { get; init; }
    public List<object> Raw { get; init; } = [];
    public string Value(int i) => i < Raw.Count ? Convert.ToString(Raw[i],CultureInfo.InvariantCulture)?.Trim() ?? "" : "";
    public DateTime Date { get; init; }
    public string Id => Value(0);
    public string DateText => Date.ToString("yyyy-MM-dd");
    public string Day => "일월화수목금토"[(int)Date.DayOfWeek].ToString();
    public string Meal => Value(2);
    public string AgeGroup => Value(10);
    public string Track => Value(11);
    public string Rice => Value(3); public string Soup => Value(4); public string Main => Value(5);
    public string Side => Value(6); public string Kimchi => Value(7); public string Dessert => Value(8);
    public string[] Menus => Enumerable.Range(3,6).Select(Value).ToArray();
    public double? Carb => WeeklyMealPlanner.Number(Value(12)); public double? Protein => WeeklyMealPlanner.Number(Value(13));
    public double? Fat => WeeklyMealPlanner.Number(Value(14)); public double? Calories => WeeklyMealPlanner.Number(Value(15));
    public double? TargetCalories => WeeklyMealPlanner.Number(Value(27)); public double? CalorieDifference => WeeklyMealPlanner.Number(Value(28));
    public string CarbDifference => Value(19); public string ProteinDifference => Value(20); public string FatDifference => Value(21);
    public string Status => Value(22);
    public string Identity => $"{DateText}|{Meal}|{AgeGroup}";
    public string Snapshot => JsonSerializer.Serialize(Raw.Select(x=>Convert.ToString(x,CultureInfo.InvariantCulture)));
    public override string ToString() => $"{DateText} ({Day}) · {Meal}" + (AgeGroup.Length>0?$" · {AgeGroup}세":"");
}
public partial class GoogleSheetsService
{
    public static List<StoredMeal> ParseStoredMeals(IList<IList<object>> rows)
    {
        if(rows.Count==0)return [];
        if(Cell(rows[0],1)!="날짜" || Cell(rows[0],2) is not ("끼니" or "구분")) throw new InvalidOperationException("메뉴 시트의 날짜·끼니 헤더를 확인해 주세요.");
        string[] legacy=["식단 ID","날짜","구분","밥","국","메인","사이드1","사이드2","후식"];
        for(int c=0;c<Math.Min(rows[0].Count,WeeklyHeaders.Length);c++)
            if(Cell(rows[0],c).Length>0 && Cell(rows[0],c)!=WeeklyHeaders[c] && !(c<legacy.Length&&Cell(rows[0],c)==legacy[c]))throw new InvalidOperationException($"메뉴 시트 {c+1}열의 구조가 다릅니다. 헤더를 확인해 주세요.");
        var result=new List<StoredMeal>();
        for(int i=1;i<rows.Count;i++) {
            var raw=rows[i];
            if(!DateTime.TryParse(Cell(raw,1),CultureInfo.InvariantCulture,DateTimeStyles.None,out var date) && !DateTime.TryParse(Cell(raw,1),out date))continue;
            if(!Enumerable.Range(3,6).Any(c=>Cell(raw,c).Length>0))continue;
            result.Add(new StoredMeal{RowIndex=i,Raw=raw.ToList(),Date=date.Date});
        }
        return result;
    }
    public async Task<List<StoredMeal>> GetStoredMealsAsync(DateTime? week=null,string? age=null)
    {
        var titles=await GetSheetTitlesAsync().ConfigureAwait(false);
        if(!titles.Contains(AppConfig.WeeklyMenuSheetName))return [];
        var rows=await GetValuesAsync(QuoteTitle(AppConfig.WeeklyMenuSheetName)).ConfigureAwait(false);
        var start=WeeklyMealPlanner.Monday(week??DateTime.Today);
        return ParseStoredMeals(rows).Where(m=>(!week.HasValue || (m.Date>=start && m.Date<start.AddDays(7))) &&
            (age==null || m.AgeGroup==age || m.AgeGroup.Length==0 || (age=="6-18" && m.AgeGroup is "6-11" or "12-18")))
            .GroupBy(m=>m.Identity).Select(g=>g.Last()).OrderBy(m=>m.Date).ThenBy(m=>m.Meal=="조식"?0:m.Meal=="중식"?1:2).ToList();
    }
    public static IList<object> EditedMealValues(StoredMeal meal,IReadOnlyList<TrackMenu> items)
    {
        if(items.Count!=6 || items.Where((m,i)=>m.Category!=WeeklyMealPlanner.Categories[i] || string.IsNullOrWhiteSpace(m.Name)).Any())throw new InvalidOperationException("여섯 분류의 메뉴를 선택해 주세요.");
        var row=meal.Raw.Take(WeeklyHeaders.Length).ToList();while(row.Count<WeeklyHeaders.Length)row.Add("");
        row[0]=meal.Date.ToString("yyMMdd")+(meal.Meal switch{"조식"=>"M","중식"=>"L","석식"=>"N",_=>throw new InvalidOperationException("끼니를 확인해 주세요.")});
        for(int i=0;i<6;i++)row[i+3]=items[i].Name;
        for(int i=12;i<16;i++)row[i]="";
        for(int i=19;i<22;i++)row[i]="";
        row[22]="영양정보 미완성";
        if(items.All(m=>m.HasNutrition)) {
            double[] totals=[items.Sum(m=>m.Carb!.Value),items.Sum(m=>m.Protein!.Value),items.Sum(m=>m.Fat!.Value),items.Sum(m=>m.Energy)];
            for(int i=0;i<4;i++)row[12+i]=Math.Round(totals[i],i==3?1:2);
            var targets=Enumerable.Range(16,3).Select(i=>WeeklyMealPlanner.Number(meal.Value(i))).ToArray();
            if(targets.All(t=>t is >0) && WeeklyMealPlanner.Number(meal.Value(26)) is double tolerance){
                for(int i=0;i<3;i++)row[19+i]=Math.Round(totals[i]-targets[i]!.Value,2);
                row[22]=Enumerable.Range(0,3).All(i=>Math.Abs(totals[i]-targets[i]!.Value)<=targets[i]!.Value*tolerance/100+1e-8)?"허용범위 내":"목표 편차 확인";
            }else row[22]="목표 미설정";
        }
        if(items.All(m=>m.HasNutrition) && WeeklyMealPlanner.Number(meal.Value(27)) is double calorieTarget)
            row[28]=Math.Round(items.Sum(m=>m.Energy)-calorieTarget,1);
        row[24]=DateTimeOffset.Now.ToString("o");row[25]=string.Join(" / ",items.Select(m=>m.Key));
        return row;
    }
    public async Task ChangeStoredMealAsync(StoredMeal selected,IReadOnlyList<TrackMenu>? replacements)
    {
        RequirePersonalWrite();
        await _mealSaveGate.WaitAsync().ConfigureAwait(false);
        try {
            var sheet=AppConfig.WeeklyMenuSheetName;
            var raw=await GetValuesAsync(QuoteTitle(sheet)).ConfigureAwait(false);
            var matches=ParseStoredMeals(raw).Where(m=>m.Identity==selected.Identity).ToList();
            if(matches.Count==0 || matches[^1].Snapshot!=selected.Snapshot)throw new InvalidOperationException("식단이 다른 곳에서 변경되었습니다. 새로고침한 뒤 다시 시도해 주세요.");
            int id=await GetSheetIdByTitleAsync(sheet).ConfigureAwait(false)??throw new InvalidOperationException("메뉴 탭이 없습니다.");
            var requests=new List<Request>();
            if(replacements==null) {
                foreach(var row in matches.OrderByDescending(m=>m.RowIndex))requests.Add(new Request{DeleteDimension=new DeleteDimensionRequest{Range=new DimensionRange{SheetId=id,Dimension="ROWS",StartIndex=row.RowIndex,EndIndex=row.RowIndex+1}}});
            } else {
                var metadata=_sheetsService.Spreadsheets.Get(SpreadsheetId);metadata.Fields="sheets(properties)";
                var book=await metadata.ExecuteAsync().ConfigureAwait(false);int columns=book.Sheets.First(s=>s.Properties.SheetId==id).Properties.GridProperties.ColumnCount??0;
                if(columns<WeeklyHeaders.Length)requests.Add(new Request{AppendDimension=new AppendDimensionRequest{SheetId=id,Dimension="COLUMNS",Length=WeeklyHeaders.Length-columns}});
                requests.Add(new Request{UpdateCells=new UpdateCellsRequest{Start=new GridCoordinate{SheetId=id,RowIndex=0,ColumnIndex=0},Rows=[new RowData{Values=WeeklyHeaders.Select(TextCell).ToList()}],Fields="userEnteredValue"}});
                requests.Add(new Request{UpdateCells=new UpdateCellsRequest{Start=new GridCoordinate{SheetId=id,RowIndex=matches[^1].RowIndex,ColumnIndex=0},Rows=[new RowData{Values=EditedMealValues(selected,replacements).Select(DataCell).ToList()}],Fields="userEnteredValue"}});
            }
            await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest{Requests=requests},SpreadsheetId).ExecuteAsync().ConfigureAwait(false);
        }finally{_mealSaveGate.Release();}
    }

    /// <summary>현재 화면에 표시한 주·연령대의 저장 식단을 모두 삭제합니다.</summary>
    public async Task<int> DeleteStoredMealsForWeekAsync(DateTime week, string age)
    {
        RequirePersonalWrite();
        await _mealSaveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var sheet = AppConfig.WeeklyMenuSheetName;
            var raw = await GetValuesAsync(QuoteTitle(sheet)).ConfigureAwait(false);
            var start = WeeklyMealPlanner.Monday(week);
            var targets = ParseStoredMeals(raw).Where(m => m.Date >= start && m.Date < start.AddDays(7) &&
                (m.AgeGroup == age || m.AgeGroup.Length == 0 || (age == "6-18" && m.AgeGroup is "6-11" or "12-18"))).ToList();
            if (targets.Count == 0) return 0;
            int id = await GetSheetIdByTitleAsync(sheet).ConfigureAwait(false) ?? throw new InvalidOperationException("메뉴 탭이 없습니다.");
            var requests = targets.OrderByDescending(m => m.RowIndex).Select(row => new Request
            {
                DeleteDimension = new DeleteDimensionRequest { Range = new DimensionRange { SheetId = id, Dimension = "ROWS", StartIndex = row.RowIndex, EndIndex = row.RowIndex + 1 } }
            }).ToList();
            await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = requests }, SpreadsheetId).ExecuteAsync().ConfigureAwait(false);
            return targets.Count;
        }
        finally { _mealSaveGate.Release(); }
    }
}
