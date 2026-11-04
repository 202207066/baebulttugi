namespace wpf;

public static class MenuClassification
{
    public static bool IsSide(TrackMenu menu) => menu.Category is "주찬" or "부찬" or "김치류";
    public static bool UsesProteinCriteria(TrackMenu menu) => menu.Category is "주찬" or "부찬";
    public static string Grade(TrackMenu menu)
    {
        if (!UsesProteinCriteria(menu)) return "분류 대상 아님";
        if (menu.ProteinReview.Length > 0 || menu.ProteinPer100g is not double value || !double.IsFinite(value) || value < 0 || value > 100)
            return "검토 필요";
        return value >= 11 ? "고단백" : value >= 7 ? "중단백" : value >= 3 ? "저단백" : "채소 메뉴";
    }
    // Apply the supplied numeric criteria directly. Legacy approval flags are metadata only.
    public static TrackMenu Official(TrackMenu menu)
    {
        if (!UsesProteinCriteria(menu) || Grade(menu) == "검토 필요") return menu;
        string category = Grade(menu) == "고단백" ? "주찬" : "부찬";
        return category == menu.Category ? menu : menu with { Category = category, StableKey = menu.Key };
    }
    public static bool AutomaticCandidate(TrackMenu menu) => !UsesProteinCriteria(menu) || Grade(menu) != "검토 필요";
    public static string Status(TrackMenu menu) => !UsesProteinCriteria(menu) ? "기존 분류 유지" :
        Grade(menu) == "검토 필요" ? "자료 확인 필요" : "수치 기준 적용";
}
