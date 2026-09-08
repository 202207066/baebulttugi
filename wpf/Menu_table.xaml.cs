using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace wpf
{
    public partial class Menu_table : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        /// <summary>시작 시 선택한 나이대("3-5", "6-11", "12-18", "adult").</summary>
        private readonly string _initialAgeGroup;

        private List<IngredientModel> cachedDbIngredients = new List<IngredientModel>();

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
        }

        public class IngredientModel
        {
            public string Name { get; set; } = string.Empty;
            public double Calories { get; set; }
            public string Materials { get; set; } = string.Empty;
            public string Allergy { get; set; } = string.Empty;

            // 1. 매운 맛 속성
            public bool IsSpicy => Name.Contains("제육") || Name.Contains("김치") || Name.Contains("청양") ||
                                   Name.Contains("매운") || Name.Contains("떡볶이") || Name.Contains("카레") ||
                                   Name.Contains("짬뽕") || (Name.Contains("무침") && Name.Contains("고추"));

            // 2. 짠맛 / 강한 양념 속성
            public bool IsSalty => Name.Contains("장조림") || Name.Contains("젓갈") || Name.Contains("조림") ||
                                   Name.Contains("찌개") || Name.Contains("자반") || Name.Contains("굴비") ||
                                   Name.Contains("스팸") || Name.Contains("소세지") || Name.Contains("피클");

            // 3. 자극적이거나 중식/튀김류 (기름진 맛)
            public bool IsStrongTaste => IsSpicy || IsSalty || Name.Contains("튀김") || Name.Contains("탕수육") || Name.Contains("돈가스");

            // 4. 담백한/순한 맛 여부
            public bool IsMild => !IsStrongTaste;
        }

        public Menu_table(string ageGroup = "")
        {
            InitializeComponent();

            _sheetsService = AppServices.Require();
            _initialAgeGroup = ageGroup ?? string.Empty;

            LoadMenuDatabase();
            ApplyInitialAgeGroup();
        }

        /// <summary>
        /// 시작 화면에서 고른 나이대를 이 화면의 콤보박스에도 미리 선택해 둡니다.
        /// (예전에는 시작 시 고른 값이 어디에서도 쓰이지 않고 버려졌습니다.)
        /// </summary>
        private void ApplyInitialAgeGroup()
        {
            if (string.IsNullOrWhiteSpace(_initialAgeGroup) || CboAgeGroup == null) return;

            // AgeSelectionWindow의 Tag 값 ↔ 이 화면 콤보박스 항목 텍스트 대응
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
        private void BtnTabAuto_Click(object sender, RoutedEventArgs e)
        {
            PanelAutoSetup.Visibility = Visibility.Visible;
            PanelDirectInput.Visibility = Visibility.Collapsed;

            BtnTabAuto.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EBF8FF"));
            BtnTabAuto.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B6CB0"));

            BtnTabDirect.Background = System.Windows.Media.Brushes.Transparent;
            BtnTabDirect.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A5568"));
        }

        private void BtnTabDirect_Click(object sender, RoutedEventArgs e)
        {
            PanelAutoSetup.Visibility = Visibility.Collapsed;
            PanelDirectInput.Visibility = Visibility.Visible;

            BtnTabDirect.Background = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#EBF8FF"));
            BtnTabDirect.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2B6CB0"));

            BtnTabAuto.Background = System.Windows.Media.Brushes.Transparent;
            BtnTabAuto.Foreground = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#4A5568"));
        }
        #endregion

        #region [ 연령대 및 영양소 힌트 설정 ]
        private void CboAgeGroup_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CboAgeGroup?.SelectedItem is ComboBoxItem selectedItem)
            {
                string ageGroup = selectedItem.Content.ToString() ?? "";

                if (ageGroup.Contains("3~5세"))
                {
                    UpdateNutrientHints("50g ~ 65g", "12g ~ 18g", "8g ~ 12g", "350kcal ~ 450kcal");
                }
                else if (ageGroup.Contains("6~11세"))
                {
                    UpdateNutrientHints("80g ~ 100g", "20g ~ 28g", "12g ~ 18g", "550kcal ~ 680kcal");
                }
                else if (ageGroup.Contains("12~18세"))
                {
                    UpdateNutrientHints("110g ~ 135g", "30g ~ 42g", "18g ~ 26g", "750kcal ~ 950kcal");
                }
                else if (ageGroup.Contains("19세 이상"))
                {
                    UpdateNutrientHints("90g ~ 115g", "25g ~ 35g", "15g ~ 22g", "650kcal ~ 800kcal");
                }
            }
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
        private void LoadMenuDatabase()
        {
            try
            {
                // 로그인 때 만든 공용 서비스를 사용합니다(페이지마다 인증하지 않습니다).
                string readRange = $"'{AppConfig.MenuSheetName}'!A2:D";
                var response = _sheetsService.Sheets.Spreadsheets.Values
                    .Get(_sheetsService.SpreadsheetId, readRange).Execute();
                IList<IList<object>> values = response.Values;

                cachedDbIngredients.Clear();
                if (values != null && values.Count > 0)
                {
                    foreach (var row in values)
                    {
                        if (row.Count >= 2)
                        {
                            string rawCal = row[1]?.ToString() ?? "0";
                            string cleanCal = Regex.Replace(rawCal, @"[^0-9.]", "");
                            double.TryParse(cleanCal, out double calValue);

                            cachedDbIngredients.Add(new IngredientModel
                            {
                                Name = row[0]?.ToString() ?? "이름없음",
                                Calories = calValue,
                                Materials = row.Count >= 3 ? row[2]?.ToString() ?? "" : "",
                                Allergy = row.Count >= 4 ? row[3]?.ToString() ?? "" : ""
                            });
                        }
                    }
                }

                PopulateDirectInputComboBoxes();
            }
            catch
            {
                // 로드 실패 시 디폴트 세팅 진행
            }
        }

        private void PopulateDirectInputComboBoxes()
        {
            var riceList = cachedDbIngredients.Where(i => i.Name.Contains("밥") || i.Name.Contains("쌀")).Select(i => i.Name).ToList();
            var soupList = cachedDbIngredients.Where(i => i.Name.Contains("국") || i.Name.Contains("탕") || i.Name.Contains("찌개")).Select(i => i.Name).ToList();
            var meatList = cachedDbIngredients.Where(i => i.Name.Contains("고기") || i.Name.Contains("불고기") || i.Name.Contains("닭") || i.Name.Contains("가슴살") || i.Name.Contains("제육")).Select(i => i.Name).ToList();
            var vegList = cachedDbIngredients.Where(i => i.Name.Contains("나물") || i.Name.Contains("무침") || i.Name.Contains("샐러드") || i.Name.Contains("김치")).Select(i => i.Name).ToList();

            riceList.Insert(0, "-- 자동 추천 선택 --");
            soupList.Insert(0, "-- 자동 추천 선택 --");
            meatList.Insert(0, "-- 자동 추천 선택 --");
            vegList.Insert(0, "-- 자동 추천 선택 --");

            CboFixedRice.ItemsSource = riceList; CboFixedRice.SelectedIndex = 0;
            CboFixedSoup.ItemsSource = soupList; CboFixedSoup.SelectedIndex = 0;
            CboFixedMeat.ItemsSource = meatList; CboFixedMeat.SelectedIndex = 0;
            CboFixedVeg.ItemsSource = vegList; CboFixedVeg.SelectedIndex = 0;
        }

        private void BtnClearDirect_Click(object sender, RoutedEventArgs e)
        {
            CboFixedRice.SelectedIndex = 0;
            CboFixedSoup.SelectedIndex = 0;
            CboFixedMeat.SelectedIndex = 0;
            CboFixedVeg.SelectedIndex = 0;
        }

        private void BtnApplyDirect_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("고정 메뉴 설정이 저장되었습니다.\n식단 자동 조합 탭에서 실행을 진행해주세요.", "설정 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            BtnTabAuto_Click(null, null);
        }
        #endregion

        #region [ 버튼 이벤트 및 맛 밸런스 조합 실행 ]
        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtCarb.Text, out double carb) || carb < 0) { MessageBox.Show("탄수화물 적정량을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning); TxtCarb.Focus(); return; }
            if (!double.TryParse(TxtProtein.Text, out double protein) || protein < 0) { MessageBox.Show("단백질 적정량을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning); TxtProtein.Focus(); return; }
            if (!double.TryParse(TxtFat.Text, out double fat) || fat < 0) { MessageBox.Show("지방 적정량을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning); TxtFat.Focus(); return; }
            if (!double.TryParse(TxtCalories.Text, out double calories) || calories < 0) { MessageBox.Show("총 칼로리 권장량을 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning); TxtCalories.Focus(); return; }

            BuildMenuFromGoogleSheets(carb, protein, fat, calories);
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtCarb.Text = string.Empty;
            TxtProtein.Text = string.Empty;
            TxtFat.Text = string.Empty;
            TxtCalories.Text = string.Empty;

            if (CboAgeGroup != null) CboAgeGroup.SelectedIndex = 0;
            BtnClearDirect_Click(null, null);

            ResultSection.Visibility = Visibility.Collapsed;
            GridDietResult.ItemsSource = null;
        }

        private void BuildMenuFromGoogleSheets(double targetCarb, double targetProtein, double targetFat, double targetCalories)
        {
            try
            {
                if (cachedDbIngredients.Count == 0) LoadMenuDatabase();

                var riceItems = cachedDbIngredients.Where(i => i.Name.Contains("밥") || i.Name.Contains("쌀")).ToList();
                var soupItems = cachedDbIngredients.Where(i => i.Name.Contains("국") || i.Name.Contains("탕") || i.Name.Contains("찌개")).ToList();
                var meatItems = cachedDbIngredients.Where(i => i.Name.Contains("고기") || i.Name.Contains("불고기") || i.Name.Contains("닭") || i.Name.Contains("가슴살") || i.Name.Contains("제육")).ToList();
                var vegetableItems = cachedDbIngredients.Where(i => i.Name.Contains("나물") || i.Name.Contains("무침") || i.Name.Contains("샐러드") || i.Name.Contains("김치")).ToList();

                List<DietResultModel> recommendations = new List<DietResultModel>();

                // 직접 고정한 메뉴 추출
                string selectedRiceName = CboFixedRice.SelectedIndex > 0 ? CboFixedRice.SelectedItem.ToString() : "";
                string selectedSoupName = CboFixedSoup.SelectedIndex > 0 ? CboFixedSoup.SelectedItem.ToString() : "";
                string selectedMeatName = CboFixedMeat.SelectedIndex > 0 ? CboFixedMeat.SelectedItem.ToString() : "";
                string selectedVegName = CboFixedVeg.SelectedIndex > 0 ? CboFixedVeg.SelectedItem.ToString() : "";

                for (int i = 1; i <= 3; i++)
                {
                    var rand = new Random(Guid.NewGuid().GetHashCode());

                    // 1. 고정 선택 메뉴 설정
                    IngredientModel rice = cachedDbIngredients.FirstOrDefault(x => x.Name == selectedRiceName) ??
                        (riceItems.Count > 0 ? riceItems[rand.Next(riceItems.Count)] : new IngredientModel { Name = "잡곡밥", Calories = 150 });

                    IngredientModel soup = cachedDbIngredients.FirstOrDefault(x => x.Name == selectedSoupName);
                    IngredientModel meat = cachedDbIngredients.FirstOrDefault(x => x.Name == selectedMeatName);
                    IngredientModel veg = cachedDbIngredients.FirstOrDefault(x => x.Name == selectedVegName);

                    // 자극적이거나 매운/짠 음식의 개수를 추적
                    int strongTasteCount = (rice.IsStrongTaste ? 1 : 0);

                    // 2. 메인(육류)선택 - 고정이 없을 시 우선 선정
                    if (meat == null)
                    {
                        meat = meatItems.Count > 0 ? meatItems[rand.Next(meatItems.Count)] : new IngredientModel { Name = "소불고기", Calories = 180 };
                    }
                    if (meat.IsStrongTaste) strongTasteCount++;

                    // 3. 국 선택 - 메인/밥이 이미 자극적이면 순한 국(계란국, 맑은 콩나물국 등) 선택
                    if (soup == null)
                    {
                        var candidateSoups = (strongTasteCount >= 1) ? soupItems.Where(x => x.IsMild).ToList() : soupItems;
                        if (candidateSoups.Count == 0) candidateSoups = soupItems;
                        soup = candidateSoups.Count > 0 ? candidateSoups[rand.Next(candidateSoups.Count)] : new IngredientModel { Name = "두부된장국", Calories = 80 };
                    }
                    if (soup.IsStrongTaste) strongTasteCount++;

                    // 4. 서브(채소/나물) 선택 - 이미 자극적인 음식 요소가 1개 이상 존재하면 무조건 순한 나물/샐러드류 선택
                    if (veg == null)
                    {
                        var candidateVegs = (strongTasteCount >= 1) ? vegetableItems.Where(x => x.IsMild).ToList() : vegetableItems;
                        if (candidateVegs.Count == 0) candidateVegs = vegetableItems;
                        veg = candidateVegs.Count > 0 ? candidateVegs[rand.Next(candidateVegs.Count)] : new IngredientModel { Name = "시금치나물", Calories = 35 };
                    }

                    double combinedCalories = rice.Calories + soup.Calories + meat.Calories + veg.Calories;

                    recommendations.Add(new DietResultModel
                    {
                        DietName = $"추천 식단 {i}",
                        Rice = rice.Name,
                        Soup = soup.Name,
                        Meat = meat.Name,
                        Vegetable = veg.Name,
                        Carb = Math.Round(targetCarb * (0.95 + (i * 0.03)), 1),
                        Protein = Math.Round(targetProtein * (0.95 + (i * 0.03)), 1),
                        Fat = Math.Round(targetFat * (0.95 + (i * 0.03)), 1),
                        Calories = Math.Round(combinedCalories, 0)
                    });
                }

                ResultSection.Visibility = Visibility.Visible;
                GridDietResult.ItemsSource = recommendations;

                // 구글 시트 저장 로직
                SaveDietToSheets(recommendations[0]);
            }
            catch (Exception ex)
            {
                MessageBox.Show("식단 추천 및 저장 중 오류가 발생했습니다:\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDietToSheets(DietResultModel targetDiet)
        {
            try
            {
                string writeRange = $"'{AppConfig.DietSheetName}'!A:F";

                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>> {
                        new List<object> {
                            targetDiet.DietName,
                            targetDiet.Rice,
                            targetDiet.Soup,
                            targetDiet.Meat,
                            targetDiet.Vegetable,
                            targetDiet.Calories.ToString() + " kcal"
                        }
                    }
                };

                var appendRequest = _sheetsService.Sheets.Spreadsheets.Values
                    .Append(valueRange, _sheetsService.SpreadsheetId, writeRange);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                appendRequest.Execute();
            }
            catch (Exception ex)
            {
                // 예전에는 catch {} 로 완전히 삼켜서, 저장에 실패해도 사용자는
                // 저장된 줄 알았습니다. 실패를 분명히 알립니다.
                MessageBox.Show(
                    $"추천 식단을 '{AppConfig.DietSheetName}' 시트에 저장하지 못했습니다.\n\n{ex.Message}",
                    "저장 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        #endregion
    }
}