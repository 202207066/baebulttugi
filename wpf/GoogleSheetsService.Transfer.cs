namespace wpf;

public partial class GoogleSheetsService
{
    public async Task<IReadOnlyList<WorkbookTable>> ExportTablesAsync()
    {
        if (CurrentFacility == null) throw new InvalidOperationException("먼저 급식소를 선택해 주세요.");
        var tables = new List<WorkbookTable>();
        foreach (string title in await GetSheetTitlesAsync(forceRefresh: true))
        {
            if (Normalize(title) == Normalize(AppConfig.FacilitySheetName)) continue;
            tables.Add(new(title, await ReadTransferValuesAsync(title)));
        }
        // Portable settings contain no remote workbook identifiers.
        tables.RemoveAll(t => t.Title == "배불뚝이 내보내기 정보");
        tables.Add(new("배불뚝이 내보내기 정보", [new object[] { "급식소", "식수", "내보낸 날짜", "형식" }, new object[] { CurrentFacility.Name, CurrentFacility.Diners, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), "Baebulttugi.Personal.v1" }]));
        return tables;
    }
    private async Task<IList<IList<object>>> ReadTransferValuesAsync(string title)
    {
        if (_localStore != null) return _localStore.ReadValues(SpreadsheetId, QuoteTitle(title));
        var request = _sheetsService.Spreadsheets.Values.Get(SpreadsheetId, QuoteTitle(title));
        // Currency/percent formatting must not become literal text in the imported cost data.
        request.ValueRenderOption = Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
        request.DateTimeRenderOption = Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.GetRequest.DateTimeRenderOptionEnum.FORMATTEDSTRING;
        return (await request.ExecuteAsync().ConfigureAwait(false)).Values ?? new List<IList<object>>();
    }
    public async Task<Facility> ImportLocalTablesAsync(string name, int diners, IReadOnlyList<WorkbookTable> tables)
    {
        var registry = FacilityRegistry;
        if (registry._localStore == null) throw new InvalidOperationException("엑셀 가져오기는 로컬 버전에서 이용할 수 있습니다.");
        var data = tables.Where(t => Normalize(t.Title) != Normalize(AppConfig.FacilitySheetName) && t.Title != "배불뚝이 내보내기 정보").ToArray();
        string[] known = [AppConfig.WeeklyMenuSheetName, AppConfig.PatientSheetName, AppConfig.CostSheetName, AppConfig.EventSheetName, "피급식자 명단", "식수인원", PersonalTrackTitle("3-5"), PersonalTrackTitle("6-18"), "급식소정보", "사용 안내"];
        if (!data.Any(t => known.Contains(t.Title))) throw new InvalidOperationException("배불뚝이 개인 자료 탭을 찾지 못했습니다. '내 자료 · 엑셀'에서 내보낸 파일이나 배불뚝이 Google 시트의 엑셀 파일을 선택해 주세요. 재료 기준표는 원가 화면에서 불러오세요.");
        await registry._facilityGate.WaitAsync();
        try
        {
            await registry.GetFacilitiesAsync();
            var imported = registry._localStore.ImportFacility(AppConfig.FacilitySheetName, name, diners, data);
            registry.InvalidateSheetTitles();
            return imported;
        }
        finally { registry._facilityGate.Release(); }
    }
}
