using System.Globalization;

namespace wpf;

public record SavedMeal(string Id, DateTime Date, string Meal, string Age, string[] Menus)
{
    public string Description => $"{Meal}{(Age.Length > 0 ? " · " + Age + "세" : "")}\n{string.Join(", ", Menus)}";
}

public partial class GoogleSheetsService
{
    public static List<SavedMeal> ParseSavedMeals(IList<IList<object>> rows)
    {
        var result = new List<SavedMeal>();
        if (rows.Count == 0) return result;
        var header = rows[0].Select(x => x?.ToString()?.Trim() ?? "").ToList();
        if (!header.Contains("날짜") || !(header.Contains("끼니") || header.Contains("구분")))
            throw new InvalidOperationException("저장된 식단 표의 날짜·끼니 열을 확인해 주세요.");
        int Index(params string[] names) => header.FindIndex(h => names.Contains(h));
        string Value(IList<object> row, params string[] names) { int i = Index(names); return i < 0 ? "" : Cell(row, i); }
        foreach (var row in rows.Skip(1))
        {
            var raw = Value(row, "날짜");
            DateTime date;
            if (!DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out date) &&
                !DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.None, out date)) continue; // Day 1 samples have no calendar date.
            string[] menus = [Value(row,"밥류","밥"),Value(row,"국류","국"),Value(row,"주찬","메인"),Value(row,"부찬","사이드1"),Value(row,"김치류","사이드2"),Value(row,"후식류","후식")];
            if (!menus.Any(s => s.Length > 0)) continue;
            result.Add(new(Value(row,"식단 ID"), date.Date, Value(row,"끼니","구분"), Value(row,"연령대"), menus.Where(x => x.Length > 0).ToArray()));
        }
        // A later generation supersedes earlier rows in the calendar, while the sheet keeps history.
        return result.GroupBy(m => (m.Date, m.Meal, m.Age)).Select(g => g.Last()).ToList();
    }

    public async Task<List<SavedMeal>> GetSavedMealsAsync(DateTime date)
    {
        var titles = await GetSheetTitlesAsync();
        if (!titles.Contains(AppConfig.WeeklyMenuSheetName)) return [];
        var rows = await GetValuesAsync(QuoteTitle(AppConfig.WeeklyMenuSheetName));
        return ParseSavedMeals(rows).Where(m => m.Date == date.Date).OrderBy(m => m.Meal == "조식" ? 0 : m.Meal == "중식" ? 1 : 2).ToList();
    }
}
