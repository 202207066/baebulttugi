using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace wpf
{
    /// <summary>
    /// 식단 자동 조합 화면.
    ///
    /// 이전 구현의 문제와 이번 변경:
    ///  1) 등록된 알러지가 조합에 전혀 반영되지 않았습니다 → 알러지 명단에서 모은
    ///     성분을 가진 메뉴를 후보에서 제외합니다.
    ///  2) 탄/단/지 수치가 입력한 목표값에 0.98·1.01·1.04를 곱한 값이었습니다
    ///     → 시트의 영양성분 열을 실제로 합산합니다.
    ///  3) 추천 3개가 서로 같거나 한 끼 안에 같은 메뉴가 겹칠 수 있었습니다
    ///     → 중복을 배제하고, 목표 영양소와 가장 가까운 조합을 고릅니다.
    ///  4) 자극적인 음식 판정이 밥 하나만 매워도 걸려 국·나물이 항상 순한 것만
    ///     뽑혔습니다 → 한 끼에 자극적인 메뉴가 2개를 넘지 않도록 고칩니다.
    /// </summary>
    public partial class Menu_table : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        /// <summary>시작 시 선택한 나이대("3-5", "6-11", "12-18", "adult").</summary>
        private readonly string _initialAgeGroup;

        private List<IngredientModel> cachedDbIngredients = new List<IngredientModel>();

        /// <summary>알러지 명단 시트에서 모은 제외 대상 성분.</summary>
        private HashSet<string> _registeredAllergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private bool _databaseLoaded;

        // ── 모델 ────────────────────────────────────────────────────────

        public class DietResultModel
        {
            public string DietName { get; set; } = string.Empty;
            public string Rice { get; set; } = string.Empty;
            public string Soup { get; set; } = string.Empty;
            public string Meat { get; set; } = string.Empty;
            public string Vegetable { get; set; } = string.Empty;

            public double Carb { get; set; }
            public double Protein { get; set; }
            public double Fat { get; set; }
            public double Calories { get; set; }

            public string MenuComposition => $"{Rice}, {Soup}, {Meat}, {Vegetable}";

            /// <summary>중복 조합 판정을 위한 키.</summary>
            public string CompositionKey => $"{Rice}|{Soup}|{Meat}|{Vegetable}";
        }

        /// <summary>메뉴 한 가지. MenuDatabase 시트의 한 행에 대응합니다.</summary>
        public class IngredientModel
        {
            public string Name { get; set; } = string.Empty;
            public double Calories { get; set; }
            public string Materials { get; set; } = string.Empty;
            public string Allergy { get; set; } = string.Empty;

            /// <summary>시트 E열의 분류. 비어 있으면 이름으로 추정합니다.</summary>
            public string Category { get; set; } = string.Empty;

            public double Carb { get; set; }
            public double Protein { get; set; }
            public double Fat { get; set; }

            /// <summary>탄·단·지가 하나라도 기록되어 있으면 true.</summary>
            public bool HasNutrients => Carb > 0 || Protein > 0 || Fat > 0;

            // 1. 매운 맛
            public bool IsSpicy => ContainsAny("제육", "김치", "청양", "매운", "떡볶이", "카레",
                                               "짬뽕", "고추장", "불닭", "낙지볶음", "오징어볶음");

            // 2. 짠맛 / 강한 양념
            public bool IsSalty => ContainsAny("장조림", "젓갈", "조림", "찌개", "자반",
                                               "굴비", "스팸", "소세지", "소시지", "피클");

            // 3. 기름진 맛까지 포함한 자극적인 메뉴
            public bool IsStrongTaste => IsSpicy || IsSalty ||
                                         ContainsAny("튀김", "탕수육", "돈가스", "까스", "강정");

            public bool IsMild => !IsStrongTaste;

            private bool ContainsAny(params string[] keywords)
            {
                foreach (var k in keywords)
                {
                    if (Name.Contains(k, StringComparison.Ordinal)) return true;
                }
                return false;
            }
        }

        /// <summary>식단을 구성하는 네 자리.</summary>
        private enum Slot { Rice, Soup, Meat, Vegetable }

        // ── 생성자 ──────────────────────────────────────────────────────

        public Menu_table(string ageGroup = "")
        {
            InitializeComponent();

            _sheetsService = AppServices.Require();
            _initialAgeGroup = ageGroup ?? string.Empty;

            Loaded += Menu_table_Loaded;
        }

        private async void Menu_table_Loaded(object sender, RoutedEventArgs e)
        {
            // 생성자에서 동기 호출하면 페이지 전환 순간 화면이 멈춥니다.
            // 화면이 그려진 뒤 비동기로 읽어 옵니다.
            await LoadMenuDatabaseAsync();
            ApplyInitialAgeGroup();
        }

        /// <summary>
        /// 시작 화면에서 고른 나이대를 이 화면의 콤보박스에도 미리 선택해 둡니다.
        /// (예전에는 시작 시 고른 값이 어디에서도 쓰이지 않고 버려졌습니다.)
        /// </summary>
        private void ApplyInitialAgeGroup()
        {
            if (string.IsNullOrWhiteSpace(_initialAgeGroup) || CboAgeGroup == null) return;

            string wanted = _initialAgeGroup switch
            {
                "3-5" => "3~5세",
                "6-11" => "6~11세",
                "12-18" => "12~18세",
                "adult" => "19세 이상",
                _ => string.Empty
            };

            if (wanted.Length == 0) return;

            foreach (var obj in CboAgeGroup.Items)
            {
                if (obj is ComboBoxItem item && (item.Content?.ToString() ?? "").Contains(wanted))
                {
                    CboAgeGroup.SelectedItem = item;
                    return;
                }
            }
        }

        #region [ 탭 전환 로직 ]

        private static readonly Brush ActiveTabBackground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
        private static readonly Brush ActiveTabForeground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CB0"));
        private static readonly Brush InactiveTabForeground =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));

        private void BtnTabAuto_Click(object sender, RoutedEventArgs e)
        {
            PanelAutoSetup.Visibility = Visibility.Visible;
            PanelDirectInput.Visibility = Visibility.Collapsed;

            BtnTabAuto.Background = ActiveTabBackground;
            BtnTabAuto.Foreground = ActiveTabForeground;

            BtnTabDirect.Background = Brushes.Transparent;
            BtnTabDirect.Foreground = InactiveTabForeground;
        }

        private void BtnTabDirect_Click(object sender, RoutedEventArgs e)
        {
            PanelAutoSetup.Visibility = Visibility.Collapsed;
            PanelDirectInput.Visibility = Visibility.Visible;

            BtnTabDirect.Background = ActiveTabBackground;
            BtnTabDirect.Foreground = ActiveTabForeground;

            BtnTabAuto.Background = Brushes.Transparent;
            BtnTabAuto.Foreground = InactiveTabForeground;
        }

        #endregion

        #region [ 연령대 및 영양소 힌트 설정 ]

        private void CboAgeGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboAgeGroup?.SelectedItem is not ComboBoxItem selectedItem) return;

            string ageGroup = selectedItem.Content?.ToString() ?? "";

            if (ageGroup.Contains("3~5세"))
                UpdateNutrientHints("50g ~ 65g", "12g ~ 18g", "8g ~ 12g", "350kcal ~ 450kcal");
            else if (ageGroup.Contains("6~11세"))
                UpdateNutrientHints("80g ~ 100g", "20g ~ 28g", "12g ~ 18g", "550kcal ~ 680kcal");
            else if (ageGroup.Contains("12~18세"))
                UpdateNutrientHints("110g ~ 135g", "30g ~ 42g", "18g ~ 26g", "750kcal ~ 950kcal");
            else if (ageGroup.Contains("19세 이상"))
                UpdateNutrientHints("90g ~ 115g", "25g ~ 35g", "15g ~ 22g", "650kcal ~ 800kcal");
        }

        private void UpdateNutrientHints(string carb, string protein, string fat, string calories)
        {
            if (TxtCarbHint != null) TxtCarbHint.Text = $"(추천: {carb})";
            if (TxtProteinHint != null) TxtProteinHint.Text = $"(추천: {protein})";
            if (TxtFatHint != null) TxtFatHint.Text = $"(추천: {fat})";
            if (TxtCaloriesHint != null) TxtCaloriesHint.Text = $"(추천: {calories})";
        }

        #endregion

        #region [ DB 메뉴 로드 및 직접 입력 ComboBox 바인딩 ]

        /// <summary>
        /// MenuDatabase 시트를 읽어 메뉴 목록을 캐시합니다.
        ///
        /// 열 구성 (E열부터는 있으면 쓰고 없으면 건너뜁니다)
        ///   A 메뉴명 | B 칼로리 | C 재료 | D 알러지 | E 분류 | F 탄수화물 | G 단백질 | H 지방
        /// </summary>
        private async Task LoadMenuDatabaseAsync()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                string readRange = $"'{AppConfig.MenuSheetName}'!A2:H";
                var values = await _sheetsService.GetValuesAsync(readRange);

                var list = new List<IngredientModel>();
                foreach (var row in values)
                {
                    string name = row.Count > 0 ? (row[0]?.ToString() ?? "").Trim() : "";
                    if (name.Length == 0) continue;

                    list.Add(new IngredientModel
                    {
                        Name = name,
                        Calories = ParseNumber(Cell(row, 1)),
                        Materials = Cell(row, 2),
                        Allergy = Cell(row, 3),
                        Category = Cell(row, 4),
                        Carb = ParseNumber(Cell(row, 5)),
                        Protein = ParseNumber(Cell(row, 6)),
                        Fat = ParseNumber(Cell(row, 7))
                    });
                }

                cachedDbIngredients = list;

                // 알러지 명단에서 제외 대상 성분을 모읍니다.
                _registeredAllergens = await _sheetsService.GetRegisteredAllergensAsync();

                _databaseLoaded = true;
                PopulateDirectInputComboBoxes();
            }
            catch (Exception ex)
            {
                // 예전에는 catch {} 로 조용히 넘어가서, 메뉴가 하나도 없는데도
                // 사용자는 이유를 알 수 없었습니다.
                MessageBox.Show(
                    $"'{AppConfig.MenuSheetName}' 시트를 불러오지 못했습니다.\n\n{ex.Message}",
                    "메뉴 DB 로드 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        private static string Cell(IList<object> row, int index) =>
            row.Count > index ? (row[index]?.ToString() ?? "").Trim() : "";

        /// <summary>"320 kcal", "12.5g" 같은 문자열에서 숫자만 뽑습니다.</summary>
        private static double ParseNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;

            string cleaned = Regex.Replace(raw, @"[^0-9.]", "");
            if (cleaned.Length == 0) return 0;

            // "1.2.3" 처럼 점이 여러 개면 첫 번째 것만 남깁니다.
            int firstDot = cleaned.IndexOf('.');
            if (firstDot >= 0)
            {
                cleaned = cleaned.Substring(0, firstDot + 1) +
                          cleaned.Substring(firstDot + 1).Replace(".", "");
            }

            return double.TryParse(cleaned, out double value) ? value : 0;
        }

        /// <summary>
        /// 메뉴가 어느 자리에 들어가는지 판정합니다.
        /// 시트 E열(분류)이 있으면 그것을 믿고, 없을 때만 이름으로 추정합니다.
        /// </summary>
        private static bool Matches(IngredientModel item, Slot slot)
        {
            string category = item.Category;

            if (!string.IsNullOrWhiteSpace(category))
            {
                return slot switch
                {
                    Slot.Rice => category.Contains("밥") || category.Contains("주식") || category.Contains("곡"),
                    Slot.Soup => category.Contains("국") || category.Contains("탕") || category.Contains("찌개"),
                    Slot.Meat => category.Contains("주찬") || category.Contains("육") || category.Contains("main"),
                    Slot.Vegetable => category.Contains("부찬") || category.Contains("나물") ||
                                      category.Contains("채소") || category.Contains("김치"),
                    _ => false
                };
            }

            // ── 분류 열이 비었을 때의 추정 (정확도가 낮으므로 E열 입력을 권장) ──
            string name = item.Name;

            return slot switch
            {
                // "국수"가 국으로 분류되던 문제를 막기 위해 국수·냉면류는 밥(주식)으로 봅니다.
                Slot.Rice => name.Contains("밥") || name.Contains("쌀") ||
                             name.Contains("국수") || name.Contains("면") || name.Contains("죽"),

                Slot.Soup => (name.Contains("국") || name.Contains("탕") || name.Contains("찌개")) &&
                             !name.Contains("국수"),

                Slot.Meat => name.Contains("고기") || name.Contains("닭") || name.Contains("돈") ||
                             name.Contains("제육") || name.Contains("가슴살") || name.Contains("생선") ||
                             name.Contains("갈비") || name.Contains("돈가스") || name.Contains("함박"),

                Slot.Vegetable => name.Contains("나물") || name.Contains("무침") ||
                                  name.Contains("샐러드") || name.Contains("김치") || name.Contains("쌈"),

                _ => false
            };
        }

        /// <summary>
        /// 등록된 알러지 성분이 들어간 메뉴인지 검사합니다.
        /// 알러지 열과 재료 열을 모두 봅니다.
        /// </summary>
        private bool IsAllergySafe(IngredientModel item)
        {
            if (_registeredAllergens.Count == 0) return true;

            string haystack = item.Allergy + "," + item.Materials;
            if (string.IsNullOrWhiteSpace(haystack)) return true;

            foreach (string allergen in _registeredAllergens)
            {
                if (allergen.Length == 0) continue;
                if (haystack.Contains(allergen, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        /// <summary>알러지 필터를 통과한, 해당 자리에 놓을 수 있는 메뉴들.</summary>
        private List<IngredientModel> CandidatesFor(Slot slot) =>
            cachedDbIngredients.Where(i => Matches(i, slot) && IsAllergySafe(i)).ToList();

        private void PopulateDirectInputComboBoxes()
        {
            FillCombo(CboFixedRice, Slot.Rice);
            FillCombo(CboFixedSoup, Slot.Soup);
            FillCombo(CboFixedMeat, Slot.Meat);
            FillCombo(CboFixedVeg, Slot.Vegetable);
        }

        private void FillCombo(ComboBox combo, Slot slot)
        {
            if (combo == null) return;

            var names = CandidatesFor(slot).Select(i => i.Name).Distinct().ToList();
            names.Insert(0, "-- 자동 추천 선택 --");

            combo.ItemsSource = names;
            combo.SelectedIndex = 0;
        }

        private void BtnClearDirect_Click(object sender, RoutedEventArgs e)
        {
            if (CboFixedRice != null) CboFixedRice.SelectedIndex = 0;
            if (CboFixedSoup != null) CboFixedSoup.SelectedIndex = 0;
            if (CboFixedMeat != null) CboFixedMeat.SelectedIndex = 0;
            if (CboFixedVeg != null) CboFixedVeg.SelectedIndex = 0;
        }

        private void BtnApplyDirect_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("고정 메뉴 설정이 저장되었습니다.\n식단 자동 조합 탭에서 실행을 진행해주세요.",
                            "설정 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            BtnTabAuto_Click(this, new RoutedEventArgs());
        }

        #endregion

        #region [ 버튼 이벤트 및 조합 실행 ]

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadTarget(TxtCarb, "탄수화물 적정량", out double carb)) return;
            if (!TryReadTarget(TxtProtein, "단백질 적정량", out double protein)) return;
            if (!TryReadTarget(TxtFat, "지방 적정량", out double fat)) return;
            if (!TryReadTarget(TxtCalories, "총 칼로리 권장량", out double calories)) return;

            await BuildMenuAsync(carb, protein, fat, calories);
        }

        private static bool TryReadTarget(TextBox box, string label, out double value)
        {
            if (!double.TryParse(box.Text, out value) || value < 0)
            {
                MessageBox.Show($"{label}을(를) 숫자로 입력해주세요.", "입력 오류",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                box.Focus();
                return false;
            }
            return true;
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtCarb.Text = string.Empty;
            TxtProtein.Text = string.Empty;
            TxtFat.Text = string.Empty;
            TxtCalories.Text = string.Empty;

            if (CboAgeGroup != null) CboAgeGroup.SelectedIndex = 0;
            BtnClearDirect_Click(this, new RoutedEventArgs());

            ResultSection.Visibility = Visibility.Collapsed;
            GridDietResult.ItemsSource = null;
        }

        /// <summary>
        /// 목표 영양소에 가장 가까운 식단 3개를 만듭니다.
        ///
        /// 무작위로 여러 조합을 만들어 보고 목표와의 편차가 가장 작은 것을 고르는
        /// 방식입니다. 한 끼 안에서 같은 메뉴가 겹치지 않고, 자극적인 메뉴는
        /// 2개를 넘지 않으며, 추천 3개가 서로 달라지도록 제약을 겁니다.
        /// </summary>
        private async Task BuildMenuAsync(double targetCarb, double targetProtein,
                                          double targetFat, double targetCalories)
        {
            try
            {
                if (!_databaseLoaded) await LoadMenuDatabaseAsync();

                var riceItems = CandidatesFor(Slot.Rice);
                var soupItems = CandidatesFor(Slot.Soup);
                var meatItems = CandidatesFor(Slot.Meat);
                var vegItems = CandidatesFor(Slot.Vegetable);

                if (riceItems.Count == 0 || soupItems.Count == 0 ||
                    meatItems.Count == 0 || vegItems.Count == 0)
                {
                    MessageBox.Show(
                        BuildShortageMessage(riceItems.Count, soupItems.Count, meatItems.Count, vegItems.Count),
                        "조합할 메뉴가 부족합니다", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 직접 입력 탭에서 고정한 메뉴
                IngredientModel? fixedRice = FindFixed(CboFixedRice);
                IngredientModel? fixedSoup = FindFixed(CboFixedSoup);
                IngredientModel? fixedMeat = FindFixed(CboFixedMeat);
                IngredientModel? fixedVeg = FindFixed(CboFixedVeg);

                var recommendations = new List<DietResultModel>();
                var usedCombinations = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 1; i <= 3; i++)
                {
                    DietResultModel? best = null;
                    double bestScore = double.MaxValue;

                    // 무작위 탐색 200회 중 목표와 가장 가까운 조합 채택
                    for (int trial = 0; trial < 200; trial++)
                    {
                        var rice = fixedRice ?? PickRandom(riceItems);
                        var meat = fixedMeat ?? PickRandom(meatItems);
                        var soup = fixedSoup ?? PickRandom(soupItems);
                        var veg = fixedVeg ?? PickRandom(vegItems);

                        // 한 끼 안에서 같은 메뉴가 두 자리를 차지하지 않도록
                        var names = new HashSet<string>(StringComparer.Ordinal)
                            { rice.Name, soup.Name, meat.Name, veg.Name };
                        if (names.Count < 4) continue;

                        // 자극적인 메뉴는 한 끼에 최대 2개까지
                        int strongCount =
                            (rice.IsStrongTaste ? 1 : 0) + (soup.IsStrongTaste ? 1 : 0) +
                            (meat.IsStrongTaste ? 1 : 0) + (veg.IsStrongTaste ? 1 : 0);
                        if (strongCount > 2) continue;

                        var candidate = Compose($"추천 식단 {i}", rice, soup, meat, veg);

                        // 앞선 추천과 완전히 같은 조합은 제외
                        if (usedCombinations.Contains(candidate.CompositionKey)) continue;

                        double score = Score(candidate, targetCarb, targetProtein, targetFat, targetCalories);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = candidate;
                        }
                    }

                    if (best == null) break; // 더 만들 수 있는 서로 다른 조합이 없음

                    usedCombinations.Add(best.CompositionKey);
                    recommendations.Add(best);
                }

                if (recommendations.Count == 0)
                {
                    MessageBox.Show(
                        "조건을 만족하는 조합을 찾지 못했습니다.\n" +
                        "고정 메뉴를 줄이거나 메뉴 DB에 항목을 더 추가해 주세요.",
                        "결과 없음", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                ResultSection.Visibility = Visibility.Visible;
                GridDietResult.ItemsSource = recommendations;

                WarnIfNutrientDataMissing();

                await SaveDietToSheetsAsync(recommendations[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show("식단 추천 중 오류가 발생했습니다:\n" + ex.Message,
                                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string BuildShortageMessage(int rice, int soup, int meat, int veg)
        {
            var missing = new List<string>();
            if (rice == 0) missing.Add("밥(주식)");
            if (soup == 0) missing.Add("국");
            if (meat == 0) missing.Add("주찬(육류·생선)");
            if (veg == 0) missing.Add("부찬(나물·채소)");

            string text =
                $"다음 자리에 넣을 메뉴가 없습니다: {string.Join(", ", missing)}\n\n" +
                $"'{AppConfig.MenuSheetName}' 시트에 메뉴를 추가하거나, E열(분류)에 " +
                "밥 / 국 / 주찬 / 부찬 을 입력해 주세요.";

            if (_registeredAllergens.Count > 0)
            {
                text += "\n\n참고: 등록된 알러지 성분(" +
                        string.Join(", ", _registeredAllergens.Take(5)) +
                        (_registeredAllergens.Count > 5 ? " 외" : "") +
                        ")이 포함된 메뉴는 후보에서 제외됩니다.";
            }

            return text;
        }

        private IngredientModel? FindFixed(ComboBox combo)
        {
            if (combo == null || combo.SelectedIndex <= 0) return null;

            string name = combo.SelectedItem?.ToString() ?? "";
            return cachedDbIngredients.FirstOrDefault(x => x.Name == name);
        }

        /// <summary>
        /// .NET 8의 Random.Shared는 스레드 안전한 공유 인스턴스입니다.
        /// 예전처럼 루프마다 new Random(Guid...)을 만들 필요가 없습니다.
        /// </summary>
        private static IngredientModel PickRandom(List<IngredientModel> items) =>
            items[Random.Shared.Next(items.Count)];

        /// <summary>네 메뉴의 영양성분을 실제로 합산합니다.</summary>
        private static DietResultModel Compose(string name, IngredientModel rice, IngredientModel soup,
                                               IngredientModel meat, IngredientModel veg)
        {
            return new DietResultModel
            {
                DietName = name,
                Rice = rice.Name,
                Soup = soup.Name,
                Meat = meat.Name,
                Vegetable = veg.Name,
                Carb = Math.Round(rice.Carb + soup.Carb + meat.Carb + veg.Carb, 1),
                Protein = Math.Round(rice.Protein + soup.Protein + meat.Protein + veg.Protein, 1),
                Fat = Math.Round(rice.Fat + soup.Fat + meat.Fat + veg.Fat, 1),
                Calories = Math.Round(rice.Calories + soup.Calories + meat.Calories + veg.Calories, 0)
            };
        }

        /// <summary>
        /// 목표치와의 상대 편차 합. 0에 가까울수록 좋습니다.
        /// 목표를 0으로 둔 항목(입력하지 않음)은 점수에서 제외합니다.
        /// </summary>
        private static double Score(DietResultModel diet, double targetCarb, double targetProtein,
                                    double targetFat, double targetCalories)
        {
            double score = 0;
            if (targetCalories > 0) score += Math.Abs(diet.Calories - targetCalories) / targetCalories;
            if (targetCarb > 0) score += Math.Abs(diet.Carb - targetCarb) / targetCarb;
            if (targetProtein > 0) score += Math.Abs(diet.Protein - targetProtein) / targetProtein;
            if (targetFat > 0) score += Math.Abs(diet.Fat - targetFat) / targetFat;
            return score;
        }

        /// <summary>
        /// 시트에 영양성분 열이 비어 있으면 결과의 탄·단·지가 0으로 나옵니다.
        /// 심사 중에 "이 수치는 뭐냐"는 질문을 받지 않도록 미리 알려 줍니다.
        /// </summary>
        private void WarnIfNutrientDataMissing()
        {
            if (cachedDbIngredients.Any(i => i.HasNutrients)) return;

            MessageBox.Show(
                $"'{AppConfig.MenuSheetName}' 시트에 영양성분 열이 비어 있어 탄수화물·단백질·지방이 0으로 표시됩니다.\n\n" +
                "F열(탄수화물), G열(단백질), H열(지방)에 값을 입력하면 실제 합산값이 나옵니다.",
                "영양성분 데이터 없음", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SaveDietToSheetsAsync(DietResultModel targetDiet)
        {
            try
            {
                // 열 구성은 기존 시트와 동일하게 유지합니다(A~F).
                string writeRange = $"'{AppConfig.DietSheetName}'!A:F";

                await _sheetsService.AppendRowAsync(writeRange, new List<object>
                {
                    targetDiet.DietName,
                    targetDiet.Rice,
                    targetDiet.Soup,
                    targetDiet.Meat,
                    targetDiet.Vegetable,
                    targetDiet.Calories.ToString("0") + " kcal"
                });
            }
            catch (Exception ex)
            {
                // 예전에는 catch {} 로 완전히 삼켜서, 저장에 실패해도 사용자는
                // 저장된 줄 알았습니다.
                MessageBox.Show(
                    $"추천 식단을 '{AppConfig.DietSheetName}' 시트에 저장하지 못했습니다.\n\n{ex.Message}",
                    "저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        #endregion
    }
}
