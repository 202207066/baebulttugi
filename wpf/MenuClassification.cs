namespace wpf;

public static class MenuClassification
{
    public static bool IsSide(TrackMenu menu) => menu.Category is "주찬" or "부찬" or "김치류";
    public static string Grade(TrackMenu menu)
    {
        if (!IsSide(menu)) return "분류 대상 아님";
        if (menu.ProteinReview.Length > 0 || menu.ProteinPer100g is not double value || !double.IsFinite(value) || value < 0 || value > 100)
            return "검토 필요";
        return value >= 11 ? "고단백" : value >= 7 ? "중단백" : value >= 3 ? "저단백" : "채소 메뉴";
    }
    // Draft grades remain visible; only reviewed grades change the operational category.
    public static TrackMenu Official(TrackMenu menu)
    {
        if (!menu.ProteinReviewed || menu.Category is not ("주찬" or "부찬") || Grade(menu) == "검토 필요") return menu;
        string category = Grade(menu) == "고단백" ? "주찬" : "부찬";
        return category == menu.Category ? menu : menu with { Category = category, StableKey = menu.Key };
    }
    public static bool AutomaticCandidate(TrackMenu menu) => !IsSide(menu) ||
        menu.ProteinReviewed && (menu.Category == "주찬" ? Grade(menu) == "고단백" : Grade(menu) is "중단백" or "저단백" or "채소 메뉴");
}
