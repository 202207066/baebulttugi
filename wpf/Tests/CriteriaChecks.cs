using wpf;

internal static class CriteriaChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var side = new TrackMenu("source-1", "기준 시험 반찬", "부찬", "없음", 10, 5.5, 2, 70, "기준 시험")
        { ServingGrams = 50, ProteinReviewed = false, Almond = "없음 확인", Sesame = "없음 확인", OtherBeans = "없음 확인" };
        foreach (var (protein, grade, category) in new[] { (11d,"고단백","주찬"), (10.999,"중단백","부찬"), (7d,"중단백","부찬"), (6.999,"저단백","부찬"), (3d,"저단백","부찬"), (2.999,"채소 메뉴","부찬"), (0d,"채소 메뉴","부찬") })
        {
            var actual = MenuClassification.Official(side with { Protein = protein, ServingGrams = 100 });
            check(actual.ProteinGrade == grade && actual.Category == category && MenuClassification.AutomaticCandidate(actual), $"승인 없이 {protein}g/100g → {grade}·{category}");
        }
        check(MenuClassification.Official(side).Category == "주찬" && MenuClassification.Official(side).Key == side.Key && !side.ProteinReviewed,
            "작은 1인분의 11g/100g 주찬을 즉시 적용하고 원본 메뉴키 보존");
        var single = MealCostData.CostMeal(MenuClassification.Official(side), "3-5");
        check(single.MenuKeys.Length == 6 && single.MenuKeys[0] == "" && single.MenuKeys[2] == side.Key, "앞의 밥·국 칸이 비어 있어도 주찬의 원본 키 위치 보존");
        check(MenuClassification.Official(side with { Calories = 900, Protein = 1 }).Category == "부찬", "열량이 높아도 단백질 밀도가 낮으면 주찬으로 분류하지 않음");
        foreach (string category in new[] { "밥류", "국류", "후식류", "김치류" })
            check(MenuClassification.Official(side with { Category = category }).Category == category, category + "는 기존 분류 유지");
        check(MenuClassification.Official(side with { Category = "김치류" }).ProteinGrade == "분류 대상 아님" && MenuClassification.AutomaticCandidate(side with { Category = "김치류", ServingGrams = null }), "김치류에는 새 단백질 분류 조건을 적용하지 않음");
        foreach (var missing in new[] { side with { ServingGrams = null }, side with { ServingGrams = 0 }, side with { Protein = null }, side with { ProteinReview = "제공량 대조 필요" }, side with { Protein = 101, ServingGrams = 100 } })
            check(!MenuClassification.AutomaticCandidate(missing with { ProteinReviewed = true }) && missing.ProteinGrade == "검토 필요", "승인 기록 유무와 관계없이 누락·모호한 수치는 제외");

        var ingredients = new[] { new PortionIngredient { Name = "닭살", Amount = .04m, Unit = "kg" }, new PortionIngredient { Name = "양파", Amount = 10, Unit = "g" } };
        var fallback = MenuIngredientReview.Apply(side, ingredients);
        check(fallback.ServingGrams == 50 && fallback.ServingBasis.Contains("대용") && MenuClassification.Official(fallback).Category == "주찬", "완성 제공량이 없으면 g·kg 재료 합계로 분류하고 대용 근거 표시");

        foreach (string age in new[] { "3-5", "6-18" })
        {
            var original = OfficialCatalog.Current.Menus.Where(m => m.Track == WeeklyMealPlanner.TrackForAge(age)).ToArray();
            var menus = OfficialCatalog.Menus(age);
            check(original.All(m => !m.Menu.ProteinReviewed) && menus.Any(m => m.Category == "주찬" && m.ProteinGrade == "고단백"), age + " 실제 내장 자료에 승인 없이 고단백 주찬 적용");
            check(original.Where(m => m.Menu.Category is "밥류" or "국류").All(m => menus.Single(n => n.Key == m.Menu.Key).Category == m.Menu.Category), age + " 실제 밥·국 분류 및 메뉴 식별자 유지");
            var eligible = menus.Where(m => MenuClassification.AutomaticCandidate(m) && !GoogleSheetsService.NeedsAllergenReview(m) && !GoogleSheetsService.MatchesRegisteredAllergen(m, ["우유", "참깨"])).ToArray();
            Console.WriteLine("CATALOG " + age + " " + string.Join(", ", WeeklyMealPlanner.Categories.Select(c => c + "=" + eligible.Count(m => m.Category == c && m.HasNutrition))));
            var generated = WeeklyMealPlanner.Generate(eligible, new(2026,11,2), [DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday], ["중식"], age, "실제 공식 자료", new(60,18,12,20), seed:13);
            check(generated.Count == 5 && generated.All(m => m.Items.Count(i => i.Category == "주찬" && i.ProteinGrade == "고단백") == 1), age + " 실제 자료로 5일 생성, 매 끼 고단백 주찬 1개");
            check(generated.All(m => m.Items.Where(i => i.Category == "부찬").All(i => i.ProteinGrade is "중단백" or "저단백" or "채소 메뉴")), age + " 부찬은 중·저단백·채소 메뉴이며 김치류는 그대로 유지");
            check(generated.SelectMany(m => m.Items).All(i => !GoogleSheetsService.MatchesRegisteredAllergen(i, ["우유", "참깨"]) && !GoogleSheetsService.NeedsAllergenReview(i)), age + " 실제 식단에서도 우유·참깨 및 알레르기 확인 필요 메뉴 제외");
            check(generated.SelectMany(m => m.Items).All(i => { var rows = MealCostData.Build(MealCostData.CostMeal(i,age), []); return rows.Count > 0 && rows.All(r => r.Name.Length > 0); }), age + " 자동 재분류된 메뉴의 식재료·원가 연결 유지");
            if (age == "6-18")
            {
                var cabbage = WeeklyMealPlanner.FixedBaechuKimchi(menus, ["우유", "참깨"]);
                var fixedWeek = WeeklyMealPlanner.Generate(eligible, new(2026,11,2), [DayOfWeek.Monday,DayOfWeek.Tuesday,DayOfWeek.Wednesday,DayOfWeek.Thursday,DayOfWeek.Friday], ["중식"], age, "배추김치 고정", new(60,18,12,20), new Dictionary<string,string> { ["김치류"] = cabbage.Key }, seed:13);
                check(fixedWeek.Count == 5 && fixedWeek.All(m => m.Kimchi == "배추김치" && m.Items[4].Key == cabbage.Key && m.Items.Count(i => i.Category == "주찬" && i.ProteinGrade == "고단백") == 1), "6~18세 실제 자료로 5일 모두 배추김치 고정 및 고단백 주찬 유지");
                check(fixedWeek.SelectMany(m => m.Items).All(i => !GoogleSheetsService.MatchesRegisteredAllergen(i, ["우유", "참깨"])), "배추김치를 고정해도 우유·참깨 제외 유지");
                MustReject(() => WeeklyMealPlanner.FixedBaechuKimchi(menus, ["새우"]), "알레르기와 겹쳐", "배추김치 고정도 대상자의 새우 알레르기를 우회하지 않음");
                MustReject(() => WeeklyMealPlanner.FixedBaechuKimchi([cabbage, cabbage with { Id = "중복" }], []), "여러 개", "중복 배추김치를 임의로 고르지 않음");
                MustReject(() => WeeklyMealPlanner.FixedBaechuKimchi([cabbage with { AllergyReview = "원재료 확인 필요" }], []), "확인할 항목", "배추김치 알레르기 정보가 모호하면 고정 중단");
            }
            else MustReject(() => WeeklyMealPlanner.FixedBaechuKimchi(menus, []), "영양값", "3~5세 배추김치의 실제 누락 영양값을 안내하고 임의 계산하지 않음");
        }
        MustReject(() => WeeklyMealPlanner.FixedBaechuKimchi([], []), "없습니다", "배추김치가 없으면 고정 해제 또는 추가 안내");

        void MustReject(Action action, string expected, string label)
        {
            try { action(); } catch (InvalidOperationException ex) { check(ex.Message.Contains(expected), label); return; }
            check(false, label);
        }
    }

    internal static void RunUi(Action<bool,string> check)
    {
        var page = new Menu_table("6-18");
        var checkbox = (System.Windows.Controls.CheckBox)page.FindName("FixedBaechuKimchiBox");
        var status = (System.Windows.Controls.TextBlock)page.FindName("KimchiChoiceStatus");
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var choices = (Dictionary<string,System.Windows.Controls.ComboBox>)typeof(Menu_table).GetField("_fixed", flags)!.GetValue(page)!;
        var catalog = new TrackCatalog("실제 자료", "", OfficialCatalog.Menus("6-18").ToList(), false);
        typeof(Menu_table).GetField("_catalog", flags)!.SetValue(page, catalog);
        check(checkbox.Content.ToString() == "배추김치 고정" && checkbox.IsChecked != true && choices["김치류"].IsEnabled, "배추김치 고정은 기본 해제된 체크박스 하나");
        checkbox.IsChecked = true;
        check(!choices["김치류"].IsEnabled && status.Text.Contains("모든 끼니"), "체크하면 배추김치 고정 안내 및 별도 김치 선택 잠금");
        checkbox.IsChecked = false;
        check(choices["김치류"].IsEnabled && status.Text.StartsWith("체크하면"), "체크를 해제하면 고정을 해제하고 기존 김치 선택 복원");
        checkbox.IsChecked = true;
        typeof(Menu_table).GetMethod("Reset_Click", flags)!.Invoke(page, [page, new System.Windows.RoutedEventArgs()]);
        check(checkbox.IsChecked == false && choices["김치류"].IsEnabled, "초기화 버튼도 배추김치 고정 해제");
        typeof(Menu_table).GetField("_catalog", flags)!.SetValue(page, new TrackCatalog("실제 자료", "", OfficialCatalog.Menus("3-5").ToList(), false));
        checkbox.IsChecked = true;
        check(status.Text.Contains("영양값"), "3~5세 배추김치 누락 정보가 체크박스 옆에 표시됨");
    }
}
