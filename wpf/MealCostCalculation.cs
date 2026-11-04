using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Google.Apis.Sheets.v4.Data;

namespace wpf;

public sealed record MealCostCalculation(string MealIdentity, int HeadCount, IReadOnlyList<IngredientItem> Items, string Fingerprint)
{
    public decimal PerPersonCost => Items.Sum(i => i.PerPersonCost ?? 0);
    public decimal TotalCost => PerPersonCost * HeadCount;
    public int MissingPrices => Items.Count(i => !i.HasPrice);
    public int MissingWaste => Items.Count(i => !i.WasteConfirmed);
    public bool Complete => Items.Count > 0 && MissingPrices == 0 && MissingWaste == 0;

    public static MealCostCalculation Create(StoredMeal meal, IReadOnlyList<CostDraft> drafts, int headCount, bool requireComplete = false)
    {
        if (headCount <= 0) throw new InvalidOperationException("식수는 1명 이상으로 입력해 주세요.");
        if (drafts.Count == 0) throw new InvalidOperationException("한 끼 원가를 계산할 식재료가 없습니다.");
        var values = MealCostData.Validate(meal, drafts, false);
        var items = values.Select((v,i) => new IngredientItem(v,headCount,!string.IsNullOrWhiteSpace(drafts[i].Price),drafts[i].Menu,!string.IsNullOrWhiteSpace(drafts[i].WastePercent))).ToArray();
        string signature = JsonSerializer.Serialize(new { meal.Snapshot, HeadCount = headCount, Rows = items.Select(i => new { i.Menu, i.Inputs, i.HasPrice, i.WasteConfirmed }) });
        var result = new MealCostCalculation(meal.Identity, headCount, items, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signature))));
        if (requireComplete && !result.Complete)
            throw new InvalidOperationException($"구매가격 미입력 {result.MissingPrices}행, 폐기율 미입력 {result.MissingWaste}행을 채워 주세요. 폐기 없음은 0을 입력합니다. 미완성 값은 ‘재료·기준값만 저장’으로 보관할 수 있습니다.");
        return result;
    }

    public IReadOnlyList<IList<object>> ToSheetRows(StoredMeal meal, DateTimeOffset savedAt)
    {
        if (!Complete || MealIdentity != meal.Identity) throw new InvalidOperationException("완성된 현재 식단의 원가만 저장할 수 있습니다.");
        List<object> Common(string kind) => new object[] { MealIdentity, kind, meal.DateText, meal.Meal, meal.AgeGroup, HeadCount, "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", savedAt.ToString("o"), Fingerprint }.ToList();
        var rows = new List<IList<object>>();
        var total = Common("합계"); total[22] = PerPersonCost; total[23] = TotalCost; rows.Add(total);
        foreach (var item in Items)
        {
            var v = item.Inputs; var basis = CostCalculator.PurchaseBasis(v); var row = Common("재료");
            row[6]=item.Menu; row[7]=item.Name; row[8]=v.UnitSize; row[9]=v.Unit; row[10]=v.WastePercent;
            row[11]=CostCalculator.GrossSize(v); row[12]=v.UnitPrice; row[13]=v.PriceQuantity; row[14]=v.PriceUnit;
            row[15]=(object?)v.PackageWeightGrams??""; row[16]=basis.Quantity; row[17]=basis.Unit;
            row[18]=CostCalculator.PricePerBaseUnit(v); row[19]=item.PerPersonCost!.Value;
            row[20]=item.TotalQuantity; row[21]=item.TotalCost!.Value; rows.Add(row);
        }
        return rows;
    }
}

public sealed record SavedMealCalculation(string Identity, int HeadCount, decimal PerPersonCost, decimal TotalCost, string SavedAt, string Fingerprint);

public partial class GoogleSheetsService
{
    public const string MealCalculationSheet = "원가계산결과";
    public static readonly string[] MealCalculationHeaders = ["식단고유키", "행구분", "날짜", "끼니", "연령대", "식수", "메뉴명", "식재료명", "1인분 정미량", "정미량·구매량 단위", "폐기율(%)", "1인분 구매량", "구매가격(원)", "가격 기준량", "가격 단위", "개·봉당 중량(g)", "환산 기준량", "환산 단위", "g·ml·개당 단가(원)", "1인분 재료비(원)", "발주량", "재료 총 원가(원)", "한 끼 1인당 원가(원)", "한 끼 총 원가(원)", "저장시각", "계산기준지문"];

    internal static void ValidateCalculationHeaders(IList<IList<object>> rows)
    {
        if (rows.Count == 0) return;
        for (int i=0;i<MealCalculationHeaders.Length;i++)
            if (Cell(rows[0],i) != MealCalculationHeaders[i]) throw new InvalidOperationException($"{MealCalculationSheet} 시트의 {i+1}열이 예상 구조와 다릅니다. 기존 자료를 덮어쓰지 않았습니다.");
    }

    public async Task<List<SavedMealCalculation>> GetSavedMealCalculationsAsync()
    {
        var titles=await GetSheetTitlesAsync().ConfigureAwait(false);
        string? title=titles.FirstOrDefault(t=>Normalize(t)==Normalize(MealCalculationSheet));
        if(title==null)return [];
        var rows=await GetValuesAsync(QuoteTitle(title)+"!A:Z").ConfigureAwait(false);
        ValidateCalculationHeaders(rows);
        return rows.Skip(1).Where(r=>Cell(r,1)=="합계").Select(r=>new SavedMealCalculation(Cell(r,0),
            int.Parse(Cell(r,5),CultureInfo.InvariantCulture),CostCalculator.Number(Cell(r,22),"저장된 1인당 원가"),
            CostCalculator.Number(Cell(r,23),"저장된 총 원가"),Cell(r,24),Cell(r,25))).ToList();
    }

    public async Task<MealCostCalculation> SaveMealCalculationAsync(StoredMeal meal, IReadOnlyList<CostDraft> source, int headCount)
    {
        RequirePersonalWrite();
        var drafts=source.Select(MealCostData.Clone).ToList();
        var calculation=MealCostCalculation.Create(meal,drafts,headCount,true);
        // Match the meal editor's concurrency check before persisting a meal-specific total.
        await _mealSaveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var latest=(await GetStoredMealsAsync().ConfigureAwait(false)).LastOrDefault(m=>m.Identity==meal.Identity);
            if(latest==null || latest.Snapshot!=meal.Snapshot) throw new InvalidOperationException("식단이 다른 곳에서 변경되거나 삭제되었습니다. 새로고침한 뒤 원가를 다시 계산해 주세요.");
            await SaveMealCostsAsync(meal,drafts,calculation:calculation).ConfigureAwait(false);
            return calculation;
        }
        finally { _mealSaveGate.Release(); }
    }

    private async Task<List<Request>> CalculationRequestsAsync(StoredMeal meal, MealCostCalculation calculation)
    {
        string title=await EnsureSheetAsync(MealCalculationSheet,MealCalculationHeaders).ConfigureAwait(false);
        var rows=await GetValuesAsync(QuoteTitle(title)+"!A:Z").ConfigureAwait(false);
        ValidateCalculationHeaders(rows);
        int id=await GetSheetIdByTitleAsync(title).ConfigureAwait(false)??throw new InvalidOperationException("원가 계산 결과 탭을 찾을 수 없습니다.");
        var metadata=_sheetsService.Spreadsheets.Get(SpreadsheetId);metadata.Fields="sheets(properties)";
        var book=await metadata.ExecuteAsync().ConfigureAwait(false);
        int columns=book.Sheets.First(s=>s.Properties.SheetId==id).Properties.GridProperties.ColumnCount??0;
        var requests=new List<Request>();
        if(columns<MealCalculationHeaders.Length) requests.Add(new Request { AppendDimension=new AppendDimensionRequest { SheetId=id,Dimension="COLUMNS",Length=MealCalculationHeaders.Length-columns } });
        for(int i=rows.Count-1;i>=1;i--)
            if(Cell(rows[i],0)==meal.Identity) requests.Add(new Request { DeleteDimension=new DeleteDimensionRequest { Range=new DimensionRange { SheetId=id,Dimension="ROWS",StartIndex=i,EndIndex=i+1 } } });
        requests.Add(new Request { UpdateCells=new UpdateCellsRequest { Start=new GridCoordinate { SheetId=id,RowIndex=0,ColumnIndex=0 },Rows=[new RowData { Values=MealCalculationHeaders.Select(TextCell).ToList() }],Fields="userEnteredValue" } });
        requests.Add(new Request { AppendCells=new AppendCellsRequest { SheetId=id,Rows=calculation.ToSheetRows(meal,DateTimeOffset.Now).Select(r=>new RowData { Values=r.Select(DataCell).ToList() }).ToList(),Fields="userEnteredValue" } });
        return requests;
    }
}
