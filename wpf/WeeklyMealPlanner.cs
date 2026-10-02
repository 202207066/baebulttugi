using System.Globalization;
using System.Text.RegularExpressions;

namespace wpf;

public record TrackMenu(string Id, string Name, string Category, string Allergens,
    double? Carb, double? Protein, double? Fat, double? Calories, string Source)
{
    // Track B restarts IDs in each section. Keep source IDs, but distinguish recipes.
    public override string ToString() => Name;
    public double? DisplayCalories => Calories ?? (HasNutrition ? Energy : null);
    public string DisplayName => Id.Length == 0 ? Name : MealPresentation.Label(Name, DisplayCalories);
    public string Key => $"{Id}:{Category}:{Name}";
    public bool HasNutrition => Carb.HasValue && Protein.HasValue && Fat.HasValue;
    public double Energy => Calories ?? (Carb!.Value * 4 + Protein!.Value * 4 + Fat!.Value * 9);
}

public record MealTargets(double Carb, double Protein, double Fat, double TolerancePercent, double? Calories = null);

public sealed class WeeklyMeal
{
    public string Id => Date.ToString("yyMMdd", CultureInfo.InvariantCulture) + (Meal switch { "조식" => "M", "중식" => "L", "석식" => "N", _ => throw new InvalidOperationException("끼니를 확인해 주세요.") });
    public DateTime Date { get; init; }
    public string DateText => Date.ToString("yyyy-MM-dd");
    public string Day => "일월화수목금토"[(int)Date.DayOfWeek].ToString();
    public string Meal { get; init; } = "";
    public string AgeGroup { get; init; } = "";
    public string Track { get; init; } = "";
    public TrackMenu[] Items { get; init; } = [];
    public MealTargets Targets { get; init; } = new(0, 0, 0, 15);
    public string Rice => Items[0].Name;
    public string Soup => Items[1].Name;
    public string Main => Items[2].Name;
    public string Side => Items[3].Name;
    public string Kimchi => Items[4].Name;
    public string Dessert => Items[5].Name;
    public double Carb => Math.Round(Items.Sum(x => x.Carb!.Value), 2);
    public double Protein => Math.Round(Items.Sum(x => x.Protein!.Value), 2);
    public double Fat => Math.Round(Items.Sum(x => x.Fat!.Value), 2);
    public double Calories => Math.Round(Items.Sum(x => x.Energy), 1);
    public double CarbDifference => Math.Round(Carb - Targets.Carb, 2);
    public double ProteinDifference => Math.Round(Protein - Targets.Protein, 2);
    public double FatDifference => Math.Round(Fat - Targets.Fat, 2);
    public double? CalorieDifference => Targets.Calories.HasValue ? Math.Round(Calories - Targets.Calories.Value, 1) : null;
    public bool WithinTolerance =>
        Math.Abs(Items.Sum(x => x.Carb!.Value) - Targets.Carb) <= Targets.Carb * Targets.TolerancePercent / 100 + 1e-8 &&
        Math.Abs(Items.Sum(x => x.Protein!.Value) - Targets.Protein) <= Targets.Protein * Targets.TolerancePercent / 100 + 1e-8 &&
        Math.Abs(Items.Sum(x => x.Fat!.Value) - Targets.Fat) <= Targets.Fat * Targets.TolerancePercent / 100 + 1e-8 &&
        (!Targets.Calories.HasValue || Math.Abs(Calories - Targets.Calories.Value) <= Targets.Calories.Value * Targets.TolerancePercent / 100 + 1e-8);
    public string Status => WithinTolerance ? "허용범위 내" : "목표 편차 확인";
}

public static class WeeklyMealPlanner
{
    public static readonly string[] Categories = ["밥류", "국류", "주찬", "부찬", "김치류", "후식류"];
    public static DateTime Monday(DateTime date) => date.Date.AddDays(-((int)date.DayOfWeek + 6) % 7);
    public static string TrackForAge(string age) => age switch
    {
        "3-5" => "A", "6-18" or "6-11" or "12-18" => "B",
        _ => throw new InvalidOperationException("지원하는 연령은 3~5세와 6~18세입니다.")
    };

    public static double? Number(string raw)
    {
        var clean = Regex.Replace(raw.Trim(), @"\s*(g|kcal)\s*$", "", RegexOptions.IgnoreCase);
        return double.TryParse(clean, NumberStyles.Float | NumberStyles.AllowThousands,
            CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && value >= 0 ? value : null;
    }

    public static string Category(string raw) => raw.Trim() switch
    {
        "밥류" or "밥" or "주식" => "밥류",
        "국·찌개류" or "국찌개류" or "국류" or "국" => "국류",
        "주찬류" or "주찬" => "주찬", "부찬류" or "부찬" => "부찬",
        "김치류" or "김치" => "김치류", "간식류" or "후식류" or "후식" => "후식류",
        _ => ""
    };

    // Find the real header below the title/summary rows. Never treat section separators as recipes.
    public static List<TrackMenu> Parse(IList<IList<object>> rows)
    {
        static string Cell(IList<object> row, int col) => col >= 0 && col < row.Count ? Convert.ToString(row[col], CultureInfo.InvariantCulture)?.Trim() ?? "" : "";
        int header = -1;
        for (int i = 0; i < Math.Min(rows.Count, 15); i++)
            if (rows[i].Any(x => x?.ToString()?.Trim() == "메뉴ID") && rows[i].Any(x => x?.ToString()?.Trim() == "메뉴명")) { header = i; break; }
        if (header < 0) throw new InvalidOperationException("트랙 시트에서 메뉴ID·메뉴명 헤더를 찾지 못했습니다.");
        var columns = rows[header].Select(x => x?.ToString()?.Trim() ?? "").ToList();
        int Col(string name) => columns.IndexOf(name);
        string Get(IList<object> row, string name) => Cell(row, Col(name));
        var result = new List<TrackMenu>();
        var ids = new HashSet<string>();
        foreach (var row in rows.Skip(header + 1))
        {
            string id = Get(row, "메뉴ID"), name = Get(row, "메뉴명");
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name)) continue;
            string key = $"{id}:{Category(Get(row, "카테고리"))}:{name}";
            if (!ids.Add(key)) throw new InvalidOperationException($"트랙에 같은 ID·분류·이름의 메뉴가 중복되어 있습니다: {id} {name}");
            result.Add(new(id, name, Category(Get(row, "카테고리")),
                Get(row, "알레르기 코드") + "," + Get(row, "추가 알레르기 코드"),
                Number(Get(row, "탄수화물(g)")), Number(Get(row, "단백질(g)")), Number(Get(row, "지방(g)")),
                Number(Get(row, "열량(kcal)")), Get(row, "영양정보 출처")));
        }
        return result;
    }

    public static List<WeeklyMeal> Generate(IReadOnlyList<TrackMenu> menus, DateTime week,
        IReadOnlyCollection<DayOfWeek> days, IReadOnlyList<string> meals, string age,
        string track, MealTargets targets, IReadOnlyDictionary<string, string>? fixedIds = null, int? seed = null)
    {
        TrackForAge(age);
        if (days.Count == 0 || days.Any(d => (int)d < 0 || (int)d > 6) || meals.Count == 0 || meals.Any(m => m is not ("조식" or "중식" or "석식")))
            throw new ArgumentException("급식 요일과 끼니를 선택해 주세요.");
        if (new[] { targets.Carb, targets.Protein, targets.Fat }.Any(x => !double.IsFinite(x) || x <= 0) ||
            (targets.Calories.HasValue && (!double.IsFinite(targets.Calories.Value) || targets.Calories.Value <= 0)) ||
            !double.IsFinite(targets.TolerancePercent) || targets.TolerancePercent < 0 || targets.TolerancePercent > 100)
            throw new ArgumentException("한 끼 영양 목표는 0보다 큰 숫자, 허용 편차는 0~100%로 입력해 주세요.");
        var pools = Categories.Select(c => menus.Where(m => m.Category == c && m.HasNutrition &&
            new[] { m.Carb!.Value, m.Protein!.Value, m.Fat!.Value }.All(n => double.IsFinite(n) && n >= 0)).ToArray()).ToArray();
        for (int i = 0; i < pools.Length; i++)
        {
            if (fixedIds != null && fixedIds.TryGetValue(Categories[i], out var id)) pools[i] = pools[i].Where(x => x.Key == id).ToArray();
        }
        var missing = Categories.Where((_, i) => pools[i].Length == 0).ToArray();
        if (missing.Length > 0) throw new InvalidOperationException(
            $"영양정보가 입력된 후보가 없습니다: {string.Join(", ", missing)}\n선택한 트랙의 탄수화물(g)·단백질(g)·지방(g)을 1인분 기준으로 입력해 주세요. 알레르기 제외 및 고정 메뉴 조건도 확인해 주세요.");

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var usage = new Dictionary<string, int>();
        var results = new List<WeeklyMeal>();
        static string NameKey(TrackMenu m) => Regex.Replace(m.Name, @"[\s①-⑳★☆]", "").Normalize();
        double Score(TrackMenu[] items)
        {
            if (items.Select(x => x.Name).Distinct().Count() != 6) return double.PositiveInfinity;
            var deviations = new List<double> { Math.Abs(items.Sum(x => x.Carb!.Value) - targets.Carb) / targets.Carb,
                Math.Abs(items.Sum(x => x.Protein!.Value) - targets.Protein) / targets.Protein,
                Math.Abs(items.Sum(x => x.Fat!.Value) - targets.Fat) / targets.Fat };
            if (targets.Calories.HasValue) deviations.Add(Math.Abs(items.Sum(x => x.Energy) - targets.Calories.Value) / targets.Calories.Value);
            // Prefer meeting all three limits; diversity only breaks near ties.
            return deviations.Sum() + deviations.Sum(d => Math.Max(0, d - targets.TolerancePercent / 100)) * 10 +
                items.Sum(x => usage.GetValueOrDefault(NameKey(x))) * 0.005;
        }
        for (int day = 0; day < 7; day++)
        {
            var date = Monday(week).AddDays(day);
            if (!days.Contains(date.DayOfWeek)) continue;
            foreach (var meal in meals.Distinct())
            {
                // Prefer unused dishes across the entire week; fixed dishes are intentionally repeated.
                var mealPools = pools.Select(pool => {
                    int least = pool.Min(m => usage.GetValueOrDefault(NameKey(m)));
                    return pool.Where(m => usage.GetValueOrDefault(NameKey(m)) == least).ToArray();
                }).ToArray();
                TrackMenu[]? best = null;
                double bestScore = double.PositiveInfinity;
                for (int restart = 0; restart < 24; restart++)
                {
                    var current = mealPools.Select(p => p[random.Next(p.Length)]).ToArray();
                    for (int pass = 0; pass < 3; pass++)
                        foreach (var slot in Enumerable.Range(0, 6).OrderBy(_ => random.Next()))
                        {
                            var chosen = current[slot];
                            double score = Score(current);
                            foreach (var item in mealPools[slot])
                            {
                                current[slot] = item;
                                double nextScore = Score(current);
                                if (nextScore < score) { score = nextScore; chosen = item; }
                            }
                            current[slot] = chosen;
                        }
                    double final = Score(current);
                    if (final < bestScore) { bestScore = final; best = current.ToArray(); }
                }
                if (best == null) throw new InvalidOperationException("6개 분류를 중복 없이 구성할 수 없습니다. 후보 메뉴를 확인해 주세요.");
                results.Add(new WeeklyMeal { Date = date, Meal = meal, AgeGroup = age, Track = track, Items = best, Targets = targets });
                foreach (var item in best) usage[NameKey(item)] = usage.GetValueOrDefault(NameKey(item)) + 1;
            }
        }
        return results;
    }
}
