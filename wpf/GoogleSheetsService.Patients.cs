using Google.Apis.Sheets.v4.Data;
using System.Text.Json;

namespace wpf;

public static class PatientSheetCodec
{
    private static string Cell(IList<object> r, int c) => c >= 0 && c < r.Count ? r[c]?.ToString()?.Trim() ?? "" : "";
    private static readonly string[][] Aliases = [["ID"], ["성명", "이름"], ["구분"], ["특이 알러지 성분", "특이사항", "알레르기", "알러지", "알레르기 코드"], ["비고(메모)", "비고"]];
    private static int[] Columns(IList<object> header) => Aliases.Select(a => header.ToList().FindIndex(h => a.Contains(h?.ToString()?.Trim() ?? ""))).ToArray();
    public static bool IsManagedColumn(IList<object> header, int index) => Columns(header).Contains(index);
    public static List<PatientModel> Parse(IList<IList<object>> rows)
    {
        if (rows.Count == 0) return new();
        var c = Columns(rows[0]);
        if (c[1] < 0 || c[3] < 0) throw new InvalidOperationException("알레르기 명단에 이름과 알레르기 열이 필요합니다. 기존 자료는 변경하지 않았습니다.");
        var result = new List<PatientModel>();
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].All(x => string.IsNullOrWhiteSpace(x?.ToString()))) continue;
            string name = Cell(rows[i], c[1]);
            if (name.Length == 0) throw new InvalidOperationException($"알레르기 명단 {i+1}행의 이름을 확인해 주세요.");
            int id = c[0] < 0 ? i : int.TryParse(Cell(rows[i], c[0]), out int n) && n > 0 ? n : throw new InvalidOperationException($"알레르기 명단 {i+1}행의 ID를 확인해 주세요.");
            if (result.Any(p => p.Id == id)) throw new InvalidOperationException("알레르기 명단의 ID가 중복됩니다.");
            result.Add(new PatientModel { Id = id, Name = name, Category = c[2] < 0 ? "일반" : Cell(rows[i], c[2]), Allergies = Cell(rows[i], c[3]), Note = Cell(rows[i], c[4]) });
        }
        return result;
    }
    public static List<IList<object>> Merge(IList<IList<object>> original, IReadOnlyList<PatientModel> patients)
    {
        var existing = Parse(original);
        if (patients.Any(p => p.Id <= 0 || string.IsNullOrWhiteSpace(p.Name)) || patients.Select(p => p.Id).Distinct().Count() != patients.Count)
            throw new InvalidOperationException("이름과 중복되지 않는 ID가 필요합니다.");
        var header = original.Count == 0 ? new List<object>() : original[0].ToList();
        int width = Math.Max(header.Count, original.Select(r => r.Count).DefaultIfEmpty(0).Max());
        while (header.Count < width) header.Add("");
        var c = Columns(header);
        for (int i=0;i<c.Length;i++) if (c[i]<0) { c[i]=header.Count; header.Add(Aliases[i][0]); }
        var oldRows = new Dictionary<int,IList<object>>();
        int k = 0;
        for (int i=1;i<original.Count;i++) if (original[i].Any(x => !string.IsNullOrWhiteSpace(x?.ToString()))) oldRows[existing[k++].Id] = original[i];
        var result = new List<IList<object>> { header };
        foreach(var p in patients)
        {
            var row = oldRows.TryGetValue(p.Id, out var prior) ? prior.ToList() : new List<object>();
            while (row.Count < header.Count) row.Add("");
            object[] values = [p.Id, p.Name, p.Category, p.Allergies, p.Note];
            for(int i=0;i<c.Length;i++) row[c[i]]=values[i];
            result.Add(row);
        }
        return result;
    }
}

public partial class GoogleSheetsService
{
    private string? _patientSnapshot;
    private async Task<IList<IList<object>>> ReadPatientRowsAsync(string title)
    {
        var get = _sheetsService.Spreadsheets.Values.Get(SpreadsheetId, QuoteTitle(title));
        get.ValueRenderOption = Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.FORMULA;
        return (await get.ExecuteAsync()).Values ?? new List<IList<object>>();
    }
    public async Task SavePatientsAsync(IReadOnlyList<PatientModel> patients)
    {
        if (!HasUserDatabase || SpreadsheetId == AppConfig.TemplateSpreadsheetId) throw new InvalidOperationException("내 식단 자료가 준비되지 않았습니다.");
        string title = await EnsurePatientSheetAsync();
        var original = await ReadPatientRowsAsync(title);
        if (_patientSnapshot == null || JsonSerializer.Serialize(original) != _patientSnapshot)
            throw new InvalidOperationException("명단이 변경되었습니다. 최신 명단을 다시 불러온 뒤 저장해 주세요.");
        var rows = PatientSheetCodec.Merge(original, patients);
        var book = await _sheetsService.Spreadsheets.Get(SpreadsheetId).ExecuteAsync();
        var props = book.Sheets.First(s => s.Properties.Title == title).Properties;
        int width = rows[0].Count, height = Math.Max(original.Count, rows.Count);
        var requests = new List<Request>();
        if (width > props.GridProperties.ColumnCount) requests.Add(new Request { AppendDimension = new AppendDimensionRequest { SheetId = props.SheetId, Dimension = "COLUMNS", Length = width - props.GridProperties.ColumnCount } });
        if (height > props.GridProperties.RowCount) requests.Add(new Request { AppendDimension = new AppendDimensionRequest { SheetId = props.SheetId, Dimension = "ROWS", Length = height - props.GridProperties.RowCount } });
        CellData Preserve(object v, int column) => !PatientSheetCodec.IsManagedColumn(rows[0], column) && v is string s && s.StartsWith("=") ? new() { UserEnteredValue = new ExtendedValue { FormulaValue = s } } : DataCell(v);
        requests.Add(new Request { UpdateCells = new UpdateCellsRequest {
            Range = new GridRange { SheetId = props.SheetId, StartRowIndex=0, EndRowIndex=height, StartColumnIndex=0, EndColumnIndex=width },
            Rows = rows.Select(r => new RowData { Values=r.Select((v,c) => Preserve(v,c)).ToList() }).ToList(), Fields="userEnteredValue" } });
        await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests=requests }, SpreadsheetId).ExecuteAsync();
        _patientSnapshot = JsonSerializer.Serialize(await ReadPatientRowsAsync(title));
    }
}
