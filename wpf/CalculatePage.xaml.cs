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
    /// <summary>
    /// 원가 / 소요량 계산 화면.
    ///
    /// 예전에는 식재료와 단가가 코드에 직접 박혀 있었고(불고기정식 6종 등),
    /// "Excel 내보내기" 버튼은 저장했다는 안내창만 띄우고 실제로는 아무 파일도
    /// 만들지 않았습니다. 이제
    ///  - 단가 시트에서 실제 데이터를 읽고,
    ///  - 시트가 없을 때만 예시 데이터를 쓰되 화면에 "데모 데이터"라고 밝히며,
    ///  - 내보내기는 실제 CSV 파일을 저장합니다.
    /// </summary>
    public partial class CalculatePage : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        /// <summary>코스명 → 식재료 목록.</summary>
        private readonly Dictionary<string, List<CostRow>> _costTable =
            new Dictionary<string, List<CostRow>>(StringComparer.Ordinal);

        /// <summary>시트를 못 읽어 예시 데이터를 쓰고 있는지 여부.</summary>
        private bool _usingDemoData;

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
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var values = await _sheetsService.GetValuesAsync($"'{AppConfig.CostSheetName}'!A2:F");

                _costTable.Clear();

                foreach (var row in values)
                {
                    string course = Cell(row, 0);
                    string name = Cell(row, 1);
                    if (course.Length == 0 || name.Length == 0) continue;

                    if (!_costTable.TryGetValue(course, out var list))
                    {
                        list = new List<CostRow>();
                        _costTable[course] = list;
                    }

                    list.Add(new CostRow(
                        name,
                        Cell(row, 2),
                        ParseNumber(Cell(row, 3)),
                        Cell(row, 4),
                        ParseNumber(Cell(row, 5))));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[원가 시트 로드 실패] {ex.Message}");
                _costTable.Clear();
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }

            if (_costTable.Count == 0)
            {
                _usingDemoData = true;
                LoadDemoCostTable();
            }

            PopulateCourseCombo();
        }

        /// <summary>
        /// 시트가 준비되지 않았을 때 쓰는 예시 데이터.
        /// 화면 목록에 "(데모 데이터)"라고 표시되므로 실제 값과 혼동되지 않습니다.
        /// </summary>
        private void LoadDemoCostTable()
        {
            _costTable["[A코스] 불고기정식 + 두부된장국 + 수리취떡"] = new List<CostRow>
            {
                new CostRow("우육 (불고기용)", "육류", 120, "g", 28),
                new CostRow("두부", "두류", 0.2, "모", 1200),
                new CostRow("국산 쌀", "곡류", 90, "g", 3.5),
                new CostRow("재래된장", "장류", 15, "g", 12),
                new CostRow("수리취 가루", "기타", 10, "g", 25),
                new CostRow("양파 및 야채류", "채소류", 50, "g", 6),
            };

            _costTable["[B코스] 닭갈비덮밥 + 계란파국 + 백김치"] = new List<CostRow>
            {
                new CostRow("계육 (닭다리살)", "육류", 150, "g", 14),
                new CostRow("양배추", "채소류", 80, "g", 4),
                new CostRow("국산 쌀", "곡류", 100, "g", 3.5),
                new CostRow("신선란", "알류", 1, "개", 250),
                new CostRow("대파", "채소류", 15, "g", 8),
            };

            _costTable["[특식] 단오 맞춤 절기식단 (앵두화채 포함)"] = new List<CostRow>
            {
                new CostRow("돈육 (갈비용)", "육류", 180, "g", 18),
                new CostRow("찹쌀", "곡류", 90, "g", 5),
                new CostRow("앵두 (화채용)", "과일류", 30, "g", 30),
                new CostRow("수리취 가루", "기타", 15, "g", 25),
            };
        }

        private void PopulateCourseCombo()
        {
            // XAML에 미리 넣어 둔 항목을 비워야 ItemsSource를 설정할 수 있습니다.
            cbTargetMenu.Items.Clear();

            string suffix = _usingDemoData ? "  (데모 데이터)" : "";
            cbTargetMenu.ItemsSource = _costTable.Keys.Select(k => k + suffix).ToList();

            if (cbTargetMenu.Items.Count > 0) cbTargetMenu.SelectedIndex = 0;
        }

        private List<CostRow> GetSelectedCourseRows()
        {
            string selected = cbTargetMenu.SelectedItem?.ToString() ?? "";
            if (_usingDemoData) selected = selected.Replace("  (데모 데이터)", "");

            return _costTable.TryGetValue(selected, out var rows) ? rows : new List<CostRow>();
        }

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
                .Select(r => new IngredientItem(r.Name, r.Category, r.UnitSize, r.Unit, r.UnitPrice, headCount))
                .ToList();

            double costPerPerson = ingredients.Sum(item => item.UnitSize * item.UnitPrice);

            int finalCostPerPerson = (int)Math.Round(costPerPerson);
            long totalCost = (long)finalCostPerPerson * headCount;

            txtTotalCount.Text = $"{ingredients.Count} 종";
            txtCostPerPerson.Text = $"₩ {finalCostPerPerson:N0}";
            txtTotalCost.Text = $"₩ {totalCost:N0}";

            _lastResult = ingredients;
            dgCalculationResult.ItemsSource = null;
            dgCalculationResult.ItemsSource = ingredients;

            if (finalCostPerPerson > budgetPerPerson)
            {
                MessageBox.Show(
                    $"⚠️ 예산 초과 경고!\n현재 식단의 인당 원가({finalCostPerPerson:N0}원)가 " +
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
                MessageBox.Show("먼저 [실시간 원가 산출]을 실행해 주세요.", "안내",
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

                if (_usingDemoData)
                {
                    sb.AppendLine();
                    sb.AppendLine("※ 이 표는 데모 데이터를 기준으로 계산되었습니다.");
                }

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

        private static double ParseNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;

            string cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.').ToArray());
            return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : 0;
        }
    }

    /// <summary>원가 시트의 한 행.</summary>
    public class CostRow
    {
        public string Name { get; }
        public string Category { get; }
        public double UnitSize { get; }
        public string Unit { get; }
        public double UnitPrice { get; }

        public CostRow(string name, string category, double unitSize, string unit, double unitPrice)
        {
            Name = name;
            Category = category;
            UnitSize = unitSize;
            Unit = unit;
            UnitPrice = unitPrice;
        }
    }

    /// <summary>표에 표시할 계산 결과 한 줄.</summary>
    public class IngredientItem
    {
        public string Name { get; set; }
        public string Category { get; set; }
        public double UnitSize { get; set; }
        public string Unit { get; set; }
        public double UnitPrice { get; set; }

        private readonly int _headCount;

        public IngredientItem(string name, string category, double unitSize, string unit, double unitPrice, int headCount)
        {
            Name = name;
            Category = category;
            UnitSize = unitSize;
            Unit = unit;
            UnitPrice = unitPrice;
            _headCount = headCount;
        }

        public string UnitPriceDisplay => $"₩ {UnitPrice:N0}";
        public string UnitWeightDisplay => $"{UnitSize} {Unit}";
        public string TotalWeightDisplay => $"{UnitSize * _headCount:N0} {Unit}";
        public string TotalPriceDisplay => $"₩ {(int)Math.Round(UnitSize * UnitPrice * _headCount):N0}";
    }
}
