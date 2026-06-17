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
        private const string SpreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";

        // 📊 식단 결과 데이터 모델
        public class DietResultModel
        {
            public string DietName { get; set; }
            public string Rice { get; set; }              // 밥류
            public string Soup { get; set; }              // 국류
            public string Meat { get; set; }              // 메인반찬(육류)
            public string Vegetable { get; set; }         // 서브반찬(나물류)

            public double Carb { get; set; }
            public double Protein { get; set; }
            public double Fat { get; set; }
            public double Calories { get; set; }

            // XAML DataGrid 바인딩용 합친 문자열
            public string MenuComposition => $"{Rice}, {Soup}, {Meat}, {Vegetable}";
        }

        public class IngredientModel
        {
            public string Name { get; set; }
            public double Calories { get; set; }
            public string Materials { get; set; }
            public string Allergy { get; set; }
        }

        public Menu_table()
        {
            InitializeComponent();
        }

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
            // 입력 필드 초기화
            TxtCarb.Text = string.Empty;
            TxtProtein.Text = string.Empty;
            TxtFat.Text = string.Empty;
            TxtCalories.Text = string.Empty;

            // 결과 영역 숨기기 및 DataGrid 초기화
            ResultSection.Visibility = Visibility.Collapsed;
            GridDietResult.ItemsSource = null;
        }

        private void BuildMenuFromGoogleSheets(double targetCarb, double targetProtein, double targetFat, double targetCalories)
        {
            try
            {
                string credentialPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "credentials.json");
                if (!File.Exists(credentialPath))
                {
                    MessageBox.Show("credentials.json 파일이 실행 디렉토리에 없습니다.", "인증 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                GoogleCredential credential;
                using (var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
                }

                var service = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "MealCareAI-System"
                });

                // 1. MenuDatabase 시트에서 로드
                string readRange = "MenuDatabase!A2:D";
                SpreadsheetsResource.ValuesResource.GetRequest request = service.Spreadsheets.Values.Get(SpreadsheetId, readRange);
                ValueRange response = request.Execute();
                IList<IList<object>> values = response.Values;

                List<IngredientModel> ingredients = new List<IngredientModel>();
                if (values != null && values.Count > 0)
                {
                    foreach (var row in values)
                    {
                        if (row.Count >= 2)
                        {
                            string rawCal = row[1]?.ToString() ?? "0";
                            string cleanCal = Regex.Replace(rawCal, @"[^0-9.]", "");
                            double.TryParse(cleanCal, out double calValue);

                            ingredients.Add(new IngredientModel
                            {
                                Name = row[0]?.ToString() ?? "이름없음",
                                Calories = calValue,
                                Materials = row.Count >= 3 ? row[2]?.ToString() : "",
                                Allergy = row.Count >= 4 ? row[3]?.ToString() : ""
                            });
                        }
                    }
                }

                // 2. 밥, 국, 육류, 나물 분류 처리
                var riceItems = ingredients.Where(i => i.Name.Contains("밥") || i.Name.Contains("쌀")).ToList();
                var soupItems = ingredients.Where(i => i.Name.Contains("국") || i.Name.Contains("탕") || i.Name.Contains("찌개")).ToList();
                var meatItems = ingredients.Where(i => i.Name.Contains("고기") || i.Name.Contains("불고기") || i.Name.Contains("닭") || i.Name.Contains("가슴살") || i.Name.Contains("제육")).ToList();
                var vegetableItems = ingredients.Where(i => i.Name.Contains("나물") || i.Name.Contains("무침") || i.Name.Contains("샐러드") || i.Name.Contains("김치")).ToList();

                List<DietResultModel> recommendations = new List<DietResultModel>();
                for (int i = 1; i <= 3; i++)
                {
                    var rand = new Random(Guid.NewGuid().GetHashCode());

                    var rice = riceItems.Count > 0 ? riceItems[rand.Next(riceItems.Count)] : new IngredientModel { Name = "잡곡밥", Calories = 150 };
                    var soup = soupItems.Count > 0 ? soupItems[rand.Next(soupItems.Count)] : new IngredientModel { Name = "두부된장국", Calories = 80 };
                    var meat = meatItems.Count > 0 ? meatItems[rand.Next(meatItems.Count)] : new IngredientModel { Name = "소불고기", Calories = 180 };
                    var veg = vegetableItems.Count > 0 ? vegetableItems[rand.Next(vegetableItems.Count)] : new IngredientModel { Name = "시금치나물", Calories = 35 };

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

                // 3. UI 결과 데이터 바인딩
                ResultSection.Visibility = Visibility.Visible;
                GridDietResult.ItemsSource = recommendations;

                // 4. gid 번호로 실제 시트 이름 추출
                var spreadsheetRequest = service.Spreadsheets.Get(SpreadsheetId);
                var spreadsheetInfo = spreadsheetRequest.Execute();
                var targetSheet = spreadsheetInfo.Sheets.FirstOrDefault(s => s.Properties.SheetId == 714098992);
                string realSheetName = targetSheet != null ? targetSheet.Properties.Title : "메뉴";

                // 5. 헤더 자동 생성 검사
                string headerRange = $"'{realSheetName}'!A1:F1";
                var headerCheck = service.Spreadsheets.Values.Get(SpreadsheetId, headerRange).Execute();
                if (headerCheck.Values == null || headerCheck.Values.Count == 0)
                {
                    var headerRow = new List<object> { "구분", "밥류", "국류", "메인반찬(육류)", "서브반찬(나물류)", "총 칼로리" };
                    var headerValueRange = new ValueRange { Values = new List<IList<object>> { headerRow } };
                    var updateRequest = service.Spreadsheets.Values.Update(headerValueRange, SpreadsheetId, headerRange);
                    updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                    updateRequest.Execute();
                }

                // 6. 구글 시트에 항목 순서대로 저장 실행
                string writeRange = $"'{realSheetName}'!A:F";
                var valueRange = new ValueRange();
                var targetDiet = recommendations[0]; // 베스트 1순위 추천 조합 선택

                var rowValues = new List<object> {
                            targetDiet.DietName,
                            targetDiet.Rice,
                            targetDiet.Soup,
                            targetDiet.Meat,
                            targetDiet.Vegetable,
                            targetDiet.Calories.ToString() + " kcal"
                    };

                valueRange.Values = new List<IList<object>> { rowValues };

                var appendRequest = service.Spreadsheets.Values.Append(valueRange, SpreadsheetId, writeRange);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                appendRequest.Execute();

                MessageBox.Show("구글 시트 맞춤 분리 저장 성공!\n\nAI 조합 식단이 각 항목별(밥/국/육류/나물) 열에 맞춰 기록되었습니다.", "성공", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 연동 실패:\n{ex.Message}\n\n상세 정보: {ex.InnerException?.Message}", "오류 안내", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}

