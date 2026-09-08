using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Microsoft.VisualBasic.FileIO;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace wpf
{
    public partial class Ingredients : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        private string spreadsheetId => _sheetsService.SpreadsheetId;

        private bool _isInitialized = false;

        public Ingredients()
        {
            InitializeComponent();

            // 로그인 때 만든 공용 서비스를 사용합니다.
            // (예전에는 클릭할 때마다 credentials.json을 다시 열어 SheetsService를 새로 만들었습니다.)
            _sheetsService = AppServices.Require();

            // 💡 SSL/TLS 연결 보안 프로토콜 강제 활성화 (일시적 튕김 차단 에러 방지용)
            System.Net.ServicePointManager.SecurityProtocol =
                System.Net.SecurityProtocolType.Tls12 |
                System.Net.SecurityProtocolType.Tls13;

            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Loaded += Ingredients_Loaded;
        }

        private void Ingredients_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSheetNamesToComboBox();
            _isInitialized = true;
            LoadDataFromGoogleSheet(isSilent: true);
        }

        private void LoadSheetNamesToComboBox()
        {
            try
            {
                var service = GetSheetsService();
                var spreadsheetRequest = service.Spreadsheets.Get(spreadsheetId);
                var spreadsheet = spreadsheetRequest.Execute();

                cmbSheets.Items.Clear();
                if (spreadsheet.Sheets != null && spreadsheet.Sheets.Count > 0)
                {
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        cmbSheets.Items.Add(sheet.Properties.Title);
                    }
                    cmbSheets.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 스프레드시트의 시트 목록을 불러오지 못했습니다.\n오류 내용: " + ex.Message, "초기화 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void cmbSheets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;
            txtFilterKeyword.Text = string.Empty;
            LoadDataFromGoogleSheet(isSilent: true);
        }

        private string GetTargetSheetName()
        {
            if (cmbSheets.SelectedItem == null) return "Sheet1";
            return cmbSheets.SelectedItem.ToString();
        }

        /// <summary>
        /// ➕ [새로 추가] 구글 스프레드시트에 실시간 새 시트(탭)를 개설하는 함수
        /// </summary>
        private void btnCreateSheet_Click(object sender, RoutedEventArgs e)
        {
            string newSheetName = txtNewSheetName.Text.Trim();

            if (string.IsNullOrEmpty(newSheetName))
            {
                MessageBox.Show("추가할 새 시트(탭) 이름을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 프로그램 내부 드롭다운 목록에 이름 중복 여부 체크
            if (cmbSheets.Items.Contains(newSheetName))
            {
                MessageBox.Show("이미 존재하는 시트 이름입니다. 다른 이름을 입력해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var service = GetSheetsService();

                // 구글 스프레드시트 구조 요청(BatchUpdate) 패킷 조립
                var batchUpdateRequest = new BatchUpdateSpreadsheetRequest
                {
                    Requests = new List<Request>
                    {
                        new Request
                        {
                            AddSheet = new AddSheetRequest
                            {
                                Properties = new SheetProperties
                                {
                                    Title = newSheetName
                                }
                            }
                        }
                    }
                };

                // 구글 클라우드에 명령 전송 및 실행
                var batchRequest = service.Spreadsheets.BatchUpdate(batchUpdateRequest, spreadsheetId);
                batchRequest.Execute();

                MessageBox.Show($"구글 스프레드시트에 [{newSheetName}] 시트가 정상 추가되었습니다!", "시트 추가 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                txtNewSheetName.Text = string.Empty; // 입력창 클리어

                // 🔄 콤보박스 목록 재갱신 및 신규 생성 시트로 초점 이동 제어
                _isInitialized = false;
                LoadSheetNamesToComboBox();
                _isInitialized = true;

                cmbSheets.SelectedItem = newSheetName; // 드롭다운을 방금 만든 탭으로 강제 변경
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 생성 작업 중 예외 에러:\n" + ex.Message, "작업 실패", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
                LoadCsvToDataGrid(openFileDialog.FileName);
            }
        }

        private void LoadCsvToDataGrid(string filePath)
        {
            try
            {
                if (!File.Exists(filePath)) return;

                DataTable dataTable = new DataTable();
                using (TextFieldParser parser = new TextFieldParser(filePath, Encoding.GetEncoding("euc-kr")))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    parser.HasFieldsEnclosedInQuotes = true;

                    if (!parser.EndOfData)
                    {
                        string[] headers = parser.ReadFields();
                        if (headers != null)
                        {
                            foreach (string header in headers) dataTable.Columns.Add(header.Trim());
                        }
                    }

                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        if (fields != null && fields.Length == dataTable.Columns.Count)
                        {
                            dataTable.Rows.Add(fields);
                        }
                    }
                }
                dataGridIngredients.ItemsSource = dataTable.DefaultView;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"CSV 미리보기 로드 중 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnUpload_Click(object sender, RoutedEventArgs e)
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

            UploadCsvToGoogleSheet(txtFilePath.Text);
        }

        private void UploadCsvToGoogleSheet(string filePath)
        {
            try
            {
                var service = GetSheetsService();
                string userSheetName = GetTargetSheetName();
                var valueRange = new ValueRange { Values = new List<IList<object>>() };

                using (TextFieldParser parser = new TextFieldParser(filePath, Encoding.GetEncoding("euc-kr")))
                {
                    parser.TextFieldType = FieldType.Delimited;
                    parser.SetDelimiters(",");
                    parser.HasFieldsEnclosedInQuotes = true;

                    while (!parser.EndOfData)
                    {
                        string[] fields = parser.ReadFields();
                        if (fields != null) valueRange.Values.Add(fields.Cast<object>().ToList());
                    }
                }

                string targetRange = $"{userSheetName}!A1";
                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, targetRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                updateRequest.Execute();

                MessageBox.Show($"구글 스프레드시트의 [{userSheetName}] 탭에 성공적으로 업로드되었습니다!", "업로드 성공", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadDataFromGoogleSheet(isSilent: true);
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 업로드 실패 에러:\n" + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnAddRow_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridIngredients.ItemsSource is DataView dataView)
            {
                DataRow newRow = dataView.Table.NewRow();
                dataView.Table.Rows.Add(newRow);

                dataGridIngredients.Focus();
                dataGridIngredients.ScrollIntoView(newRow);
            }
            else if (dataGridIngredients.ItemsSource is DataTable dt)
            {
                DataRow newRow = dt.NewRow();
                dt.Rows.Add(newRow);

                dataGridIngredients.Focus();
                dataGridIngredients.ScrollIntoView(newRow);
            }
            else
            {
                MessageBox.Show("현재 추가할 표의 구조(헤더)가 존재하지 않습니다.\n구글 시트를 먼저 조회하거나 CSV 파일을 선택해 주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                else
                {
                    int selectedIndex = dataGridIngredients.SelectedIndex;
                    if (selectedIndex >= 0)
                    {
                        if (dataGridIngredients.ItemsSource is DataView dv)
                        {
                            dv.Delete(selectedIndex);
                        }
                        else if (dataGridIngredients.ItemsSource is DataTable table)
                        {
                            table.Rows.RemoveAt(selectedIndex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"행 삭제 작업 중 오류가 발생했습니다:\n{ex.Message}", "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSheets.SelectedItem == null)
            {
                MessageBox.Show("저장할 대상 구글 시트 탭을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dataGridIngredients.ItemsSource is DataView dataView)
            {
                try
                {
                    var service = GetSheetsService();
                    string userSheetName = GetTargetSheetName();
                    var valueRange = new ValueRange { Values = new List<IList<object>>() };

                    DataTable dt = dataView.Table;

                    List<object> headers = new List<object>();
                    foreach (DataColumn column in dt.Columns)
                    {
                        headers.Add(column.ColumnName);
                    }
                    valueRange.Values.Add(headers);

                    foreach (DataRowView rowView in dataView)
                    {
                        List<object> rowData = new List<object>();
                        foreach (DataColumn column in dt.Columns)
                        {
                            rowData.Add(rowView[column.ColumnName] ?? "");
                        }
                        valueRange.Values.Add(rowData);
                    }

                    var clearRequest = service.Spreadsheets.Values.Clear(new ClearValuesRequest(), spreadsheetId, $"{userSheetName}!A1:Z2000");
                    clearRequest.Execute();

                    string targetRange = $"{userSheetName}!A1";
                    var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, targetRange);
                    updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                    updateRequest.Execute();

                    MessageBox.Show($"현재 화면의 편집 내용(수정/삭제/추가)이 구글 [{userSheetName}] 시트에 정상 저장되었습니다.", "저장 성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    LoadDataFromGoogleSheet(isSilent: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("구글 시트 저장 중 에러가 발생했습니다:\n" + ex.Message, "저장 에러", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void LoadDataFromGoogleSheet(bool isSilent = false)
        {
            try
            {
                var service = GetSheetsService();
                string userSheetName = GetTargetSheetName();
                string targetRange = $"{userSheetName}!A1:Z2000";

                var request = service.Spreadsheets.Values.Get(spreadsheetId, targetRange);
                var response = request.Execute();
                var values = response.Values;

                DataTable dt = new DataTable();
                if (values != null && values.Count > 0)
                {
                    foreach (var header in values[0]) dt.Columns.Add(header.ToString());

                    for (int i = 1; i < values.Count; i++)
                    {
                        var row = values[i].ToList();
                        while (row.Count < dt.Columns.Count) row.Add("");
                        dt.Rows.Add(row.ToArray());
                    }

                    dataGridIngredients.ItemsSource = dt.DefaultView;
                }
                else
                {
                    dataGridIngredients.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 불러오기 실패:\n" + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void txtFilterKeyword_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (dataGridIngredients.ItemsSource is DataView dataView)
                {
                    string keyword = txtFilterKeyword.Text.Trim().Replace("'", "''");

                    if (string.IsNullOrEmpty(keyword))
                    {
                        dataView.RowFilter = string.Empty;
                        return;
                    }

                    List<string> filterExpressions = new List<string>();
                    foreach (DataColumn column in dataView.Table.Columns)
                    {
                        filterExpressions.Add($"Convert([{column.ColumnName}], 'System.String') LIKE '%{keyword}%'");
                    }

                    dataView.RowFilter = string.Join(" OR ", filterExpressions);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"필터 적용 중 오류: {ex.Message}");
            }
        }

        /// <summary>
        /// 앱 전체가 공유하는 SheetsService를 돌려줍니다.
        /// 호출부는 그대로 두고 내부만 교체했습니다.
        /// </summary>
        private SheetsService GetSheetsService() => _sheetsService.Sheets;
    }
}