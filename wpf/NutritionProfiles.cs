namespace wpf;

/// <summary>2025 한국인 영양소 섭취기준의 하루 에너지와 에너지적정비율을 식단 조합에 쓰는 값입니다.</summary>
public sealed record NutritionProfile(string AgeBand, string Sex, double DailyCalories, string SourceNote)
{
    public const double CarbRatio = 0.575;     // 에너지적정비율 50~65%의 중간값
    public const double ProteinRatio = 0.15;   // 에너지적정비율 10~20%의 중간값
    public const double FatRatio = 0.225;      // 에너지적정비율 15~30%의 중간값

    public MealTargets PerMeal(int mealCount, double tolerancePercent = 15)
    {
        if (mealCount is < 1 or > 3) throw new ArgumentOutOfRangeException(nameof(mealCount));
        double calories = DailyCalories / mealCount;
        return new MealTargets(
            Math.Round(calories * CarbRatio / 4, 1),
            Math.Round(calories * ProteinRatio / 4, 1),
            Math.Round(calories * FatRatio / 9, 1),
            tolerancePercent,
            Math.Round(calories, 1));
    }
}

public static class NutritionProfiles
{
    private const string Source = "2025 한국인 영양소 섭취기준 요약표 · 에너지와 다량영양소/에너지적정비율";
    public static readonly IReadOnlyList<NutritionProfile> All = [
        new("3-5", "공통", 1400, Source),
        // 2025 기준표는 6세 이상 에너지를 남·여로 나눠 제시합니다. 식단을
        // 성별 혼합 인원에게 제공할 때는 두 값의 평균을 공통 기본값으로 씁니다.
        new("6-8", "공통", 1600, Source),
        new("9-11", "공통", 1900, Source),
        new("12-14", "공통", 2250, Source),
        new("15-18", "공통", 2350, Source),
        new("6-8", "남", 1700, Source), new("6-8", "여", 1500, Source),
        new("9-11", "남", 2000, Source), new("9-11", "여", 1800, Source),
        new("12-14", "남", 2500, Source), new("12-14", "여", 2000, Source),
        new("15-18", "남", 2700, Source), new("15-18", "여", 2000, Source)
    ];

    public static NutritionProfile Get(string ageBand, string sex) =>
        All.FirstOrDefault(p => p.AgeBand == ageBand && p.Sex == sex)
        ?? All.FirstOrDefault(p => p.AgeBand == ageBand && p.Sex == "공통")
        ?? throw new InvalidOperationException("선택한 성별·연령의 영양 기준을 찾지 못했습니다.");
}
