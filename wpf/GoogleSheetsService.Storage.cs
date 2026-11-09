using Google.Apis.Sheets.v4.Data;
using System.IO;

namespace wpf;

public partial class GoogleSheetsService
{
    private readonly LocalWorkbookStore? _localStore;
    public bool IsLocal => _localStore != null;
    public string? LocalDataPath => _localStore?.FilePath;
    public static GoogleSheetsService OpenLocal(string? path = null) => new(new LocalWorkbookStore(path ?? Path.Combine(AppConfig.UserDataDirectory, "data.json")));
    private GoogleSheetsService(LocalWorkbookStore store)
    {
        _localStore = store;
        _sheetsService = null!;
        _driveService = null!;
        _userSpreadsheetId = LocalWorkbookStore.RegistryId;
    }
    public void CloseLocal() => FacilityRegistry._localStore?.Dispose();
    public void ExportLocalBackup(string path) => (_localStore ?? throw new InvalidOperationException("로컬 버전에서 사용할 수 있습니다.")).ExportBackup(path);
    private async Task<Spreadsheet> ReadBookAsync(string id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_localStore != null) return _localStore.ReadBook(id);
        var request = _sheetsService.Spreadsheets.Get(id);
        request.Fields = "spreadsheetId,properties,sheets(properties)";
        return await request.ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
    private async Task<Spreadsheet> CreateBookAsync(Spreadsheet book, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_localStore != null) return _localStore.CreateBook(book);
        return await _sheetsService.Spreadsheets.Create(book).ExecuteAsync(cancellationToken).ConfigureAwait(false);
    }
    private async Task ApplyBookChangesAsync(BatchUpdateSpreadsheetRequest request, string id)
    {
        if (_localStore != null) { _localStore.Apply(id, request); return; }
        await _sheetsService.Spreadsheets.BatchUpdate(request, id).ExecuteAsync().ConfigureAwait(false);
    }
    private async Task<IList<IList<object>>> ReadValuesAsync(string id, string range, bool formulas = false)
    {
        if (_localStore != null) return _localStore.ReadValues(id, range);
        var request = _sheetsService.Spreadsheets.Values.Get(id, range);
        if (formulas) request.ValueRenderOption = Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMULA;
        return (await request.ExecuteAsync().ConfigureAwait(false)).Values ?? new List<IList<object>>();
    }
    private async Task ClearBookValuesAsync(string id, IList<string> ranges)
    {
        if (_localStore != null) { _localStore.ClearValues(id, ranges); return; }
        await _sheetsService.Spreadsheets.Values.BatchClear(new BatchClearValuesRequest { Ranges = ranges }, id).ExecuteAsync().ConfigureAwait(false);
    }
}
