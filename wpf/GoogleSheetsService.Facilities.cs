using Google.Apis.Sheets.v4.Data;
namespace wpf;

public record Facility(string Id, string Name, string BookId, int Diners, bool Selected, bool Deleted = false);

public partial class GoogleSheetsService
{
    private GoogleSheetsService? _facilityRegistry;
    public GoogleSheetsService FacilityRegistry => _facilityRegistry ?? this;
    public Facility? CurrentFacility { get; private set; }
    private GoogleSheetsService(GoogleSheetsService registry, Facility facility)
    {
        _sheetsService = registry._sheetsService;
        _driveService = registry._driveService;
        _facilityRegistry = registry.FacilityRegistry;
        _userSpreadsheetId = facility.BookId;
        CurrentFacility = facility;
    }
    public GoogleSheetsService ForFacility(Facility facility) => new(FacilityRegistry, facility);
    private readonly System.Threading.SemaphoreSlim _facilityGate = new(1, 1);
    public async Task<List<Facility>> GetFacilitiesAsync()
    {
        if (FacilityRegistry != this) return await FacilityRegistry.GetFacilitiesAsync();
        RequirePersonalWrite();
        var title = await EnsureSheetAsync(AppConfig.FacilitySheetName, "급식소ID", "급식소 이름", "자료ID", "급식소 식수 인원", "선택", "삭제");
        var rows = await GetValuesAsync(QuoteTitle(title) + "!A:F");
        if (rows.Count > 0 && Cell(rows[0],0) != "급식소ID") throw new InvalidOperationException("급식소 시트의 구조를 확인해 주세요. 기존 자료는 변경하지 않았습니다.");
        if (rows.Count <= 1)
        {
            int diners = await ReadInitialDinerCountAsync();
            var initial = new Facility("default", "기본 급식소", SpreadsheetId, diners, true);
            await WriteRawAsync(QuoteTitle(title) + "!A2", [FacilityRow(initial)]);
            return [initial];
        }
        if (Cell(rows[0],0) != "급식소ID") throw new InvalidOperationException("급식소 시트의 구조를 확인해 주세요. 기존 자료는 변경하지 않았습니다.");
        return rows.Skip(1).Where(r => Cell(r,0).Length > 0).Select(r => new Facility(Cell(r,0), Cell(r,1), Cell(r,2), int.TryParse(Cell(r,3), out int n) ? n : 0, Cell(r,4) == "1", Cell(r,5) == "1")).ToList();
    }
    private static IList<object> FacilityRow(Facility f) => new object[] { f.Id, f.Name, f.BookId, f.Diners, f.Selected ? "1" : "0", f.Deleted ? "1" : "0" };
    public async Task SaveFacilityDashboardAsync(DashboardData data)
    {
        if (CurrentFacility == null) return;
        RequirePersonalWrite();
        // Dedicated snapshot avoids overwriting formulas in an older dashboard sheet.
        var title = await EnsureSheetAsync("급식소 대시보드", "급식소", "총 피급식자 수", "알레르기 환자 수", "식단 구성 상태", "오늘의 메뉴", "대체메뉴");
        await WriteRawAsync(QuoteTitle(title) + "!A2", [new object[] { CurrentFacility.Name, CurrentFacility.Diners, data.AllergyPatients, data.DietStatus, string.Join(" / ",data.TodayMenu), string.Join(" / ",data.TodayAlternativeMenu) }]);
    }
    public async Task<Facility?> SaveFacilityAsync(Facility? existing, string name, int diners, bool delete = false, bool select = true)
    {
        var registry = FacilityRegistry;
        if (registry != this) return await registry.SaveFacilityAsync(existing,name,diners,delete,select);
        if (string.IsNullOrWhiteSpace(name) || diners < 0) throw new InvalidOperationException("급식소 이름과 0 이상의 식수를 입력해 주세요.");
        await _facilityGate.WaitAsync();
        try
        {
            var all = await GetFacilitiesAsync();
            if (!delete && all.Any(f => !f.Deleted && f.Id != existing?.Id && f.Name == name.Trim())) throw new InvalidOperationException("이미 등록된 급식소 이름입니다.");
            if (existing != null && !all.Any(f => f.Id == existing.Id && !f.Deleted)) throw new InvalidOperationException("급식소 목록이 변경되었습니다. 다시 선택해 주세요.");
            string bookId = existing?.BookId ?? "";
            if (delete)
            {
                var stored = all.Single(f => f.Id == existing!.Id && !f.Deleted);
                if (stored.BookId == AppConfig.TemplateSpreadsheetId || stored.BookId == AppConfig.SpreadsheetId || string.IsNullOrWhiteSpace(stored.BookId))
                    throw new InvalidOperationException("공공 자료는 삭제할 수 없습니다.");
                if (all.Any(f => !f.Deleted && f.Id != stored.Id && f.BookId == stored.BookId))
                    throw new InvalidOperationException("다른 급식소가 함께 사용하는 자료는 삭제할 수 없습니다.");
                var scope = ForFacility(stored);
                var titles = await scope.GetSheetTitlesAsync();
                var ranges = titles.Where(t => stored.BookId != SpreadsheetId || Normalize(t) != Normalize(AppConfig.FacilitySheetName)).Select(QuoteTitle).ToList();
                if (ranges.Count > 0)
                    await _sheetsService.Spreadsheets.Values.BatchClear(new BatchClearValuesRequest { Ranges = ranges }, stored.BookId).ExecuteAsync();
                bookId = "";
            }
            if (existing == null)
            {
                var book = await _sheetsService.Spreadsheets.Create(new Spreadsheet { Properties = new SpreadsheetProperties { Title = "배불뚝이 · " + name.Trim() }, Sheets = [new Sheet { Properties = new SheetProperties { Title = "급식소정보" } }] }).ExecuteAsync();
                bookId = book.SpreadsheetId;
            }
            var updated = new Facility(existing?.Id ?? Guid.NewGuid().ToString("N"), delete ? "" : name.Trim(), bookId, delete ? 0 : diners, select && !delete, delete);
            int index = all.FindIndex(f => f.Id == updated.Id);
            if (index < 0) all.Add(updated); else all[index] = updated;
            if (select || delete)
            {
                string? selectedId = delete ? all.FirstOrDefault(f => !f.Deleted)?.Id : updated.Id;
                all = all.Select(f => f with { Selected = f.Id == selectedId }).ToList();
            }
            await WriteRawAsync(QuoteTitle(AppConfig.FacilitySheetName) + "!A2", all.Select(FacilityRow).ToList());
            return all.FirstOrDefault(f => f.Selected && !f.Deleted);
        }
        finally { _facilityGate.Release(); }
    }
}
