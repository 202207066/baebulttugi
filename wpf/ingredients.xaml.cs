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
        private string spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";
        private string clientSecretPath = "credentials.json";

        private bool _isInitialized = false;

        public Ingredients()
        {
            InitializeComponent();
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

        // 📁 CSV 파일 선택
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

        // 📤 [원상복구] 순수 CSV 파일을 구글 시트에 그대로 덮어쓰는 기존 로직
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

        /// <summary>
        /// 💾 [신규 기능] 현재 데이터 그리드 표에서 직관적으로 수정/삭제/추가한 상태를 구글 시트에 업데이트합니다.
        /// </summary>
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

                    // 1. 헤더(열 이름) 추출 및 삽입
                    List<object> headers = new List<object>();
                    foreach (DataColumn column in dt.Columns)
                    {
                        headers.Add(column.ColumnName);
                    }
                    valueRange.Values.Add(headers);

                    // 2. 화면 상에 수정/추가되어 남아있는 전체 데이터 행 추출
                    foreach (DataRowView rowView in dataView)
                    {
                        List<object> rowData = new List<object>();
                        foreach (DataColumn column in dt.Columns)
                        {
                            rowData.Add(rowView[column.ColumnName] ?? "");
                        }
                        valueRange.Values.Add(rowData);
                    }

                    // 3. 기존 시트 영역 클리어 (삭제된 데이터 잔상 방지)
                    var clearRequest = service.Spreadsheets.Values.Clear(new ClearValuesRequest(), spreadsheetId, $"{userSheetName}!A1:Z2000");
                    clearRequest.Execute();

                    // 4. 새 데이터 세트로 최종 저장
                    string targetRange = $"{userSheetName}!A1";
                    var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, targetRange);
                    updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                    updateRequest.Execute();

                    MessageBox.Show($"현재 화면의 편집 내용(수정/삭제/추가)이 구글 [{userSheetName}] 시트에 정상 저장되었습니다.", "저장 성공", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 그리드 리프레시
                    LoadDataFromGoogleSheet(isSilent: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("구글 시트 저장 중 에러가 발생했습니다:\n" + ex.Message, "저장 에러", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("저장할 대상 데이터가 표에 로드되어 있지 않습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    if (!isSilent) MessageBox.Show($"구글 클라우드 [{userSheetName}] 탭 데이터 동기화 완료", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private SheetsService GetSheetsService()
        {
            GoogleCredential credential;
            using (var stream = new FileStream(clientSecretPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(new[] { SheetsService.Scope.Spreadsheets });
            }
            return new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GoogleSheetCSVApp",
            });
        }
    }
}