using System.Windows;
namespace wpf;

public static class ManualAllergyReview
{
    public static IReadOnlyList<string> Describe(IEnumerable<TrackMenu> menus, IReadOnlyList<PatientModel> patients)
    {
        var warnings = new List<string>();
        if (patients.Count == 0) warnings.Add("등록된 대상자가 없어 알레르기 정보를 확인할 수 없습니다.");
        foreach (var p in patients.Where(p => p.NeedsAllergyReview || p.AllergyStatus == "미확인"))
            warnings.Add($"{p.Name}: 알레르기 상태 확인 필요 · {p.AllergyReview}");
        foreach (var menu in menus.Where(m => m.Id != "PENDING").DistinctBy(m => m.Key))
        {
            if (GoogleSheetsService.NeedsAllergenReview(menu)) warnings.Add($"{menu.Name}: 알레르기·원재료 확인 필요 {menu.AllergyReview}");
            foreach (var patient in patients)
            {
                var matches = GoogleSheetsService.SplitAllergens(patient.Allergies).Where(a => GoogleSheetsService.MatchesRegisteredAllergen(menu, [a])).Distinct().ToArray();
                if (matches.Length > 0) warnings.Add($"{menu.Name} → {patient.Name}: {string.Join(", ", matches.Select(Label))} 알레르기와 겹칩니다.");
            }
        }
        return warnings.Distinct().ToArray();
    }
    private static string Label(string value) => int.TryParse(value, out int n) && n is >= 1 and <= 19 ? AllergenPicker.Names[n - 1] : value;
    public static bool Confirm(IEnumerable<TrackMenu> menus, IReadOnlyList<PatientModel> patients, Window? owner = null)
    {
        var warnings = Describe(menus, patients);
        if (warnings.Count == 0) return true;
        string text = string.Join("\n", warnings.Take(12)) + (warnings.Count > 12 ? $"\n외 {warnings.Count - 12}건" : "") +
            "\n\n해당 대상자에게 그대로 제공할 수 없습니다. 대체식과 제품 원재료를 확인해 주세요.\n경고를 확인하고 이 메뉴 구성을 유지할까요?";
        return owner == null ? MessageBox.Show(text, "알레르기 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes :
            MessageBox.Show(owner, text, "알레르기 경고", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes;
    }
}

public partial class GoogleSheetsService
{
    public async Task<List<PatientModel>> GetMealReviewPatientsAsync()
    {
        var titles = await GetSheetTitlesAsync().ConfigureAwait(false);
        var title = titles.FirstOrDefault(t => Normalize(t) is "알러지인원" or "알레르기인원") ?? titles.FirstOrDefault(t => t == AppConfig.PatientSheetName);
        if (title == null) throw new InvalidOperationException("알레르기 명단을 찾을 수 없습니다. 대상자 화면에서 명단을 확인해 주세요.");
        var rows = await GetValuesAsync(QuoteTitle(title)).ConfigureAwait(false);
        if (rows.Count == 0 || PatientSheetCodec.AllergyColumns(rows[0]).Length == 0) throw new InvalidOperationException("알레르기 열이 없어 메뉴 변경 전 확인할 수 없습니다.");
        return PatientSheetCodec.Parse(rows);
    }
}
