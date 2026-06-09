using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace wpf
{
    public partial class CalculatePage : Page
    {
        public CalculatePage()
        {
            InitializeComponent();
        }

        // ⚡ 실시간 원가 산출 버튼 클릭 이벤트
        private void btnRunCalculate_Click(object sender, RoutedEventArgs e)
        {
            // 1. 입력 데이터 유효성 검사 및 파싱
            if (!int.TryParse(txtHeadCount.Text, out int headCount) || headCount <= 0)
            {
                MessageBox.Show("예상 피급식자 수를 올바른 숫자로 입력해주세요.", "입력 오류");
                return;
            }

            if (!int.TryParse(txtBudgetPerPerson.Text, out int budgetPerPerson) || budgetPerPerson <= 0)
            {
                MessageBox.Show("인당 목표 급식비를 올바른 숫자로 입력해주세요.", "입력 오류");
                return;
            }

            // 2. 선택된 식단 콤보박스에 따라 식재료 가짜 가상 데이터베이스 생성
            List<IngredientItem> ingredients = new List<IngredientItem>();
            int selectedMenuIndex = cbTargetMenu.SelectedIndex;

            if (selectedMenuIndex == 0) // 불고기정식 코스
            {
                ingredients.Add(new IngredientItem("우육 (불고기용)", "육류", 120, "g", 28, headCount));
                ingredients.Add(new IngredientItem("두부", "대두구분", 0.2, "모", 1200, headCount));
                ingredients.Add(new IngredientItem("국산 쌀", "곡류", 90, "g", 3.5, headCount));
                ingredients.Add(new IngredientItem("재래된장", "장류", 15, "g", 12, headCount));
                ingredients.Add(new IngredientItem("수리취 가루", "기타", 10, "g", 25, headCount));
                ingredients.Add(new IngredientItem("양파 및 야채류", "채소류", 50, "g", 6, headCount));
            }
            else if (selectedMenuIndex == 1) // 닭갈비덮밥 코스
            {
                ingredients.Add(new IngredientItem("계육 (닭다리살)", "육류", 150, "g", 14, headCount));
                ingredients.Add(new IngredientItem("양배추", "채소류", 80, "g", 4, headCount));
                ingredients.Add(new IngredientItem("국산 쌀", "곡류", 100, "g", 3.5, headCount));
                ingredients.Add(new IngredientItem("신선란", "알류", 1, "개", 250, headCount));
                ingredients.Add(new IngredientItem("대파", "채소류", 15, "g", 8, headCount));
            }
            else // 특식 단오 맞춤 절기식단
            {
                ingredients.Add(new IngredientItem("돈육 (갈비용)", "육류", 180, "g", 18, headCount));
                ingredients.Add(new IngredientItem("찹쌀", "곡류", 90, "g", 5, headCount));
                ingredients.Add(new IngredientItem("앵두 (화채용)", "과일류", 30, "g", 30, headCount));
                ingredients.Add(new IngredientItem("수리취 가루", "기타", 15, "g", 25, headCount));
            }

            // 3. 총합 및 대시보드 카드 연산
            double costPerPerson = 0;
            foreach (var item in ingredients)
            {
                costPerPerson += (item.UnitSize * item.UnitPrice);
            }

            int finalCostPerPerson = (int)Math.Round(costPerPerson);
            long totalCost = (long)finalCostPerPerson * headCount;

            // 4. 대시보드 UI 카드에 연산 결과 매핑
            txtTotalCount.Text = $"{ingredients.Count} 종";
            txtCostPerPerson.Text = $"₩ {finalCostPerPerson:N0}";
            txtTotalCost.Text = $"₩ {totalCost:N0}";

            // 5. 데이터 그리드 바인딩
            dgCalculationResult.ItemsSource = null;
            dgCalculationResult.ItemsSource = ingredients;

            // 💡 스마트 알레르기/예산 케어 핵심 로직: 설정한 목표 급식비를 초과하면 알림창 띄우기
            if (finalCostPerPerson > budgetPerPerson)
            {
                MessageBox.Show($"⚠️ 예산 초과 경고!\n현재 식단의 인당 원가({finalCostPerPerson:N0}원)가 설정하신 목표 급식비({budgetPerPerson:N0}원)를 초과했습니다. 식재료 조절이 필요합니다.", "예산 경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // 📥 엑셀 내보내기 가짜 연동 버튼
        private void btnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("현재 화면의 명세표를 Excel 파일로 변환하여 다운로드 폴더에 저장했습니다!", "Excel 내보내기 완려");
        }
    }

    // 📦 정밀 원가 산출용 데이터 모델 클래스
    public class IngredientItem
    {
        public string Name { get; set; }           // 식재료명
        public string Category { get; set; }       // 분류
        public double UnitSize { get; set; }        // 1인당 소요량 숫자
        public string Unit { get; set; }            // 단위 (g, 모, 개)
        public double UnitPrice { get; set; }       // 시장 단가

        private int _headCount;

        public IngredientItem(string name, string category, double unitSize, string unit, double unitPrice, int headCount)
        {
            Name = name;
            Category = category;
            UnitSize = unitSize;
            Unit = unit;
            UnitPrice = unitPrice;
            _headCount = headCount;
        }

        // DataGrid 화면에 예쁘게 포맷팅해서 보여주기 위한 읽기전용 속성들
        public string UnitPriceDisplay => $"₩ {UnitPrice:N0}";
        public string UnitWeightDisplay => $"{UnitSize} {Unit}";
        public string TotalWeightDisplay => $"{UnitSize * _headCount:N0} {Unit}";
        public string TotalPriceDisplay => $"₩ {(int)Math.Round(UnitSize * UnitPrice * _headCount):N0}";
    }
}