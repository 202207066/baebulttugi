using Google.Apis.Sheets.v4.Data;
using System.Globalization;
using Microsoft.VisualBasic.FileIO;
using System.IO;

namespace wpf;

public record PersonalRecipe(TrackMenu Menu, string Ingredients);
public record Diner(string Name, string Age, string Gender, string Notes);
public static class DinerCsv
{
    public static List<Diner> Parse(TextReader reader)
    {
        using var parser = new TextFieldParser(reader) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = parser.ReadFields() ?? throw new InvalidOperationException("CSV가 비어 있습니다.");
        header = header.Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        int name = Array.IndexOf(header, "이름"), age = Array.IndexOf(header, "나이"), gender = Array.IndexOf(header, "성별"), notes = Array.IndexOf(header, "특이사항");
        if (name < 0) throw new InvalidOperationException("CSV 첫 행에 이름 열이 필요합니다. 나이·성별·특이사항 열은 선택입니다.");
        var result = new List<Diner>();
        while (!parser.EndOfData) {
            var row = parser.ReadFields()!;
            string At(int i) => i >= 0 && i < row.Length ? row[i].Trim() : "";
            if (row.All(string.IsNullOrWhiteSpace)) continue;
            var diner = new Diner(At(name), At(age), At(gender), At(notes));
            Validate(diner); result.Add(diner);
        }
        return result;
    }
    public static void Validate(Diner diner)
    {
        if (string.IsNullOrWhiteSpace(diner.Name)) throw new InvalidOperationException("이름을 입력해 주세요.");
        if (diner.Age.Length > 0 && (!int.TryParse(diner.Age, out int age) || age < 0 || age > 120)) throw new InvalidOperationException($"{diner.Name}: 나이는 0~120의 정수로 입력해 주세요.");
    }
}

public partial class GoogleSheetsService
{
    private readonly System.Threading.SemaphoreSlim _entryGate = new(1, 1);
    public static readonly string[] PersonalTrackHeaders = ["#", "메뉴ID", "메뉴명", "카테고리", "대표출처센터", "레시피파일유형", "전체출처", "레시피매칭", "알레르기 코드", "검수", "추가 알레르기 코드", "탄수화물(g)", "단백질(g)", "지방(g)", "열량(kcal)", "1인분중량(g)", "영양정보 출처", "레시피기준연령", "재료투입량합계(g)", "영양정보상태", "확인필요항목", "재료·분량·조리법"];
    private static string PersonalTrackTitle(string age) => "내_트랙" + WeeklyMealPlanner.TrackForAge(age) + "_레시피";
    private void RequirePersonalWrite()
    {
        if (!HasUserDatabase || SpreadsheetId == AppConfig.TemplateSpreadsheetId || SpreadsheetId == AppConfig.SpreadsheetId)
            throw new InvalidOperationException("개인 DB를 준비한 뒤 저장해 주세요.");
    }
    public async Task<List<PersonalRecipe>> ReadPersonalRecipesAsync(string age)
    {
        if (!HasUserDatabase) return [];
        string title = PersonalTrackTitle(age);
        var titles = await GetSheetTitlesAsync().ConfigureAwait(false);
        if (!titles.Contains(title)) return [];
        var rows = await GetValuesAsync(QuoteTitle(title) + "!A:V").ConfigureAwait(false);
        if (rows.Count == 0) return [];
        var parsed = WeeklyMealPlanner.Parse(rows);
        return parsed.Select(menu => new PersonalRecipe(menu, rows.Skip(1).Where(r => Cell(r, 1) == menu.Id && Cell(r, 2) == menu.Name && WeeklyMealPlanner.Category(Cell(r, 3)) == menu.Category).Select(r => Cell(r, 21)).FirstOrDefault() ?? "")).ToList();
    }
    public async Task SavePersonalRecipeAsync(string age, TrackMenu menu, string ingredients)
    {
        RequirePersonalWrite();
        if (string.IsNullOrWhiteSpace(menu.Name) || !WeeklyMealPlanner.Categories.Contains(menu.Category)) throw new InvalidOperationException("메뉴명과 분류를 확인해 주세요.");
        if (new[] { menu.Carb, menu.Protein, menu.Fat, menu.Calories }.Any(x => x.HasValue && (!double.IsFinite(x.Value) || x < 0))) throw new InvalidOperationException("영양량은 0 이상의 숫자로 입력해 주세요.");
        await _catalogGate.WaitAsync().ConfigureAwait(false);
        try {
            var sheet = await EnsureSheetAsync(PersonalTrackTitle(age), PersonalTrackHeaders).ConfigureAwait(false);
            var rows = await GetValuesAsync(QuoteTitle(sheet) + "!A:V").ConfigureAwait(false);
            if (rows.Count > 0 && !rows[0].Select(x => x.ToString()).SequenceEqual(PersonalTrackHeaders)) throw new InvalidOperationException("개인 레시피 시트의 헤더가 변경되어 저장을 중단했습니다.");
            int index = -1;
            for (int i = 1; i < rows.Count; i++) if (Cell(rows[i], 1) == menu.Id && Cell(rows[i], 2) == menu.Name && WeeklyMealPlanner.Category(Cell(rows[i], 3)) == menu.Category) index = i;
            object Num(double? value) => value.HasValue ? value.Value : "";
            IList<object> row = new object[] { "", menu.Id, menu.Name, menu.Category, "사용자", "직접 입력", "", "", menu.Allergens, "", "", Num(menu.Carb), Num(menu.Protein), Num(menu.Fat), Num(menu.Calories ?? (menu.HasNutrition ? menu.Energy : null)), "", menu.Source, age, "", menu.HasNutrition ? "사용자 입력" : "영양정보 미완성", "", ingredients };
            await WriteRawAsync(QuoteTitle(sheet) + "!A" + (index < 0 ? Math.Max(2, rows.Count + 1) : index + 1), [row]).ConfigureAwait(false);
            _catalogCache.Clear();
        } finally { _catalogGate.Release(); }
    }
    private async Task WriteRawAsync(string range, IList<IList<object>> rows)
    {
        var request = _sheetsService.Spreadsheets.Values.Update(new ValueRange { Values = rows }, SpreadsheetId, range);
        request.ValueInputOption = Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
        await request.ExecuteAsync().ConfigureAwait(false);
    }
    private async Task<(string Title, IList<IList<object>> Rows, int Header)> ReadDinerRowsAsync(bool create)
    {
        var titles = await GetSheetTitlesAsync().ConfigureAwait(false);
        string? title = titles.FirstOrDefault(t => Normalize(t) == "식수인원");
        if (title == null && !create) return ("식수인원", new List<IList<object>>(), -1);
        title ??= await EnsureSheetAsync("식수인원", "이름", "나이", "성별", "특이사항", "등록일자").ConfigureAwait(false);
        var rows = await GetValuesAsync(QuoteTitle(title) + "!A:Z").ConfigureAwait(false);
        int header = -1;
        for (int i = 0; i < Math.Min(15, rows.Count); i++) if (rows[i].Any(c => c.ToString()?.Trim() == "이름")) { header = i; break; }
        if (header < 0) throw new InvalidOperationException("식수인원 시트에서 이름 헤더를 찾지 못했습니다.");
        return (title, rows, header);
    }
    public async Task<List<Diner>> GetDinersAsync()
    {
        var data = await ReadDinerRowsAsync(false).ConfigureAwait(false);
        if (data.Header < 0) return [];
        var h = data.Rows[data.Header].Select(x => x.ToString()?.Trim()).ToList();
        string At(IList<object> r, string col) { int i = h.IndexOf(col); return i < 0 ? "" : Cell(r, i); }
        return data.Rows.Skip(data.Header + 1).Where(r => At(r, "이름").Length > 0).Select(r => new Diner(At(r,"이름"),At(r,"나이"),At(r,"성별"),At(r,"특이사항"))).ToList();
    }
    public async Task<int> AddDinersAsync(IReadOnlyList<Diner> diners)
    {
        RequirePersonalWrite();
        foreach (var diner in diners) DinerCsv.Validate(diner);
        await _entryGate.WaitAsync().ConfigureAwait(false);
        try {
            var data = await ReadDinerRowsAsync(true).ConfigureAwait(false);
            var h = data.Rows[data.Header].Select(x => x.ToString()?.Trim()).ToList();
            foreach (string column in new[] { "이름", "나이", "성별", "특이사항" }) if (!h.Contains(column)) throw new InvalidOperationException($"식수인원 시트에 {column} 열이 없습니다.");
            var rows = new List<IList<object>>();
            var known = data.Rows.Skip(data.Header + 1).Select(r => string.Join("\u001f", new[] { "이름", "나이", "성별", "특이사항" }.Select(c => Cell(r, h.IndexOf(c))))).ToHashSet();
            foreach (var d in diners) {
                if (!known.Add(string.Join("\u001f", d.Name, d.Age, d.Gender, d.Notes))) continue;
                var row = Enumerable.Repeat<object>("", h.Count).ToArray();
                row[h.IndexOf("이름")] = d.Name; row[h.IndexOf("나이")] = d.Age; row[h.IndexOf("성별")] = d.Gender; row[h.IndexOf("특이사항")] = d.Notes;
                if (h.Contains("등록일자")) row[h.IndexOf("등록일자")] = DateTime.Today.ToString("yyyy-MM-dd");
                rows.Add(row);
            }
            if (rows.Count > 0) await WriteRawAsync(QuoteTitle(data.Title) + "!A" + (data.Rows.Count + 1), rows).ConfigureAwait(false);
            return rows.Count;
        } finally { _entryGate.Release(); }
    }
}
