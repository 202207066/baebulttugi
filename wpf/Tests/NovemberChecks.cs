using System.IO;
using System.IO.Compression;
using wpf;

internal static class NovemberChecks
{
    public static TrackMenu Reviewed(string name, string category, double protein = 3, string allergens = "없음") => new("TEST-" + name, name, category, allergens, 10, protein, 2, null, "기능 검증용 가상자료") { ServingGrams = category == "주찬" ? protein / 12 * 100 : 100, ServingBasis = "검증용 제공량", ProteinReviewed = true, Almond = "없음 확인", Sesame = "없음 확인", OtherBeans = "없음 확인" };
    public static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        foreach (var pair in new[] { (11d, "고단백"), (10.999, "중단백"), (7d, "중단백"), (6.999, "저단백"), (3d, "저단백"), (2.999, "채소 메뉴"), (0d, "채소 메뉴") })
            check((Reviewed("경계", "부찬", pair.Item1) with { ServingGrams = 100 }).ProteinGrade == pair.Item2, "100g 단백질 경계 " + pair);
        var high = Reviewed("주찬", "부찬", 5.5) with { ServingGrams = 50, Calories = 50 };
        check(MenuClassification.Official(high).Category == "주찬" && MenuClassification.Official(high).Key == high.Key, "작은 제공량의 고단백 반찬은 낮은 열량에서도 주찬이며 원본키 유지");
        check(MenuClassification.Official(high with { Category = "밥류" }).Category == "밥류" && MenuClassification.Official(high with { Category = "국류" }).Category == "국류", "밥류·국류는 단백질 수치로 분류를 바꾸지 않음");
        check((high with { ServingGrams = null }).ProteinGrade == "검토 필요" && (high with { Protein = null }).ProteinGrade == "검토 필요", "빈 제공량·단백질을 0으로 계산하지 않음");
        check(MenuClassification.AutomaticCandidate(MenuClassification.Official(high with { ProteinReviewed = false })), "제공량·단백질 기준을 충족하면 별도 승인 없이 자동 생성에 사용");
        TrackMenu Infer(params string[] names) => MenuIngredientReview.Apply(Reviewed("추론", "부찬"), names.Select(n => new PortionIngredient { Name = n, Amount = .01m, Unit = "g" }));
        foreach (string word in new[] { "참깨", "통깨", "깨소금", "참기름", "흑임자" }) check(Infer(word).Sesame == "포함", "소량 참깨 계열 포함: " + word);
        foreach (string word in new[] { "아몬드", "아몬드가루", "아몬드 슬라이스", "아몬드 우유" }) check(Infer(word).Almond == "포함", "아몬드 계열 포함: " + word);
        foreach (string word in new[] { "완두콩", "강낭콩", "병아리콩", "녹두", "팥" }) check(Infer(word).OtherBeans == "포함", "대두 외 콩 포함: " + word);
        check(Infer("대두", "콩기름", "두부").OtherBeans != "포함", "대두와 기타 콩류를 별도로 구분");
        check(GoogleSheetsService.MatchesRegisteredAllergen(Infer("참기름"), ["참깨"]), "추가 알레르기 열을 실제 제외 판별에서 읽음");
        check(!GoogleSheetsService.MatchesRegisteredAllergen(Reviewed("콩 요리", "부찬", allergens: "5"), ["콩류(기타)"]), "기타 콩류 등록을 대두 5번으로 바꾸지 않음");
        check(Infer("들깨").Sesame == "검토 필요" && GoogleSheetsService.NeedsAllergenReview(Infer("드레싱")) && GoogleSheetsService.NeedsAllergenReview(Infer()), "들깨·복합재료·재료 누락은 확인 필요");
        var mass = MenuIngredientReview.Apply(high, [new() { Name = "두부", Amount = .1m, Unit = "kg" }, new() { Name = "당근", Amount = 20, Unit = "g" }]);
        check(mass.ServingGrams == 120 && mass.ServingBasis.Contains("대용"), "kg·g 재료 합계 대용 근거 표시");
        check(MenuIngredientReview.Apply(high, [new() { Name = "간장", Amount = 5, Unit = "ml" }]).ServingGrams == null, "ml를 g로 임의 환산하지 않음");
        var patient = new PatientModel { Name = "테스트-우유참깨", Allergies = "2,참깨", AllergyState = "있음 확인" };
        var milk = Reviewed("우유감자", "부찬", allergens: "2");
        check(ManualAllergyReview.Describe([milk], [patient]).Any(s => s.Contains("우유감자") && s.Contains("우유") && s.Contains(patient.Name)), "직접 추가한 우유 메뉴가 해당 대상자와 함께 경고됨");
        check(ManualAllergyReview.Describe([Infer("참기름")], [patient]).Any(s => s.Contains("참깨") && s.Contains("겹칩니다")), "직접 교체한 메뉴의 참깨 열도 경고됨");
        check(ManualAllergyReview.Describe([Reviewed("안전자료", "밥류")], [patient]).Count == 0, "확인된 무충돌 메뉴만 경고 없음");
        check(ManualAllergyReview.Describe([milk], []).Any(s => s.Contains("확인할 수 없습니다")), "대상자 미등록을 알레르기 없음으로 간주하지 않음");
        var catalog = WeeklyMealPlanner.Categories.SelectMany(c => Enumerable.Range(1, 5).Select(i => Reviewed(c + i, c))).Append(milk).Append(Infer("참기름")).ToArray();
        var eligible = catalog.Where(m => MenuClassification.AutomaticCandidate(m) && !GoogleSheetsService.NeedsAllergenReview(m) && !GoogleSheetsService.MatchesRegisteredAllergen(m, ["2", "참깨"])).ToArray();
        var week = WeeklyMealPlanner.Generate(eligible, new(2026, 11, 2), [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday], ["중식"], "3-5", "검증용", new(60, 18, 12, 20), seed: 7);
        check(week.Count == 5 && week.All(m => m.Items.Count(i => i.Category == "주찬" && i.ProteinGrade == "고단백") == 1 && m.Items.All(i => !GoogleSheetsService.MatchesRegisteredAllergen(i, ["우유", "참깨"]))), "5일 생성: 매 끼 고단백 주찬 1개 및 우유·참깨 제외");
        check(WeeklyMealPlanner.Generate(eligible.Select(m => m with { ProteinReviewed = false }).ToArray(), DateTime.Today, [DayOfWeek.Monday], ["중식"], "3-5", "검증", new(60, 18, 12, 20)).Count == 1, "별도 승인 표시가 없어도 수치 기준으로 식단 생성");
        var saved = new SavedCost("", "MASTER:양파", new() { Menu = IngredientStandards.Marker, Name = "양파", Price = "2000", PriceQuantity = "1", PriceUnit = "kg", WastePercent = "10", PackageWeightGrams = "150" });
        var old = new CostDraft { Menu = "옛 레시피", Name = "양파", Amount = "50", Unit = "g", Price = "1", PriceQuantity = "1", PriceUnit = "g" };
        IngredientStandards.Apply(old, [saved]);
        check(old.Price == "2000" && old.WastePercent == "10" && old.PackageWeightGrams == "150" && old.Amount == "50", "공통 단가·폐기율·개당 중량이 기존 레시피에도 적용되고 소요량 유지");
        check(decimal.Round(CostCalculator.CostPerPerson(old.ToCostRow()), 6) == 111.111111m, "정미량 50g·폐기율10%·kg당2000원의 원가 계산");
        var other = Clone(old); other.Name = "양파, 다진것"; other.Price = "3"; IngredientStandards.Apply(other, [saved]); check(other.Price == "3", "이름이 다른 재료를 임의로 통합하지 않음");
        reject(() => IngredientStandards.Validate([old, Clone(old).WithPrice("3000")]), "같은 재료의 상충 단가 거부");
        reject(() => IngredientStandards.Validate([new() { Name = "양파", WastePercent = "100" }]), "100% 폐기율 저장 거부");
        var partial = IngredientStandards.Merge(saved.Draft, new() { Name = "양파", Price = "", WastePercent = "0" });
        check(partial.Price == "2000" && partial.WastePercent == "0", "빈 입력은 기존 단가를 보존하고 명시적 0%는 반영");
        var partialMaster = new SavedCost("", "MASTER:양파", new() { Menu = IngredientStandards.Marker, Name = "양파", Price = "", PriceQuantity = "", PriceUnit = "", WastePercent = "5" });
        var existing = new CostDraft { Name = "양파", Price = "3000", PriceQuantity = "1", PriceUnit = "kg", PackageWeightGrams = "150" };
        IngredientStandards.Apply(existing, [partialMaster]);
        check(existing.Price == "3000" && existing.PackageWeightGrams == "150" && existing.WastePercent == "5", "일부만 입력한 공통 기준은 기존 단가·개당 중량을 지우지 않음");
        var requests = typeof(GoogleSheetsService).GetMethod("StandardRequests", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var currentRows = new List<IList<object>> { GoogleSheetsService.CostHeaders.Cast<object>().ToArray(), new object[] { IngredientStandards.Marker, "양파", "", "", "", "4500", "1", "kg", "", "MASTER:양파", "5", "" } };
        bool staleRejected = false;
        try { requests.Invoke(null, [1, currentRows, new[] { existing }]); } catch (System.Reflection.TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { staleRejected = true; }
        check(staleRejected, "다른 화면에서 바뀐 공통 단가를 오래된 편집 내용으로 덮어쓰지 않음");
        ExcelChecks(check, reject);
    }
    private static CostDraft Clone(CostDraft row)=>System.Text.Json.JsonSerializer.Deserialize<CostDraft>(System.Text.Json.JsonSerializer.Serialize(row))!;
    private static CostDraft WithPrice(this CostDraft row, string price) { row.Price = price; return row; }
    private static void ExcelChecks(Action<bool, string> check, Action<Action, string> reject)
    {
        string file = Path.Combine(Path.GetTempPath(), "baebulttugi-import-test-" + Guid.NewGuid().ToString("N") + ".xlsx");
        void Write(string data)
        {
            using var archive = ZipFile.Open(file, ZipArchiveMode.Create);
            void Entry(string name, string xml) { using var writer = new StreamWriter(archive.CreateEntry(name).Open()); writer.Write(xml); }
            Entry("xl/workbook.xml", "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='재료기준표' sheetId='1' r:id='r1'/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels", "<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Target='worksheets/sheet1.xml'/></Relationships>");
            string header = string.Join("", IngredientStandards.Headers.Select((h, i) => $"<c r='{(char)('A' + i)}1' t='inlineStr'><is><t>{h}</t></is></c>"));
            Entry("xl/worksheets/sheet1.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'>" + header + "</row>" + data + "</sheetData></worksheet>");
        }
        const string row = "<row r='2'><c r='A2' t='inlineStr'><is><t>양파</t></is></c><c r='B2'><v>2000</v></c><c r='C2'><v>1</v></c><c r='D2' t='inlineStr'><is><t>kg</t></is></c><c r='E2'><v>10</v></c></row>";
        try {
            using (var template = typeof(IngredientStandardsWindow).Assembly.GetManifestResourceStream("wpf.IngredientTemplate") ?? throw new Exception("배포 양식 누락"))
            using (var output = File.Create(file)) template.CopyTo(output);
            check(IngredientExcel.Read(file).Count == 0, "내장 Excel 양식의 실제 구조를 읽고 1596개의 미입력 기준값은 기존 단가를 지우지 않음");
            File.Delete(file); Write(row); var data = IngredientExcel.Read(file); check(data.Count == 1 && data[0].Name == "양파" && data[0].Price == "2000", "Excel의 문자열·숫자·비어 있는 셀 일괄 입력"); File.Delete(file); Write(row.Replace("<v>2000</v>", "<f>1000*2</f><v>2000</v>")); reject(() => IngredientExcel.Read(file), "수식의 오래된 캐시를 구매가격으로 가져오지 않음");
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }
}
