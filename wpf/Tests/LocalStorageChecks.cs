using System.IO;
using System.IO.Compression;
using System.Globalization;
using System.Text.Json;
using System.Xml.Linq;
using Google.Apis.Sheets.v4.Data;
using wpf;

internal static class LocalStorageChecks
{
    internal static void Run(Action<bool, string> check, Action<Action, string> reject)
    {
        string directory = Path.Combine(Environment.CurrentDirectory, "local-checks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory); string path = Path.Combine(directory, "data.json");
        GoogleSheetsService? root = null;
        try
        {
            root = GoogleSheetsService.OpenLocal(path);
            check(root.IsLocal && File.Exists(path), "로컬 시작: OAuth 없이 실제 자료 파일 생성");
            reject(() => GoogleSheetsService.OpenLocal(path), "같은 로컬 자료를 두 프로세스가 동시에 덮어쓰지 못함");
            check(root.LoadCurrentUserAsync().GetAwaiter().GetResult().FriendlyName == "이 PC 사용자", "로컬 계정 표시에는 Google 조회가 필요 없음");
            var initial = root.GetFacilitiesAsync().GetAwaiter().GetResult().Single();
            var f = root.SaveFacilityAsync(initial, "검증 급식소", 30).GetAwaiter().GetResult()!;
            var service = root.ForFacility(f);
            service.GetPatientsAsync().GetAwaiter().GetResult();
            var patient = new PatientModel { Id = 1, Name = "검증대상-001", Category = "일반", Allergies = "우유,참깨", AllergyState = "있음 확인", AllergyChangeConfirmed = true };
            service.SavePatientsAsync([patient]).GetAwaiter().GetResult();
            check(service.GetMealReviewPatientsAsync().GetAwaiter().GetResult().Single().Allergies.Contains("참깨"), "대상자 알레르기를 로컬 파일에 저장·조회");
            service.AddDinersAsync([new Diner("검증대상-001", "7", "", "익명")]).GetAwaiter().GetResult();
            check(service.GetDinersAsync().GetAwaiter().GetResult().Single().Name == "검증대상-001", "일반 피급식자 명단 저장");
            var categories = WeeklyMealPlanner.Categories;
            var items = categories.Select((c, i) => new TrackMenu("LOCAL" + i, "검증메뉴" + i, c, "없음", 10, c == "주찬" ? 12 : 2, 2, 80, "로컬 검증") { ServingGrams = 100, Almond = "없음 확인", Sesame = "없음 확인", OtherBeans = "없음 확인" }).ToArray();
            var week = Enumerable.Range(0, 5).Select(i => new WeeklyMeal { Date = new DateTime(2026, 11, 9).AddDays(i), Meal = "중식", AgeGroup = "6-18", Track = "B", Items = items, Targets = new(60, 20, 12, 15) }).ToArray();
            service.SaveWeeklyMealsAsync(week).GetAwaiter().GetResult();
            check(service.GetStoredMealsAsync().GetAwaiter().GetResult().Count == 5, "한 주 식단 5끼 실제 로컬 저장");
            service.SaveWeeklyMealsAsync(week).GetAwaiter().GetResult();
            check(service.GetStoredMealsAsync().GetAwaiter().GetResult().Count == 5, "동일 주간 식단 저장은 중복 없이 갱신");
            var selected = service.GetStoredMealsAsync().GetAwaiter().GetResult()[0];
            var eight = items.Concat([items[3] with { Id = "EX1", Name = "검증추가1" }, items[3] with { Id = "EX2", Name = "검증추가2" }]).ToArray();
            service.ChangeStoredMealAsync(selected, eight).GetAwaiter().GetResult();
            selected = service.GetStoredMealsAsync().GetAwaiter().GetResult()[0];
            check(selected.Menus.Length == 8 && selected.SavedItems!.Count(m => m.Category is "주찬" or "부찬" or "김치류") == 5, "반찬 5개 식단 수정 후 세부 메뉴 저장");
            var milk = items[3] with { Allergens = "2", Name = "우유반찬" };
            check(ManualAllergyReview.Describe([milk], service.GetMealReviewPatientsAsync().GetAwaiter().GetResult()).Any(x => x.Contains("검증대상-001") && x.Contains("우유")), "로컬 대상자에게 우유 반찬 직접 추가 시 경고 근거 표시");
            var inputs = CostCalculationChecks.Inputs(selected);
            var calculation = service.SaveMealCalculationAsync(selected, inputs, 30).GetAwaiter().GetResult();
            check(calculation.PerPersonCost == 1600 && calculation.TotalCost == 48000, "8개 메뉴의 한 끼 원가·식수 합계 로컬 계산 저장");
            service.SaveMealCalculationAsync(selected, inputs, 30).GetAwaiter().GetResult();
            check(service.GetSavedMealCalculationsAsync().GetAwaiter().GetResult().Count == 1, "계산 결과 재저장 시 이전 결과 교체");
            check(service.GetMealCostsAsync(true).GetAwaiter().GetResult().Count(IngredientStandards.IsStandard) == 1, "공통 재료 단가는 모든 메뉴에 한 번만 저장");
            service.SavePersonalRecipeAsync("6-18", items[2], "닭고기 100g").GetAwaiter().GetResult();
            check(service.ReadPersonalRecipesAsync("6-18").GetAwaiter().GetResult().Single().Ingredients == "닭고기 100g", "사용자 레시피와 재료 로컬 저장");
            check(service.GetTrackCatalogAsync("6-18").GetAwaiter().GetResult().Menus.Any(m => m.Id == items[2].Id), "공식 메뉴와 로컬 레시피 통합");
            string events = service.EnsureEventSheetAsync().GetAwaiter().GetResult();
            service.AppendRowAsync("'" + events + "'!A:B", new object[] { "2026-07-27", "[매년] 생일" }).GetAwaiter().GetResult();
            var other = root.SaveFacilityAsync(null, "다른 급식소", 12).GetAwaiter().GetResult()!;
            check(root.ForFacility(other).GetStoredMealsAsync().GetAwaiter().GetResult().Count == 0, "급식소별 식단 자료 격리");
            var tables = service.ExportTablesAsync().GetAwaiter().GetResult();
            check(!tables.Any(t => t.Title == AppConfig.FacilitySheetName), "엑셀에 다른 급식소 ID·목록을 포함하지 않음");
            string xlsx = Path.Combine(directory, "transfer.xlsx"); WorkbookExcel.Write(xlsx, tables);
            var read = WorkbookExcel.Read(xlsx);
            string Canonical(IEnumerable<WorkbookTable> source) => JsonSerializer.Serialize(source.Select(t => new { t.Title, Rows = t.Rows.Select(r => r.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture))) }));
            check(Canonical(read) == Canonical(tables), "개인 자료 엑셀 내보내기·읽기 값 일치");
            var imported = root.ImportLocalTablesAsync("엑셀 가져온 급식소", 30, read).GetAwaiter().GetResult();
            check(root.ForFacility(imported).GetStoredMealsAsync().GetAwaiter().GetResult().First().Menus.Length == 8, "엑셀 가져오기에서 5개 반찬과 메뉴 스냅샷 보존");
            var duplicateFile = File.ReadAllBytes(path);
            reject(() => root.ImportLocalTablesAsync("엑셀 가져온 급식소", 30, read).GetAwaiter().GetResult(), "중복 이름 엑셀 가져오기 거부");
            check(File.ReadAllBytes(path).SequenceEqual(duplicateFile), "가져오기 실패 시 파일이 부분 변경되지 않음");
            string backup = Path.Combine(directory, "backup.json"); root.ExportLocalBackup(backup);
            root.CloseLocal(); root = GoogleSheetsService.OpenLocal(path); service = root.ForFacility(f);
            check(service.GetStoredMealsAsync().GetAwaiter().GetResult().Count == 5 && service.GetStoredMealsAsync().GetAwaiter().GetResult()[0].Menus.Length == 8, "앱 재실행 후 주간 식단·5개 반찬 복원");
            check(service.GetPatientsAsync().GetAwaiter().GetResult().Single().Allergies.Contains("우유"), "앱 재실행 후 알레르기 명단 복원");
            check(service.ReadPersonalRecipesAsync("6-18").GetAwaiter().GetResult().Count == 1 && service.GetSavedMealCalculationsAsync().GetAwaiter().GetResult().Single().TotalCost == 48000, "재실행 후 레시피·재료·원가 합계 복원");
            check((service.GetValuesAsync("'" + events + "'!A:B").GetAwaiter().GetResult()[1][1].ToString() ?? "").Contains("생일"), "재실행 후 이벤트 복원");
            service.GetPatientsAsync().GetAwaiter().GetResult(); service.SavePatientsAsync([]).GetAwaiter().GetResult();
            check(service.GetPatientsAsync().GetAwaiter().GetResult().Count == 0, "명단 삭제 저장 시 이전 뒤쪽 행을 남기지 않음");
            root.CloseLocal(); root = null;
            using (var restored = new LocalWorkbookStore(backup)) check(restored.ReadBook(f.BookId).Sheets.Count > 3, "전체 백업 파일에서 급식소 자료 복원 가능");
            using (var store = new LocalWorkbookStore(path))
            {
                var before = File.ReadAllBytes(path);
                reject(() => store.Apply(f.BookId, new() { Requests = [new() { AddSheet = new() { Properties = new() { Title = "실패 시 남으면 안 됨" } } }, new() { DeleteDimension = new() { Range = new() { SheetId = -99, Dimension = "ROWS", StartIndex = 0, EndIndex = 1 } } }] }), "일괄 저장 중 오류 발생 시 전체 변경 거부");
                check(before.SequenceEqual(File.ReadAllBytes(path)) && store.ReadBook(f.BookId).Sheets.All(s => s.Properties.Title != "실패 시 남으면 안 됨"), "실패한 일괄 저장은 메모리와 디스크 모두 원상 유지");
            }
            string corrupt = Path.Combine(directory, "corrupt.json"); File.WriteAllText(corrupt, "broken-data");
            reject(() => new LocalWorkbookStore(corrupt), "손상된 로컬 자료를 새 빈 자료로 초기화하지 않음");
            check(File.ReadAllText(corrupt) == "broken-data", "손상 파일 원문 보존");
            string literals = Path.Combine(directory, "literal.xlsx");
            WorkbookExcel.Write(literals, [new("자료", [new object[] { "=HYPERLINK(\"https://example.invalid\")", 0, "", false, "참깨" }])]);
            check(WorkbookExcel.Read(literals)[0].Rows[0][0].ToString()!.StartsWith("=HYPERLINK"), "등호로 시작하는 사용자 텍스트를 실행 가능한 엑셀 수식으로 만들지 않음");
            using (var zip = ZipFile.OpenRead(literals))
            using (var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()) check(!XDocument.Load(stream).Descendants().Any(e => e.Name.LocalName == "f"), "내보낸 개인 자료에는 실행 수식이 없음");
        }
        finally { root?.CloseLocal(); Console.WriteLine("Local storage test artifacts: " + directory); }
    }
}
