using System.Globalization;
using System.Text.Json;
using Google.Apis.Sheets.v4.Data;
namespace wpf;

public sealed class RecipePortion
{
    public string Track {get;set;}=""; public string Key {get;set;}=""; public string Name {get;set;}="";
    public string Category {get;set;}=""; public string Source {get;set;}="";
    public List<PortionIngredient> Ingredients {get;set;}=[];
}
public sealed class PortionIngredient { public string Name {get;set;}="";public decimal Amount {get;set;} public string Unit {get;set;}="g"; }
public sealed class CostDraft
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string? MenuLabel { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayMenu => MenuLabel ?? MealPresentation.Label(Menu, null);
    public string Menu {get;set;}="";public string Name {get;set;}="";public string Category {get;set;}="";
    public string Amount {get;set;}="";public string Unit {get;set;}="g";public string Price {get;set;}="";
    public string PriceQuantity {get;set;}="1";public string PriceUnit {get;set;}="kg";public string Source {get;set;}="직접 입력";
    public CostRow ToCostRow()=>new(Name,Category,CostCalculator.Number(Amount,Menu+" / "+Name+" 소요량"),Unit,CostCalculator.Number(Price,Name+" 구매 단가"),CostCalculator.Number(PriceQuantity,Name+" 단가 기준량",true),PriceUnit);
}
public record SavedCost(string Age,string Key,CostDraft Draft);
public static class MealCostData
{
    private static readonly Lazy<List<RecipePortion>> recipes=new(()=> {
        using var stream=typeof(MealCostData).Assembly.GetManifestResourceStream("wpf.RecipePortions")??throw new InvalidOperationException("레시피 재료 자료가 없습니다.");
        return JsonSerializer.Deserialize<List<RecipePortion>>(stream)??[];
    });
    public static List<CostDraft> Build(StoredMeal meal,IReadOnlyList<SavedCost> saved,IReadOnlySet<string>? personalKeys=null)
    {
        string track=meal.AgeGroup.Length>0?WeeklyMealPlanner.TrackForAge(meal.AgeGroup):meal.Track.Contains("트랙B")?"B":"A";
        var result=new List<CostDraft>();var keys=meal.Value(25).Split(" / ");
        for(int i=0;i<6;i++) {
            string name=meal.Menus[i],key=i<keys.Length?keys[i]:"",category=WeeklyMealPlanner.Categories[i];if(name.Length==0)continue;
            var owned=saved.Where(s=>s.Draft.Menu==name && s.Age==meal.AgeGroup && (s.Key==key||s.Key.Length==0)).ToList();
            if(owned.Count==0)owned=saved.Where(s=>s.Draft.Menu==name&&s.Age.Length==0&&s.Key.Length==0).ToList();
            if(owned.Count>0) {result.AddRange(owned.Select(s=>Clone(s.Draft)));continue;}
            RecipePortion? recipe=null;
            if(personalKeys?.Contains(key)!=true){
                recipe=recipes.Value.FirstOrDefault(r=>r.Track==track&&r.Key==key);
                if(recipe==null&&key.Length==0){var candidates=recipes.Value.Where(r=>r.Track==track&&r.Name==name&&r.Category==category).ToList();if(candidates.Count==1)recipe=candidates[0];}
            }
            if(recipe==null){result.Add(new CostDraft{Menu=name,Category=category,Source="재료·1인분 분량을 직접 입력해 주세요"});continue;}
            foreach(var ingredient in recipe.Ingredients) {
                var draft=new CostDraft{Menu=name,Name=ingredient.Name,Category=category,Amount=ingredient.Amount.ToString(CultureInfo.InvariantCulture),Unit=ingredient.Unit,Source=recipe.Source};
                var prices=saved.Where(s=>s.Draft.Name==ingredient.Name && s.Draft.Price.Length>0).Select(s=>(s.Draft.Price,s.Draft.PriceQuantity,s.Draft.PriceUnit)).Distinct().ToList();
                if(prices.Count==1){draft.Price=prices[0].Price;draft.PriceQuantity=prices[0].PriceQuantity;draft.PriceUnit=prices[0].PriceUnit;}
                result.Add(draft);
            }
        }
        return result;
    }
    private static CostDraft Clone(CostDraft d)=>new(){Menu=d.Menu,Name=d.Name,Category=d.Category,Amount=d.Amount,Unit=d.Unit,Price=d.Price,PriceQuantity=d.PriceQuantity,PriceUnit=d.PriceUnit,Source="내 원가 시트"};
    /// <summary>확정 구매단가가 있는 경우에만 선택 메뉴의 1인분 원가를 반환합니다.</summary>
    public static decimal? EstimateMenuCost(TrackMenu menu, string age, IReadOnlyList<SavedCost> saved, IReadOnlySet<string>? personalKeys = null)
    {
        var own = saved.Where(s => s.Age == age && s.Draft.Menu == menu.Name && (s.Key == menu.Key || s.Key.Length == 0)).Select(s => s.Draft).ToList();
        if (own.Count > 0)
        {
            try { return own.Sum(d => CostCalculator.CostPerPerson(d.ToCostRow())); }
            catch (InvalidOperationException) { return null; }
        }
        if (personalKeys?.Contains(menu.Key) == true) return null;
        var recipe = recipes.Value.FirstOrDefault(r => r.Track == WeeklyMealPlanner.TrackForAge(age) && r.Key == menu.Key);
        if (recipe == null) return null;
        decimal total = 0;
        foreach (var ingredient in recipe.Ingredients)
        {
            var prices = saved.Where(s => s.Draft.Name == ingredient.Name && s.Draft.Price.Length > 0)
                .Select(s => (s.Draft.Price, s.Draft.PriceQuantity, s.Draft.PriceUnit)).Distinct().ToList();
            if (prices.Count != 1) return null;
            try { total += CostCalculator.CostPerPerson(new CostRow(ingredient.Name, menu.Category, ingredient.Amount, ingredient.Unit,
                CostCalculator.Number(prices[0].Price, ingredient.Name + " 구매 단가"), CostCalculator.Number(prices[0].PriceQuantity, ingredient.Name + " 단가 기준량", true), prices[0].PriceUnit)); }
            catch (InvalidOperationException) { return null; }
        }
        return total;
    }
    public static List<CostRow> Validate(StoredMeal meal,IEnumerable<CostDraft> drafts,bool requirePrices)
    {
        var rows=drafts.ToList();
        if(meal.Menus.Where(n=>n.Length>0).Any(n=>!rows.Any(r=>r.Menu==n)))throw new InvalidOperationException("식단의 모든 메뉴에 재료가 필요합니다. 빠진 메뉴의 재료를 추가해 주세요.");
        var result=new List<CostRow>();
        foreach(var d in rows){
            if(!meal.Menus.Contains(d.Menu) || string.IsNullOrWhiteSpace(d.Name))throw new InvalidOperationException("메뉴명은 선택한 식단의 메뉴와 같아야 하며, 식재료명은 필수입니다.");
            var row=new CostRow(d.Name,d.Category,CostCalculator.Number(d.Amount,d.Name+" 소요량"),d.Unit,
                !requirePrices&&d.Price.Trim().Length==0?0:CostCalculator.Number(d.Price,d.Name+" 구매 단가"),CostCalculator.Number(d.PriceQuantity,d.Name+" 단가 기준량",true),d.PriceUnit);
            CostCalculator.CostPerPerson(row);result.Add(row);
        }
        return result;
    }
}
public partial class GoogleSheetsService
{
    public static readonly string[] CostHeaders=["식단명","식재료명","분류","1인당 소요량","단위","단가(원)","단가 기준량","단가 단위","연령대","메뉴키"];
    public async Task<List<SavedCost>> GetMealCostsAsync()
    {
        if(!(await GetSheetTitlesAsync().ConfigureAwait(false)).Contains(AppConfig.CostSheetName))return [];
        var rows=await GetValuesAsync(QuoteTitle(AppConfig.CostSheetName)+"!A:J").ConfigureAwait(false);
        return rows.Skip(1).Where(r=>Cell(r,0).Length>0&&Cell(r,1).Length>0).Select(r=>new SavedCost(Cell(r,8),Cell(r,9),new CostDraft{Menu=Cell(r,0),Name=Cell(r,1),Category=Cell(r,2),Amount=Cell(r,3),Unit=Cell(r,4),Price=Cell(r,5),PriceQuantity=Cell(r,6).Length==0?"1":Cell(r,6),PriceUnit=Cell(r,7).Length==0?Cell(r,4):Cell(r,7)})).ToList();
    }
    public async Task SaveMealCostsAsync(StoredMeal meal,IReadOnlyList<CostDraft> drafts)
    {
        RequirePersonalWrite();var costs=MealCostData.Validate(meal,drafts,false);await _entryGate.WaitAsync().ConfigureAwait(false);
        try {
            var sheet=await EnsureSheetAsync(AppConfig.CostSheetName,CostHeaders).ConfigureAwait(false);
            var rows=await GetValuesAsync(QuoteTitle(sheet)+"!A:J").ConfigureAwait(false);
            if(rows.Count>0 && (Cell(rows[0],0)!="식단명"||Cell(rows[0],1)!="식재료명"))throw new InvalidOperationException("원가 시트의 식단명·식재료명 헤더를 확인해 주세요.");
            if(rows.Count>0 && ((Cell(rows[0],8).Length>0&&Cell(rows[0],8)!="연령대")||(Cell(rows[0],9).Length>0&&Cell(rows[0],9)!="메뉴키")))throw new InvalidOperationException("원가 시트 I~J열에 다른 자료가 있어 저장을 중단했습니다.");
            int id=await GetSheetIdByTitleAsync(sheet).ConfigureAwait(false)??throw new InvalidOperationException("원가 탭이 없습니다.");
            var keys=meal.Value(25).Split(" / ");string Key(string name){int i=Array.IndexOf(meal.Menus,name);return i>=0&&i<keys.Length?keys[i]:"";}
            var requests=new List<Request>();
            var metadata=_sheetsService.Spreadsheets.Get(SpreadsheetId);metadata.Fields="sheets(properties)";
            var book=await metadata.ExecuteAsync().ConfigureAwait(false);int columns=book.Sheets.First(s=>s.Properties.SheetId==id).Properties.GridProperties.ColumnCount??0;
            if(columns<10)requests.Add(new Request{AppendDimension=new AppendDimensionRequest{SheetId=id,Dimension="COLUMNS",Length=10-columns}});
            for(int i=rows.Count-1;i>=1;i--)if(meal.Menus.Contains(Cell(rows[i],0))&&Cell(rows[i],8)==meal.AgeGroup&&Cell(rows[i],9)==Key(Cell(rows[i],0)))requests.Add(new Request{DeleteDimension=new DeleteDimensionRequest{Range=new DimensionRange{SheetId=id,Dimension="ROWS",StartIndex=i,EndIndex=i+1}}});
            requests.Add(new Request{UpdateCells=new UpdateCellsRequest{Start=new GridCoordinate{SheetId=id,RowIndex=0,ColumnIndex=0},Rows=[new RowData{Values=CostHeaders.Select(TextCell).ToList()}],Fields="userEnteredValue"}});
            requests.Add(new Request{AppendCells=new AppendCellsRequest{SheetId=id,Rows=drafts.Select((d,i)=>new RowData{Values=new object[]{d.Menu,d.Name,d.Category,costs[i].UnitSize,d.Unit,d.Price.Trim().Length==0?"":costs[i].UnitPrice,costs[i].PriceQuantity,d.PriceUnit,meal.AgeGroup,Key(d.Menu)}.Select(DataCell).ToList()}).ToList(),Fields="userEnteredValue"}});
            await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest{Requests=requests},SpreadsheetId).ExecuteAsync().ConfigureAwait(false);
        }finally{_entryGate.Release();}
    }
}
