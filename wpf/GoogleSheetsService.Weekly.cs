using Google.Apis.Sheets.v4.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace wpf;

public record TrackCatalog(string Title, string SpreadsheetId, List<TrackMenu> Menus, bool FromTemplate);

public partial class GoogleSheetsService
{
    public static readonly string[] WeeklyHeaders = ["식단 ID", "날짜", "끼니", "밥류", "국류", "주찬", "부찬", "김치류", "후식류",
        "요일", "연령대", "출처 트랙", "탄수화물(g)", "단백질(g)", "지방(g)", "열량(kcal)",
        "목표 탄수화물(g)", "목표 단백질(g)", "목표 지방(g)", "탄수화물 차이(g)", "단백질 차이(g)", "지방 차이(g)",
        "목표 충족 상태", "주 시작일", "생성 시각", "메뉴ID 목록", "허용 편차(%)"];
    private static string QuoteTitle(string title) => "'" + title.Replace("'", "''") + "'";

    private readonly System.Threading.SemaphoreSlim _catalogGate = new(1, 1);
    private readonly Dictionary<string, (DateTime Time, TrackCatalog Catalog)> _catalogCache = new();
    public async Task<TrackCatalog> GetTrackCatalogAsync(string age, bool forceRefresh = false)
    {
        string key = SpreadsheetId + ":" + WeeklyMealPlanner.TrackForAge(age);
        await _catalogGate.WaitAsync().ConfigureAwait(false);
        try {
            if (!forceRefresh && _catalogCache.TryGetValue(key, out var cached) && DateTime.UtcNow - cached.Time < TimeSpan.FromMinutes(5)) return cached.Catalog;
            var catalog = await LoadTrackCatalogAsync(age).ConfigureAwait(false);
            var own = await ReadPersonalRecipesAsync(age).ConfigureAwait(false);
            var overrides = own.Select(x => x.Menu.Key).ToHashSet();
            catalog = catalog with { Menus = catalog.Menus.Where(m => !overrides.Contains(m.Key)).Concat(own.Select(x => x.Menu)).ToList() };
            _catalogCache[key] = (DateTime.UtcNow, catalog);
            return catalog;
        } finally { _catalogGate.Release(); }
    }

    private async Task<TrackCatalog> LoadTrackCatalogAsync(string age)
    {
        string code = WeeklyMealPlanner.TrackForAge(age);
        string wanted = code == "A" ? AppConfig.TrackASheetName : AppConfig.TrackBSheetName;
        // Tracks are the shared recipe catalog; read the master so existing personal copies
        // receive updated nutrition data without overwriting any personal meal history.
        string sourceId = AppConfig.TemplateSpreadsheetId;
        var metadata = _sheetsService.Spreadsheets.Get(sourceId);
        metadata.Fields = "sheets(properties(title))";
        var book = await metadata.ExecuteAsync().ConfigureAwait(false);
        string? Match(Spreadsheet b) => b.Sheets?.Select(s => s.Properties.Title).FirstOrDefault(t => t == wanted)
            ?? b.Sheets?.Select(s => s.Properties.Title).FirstOrDefault(t => t.Contains("트랙" + code, StringComparison.OrdinalIgnoreCase));
        string? title = Match(book);
        bool template = sourceId != SpreadsheetId;
        if (title == null) throw new InvalidOperationException($"'{wanted}' 시트를 찾을 수 없습니다.");
        var request = _sheetsService.Spreadsheets.Values.Get(sourceId, QuoteTitle(title) + "!A:U");
        request.ValueRenderOption = Google.Apis.Sheets.v4.SpreadsheetsResource.ValuesResource.GetRequest.ValueRenderOptionEnum.UNFORMATTEDVALUE;
        var values = await request.ExecuteAsync().ConfigureAwait(false);
        return new(title, sourceId, WeeklyMealPlanner.Parse(values.Values ?? new List<IList<object>>()), template);
    }

    // Strict read for generation: a network/schema failure must not silently disable allergy exclusions.
    public async Task<HashSet<string>> GetPlannerAllergensAsync()
    {
        var titles = await GetSheetTitlesAsync();
        var title = titles.FirstOrDefault(t => Normalize(t) == "알러지인원" || Normalize(t) == "알레르기인원")
            ?? titles.FirstOrDefault(t => t == AppConfig.PatientSheetName);
        if (title == null) return new(StringComparer.OrdinalIgnoreCase);
        var rows = await GetValuesAsync(QuoteTitle(title) + "!A:Z");
        if (rows.Count == 0) return new(StringComparer.OrdinalIgnoreCase);
        var headers = rows[0].Select(x => x?.ToString()?.Trim() ?? "").ToList();
        int col = headers.FindIndex(h => h is "특이 알러지 성분" or "알레르기" or "알러지" or "특이사항" or "알레르기 코드");
        if (col < 0) throw new InvalidOperationException("알러지 명단에서 알레르기 열을 찾지 못했습니다.");
        return rows.Skip(1).SelectMany(r => SplitAllergens(Cell(r, col))).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly string[] AllergenNames = ["난류", "우유", "메밀", "땅콩", "대두", "밀", "고등어", "게", "새우", "돼지고기", "복숭아", "토마토", "아황산류", "호두", "닭고기", "쇠고기", "오징어", "조개류", "잣"];
    private static HashSet<string> AllergenTokens(string text)
    {
        text = text.Normalize(NormalizationForm.FormKC).Replace("계란", "난류").Replace("달걀", "난류")
            .Replace("소고기", "쇠고기").Replace("대두콩", "대두");
        var tokens = Regex.Matches(text, @"\d+").Select(m => m.Value).ToHashSet();
        for (int i = 0; i < AllergenNames.Length; i++) if (text.Contains(AllergenNames[i])) tokens.Add((i + 1).ToString());
        if (text.Contains("땅콩") == false && (text == "콩" || text.Contains("콩류"))) tokens.Add("5");
        foreach (var token in Regex.Split(text, @"[,;/\s]+")) if (token.Length > 0) tokens.Add(token);
        return tokens;
    }

    public static bool MatchesRegisteredAllergen(TrackMenu menu, IEnumerable<string> registered)
    {
        // Keep circled numbers separate before compatibility normalization (⑤⑥ must not become 56).
        string Expand(string s) => Regex.Replace(s, "[①-⑳]", m => ((int)m.Value[0] - '①' + 1).ToString() + ",");
        var actual = AllergenTokens(Expand(menu.Allergens));
        return registered.Any(r => actual.Overlaps(AllergenTokens(Expand(r))) || menu.Name.Contains(r, StringComparison.OrdinalIgnoreCase));
    }

    private static CellData TextCell(string text) => new() { UserEnteredValue = new ExtendedValue { StringValue = text } };
    private static CellData DataCell(object value) => value is double or float or decimal or int or long or short
        ? new() { UserEnteredValue = new ExtendedValue { NumberValue = Convert.ToDouble(value, CultureInfo.InvariantCulture) } }
        : value is bool flag ? new() { UserEnteredValue = new ExtendedValue { BoolValue = flag } }
        : TextCell(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "");

    private readonly System.Threading.SemaphoreSlim _mealSaveGate = new(1, 1);
    public async Task SaveWeeklyMealsAsync(IReadOnlyList<WeeklyMeal> meals)
    {
        await _mealSaveGate.WaitAsync().ConfigureAwait(false);
        try { await SaveWeeklyMealsCoreAsync(meals).ConfigureAwait(false); }
        finally { _mealSaveGate.Release(); }
    }
    private async Task SaveWeeklyMealsCoreAsync(IReadOnlyList<WeeklyMeal> meals)
    {
        if (!HasUserDatabase || SpreadsheetId == AppConfig.TemplateSpreadsheetId || SpreadsheetId == AppConfig.SpreadsheetId)
            throw new InvalidOperationException("개인 DB가 준비되지 않았습니다. 원본 시트에는 식단을 저장하지 않습니다. 다시 로그인해 개인 DB를 준비해 주세요.");
        if (meals.Count == 0) throw new ArgumentException("저장할 식단이 없습니다.");
        var sheet = await EnsureSheetAsync(AppConfig.WeeklyMenuSheetName);
        int id = await GetSheetIdByTitleAsync(sheet) ?? throw new InvalidOperationException("저장 탭을 찾지 못했습니다.");
        var existing = await GetValuesAsync(QuoteTitle(sheet));
        if (existing.Count > 0 && existing[0].Count > 0)
        {
            string[] legacy = ["식단 ID", "날짜", "구분", "밥", "국", "메인", "사이드1", "사이드2", "후식"];
            for (int i = 0; i < Math.Min(existing[0].Count, WeeklyHeaders.Length); i++)
                if (!string.IsNullOrWhiteSpace(Cell(existing[0], i)) && Cell(existing[0], i) != WeeklyHeaders[i] &&
                    !(i < legacy.Length && Cell(existing[0], i) == legacy[i]))
                    throw new InvalidOperationException($"메뉴 시트 {i + 1}번째 열이 예상 구조와 다릅니다. 기존 데이터 보존을 위해 저장을 중단했습니다.");
        }
        var pending = meals.GroupBy(m => (m.Date.Date, m.Meal, m.AgeGroup)).Select(g => g.Last()).ToList();
        var now = DateTimeOffset.Now.ToString("o");
        var rows = pending.Select(m => new RowData { Values = new object[] {
            m.Id, m.DateText, m.Meal, m.Rice, m.Soup, m.Main, m.Side, m.Kimchi, m.Dessert,
            m.Day, m.AgeGroup, m.Track, m.Carb, m.Protein, m.Fat, m.Calories,
            m.Targets.Carb, m.Targets.Protein, m.Targets.Fat, m.CarbDifference, m.ProteinDifference, m.FatDifference,
            m.Status, WeeklyMealPlanner.Monday(m.Date).ToString("yyyy-MM-dd"), now,
            string.Join(" / ", m.Items.Select(i => i.Key)), m.Targets.TolerancePercent }.Select(DataCell).ToList() }).ToList();
        var book = await _sheetsService.Spreadsheets.Get(SpreadsheetId).ExecuteAsync();
        int columns = book.Sheets.First(s => s.Properties.SheetId == id).Properties.GridProperties.ColumnCount ?? 0;
        var requests = new List<Request>();
        if (columns < WeeklyHeaders.Length) requests.Add(new Request { AppendDimension = new AppendDimensionRequest { SheetId = id, Dimension = "COLUMNS", Length = WeeklyHeaders.Length - columns } });
        requests.Add(new Request { UpdateCells = new UpdateCellsRequest {
            Start = new GridCoordinate { SheetId = id, RowIndex = 0, ColumnIndex = 0 },
            Rows = [new RowData { Values = WeeklyHeaders.Select(TextCell).ToList() }], Fields = "userEnteredValue" } });
        var appended = new List<RowData>();
        for (int n = 0; n < pending.Count; n++) {
            var meal = pending[n];
            int index = -1;
            for (int r = 1; r < existing.Count; r++)
                if (Cell(existing[r], 1) == meal.DateText && Cell(existing[r], 2) == meal.Meal && Cell(existing[r], 10) == meal.AgeGroup) index = r;
            if (index < 0) appended.Add(rows[n]);
            else requests.Add(new Request { UpdateCells = new UpdateCellsRequest {
                Start = new GridCoordinate { SheetId = id, RowIndex = index, ColumnIndex = 0 }, Rows = [rows[n]], Fields = "userEnteredValue" } });
        }
        if (appended.Count > 0) requests.Add(new Request { AppendCells = new AppendCellsRequest { SheetId = id, Rows = appended, Fields = "userEnteredValue" } });
        await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest { Requests = requests }, SpreadsheetId).ExecuteAsync();
    }
}
