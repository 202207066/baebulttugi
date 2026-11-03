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
    public string? SharedBasisSignature { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string DisplayMenu => MenuLabel ?? MealPresentation.Label(Menu, null);
    public string Menu {get;set;}="";public string Name {get;set;}="";public string Category {get;set;}="";
    public string Amount {get;set;}="";public string Unit {get;set;}="g";public string Price {get;set;}="";
    public string PriceQuantity {get;set;}="1";public string PriceUnit {get;set;}="kg";public string Source {get;set;}="직접 입력";
    public string WastePercent {get;set;}="";public string PackageWeightGrams {get;set;}="";
    public decimal WasteValue=>string.IsNullOrWhiteSpace(WastePercent)?0:CostCalculator.Number(WastePercent,Name+" 폐기율");
    public decimal? PackageValue=>string.IsNullOrWhiteSpace(PackageWeightGrams)?null:CostCalculator.Number(PackageWeightGrams,Name+" 개당 중량",true);
    public CostRow ToCostRow()=>new(Name,Category,CostCalculator.Number(Amount,Menu+" / "+Name+" 소요량"),Unit,CostCalculator.Number(Price,Name+" 구매 단가"),CostCalculator.Number(PriceQuantity,Name+" 단가 기준량",true),PriceUnit,WasteValue,PackageValue);
}
public record SavedCost(string Age,string Key,CostDraft Draft);
public static class MealCostData
{
    private static List<RecipePortion> Recipes => OfficialCatalog.Current.Recipes;
    public static StoredMeal CostMeal(TrackMenu menu,string age)
    {
        var row=Enumerable.Repeat<object>("",30).ToList();int slot=Array.IndexOf(WeeklyMealPlanner.Categories,menu.Category);if(slot<0)throw new InvalidOperationException("메뉴 분류를 선택해 주세요.");row[3+slot]=menu.Name;row[10]=age;row[11]=WeeklyMealPlanner.TrackForAge(age);var keys=new string[6];keys[slot]=menu.Key;row[25]=string.Join(" / ",keys);return new StoredMeal{Raw=row,Date=DateTime.Today};
    }
    public static List<CostDraft> Build(StoredMeal meal,IReadOnlyList<SavedCost> saved,IReadOnlySet<string>? personalKeys=null)
    {
        string track=meal.AgeGroup.Length>0?WeeklyMealPlanner.TrackForAge(meal.AgeGroup):meal.Track.Contains("트랙B")?"B":"A";
        var result=new List<CostDraft>();var keys=meal.Value(25).Split(" / ");
        for(int i=0;i<meal.Menus.Length;i++) {
            string name=meal.Menus[i],key=i<keys.Length?keys[i]:"",category=MealSnapshot.CategoryAt(i);if(name.Length==0)continue;
            var owned=saved.Where(s=>s.Draft.Menu==name && s.Age==meal.AgeGroup && (s.Key==key||s.Key.Length==0)).ToList();
            if(owned.Count==0)owned=saved.Where(s=>s.Draft.Menu==name&&s.Age.Length==0&&s.Key.Length==0).ToList();
            if(owned.Count>0) {result.AddRange(owned.Select(s=>Clone(s.Draft)));continue;}
            RecipePortion? recipe=null;
            if(personalKeys?.Contains(key)!=true){
                recipe=Recipes.FirstOrDefault(r=>r.Track==track&&r.Key==key);
                if(recipe==null&&key.Length==0){var candidates=Recipes.Where(r=>r.Track==track&&r.Name==name&&r.Category==category).ToList();if(candidates.Count==1)recipe=candidates[0];}
            }
            if(recipe==null){result.Add(new CostDraft{Menu=name,Category=category,Source="재료·1인분 분량을 직접 입력해 주세요"});continue;}
            foreach(var ingredient in recipe.Ingredients) {
                var draft=new CostDraft{Menu=name,Name=ingredient.Name,Category=category,Amount=ingredient.Amount.ToString(CultureInfo.InvariantCulture),Unit=ingredient.Unit,Source=recipe.Source};
                var prices=saved.Where(s=>s.Draft.Name==ingredient.Name && s.Draft.Price.Length>0).Select(s=>(s.Draft.Price,s.Draft.PriceQuantity,s.Draft.PriceUnit,s.Draft.WastePercent,s.Draft.PackageWeightGrams)).Distinct().ToList();
                if(prices.Count==1){draft.Price=prices[0].Price;draft.PriceQuantity=prices[0].PriceQuantity;draft.PriceUnit=prices[0].PriceUnit;draft.WastePercent=prices[0].WastePercent;draft.PackageWeightGrams=prices[0].PackageWeightGrams;}
                result.Add(draft);
            }
        }
        foreach(var draft in result) IngredientStandards.Apply(draft,saved);
        return result;
    }
    internal static CostDraft Clone(CostDraft d)=>new(){Menu=d.Menu,Name=d.Name,Category=d.Category,Amount=d.Amount,Unit=d.Unit,Price=d.Price,PriceQuantity=d.PriceQuantity,PriceUnit=d.PriceUnit,WastePercent=d.WastePercent,PackageWeightGrams=d.PackageWeightGrams,SharedBasisSignature=d.SharedBasisSignature,Source="내 원가 시트"};
    /// <summary>확정 구매단가가 있는 경우에만 선택 메뉴의 1인분 원가를 반환합니다.</summary>
    public static decimal? EstimateMenuCost(TrackMenu menu, string age, IReadOnlyList<SavedCost> saved, IReadOnlySet<string>? personalKeys = null)
    {
        try {
            var rows=Build(CostMeal(menu,age),saved,personalKeys);
            if(rows.Count==0 || rows.Any(d=>d.Price.Length==0 || d.Name.Length==0))return null;
            return rows.Sum(d=>CostCalculator.CostPerPerson(d.ToCostRow()));
        } catch(InvalidOperationException) {return null;}
    }

    public static List<CostRow> Validate(StoredMeal meal,IEnumerable<CostDraft> drafts,bool requirePrices)
    {
        var rows=drafts.ToList();
        if(meal.Menus.Where(n=>n.Length>0).Any(n=>!rows.Any(r=>r.Menu==n)))throw new InvalidOperationException("식단의 모든 메뉴에 재료가 필요합니다. 빠진 메뉴의 재료를 추가해 주세요.");
        var result=new List<CostRow>();
        foreach(var d in rows){
            if(!meal.Menus.Contains(d.Menu) || string.IsNullOrWhiteSpace(d.Name))throw new InvalidOperationException("메뉴명은 선택한 식단의 메뉴와 같아야 하며, 식재료명은 필수입니다.");
            var row=new CostRow(d.Name,d.Category,CostCalculator.Number(d.Amount,d.Name+" 소요량"),d.Unit,
                !requirePrices&&d.Price.Trim().Length==0?0:CostCalculator.Number(d.Price,d.Name+" 구매 단가"),CostCalculator.Number(d.PriceQuantity,d.Name+" 단가 기준량",true),d.PriceUnit,d.WasteValue,d.PackageValue);
            CostCalculator.GrossSize(row);if(requirePrices||d.Price.Trim().Length>0)CostCalculator.CostPerPerson(row);result.Add(row);
        }
        return result;
    }
}
public partial class GoogleSheetsService
{
    public static readonly string[] CostHeaders=["식단명","식재료명","분류","1인당 소요량","단위","단가(원)","단가 기준량","단가 단위","연령대","메뉴키","폐기율(%)","개당중량(g)"];
    private (string Book,DateTime ReadAt,List<SavedCost> Rows)? _mealCostCache;
    public async Task<List<SavedCost>> GetMealCostsAsync(bool forceRefresh=false)
    {
        await _entryGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if(!forceRefresh && _mealCostCache is {} cache && cache.Book==SpreadsheetId && DateTime.UtcNow-cache.ReadAt<TimeSpan.FromMinutes(5))
                return cache.Rows.Select(x=>x with{Draft=MealCostData.Clone(x.Draft)}).ToList();
            var result=await ReadMealCostsCoreAsync().ConfigureAwait(false);
            _mealCostCache=(SpreadsheetId,DateTime.UtcNow,result);
            return result.Select(x=>x with{Draft=MealCostData.Clone(x.Draft)}).ToList();
        }finally{_entryGate.Release();}
    }
    private async Task<List<SavedCost>> ReadMealCostsCoreAsync()
    {
        if(!(await GetSheetTitlesAsync().ConfigureAwait(false)).Contains(AppConfig.CostSheetName))return [];
        var rows=await GetValuesAsync(QuoteTitle(AppConfig.CostSheetName)+"!A:L").ConfigureAwait(false);
        return rows.Skip(1).Where(r=>Cell(r,0).Length>0&&Cell(r,1).Length>0).Select(r=>new SavedCost(Cell(r,8),Cell(r,9),new CostDraft{Menu=Cell(r,0),Name=Cell(r,1),Category=Cell(r,2),Amount=Cell(r,3),Unit=Cell(r,4),Price=Cell(r,5),PriceQuantity=Cell(r,6).Length==0?"1":Cell(r,6),PriceUnit=Cell(r,7).Length==0?Cell(r,4):Cell(r,7),WastePercent=Cell(r,10),PackageWeightGrams=Cell(r,11)})).ToList();
    }
    public async Task SaveMealCostsAsync(StoredMeal meal,IReadOnlyList<CostDraft> drafts,bool allowEmpty=false)
    {
        RequirePersonalWrite();var costs=allowEmpty&&drafts.Count==0?new List<CostRow>():MealCostData.Validate(meal,drafts,false);await _entryGate.WaitAsync().ConfigureAwait(false);
        try {
            var sheet=await EnsureSheetAsync(AppConfig.CostSheetName,CostHeaders).ConfigureAwait(false);
            var rows=await GetValuesAsync(QuoteTitle(sheet)+"!A:L").ConfigureAwait(false);
            if(rows.Count>0 && (Cell(rows[0],0)!="식단명"||Cell(rows[0],1)!="식재료명"))throw new InvalidOperationException("원가 시트의 식단명·식재료명 헤더를 확인해 주세요.");
            if(rows.Count>0 && ((Cell(rows[0],8).Length>0&&Cell(rows[0],8)!="연령대")||(Cell(rows[0],9).Length>0&&Cell(rows[0],9)!="메뉴키")))throw new InvalidOperationException("원가 시트 I~J열에 다른 자료가 있어 저장을 중단했습니다.");
            for(int col=10;col<12;col++)if(rows.Count>0&&((Cell(rows[0],col).Length>0&&Cell(rows[0],col)!=CostHeaders[col])||(Cell(rows[0],col).Length==0&&rows.Skip(1).Any(r=>Cell(r,col).Length>0))))throw new InvalidOperationException("원가 시트 K~L열에 다른 자료가 있어 저장을 중단했습니다.");
            int id=await GetSheetIdByTitleAsync(sheet).ConfigureAwait(false)??throw new InvalidOperationException("원가 탭이 없습니다.");
            var keys=meal.Value(25).Split(" / ");string Key(string name){int i=Array.IndexOf(meal.Menus,name);return i>=0&&i<keys.Length?keys[i]:"";}
            ValidateCostHeaders(rows);
            var requests=StandardRequests(id,rows,drafts);
            var metadata=_sheetsService.Spreadsheets.Get(SpreadsheetId);metadata.Fields="sheets(properties)";
            var book=await metadata.ExecuteAsync().ConfigureAwait(false);int columns=book.Sheets.First(s=>s.Properties.SheetId==id).Properties.GridProperties.ColumnCount??0;
            if(columns<12)requests.Insert(0,new Request{AppendDimension=new AppendDimensionRequest{SheetId=id,Dimension="COLUMNS",Length=12-columns}});
            for(int i=rows.Count-1;i>=1;i--)if(meal.Menus.Contains(Cell(rows[i],0))&&Cell(rows[i],8)==meal.AgeGroup&&Cell(rows[i],9)==Key(Cell(rows[i],0)))requests.Add(new Request{DeleteDimension=new DeleteDimensionRequest{Range=new DimensionRange{SheetId=id,Dimension="ROWS",StartIndex=i,EndIndex=i+1}}});
            requests.Add(new Request{UpdateCells=new UpdateCellsRequest{Start=new GridCoordinate{SheetId=id,RowIndex=0,ColumnIndex=0},Rows=[new RowData{Values=CostHeaders.Select(TextCell).ToList()}],Fields="userEnteredValue"}});
            if(drafts.Count>0)requests.Add(new Request{AppendCells=new AppendCellsRequest{SheetId=id,Rows=drafts.Select((d,i)=>new RowData{Values=new object[]{d.Menu,d.Name,d.Category,costs[i].UnitSize,d.Unit,d.Price.Trim().Length==0?"":costs[i].UnitPrice,costs[i].PriceQuantity,d.PriceUnit,meal.AgeGroup,Key(d.Menu),d.WastePercent.Trim().Length==0?"":costs[i].WastePercent,d.PackageWeightGrams.Trim().Length==0?"":(object?)costs[i].PackageWeightGrams??""}.Select(DataCell).ToList()}).ToList(),Fields="userEnteredValue"}});
            await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest{Requests=requests},SpreadsheetId).ExecuteAsync().ConfigureAwait(false);
            _mealCostCache=null;
        }finally{_entryGate.Release();}
    }
}
