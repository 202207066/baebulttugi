using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace wpf
{
    public partial class CalculatePage : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        /// <summary>코스명 → 식재료 목록.</summary>
        private readonly Dictionary<string, List<CostRow>> _costTable =
            new Dictionary<string, List<CostRow>>(StringComparer.Ordinal);

        /// <summary>시트를 못 읽어 예시 데이터를 쓰고 있는지 여부.</summary>


        private List<IngredientItem> _lastResult = new List<IngredientItem>();

        public CalculatePage()
        {
            InitializeComponent();

            _sheetsService = AppServices.Require();

            Loaded += CalculatePage_Loaded;
        }

        private async void CalculatePage_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadCostTableAsync();
        }

        // ── 단가 데이터 ────────────────────────────────────────────────

        /// <summary>
        /// 원가 시트를 읽습니다.
        ///   A 식단명 | B 식재료명 | C 분류 | D 1인당 소요량 | E 단위 | F 단가
        /// </summary>
        private async Task LoadCostTableAsync()
        {
            using var activity = AppActivity.Begin("식재료 소요량과 단가를 불러오는 중입니다…");
            btnRunCalculate.IsEnabled = false;
            _costTable.Clear();
            try
            {
                var titles = await _sheetsService.GetSheetTitlesAsync();
                if (!titles.Contains(AppConfig.CostSheetName))
                {
                    TxtDataStatus.Text = "아직 원가 자료가 작성되지 않았습니다. 내 구글 시트의 원가 탭에 식재료별 소요량과 단가를 입력해 주세요.";
                    return;
                }
                var values = await _sheetsService.GetValuesAsync($"'{AppConfig.CostSheetName}'!A2:H");
                int line = 1;
                foreach (var row in values)
                {
                    line++;
                    if (row.All(c => string.IsNullOrWhiteSpace(c?.ToString()))) continue;
                    string course = Cell(row, 0), name = Cell(row, 1);
                    if (course.Length == 0 || name.Length == 0) throw new InvalidOperationException($"원가 {line}행: 식단명과 식재료명이 필요합니다.");
                    var item = new CostRow(name, Cell(row, 2), CostCalculator.Number(Cell(row, 3), $"{line}행 소요량"), Cell(row, 4),
                        CostCalculator.Number(Cell(row, 5), $"{line}행 단가"),
                        Cell(row, 6).Length == 0 ? 1 : CostCalculator.Number(Cell(row, 6), $"{line}행 단가 기준량", true), Cell(row, 7));
                    CostCalculator.CostPerPerson(item);
                    if (!_costTable.TryGetValue(course, out var list)) _costTable[course] = list = new();
                    list.Add(item);
                }
                TxtDataStatus.Text = _costTable.Count == 0 ? "아직 원가 자료가 작성되지 않았습니다. 내 구글 시트에 자료를 입력해 주세요."
                    : "등록된 1인분 소요량 × 인원으로 계산합니다. 단가는 기준량·단위를 환산하며, 구매 포장 단위 반올림은 포함하지 않습니다.";
                cbTargetMenu.ItemsSource = _costTable.Keys.ToList();
                if (_costTable.Count > 0) cbTargetMenu.SelectedIndex = 0;
                btnRunCalculate.IsEnabled = _costTable.Count > 0;
            }
            catch (Exception ex)
            {
                _costTable.Clear();
                cbTargetMenu.ItemsSource = null;
                TxtDataStatus.Text = "원가 자료를 확인하지 못했습니다. " + ex.Message;
            }
        }

        private List<CostRow> GetSelectedCourseRows() =>
            _costTable.TryGetValue(cbTargetMenu.SelectedItem?.ToString() ?? "", out var rows) ? rows : new();

        // ── 계산 ────────────────────────────────────────────────────────

        private void btnRunCalculate_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtHeadCount.Text, out int headCount) || headCount <= 0)
            {
                MessageBox.Show("예상 피급식자 수를 올바른 숫자로 입력해주세요.", "입력 오류");
                txtHeadCount.Focus();
                return;
            }

            if (!int.TryParse(txtBudgetPerPerson.Text, out int budgetPerPerson) || budgetPerPerson <= 0)
            {
                MessageBox.Show("인당 목표 급식비를 올바른 숫자로 입력해주세요.", "입력 오류");
                txtBudgetPerPerson.Focus();
                return;
            }

            var rows = GetSelectedCourseRows();
            if (rows.Count == 0)
            {
                MessageBox.Show(
                    $"선택한 식단의 식재료 정보가 없습니다.\n" +
                    $"'{AppConfig.CostSheetName}' 시트에 식재료와 단가를 입력해 주세요.",
                    "데이터 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var ingredients = rows
                .Select(r => new IngredientItem(r, headCount))
                .ToList();

            decimal costPerPerson = ingredients.Sum(item => item.PerPersonCost);

            decimal finalCostPerPerson = costPerPerson;
            decimal totalCost = ingredients.Sum(item => item.TotalCost);

            txtTotalCount.Text = $"{ingredients.Count} 종";
            txtCostPerPerson.Text = $"₩ {finalCostPerPerson:N2}";
            txtTotalCost.Text = $"₩ {totalCost:N2}";

            _lastResult = ingredients;
            dgCalculationResult.ItemsSource = null;
            dgCalculationResult.ItemsSource = ingredients;

            if (finalCostPerPerson > budgetPerPerson)
            {
                MessageBox.Show(
                    $"⚠️ 예산 초과 경고!\n현재 식단의 인당 원가({finalCostPerPerson:N2}원)가 " +
                    $"목표 급식비({budgetPerPerson:N0}원)를 초과했습니다. 식재료 조절이 필요합니다.",
                    "예산 경고", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // ── 내보내기 ────────────────────────────────────────────────────

        /// <summary>
        /// 명세표를 CSV로 저장합니다.
        ///
        /// 예전에는 저장했다는 안내창만 띄우고 파일을 만들지 않았습니다.
        /// Excel에서 한글이 깨지지 않도록 UTF-8 BOM으로 씁니다.
        /// </summary>
        private void btnExportExcel_Click(object sender, RoutedEventArgs e)
        {
            if (_lastResult.Count == 0)
            {
                MessageBox.Show("먼저 [원가 계산하기]을 실행해 주세요.", "안내",
                                MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv",
                FileName = $"원가명세표_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dialog.ShowDialog() != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("식재료명,분류,1인당 소요량,총 소요량,시장 단가,총 소요금액");

                foreach (var item in _lastResult)
                {
                    sb.AppendLine(string.Join(",",
                        Csv(item.Name),
                        Csv(item.Category),
                        Csv(item.UnitWeightDisplay),
                        Csv(item.TotalWeightDisplay),
                        Csv(item.UnitPriceDisplay),
                        Csv(item.TotalPriceDisplay)));
                }

                sb.AppendLine();
                sb.AppendLine($"인당 원가,{Csv(txtCostPerPerson.Text)}");
                sb.AppendLine($"총 소요 예산,{Csv(txtTotalCost.Text)}");

                // UTF-8 BOM: 엑셀이 한글을 올바로 열도록
                File.WriteAllText(dialog.FileName, sb.ToString(), new UTF8Encoding(true));

                MessageBox.Show($"명세표를 저장했습니다.\n\n{dialog.FileName}",
                                "내보내기 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("파일을 저장하지 못했습니다:\n" + ex.Message,
                                "내보내기 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>CSV 한 칸을 안전하게 감쌉니다(쉼표·따옴표·줄바꿈 대응).</summary>
        private static string Csv(string value)
        {
            string text = value ?? "";
            if (text.Contains(',') || text.Contains('"') || text.Contains('\n'))
            {
                return "\"" + text.Replace("\"", "\"\"") + "\"";
            }
            return text;
        }

        // ── 헬퍼 ────────────────────────────────────────────────────────

        private static string Cell(IList<object> row, int index) =>
            row.Count > index ? (row[index]?.ToString() ?? "").Trim() : "";

    }
}
