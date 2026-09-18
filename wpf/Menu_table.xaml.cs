using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 메뉴는 식품영양학과에서 정리한 연령 트랙별 메뉴풀에서 가져옵니다.
    /// (트랙A 만 3~5세 / 트랙B 만 6~18세) 화면에서 연령대를 바꾸면 해당 트랙의
    /// 메뉴만 후보가 됩니다. 메뉴풀 시트가 없는 DB에서는 기본 MenuDatabase
    /// 형식으로 자동 전환되므로, 새로 만든 빈 DB에서도 그대로 동작합니다.
    ///
    /// 조합 규칙
    ///  · 등록된 알러지 성분이 든 메뉴는 후보에서 제외
    ///  · 한 끼 안에서 같은 메뉴가 두 자리를 차지하지 않음
    ///  · 자극적인(맵거나 짠) 메뉴는 한 끼에 2개까지
    ///  · 무작위 탐색 중 목표 영양소와 편차가 가장 작은 조합을 채택
    /// </summary>
    public partial class Menu_table : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        /// <summary>시작 시 선택한 나이대("3-5", "6-11", "12-18", "adult").</summary>
        private readonly string _initialAgeGroup;

        /// <summary>현재 트랙의 메뉴풀.</summary>
        private List<MenuItem> _menuPool = new List<MenuItem>();

        /// <summary>알러지 명단 시트에서 모은 제외 대상 성분.</summary>
        private HashSet<string> _registeredAllergens =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private GoogleSheetsService.AgeTrack _currentTrack = GoogleSheetsService.AgeTrack.A3to5;
        private bool _poolLoaded;
        private bool _loading;

        // ── 결과 모델 ───────────────────────────────────────────────────

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
            ApplyInitialAgeGroup();
            await ReloadPoolAsync();
        }

        /// <summary>시작 화면에서 고른 나이대를 콤보박스에 미리 선택해 둡니다.</summary>
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

        // ── 메뉴풀 로드 ─────────────────────────────────────────────────

        /// <summary>선택된 연령대에 해당하는 트랙.</summary>
        private GoogleSheetsService.AgeTrack TrackForSelectedAge()
        {
            string age = (CboAgeGroup?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";

            // 만 3~5세만 트랙A, 나머지 연령은 트랙B가 대상입니다.
            return age.Contains("3~5세")
                ? GoogleSheetsService.AgeTrack.A3to5
                : GoogleSheetsService.AgeTrack.B6to18;
        }

        private async Task ReloadPoolAsync()
        {
            if (_loading) return;
            _loading = true;
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                _currentTrack = TrackForSelectedAge();

                _menuPool = await _sheetsService.GetMenuPoolAsync(_currentTrack);
                _registeredAllergens = await _sheetsService.GetRegisteredAllergensAsync();

                _poolLoaded = true;
                PopulateDirectInputComboBoxes();

                if (_menuPool.Count == 0)
                {
                    MessageBox.Show(
                        "이 연령대에 해당하는 메뉴가 없습니다.\n\n" + PoolSourceText(),
                        "메뉴풀 비어 있음", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("메뉴풀을 불러오지 못했습니다.\n\n" + ex.Message,
                                "로드 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                _loading = false;
                Mouse.OverrideCursor = null;
            }
        }

        // ── 후보 필터 ───────────────────────────────────────────────────

        /// <summary>등록된 알러지 성분이 들어간 메뉴인지 검사합니다.</summary>
        private bool IsAllergySafe(MenuItem item)
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

        private List<MenuItem> CandidatesFor(MenuSlot slot) =>
            _menuPool.Where(i => i.Slot == slot && IsAllergySafe(i)).ToList();

        // ── 탭 전환 ─────────────────────────────────────────────────────

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

        // ── 연령대 ──────────────────────────────────────────────────────

        private async void CboAgeGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
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

            // 연령대가 바뀌면 해당 트랙의 메뉴풀로 갈아끼웁니다.
            if (_poolLoaded && TrackForSelectedAge() != _currentTrack)
            {
                await ReloadPoolAsync();
            }
        }

        private void UpdateNutrientHints(string carb, string protein, string fat, string calories)
        {
            if (TxtCarbHint != null) TxtCarbHint.Text = $"(추천: {carb})";
            if (TxtProteinHint != null) TxtProteinHint.Text = $"(추천: {protein})";
            if (TxtFatHint != null) TxtFatHint.Text = $"(추천: {fat})";
            if (TxtCaloriesHint != null) TxtCaloriesHint.Text = $"(추천: {calories})";
        }

        // ── 고정 메뉴 콤보박스 ──────────────────────────────────────────

        private void PopulateDirectInputComboBoxes()
        {
            FillCombo(CboFixedRice, MenuSlot.Rice);
            FillCombo(CboFixedSoup, MenuSlot.Soup);
            FillCombo(CboFixedMeat, MenuSlot.Main);
            FillCombo(CboFixedVeg, MenuSlot.Side);
        }

        private void FillCombo(ComboBox combo, MenuSlot slot)
        {
            if (combo == null) return;

            var names = CandidatesFor(slot)
                .Select(i => i.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)
                .ToList();

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

        // ── 실행 ────────────────────────────────────────────────────────

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

        private async Task BuildMenuAsync(double targetCarb, double targetProtein,
                                          double targetFat, double targetCalories)
        {
            try
            {
                if (!_poolLoaded) await ReloadPoolAsync();

                var riceItems = CandidatesFor(MenuSlot.Rice);
                var soupItems = CandidatesFor(MenuSlot.Soup);
                var mainItems = CandidatesFor(MenuSlot.Main);
                var sideItems = CandidatesFor(MenuSlot.Side);

                if (riceItems.Count == 0 || soupItems.Count == 0 ||
                    mainItems.Count == 0 || sideItems.Count == 0)
                {
                    MessageBox.Show(
                        BuildShortageMessage(riceItems.Count, soupItems.Count, mainItems.Count, sideItems.Count),
                        "조합할 메뉴가 부족합니다", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                MenuItem? fixedRice = FindFixed(CboFixedRice);
                MenuItem? fixedSoup = FindFixed(CboFixedSoup);
                MenuItem? fixedMain = FindFixed(CboFixedMeat);
                MenuItem? fixedSide = FindFixed(CboFixedVeg);

                var recommendations = new List<DietResultModel>();
                var used = new HashSet<string>(StringComparer.Ordinal);

                for (int i = 1; i <= 3; i++)
                {
                    DietResultModel? best = null;
                    double bestScore = double.MaxValue;

                    for (int trial = 0; trial < 300; trial++)
                    {
                        var rice = fixedRice ?? PickRandom(riceItems);
                        var soup = fixedSoup ?? PickRandom(soupItems);
                        var main = fixedMain ?? PickRandom(mainItems);
                        var side = fixedSide ?? PickRandom(sideItems);

                        var names = new HashSet<string>(StringComparer.Ordinal)
                            { rice.Name, soup.Name, main.Name, side.Name };
                        if (names.Count < 4) continue;

                        int strongCount =
                            (rice.IsStrongTaste ? 1 : 0) + (soup.IsStrongTaste ? 1 : 0) +
                            (main.IsStrongTaste ? 1 : 0) + (side.IsStrongTaste ? 1 : 0);
                        if (strongCount > 2) continue;

                        var candidate = Compose($"추천 식단 {i}", rice, soup, main, side);
                        if (used.Contains(candidate.CompositionKey)) continue;

                        double score = Score(candidate, targetCarb, targetProtein, targetFat, targetCalories);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            best = candidate;
                        }
                    }

                    if (best == null) break;

                    used.Add(best.CompositionKey);
                    recommendations.Add(best);
                }

                if (recommendations.Count == 0)
                {
                    MessageBox.Show(
                        "조건을 만족하는 조합을 찾지 못했습니다.\n" +
                        "고정 메뉴를 줄이거나 메뉴풀을 확인해 주세요.",
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

        private string BuildShortageMessage(int rice, int soup, int main, int side)
        {
            var missing = new List<string>();
            if (rice == 0) missing.Add("밥류");
            if (soup == 0) missing.Add("국·찌개류");
            if (main == 0) missing.Add("주찬류");
            if (side == 0) missing.Add("부찬류");

            string text =
                $"다음 자리에 넣을 메뉴가 없습니다: {string.Join(", ", missing)}\n\n" +
                $"현재 메뉴풀: {_menuPool.Count}개 " +
                $"({(_currentTrack == GoogleSheetsService.AgeTrack.A3to5 ? "트랙A 3~5세" : "트랙B 6~18세")})\n" +
                PoolSourceText();

            if (_registeredAllergens.Count > 0)
            {
                text += "\n\n등록된 알러지 성분(" +
                        string.Join(", ", _registeredAllergens.Take(6)) +
                        (_registeredAllergens.Count > 6 ? " 외" : "") +
                        ")이 포함된 메뉴는 후보에서 제외됩니다.";
            }

            return text;
        }

        /// <summary>메뉴풀을 어느 시트에서 읽었는지 한 줄로 보여 줍니다.</summary>
        private string PoolSourceText()
        {
            var sources = _sheetsService.LastPoolSources;

            if (sources.Count == 0)
                return "읽어 온 시트가 없습니다. «데이터베이스 설정»에서 연결된 스프레드시트를 확인해 주세요.";

            return "읽어 온 시트\n· " + string.Join("\n· ", sources);
        }

        private MenuItem? FindFixed(ComboBox combo)
        {
            if (combo == null || combo.SelectedIndex <= 0) return null;

            string name = combo.SelectedItem?.ToString() ?? "";
            return _menuPool.FirstOrDefault(x => x.Name == name);
        }

        private static MenuItem PickRandom(List<MenuItem> items) =>
            items[Random.Shared.Next(items.Count)];

        private static DietResultModel Compose(string name, MenuItem rice, MenuItem soup,
                                               MenuItem main, MenuItem side)
        {
            return new DietResultModel
            {
                DietName = name,
                Rice = rice.Name,
                Soup = soup.Name,
                Meat = main.Name,
                Vegetable = side.Name,
                Carb = Math.Round(rice.Carb + soup.Carb + main.Carb + side.Carb, 1),
                Protein = Math.Round(rice.Protein + soup.Protein + main.Protein + side.Protein, 1),
                Fat = Math.Round(rice.Fat + soup.Fat + main.Fat + side.Fat, 1),
                Calories = Math.Round(rice.Calories + soup.Calories + main.Calories + side.Calories, 0)
            };
        }

        /// <summary>목표치와의 상대 편차 합. 0에 가까울수록 좋습니다.</summary>
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

        private bool _nutrientWarningShown;

        /// <summary>
        /// 메뉴풀에 영양성분이 아직 없으면(레시피 매칭 진행 중) 한 번만 알려 줍니다.
        /// </summary>
        private void WarnIfNutrientDataMissing()
        {
            if (_nutrientWarningShown) return;
            if (_menuPool.Any(i => i.HasNutrients)) return;

            _nutrientWarningShown = true;

            MessageBox.Show(
                "메뉴풀에 영양성분 정보가 아직 없어 탄수화물·단백질·지방이 0으로 표시됩니다.\n\n" +
                "레시피 매칭이 끝나 각 메뉴의 열량·영양소가 채워지면 자동으로 반영됩니다.\n" +
                "지금은 알러지 제외와 메뉴 구성만 기준으로 조합합니다.",
                "영양성분 매칭 진행 중", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async Task SaveDietToSheetsAsync(DietResultModel targetDiet)
        {
            try
            {
                string dietSheet = await _sheetsService.EnsureDietSheetAsync();

                await _sheetsService.AppendRowAsync($"'{dietSheet}'!A:F", new List<object>
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
                MessageBox.Show(
                    $"추천 식단을 저장하지 못했습니다.\n\n{ex.Message}",
                    "저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
