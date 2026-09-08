using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

namespace wpf
{
    /// <summary>
    /// 식재료 원천 DB 화면. 구글 시트의 임의 탭을 표로 열어 편집하고 저장합니다.
    ///
    /// 예전에는 모든 구글 호출이 동기 Execute()였고, 클릭할 때마다 인증 파일을
    /// 다시 읽어 SheetsService를 새로 만들었습니다. 네트워크가 느리면 그때마다
    /// 화면이 얼어붙었습니다. 지금은 공용 서비스를 비동기로 호출합니다.
    /// </summary>
    public partial class Ingredients : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        private bool _isInitialized;

        public Ingredients()
        {
            InitializeComponent();

            // 로그인 때 만든 공용 서비스를 사용합니다.
            _sheetsService = AppServices.Require();

            // CSV가 EUC-KR로 저장되는 경우가 많아 인코딩 공급자를 등록합니다.
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            Loaded += Ingredients_Loaded;
        }

        private async void Ingredients_Loaded(object sender, RoutedEventArgs e)
        {
            await LoadSheetNamesToComboBoxAsync();
            _isInitialized = true;
            await LoadDataFromGoogleSheetAsync();
        }

        // ── 시트(탭) 목록 ───────────────────────────────────────────────

        private async Task LoadSheetNamesToComboBoxAsync()
        {
            try
            {
                var titles = await _sheetsService.GetSheetTitlesAsync();

                cmbSheets.Items.Clear();
                foreach (var title in titles) cmbSheets.Items.Add(title);

                if (cmbSheets.Items.Count > 0) cmbSheets.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 스프레드시트의 시트 목록을 불러오지 못했습니다.\n오류 내용: " + ex.Message,
                                "초기화 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void cmbSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            txtFilterKeyword.Text = string.Empty;
            await LoadDataFromGoogleSheetAsync();
        }

        private string GetTargetSheetName() => cmbSheets.SelectedItem?.ToString() ?? "Sheet1";

        /// <summary>시트 이름에 공백·특수문자가 있어도 안전하도록 따옴표를 씌웁니다.</summary>
        private static string Quote(string sheetName)
        {
            // 시트 이름 안의 작은따옴표는 두 번 써서 이스케이프합니다.
            string escaped = sheetName.Replace("'", "''");
            return "'" + escaped + "'";
        }

        /// <summary>
        /// 구글 스프레드시트에 새 시트(탭)를 만듭니다.
        /// </summary>
        private async void btnCreateSheet_Click(object sender, RoutedEventArgs e)
        {
            string newSheetName = txtNewSheetName.Text.Trim();

            if (string.IsNullOrEmpty(newSheetName))
            {
                MessageBox.Show("추가할 새 시트(탭) 이름을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbSheets.Items.Contains(newSheetName))
            {
                MessageBox.Show("이미 존재하는 시트 이름입니다. 다른 이름을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                await _sheetsService.AddSheetAsync(newSheetName);

                MessageBox.Show($"구글 스프레드시트에 [{newSheetName}] 시트가 정상 추가되었습니다!",
                                "시트 추가 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                txtNewSheetName.Text = string.Empty;

                // 목록을 다시 읽는 동안에는 SelectionChanged가 불필요하게 조회하지 않도록 막습니다.
                _isInitialized = false;
                await LoadSheetNamesToComboBoxAsync();
                _isInitialized = true;

                cmbSheets.SelectedItem = newSheetName;
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 생성 작업 중 오류:\n" + ex.Message,
                                "작업 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── CSV 불러오기 / 업로드 ───────────────────────────────────────

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
                LoadCsvToDataGrid(openFileDialog.FileName);
            }
        }

        /// <summary>
        /// CSV를 읽어 표로 보여 줍니다.
        /// UTF-8(BOM 포함)과 EUC-KR을 모두 다룹니다.
        /// </summary>
        private void LoadCsvToDataGrid(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                var rows = ReadCsv(filePath);
                if (rows.Count == 0)
                {
                    MessageBox.Show("CSV에 내용이 없습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                DataTable dataTable = BuildTable(rows[0]);

                for (int i = 1; i < rows.Count; i++)
                {
                    var fields = rows[i].ToList();
                    while (fields.Count < dataTable.Columns.Count) fields.Add("");
                    if (fields.Count > dataTable.Columns.Count)
                        fields = fields.Take(dataTable.Columns.Count).ToList();

                    dataTable.Rows.Add(fields.Cast<object>().ToArray());
                }

                dataGridIngredients.ItemsSource = dataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 미리보기 로드 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// CSV 파일을 문자열 행 목록으로 읽습니다.
        /// 먼저 UTF-8로 읽어 보고, 한글이 깨진 흔적(U+FFFD)이 있으면 EUC-KR로 다시 읽습니다.
        /// (예전에는 EUC-KR로 고정이라 UTF-8 CSV의 한글이 깨졌습니다.)
        /// </summary>
        private static List<string[]> ReadCsv(string filePath)
        {
            var utf8Rows = ReadCsvWith(filePath, Encoding.UTF8);

            bool looksBroken = utf8Rows.Any(r => r.Any(c => c.Contains('�')));
            if (!looksBroken) return utf8Rows;

            try
            {
                return ReadCsvWith(filePath, Encoding.GetEncoding("euc-kr"));
            }
            catch
            {
                return utf8Rows;
            }
        }

        private static List<string[]> ReadCsvWith(string filePath, Encoding encoding)
        {
            var rows = new List<string[]>();

            using (var parser = new TextFieldParser(filePath, encoding))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;

                while (!parser.EndOfData)
                {
                    string[]? fields = parser.ReadFields();
                    if (fields != null) rows.Add(fields);
                }
            }

            return rows;
        }

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtFilePath.Text) || txtFilePath.Text == "선택된 파일이 없습니다.")
            {
                MessageBox.Show("먼저 업로드할 CSV 파일을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbSheets.SelectedItem == null)
            {
                MessageBox.Show("업로드할 대상 구글 시트 탭을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string sheetName = GetTargetSheetName();

            if (MessageBox.Show(
                    $"[{sheetName}] 탭의 기존 내용을 CSV 내용으로 덮어씁니다.\n계속할까요?",
                    "업로드 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            {
                return;
            }

            await UploadCsvToGoogleSheetAsync(txtFilePath.Text, sheetName);
        }

        private async Task UploadCsvToGoogleSheetAsync(string filePath, string sheetName)
        {
            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                var rows = ReadCsv(filePath);
                var values = rows
                    .Select(r => (IList<object>)r.Cast<object>().ToList())
                    .ToList();

                await WriteTableAsync(sheetName, values);

                MessageBox.Show($"구글 스프레드시트의 [{sheetName}] 탭에 업로드되었습니다.",
                                "업로드 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadDataFromGoogleSheetAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 업로드 실패:\n" + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        // ── 표 편집 ─────────────────────────────────────────────────────

        private void btnAddRow_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridIngredients.ItemsSource is DataView dataView && dataView.Table != null)
            {
                DataTable table = dataView.Table;
                DataRow newRow = table.NewRow();
                table.Rows.Add(newRow);

                dataGridIngredients.Focus();
                dataGridIngredients.ScrollIntoView(newRow);
            }
            else
            {
                MessageBox.Show("현재 추가할 표의 구조(헤더)가 존재하지 않습니다.\n구글 시트를 먼저 조회하거나 CSV 파일을 선택해 주세요.",
                                "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void btnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridIngredients.SelectedItem == null)
            {
                MessageBox.Show("표에서 삭제할 행(줄)을 마우스로 먼저 선택해 주세요.", "삭제 안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (dataGridIngredients.SelectedItem is DataRowView rowView)
                {
                    rowView.Row.Delete();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"행 삭제 작업 중 오류가 발생했습니다:\n{ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSheets.SelectedItem == null)
            {
                MessageBox.Show("저장할 대상 구글 시트 탭을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dataGridIngredients.ItemsSource is not DataView dataView)
            {
                MessageBox.Show("저장할 표가 없습니다. 먼저 시트를 조회하거나 CSV를 불러오세요.",
                                "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string sheetName = GetTargetSheetName();

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                DataTable? dt = dataView.Table;
                if (dt == null)
                {
                    MessageBox.Show("표 구조를 읽을 수 없습니다.", "안내",
                                    MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var values = new List<IList<object>>();

                var headers = new List<object>();
                foreach (DataColumn column in dt.Columns) headers.Add(column.ColumnName);
                values.Add(headers);

                // 화면의 필터가 걸려 있어도 저장은 표 전체를 대상으로 합니다.
                // (예전에는 필터된 행만 저장되어 나머지가 사라질 수 있었습니다.)
                foreach (DataRow row in dt.Rows)
                {
                    if (row.RowState == DataRowState.Deleted) continue;

                    var rowData = new List<object>();
                    foreach (DataColumn column in dt.Columns) rowData.Add(row[column] ?? "");
                    values.Add(rowData);
                }

                await WriteTableAsync(sheetName, values);

                MessageBox.Show($"편집 내용이 구글 [{sheetName}] 시트에 저장되었습니다.",
                                "저장 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadDataFromGoogleSheetAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 저장 중 에러가 발생했습니다:\n" + ex.Message,
                                "저장 에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// 표 전체를 시트에 씁니다.
        ///
        /// 먼저 A1부터 덮어쓰고, 그 아래 남은 예전 행만 지웁니다.
        /// (Clear를 먼저 하면 그 뒤 쓰기가 실패했을 때 시트가 빈 채로 남습니다.)
        /// </summary>
        private async Task WriteTableAsync(string sheetName, List<IList<object>> values)
        {
            string quoted = Quote(sheetName);

            await _sheetsService.UpdateValuesAsync($"{quoted}!A1", values);

            try
            {
                await _sheetsService.ClearValuesAsync($"{quoted}!A{values.Count + 1}:Z2000");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[꼬리 행 정리 실패] {ex.Message}");
            }
        }

        // ── 시트 → 표 ──────────────────────────────────────────────────

        private async Task LoadDataFromGoogleSheetAsync()
        {
            try
            {
                string sheetName = GetTargetSheetName();
                var values = await _sheetsService.GetValuesAsync($"{Quote(sheetName)}!A1:Z2000");

                if (values.Count == 0)
                {
                    dataGridIngredients.ItemsSource = null;
                    return;
                }

                var headerCells = values[0].Select(v => v?.ToString() ?? "").ToArray();
                DataTable dt = BuildTable(headerCells);

                for (int i = 1; i < values.Count; i++)
                {
                    var row = values[i].Select(v => v?.ToString() ?? "").ToList();
                    while (row.Count < dt.Columns.Count) row.Add("");
                    if (row.Count > dt.Columns.Count) row = row.Take(dt.Columns.Count).ToList();

                    dt.Rows.Add(row.Cast<object>().ToArray());
                }

                dataGridIngredients.ItemsSource = dt.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 불러오기 실패:\n" + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 헤더 문자열로 DataTable을 만듭니다.
        ///
        /// 헤더가 비어 있거나 같은 이름이 두 번 나오면 DataTable.Columns.Add가
        /// 예외를 던져 화면 전체가 뜨지 않았습니다. 빈 이름은 "열 N",
        /// 중복 이름은 "이름 (2)" 식으로 바꿔 항상 열리도록 합니다.
        /// </summary>
        private static DataTable BuildTable(IEnumerable<string> headerCells)
        {
            var dt = new DataTable();
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            int index = 0;
            foreach (string raw in headerCells)
            {
                index++;
                string name = (raw ?? "").Trim();
                if (name.Length == 0) name = $"열 {index}";

                string unique = name;
                int suffix = 2;
                while (used.Contains(unique))
                {
                    unique = $"{name} ({suffix})";
                    suffix++;
                }

                used.Add(unique);
                dt.Columns.Add(unique, typeof(string));
            }

            return dt;
        }

        // ── 검색 필터 ───────────────────────────────────────────────────

        private void txtFilterKeyword_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (dataGridIngredients.ItemsSource is not DataView dataView) return;

                string keyword = txtFilterKeyword.Text.Trim();

                if (string.IsNullOrEmpty(keyword))
                {
                    dataView.RowFilter = string.Empty;
                    return;
                }

                string escaped = EscapeForLike(keyword);

                if (dataView.Table == null) return;

                var expressions = dataView.Table.Columns
                    .Cast<DataColumn>()
                    .Select(c => $"Convert([{EscapeColumnName(c.ColumnName)}], 'System.String') LIKE '%{escaped}%'");

                dataView.RowFilter = string.Join(" OR ", expressions);
            }
            catch (Exception ex)
            {
                // 필터 식이 깨져도 화면이 죽지 않도록 필터를 풉니다.
                System.Diagnostics.Debug.WriteLine($"필터 적용 중 오류: {ex.Message}");
                if (dataGridIngredients.ItemsSource is DataView dv) dv.RowFilter = string.Empty;
            }
        }

        /// <summary>
        /// DataView.RowFilter의 LIKE 값에 쓰기 위한 이스케이프.
        /// 작은따옴표뿐 아니라 와일드카드(* %)와 대괄호도 처리해야 합니다.
        /// (예전에는 작은따옴표만 처리해 '[' 하나만 입력해도 필터가 깨졌습니다.)
        /// </summary>
        private static string EscapeForLike(string value)
        {
            var sb = new StringBuilder();
            foreach (char c in value)
            {
                switch (c)
                {
                    case '\'': sb.Append("''"); break;
                    case '[': sb.Append("[[]"); break;
                    case '*': sb.Append("[*]"); break;
                    case '%': sb.Append("[%]"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>열 이름에 대괄호가 있으면 필터 식에서 깨지므로 escape 합니다.</summary>
        private static string EscapeColumnName(string name) =>
            name.Replace("\\", "\\\\").Replace("]", "\\]");
    }
}
