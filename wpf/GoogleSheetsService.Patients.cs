using Google.Apis.Sheets.v4.Data;
using System.Text.Json;

namespace wpf;

public static class PatientSheetCodec
{
    private static string Cell(IList<object> r, int c) => c >= 0 && c < r.Count ? r[c]?.ToString()?.Trim() ?? "" : "";
    private static readonly string[][] Aliases = [["ID"], ["성명", "이름"], ["구분"], ["특이 알러지 성분", "특이사항", "알레르기", "알러지", "알레르기 코드", "알러지 코드", "특이 알레르기 성분", "알레르기 정보", "알러지 정보", "알레르기(코드)", "알러지(코드)", "알레르기 유발 물질", "추가 알레르기 코드", "추가 알러지 코드"], ["비고(메모)", "비고"]];
    private static string HeaderKey(string text) => System.Text.RegularExpressions.Regex.Replace(text.TrimStart('\uFEFF').Normalize(System.Text.NormalizationForm.FormKC), @"\s+", "");
    public static int[] AllergyColumns(IList<object> header) => header.Select((h,i)=>(h,i))
        .Where(x=>Aliases[3].Any(a=>HeaderKey(a)==HeaderKey(x.h?.ToString()??""))).Select(x=>x.i).ToArray();
    private static int[] Columns(IList<object> header) => Aliases.Select(a => header.ToList().FindIndex(h => a.Any(alias=>HeaderKey(alias)==HeaderKey(h?.ToString()??"")))).ToArray();
    public static bool IsManagedColumn(IList<object> header, int index) => Columns(header).Contains(index) || HeaderKey(header[index]?.ToString()??"")=="알레르기상태";
    public static List<PatientModel> Parse(IList<IList<object>> rows)
    {
        if (rows.Count == 0) return new();
        var c = Columns(rows[0]);
        var allergyColumns = AllergyColumns(rows[0]);
        if (c[1] < 0 && c[0]<0) throw new InvalidOperationException("알레르기 명단에 이름 열이 필요합니다. 기존 자료는 변경하지 않았습니다.");
        var result = new List<PatientModel>();
        for (int i = 1; i < rows.Count; i++)
        {
            if (rows[i].All(x => string.IsNullOrWhiteSpace(x?.ToString()))) continue;
            string name = Cell(rows[i], c[1]);
            int id = c[0] < 0 ? i : int.TryParse(Cell(rows[i], c[0]), out int n) && n > 0 ? n : throw new InvalidOperationException($"알레르기 명단 {i+1}행의 ID를 확인해 주세요.");
            if(name.Length==0)name=$"대상자-{id:D4}";
            if (result.Any(p => p.Id == id)) throw new InvalidOperationException("알레르기 명단의 ID가 중복됩니다.");
            var allergy = PatientAllergyInfo.Merge(allergyColumns.Select(column=>Cell(rows[i],column)),allergyColumns.Length>0);
            int stateColumn=rows[0].ToList().FindIndex(h=>HeaderKey(h?.ToString()??"")=="알레르기상태");
            string state=Cell(rows[i],stateColumn);
            var issues=new List<string>();if(allergy.Review.Length>0)issues.Add(allergy.Review);
            if(state=="미확인")issues.Add("사용자 확인 전");
            else if(state=="없음 확인" && (!PatientAllergyInfo.IsNone(allergy.Value)||allergyColumns.Length==0))issues.Add("없음 확인 상태와 알레르기 내용이 불일치");
            else if(state=="있음 확인" && (allergy.Value.Length==0||PatientAllergyInfo.IsNone(allergy.Value)))issues.Add("있음 확인 상태에 알레르기 항목 없음");
            else if(state.Length>0&&!new[]{"미확인","없음 확인","있음 확인"}.Contains(state))issues.Add("알레르기 상태 확인 필요: "+state);
            result.Add(new PatientModel { Id = id, Name = name, Category = c[2] < 0 ? "일반" : Cell(rows[i], c[2]), Allergies = allergy.Value, AllergyReview = string.Join(" · ",issues), AllergyState=state, Note = Cell(rows[i], c[4]) });
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
        int stateColumn=header.FindIndex(h=>HeaderKey(h?.ToString()??"")=="알레르기상태");
        if(stateColumn<0){stateColumn=header.Count;header.Add("알레르기 상태");}
        var oldRows = new Dictionary<int,IList<object>>();
        int k = 0;
        for (int i=1;i<original.Count;i++) if (original[i].Any(x => !string.IsNullOrWhiteSpace(x?.ToString()))) oldRows[existing[k++].Id] = original[i];
        var result = new List<IList<object>> { header };
        foreach(var p in patients)
        {
            var row = oldRows.TryGetValue(p.Id, out var prior) ? prior.ToList() : new List<object>();
            while (row.Count < header.Count) row.Add("");
            object[] values = [p.Id, p.Name, p.Category, p.Allergies, p.Note];
            for(int i=0;i<c.Length;i++) if(i!=3) row[c[i]]=values[i];
            var previous = existing.FirstOrDefault(item=>item.Id==p.Id);
            if(previous==null || previous.Allergies!=p.Allergies)
            {
                // 확인이 필요한 원본은 일반 정보 수정만으로 덮어쓰지 않습니다.
                if(previous?.NeedsAllergyReview==true && !p.AllergyChangeConfirmed)
                    throw new InvalidOperationException($"{p.Name}: 알레르기 정보 확인 후 변경 내용을 승인해 주세요.");
                foreach(int column in AllergyColumns(header)) row[column]="";
                row[c[3]]=p.Allergies;
            }
            string state=p.AllergyState.Length>0?p.AllergyState:p.AllergyStatus;
            if(state=="없음 확인"&&!PatientAllergyInfo.IsNone(p.Allergies))throw new InvalidOperationException("없음 확인과 알레르기 항목을 함께 저장할 수 없습니다.");
            if(state=="있음 확인"&&(p.Allergies.Length==0||PatientAllergyInfo.Merge([p.Allergies]).Review.Length>0||PatientAllergyInfo.IsNone(p.Allergies)))throw new InvalidOperationException("있음 확인 상태에서는 알레르기 항목을 확인해 주세요.");
            if(previous!=null&&previous.AllergyStatus!=state&&!p.AllergyChangeConfirmed)throw new InvalidOperationException("알레르기 상태 변경을 확인해 주세요.");
            row[stateColumn]=state;
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
