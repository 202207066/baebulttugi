using System.Globalization;
using Google.Apis.Sheets.v4.Data;
namespace wpf;

public static class IngredientStandards
{
    public const string Marker = "__공통_식재료기준__";
    public static readonly string[] Headers = ["식재료명", "구매가격(원)", "가격 기준량", "가격 단위", "폐기율(%)", "개·봉당 중량(g)", "비고"];
    // Only trim surrounding whitespace. Different cuts/products must not share a guessed price.
    public static string Key(string name) => name.Trim().Normalize();
    public static bool IsStandard(SavedCost row) => row.Draft.Menu == Marker && row.Age.Length == 0 && row.Key == "MASTER:" + Key(row.Draft.Name);
    public static List<CostDraft> Validate(IEnumerable<CostDraft> source)
    {
        var result = new List<CostDraft>();
        foreach (var input in source)
        {
            var row = MealCostData.Clone(input); row.Name = Key(row.Name);
            if (row.Name.Length == 0 || row.Name.Length > 300) throw new InvalidOperationException("식재료명을 1~300자로 입력해 주세요.");
            if (row.Price.Length > 0) CostCalculator.Number(row.Price, row.Name + " 구매가격");
            if (row.PriceQuantity.Length > 0) CostCalculator.Number(row.PriceQuantity, row.Name + " 가격 기준량", true);
            if (row.PriceUnit.Length > 0 && row.PriceUnit.ToLowerInvariant() is not ("g" or "kg" or "ml" or "l" or "개" or "봉")) throw new InvalidOperationException(row.Name + ": 가격 단위는 g, kg, ml, L, 개, 봉 중 하나입니다.");
            if (row.WastePercent.Length > 0 && CostCalculator.Number(row.WastePercent, row.Name + " 폐기율") >= 100) throw new InvalidOperationException(row.Name + ": 폐기율은 0 이상 100 미만입니다.");
            if (row.PackageWeightGrams.Length > 0) CostCalculator.Number(row.PackageWeightGrams, row.Name + " 개·봉당 중량", true);
            var old = result.FirstOrDefault(r => r.Name == row.Name);
            if (old != null) { if (Signature(old) != Signature(row)) throw new InvalidOperationException(row.Name + ": 같은 재료의 기준값이 서로 다릅니다. 하나로 확인해 주세요."); continue; }
            result.Add(row);
        }
        return result;
    }
    internal static string Signature(CostDraft row) => string.Join("|", row.Price, row.PriceQuantity, row.PriceUnit, row.WastePercent, row.PackageWeightGrams);
    public static void Apply(CostDraft draft, IReadOnlyList<SavedCost> saved)
    {
        var masters = saved.Where(IsStandard).Where(s => Key(s.Draft.Name) == Key(draft.Name)).ToArray();
        if (masters.Length > 1) throw new InvalidOperationException(draft.Name + ": 공통 재료 기준이 중복되어 있습니다. 원가 시트를 확인해 주세요.");
        if (masters.Length == 0) return;
        var m = masters[0].Draft;
        if(m.Price.Length>0){draft.Price = m.Price; draft.PriceQuantity = m.PriceQuantity; draft.PriceUnit = m.PriceUnit;}
        if(m.WastePercent.Length>0)draft.WastePercent = m.WastePercent;
        if(m.PackageWeightGrams.Length>0)draft.PackageWeightGrams = m.PackageWeightGrams;
        draft.SharedBasisSignature=Signature(m);
        draft.Source += " · 재료 공통 기준 적용";
    }
    public static CostDraft Merge(CostDraft? old, CostDraft incoming)
    {
        var result = old == null ? new CostDraft { Name = incoming.Name, PriceQuantity = "", PriceUnit = "" } : MealCostData.Clone(old);
        if (incoming.Price.Length > 0) { result.Price = incoming.Price; result.PriceQuantity = incoming.PriceQuantity; result.PriceUnit = incoming.PriceUnit; }
        if (incoming.WastePercent.Length > 0) result.WastePercent = incoming.WastePercent;
        if (incoming.PackageWeightGrams.Length > 0) result.PackageWeightGrams = incoming.PackageWeightGrams;
        if (result.Price.Length > 0 && (result.PriceQuantity.Length == 0 || result.PriceUnit.Length == 0)) throw new InvalidOperationException(result.Name + ": 구매가격과 가격 기준량·단위를 함께 입력해 주세요.");
        return result;
    }
}

public partial class GoogleSheetsService
{
    private static void ValidateCostHeaders(IList<IList<object>> rows)
    {
        if (rows.Count == 0) return;
        for (int c = 0; c < CostHeaders.Length; c++)
            if (Cell(rows[0], c).Length > 0 ? Cell(rows[0], c) != CostHeaders[c] : rows.Skip(1).Any(r => Cell(r, c).Length > 0))
                throw new InvalidOperationException($"원가 시트 {c + 1}열의 헤더·자료가 예상 구조와 달라 저장을 중단했습니다.");
    }
    private static List<Request> StandardRequests(int id, IList<IList<object>> rows, IReadOnlyList<CostDraft> incoming)
    {
        var result = new List<Request>();
        foreach (var row in IngredientStandards.Validate(incoming))
        {
            if (row.Price.Length == 0 && row.WastePercent.Length == 0 && row.PackageWeightGrams.Length == 0) continue;
            var matches = rows.Select((r, i) => (r, i)).Where(x => x.i > 0 && Cell(x.r, 0) == IngredientStandards.Marker && IngredientStandards.Key(Cell(x.r, 1)) == row.Name).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException(row.Name + ": 기존 공통 기준이 중복되어 있습니다.");
            CostDraft? old = matches.Length == 0 ? null : new CostDraft { Name = row.Name, Price = Cell(matches[0].r, 5), PriceQuantity = Cell(matches[0].r, 6), PriceUnit = Cell(matches[0].r, 7), WastePercent = Cell(matches[0].r, 10), PackageWeightGrams = Cell(matches[0].r, 11) };
            if(row.SharedBasisSignature!=null && (old==null?"":IngredientStandards.Signature(old))!=row.SharedBasisSignature) throw new InvalidOperationException(row.Name+": 공통 기준이 다른 곳에서 변경되었습니다. 새로고침 후 다시 입력해 주세요.");
            var merged = IngredientStandards.Merge(old, row);
            var values = new object[] { IngredientStandards.Marker, row.Name, "재료 공통 기준", "", "", merged.Price, merged.PriceQuantity, merged.PriceUnit, "", "MASTER:" + row.Name, merged.WastePercent, merged.PackageWeightGrams };
            var data = new RowData { Values = values.Select(DataCell).ToList() };
            if (matches.Length == 1) result.Add(new Request { UpdateCells = new UpdateCellsRequest { Start = new GridCoordinate { SheetId = id, RowIndex = matches[0].i, ColumnIndex = 0 }, Rows = [data], Fields = "userEnteredValue" } });
            else result.Add(new Request { AppendCells = new AppendCellsRequest { SheetId = id, Rows = [data], Fields = "userEnteredValue" } });
        }
        return result;
    }
    public async Task SaveIngredientStandardsAsync(IReadOnlyList<CostDraft> inputs)
    {
        RequirePersonalWrite(); var validated = IngredientStandards.Validate(inputs);
        await _entryGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var sheet = await EnsureSheetAsync(AppConfig.CostSheetName, CostHeaders).ConfigureAwait(false);
            var rows = await GetValuesAsync(QuoteTitle(sheet) + "!A:L").ConfigureAwait(false); ValidateCostHeaders(rows);
            int id = await GetSheetIdByTitleAsync(sheet).ConfigureAwait(false) ?? throw new InvalidOperationException("원가 탭이 없습니다.");
            var requests = StandardRequests(id, rows, validated);
            if (requests.Count == 0) return;
            var book = await ReadBookAsync(SpreadsheetId).ConfigureAwait(false); int columns = book.Sheets.First(s => s.Properties.SheetId == id).Properties.GridProperties.ColumnCount ?? 0;
            if (columns < 12) requests.Insert(0, new Request { AppendDimension = new AppendDimensionRequest { SheetId = id, Dimension = "COLUMNS", Length = 12 - columns } });
            requests.Insert(0, new Request { UpdateCells = new UpdateCellsRequest { Start = new GridCoordinate { SheetId = id, RowIndex = 0, ColumnIndex = 0 }, Rows = [new RowData { Values = CostHeaders.Select(TextCell).ToList() }], Fields = "userEnteredValue" } });
            // Expand before writing headers when migrating an older sheet.
            if (columns < 12) { var expand = requests[1]; requests.RemoveAt(1); requests.Insert(0, expand); }
            await ApplyBookChangesAsync(new BatchUpdateSpreadsheetRequest { Requests = requests }, SpreadsheetId).ConfigureAwait(false);
            _mealCostCache = null;
        }
        finally { _entryGate.Release(); }
    }
}
