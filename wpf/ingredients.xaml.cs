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
        private string clientSecretPath = @"C:\google\client_secret.json";

        public Ingredients()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 화면이 로드될 때 자동으로 구글 시트의 탭(시트) 목록을 조회하여 콤보박스에 채웁니다.
            Loaded += Ingredients_Loaded;
        }

        private void Ingredients_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSheetNamesToComboBox();
        }

        /// <summary>
        /// [핵심 추가] 구글 스프레드시트의 파일 구조를 파싱하여 존재하는 모든 탭(시트) 이름을 콤보박스에 바인딩합니다.
        /// </summary>
        private void LoadSheetNamesToComboBox()
        {
            try
            {
                var service = GetSheetsService();

                // 스프레드시트의 메타데이터(시트 정보 포함)를 가져옵니다.
                var spreadsheetRequest = service.Spreadsheets.Get(spreadsheetId);
                var spreadsheet = spreadsheetRequest.Execute();

                cmbSheets.Items.Clear();

                if (spreadsheet.Sheets != null && spreadsheet.Sheets.Count > 0)
                {
                    foreach (var sheet in spreadsheet.Sheets)
                    {
                        // 실제 구글 시트에 존재하는 탭 이름을 콤보박스 아이템으로 추가
                        cmbSheets.Items.Add(sheet.Properties.Title);
                    }

                    // 첫 번째 시트를 기본 선택값으로 지정
                    cmbSheets.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 스프레드시트의 시트(탭) 목록을 불러오지 못했습니다.\n네트워크 상태나 client_secret.json 경로를 확인해주세요.\n\n오류 내용: " + ex.Message, "초기화 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// 사용자가 콤보박스에서 선택한 시트 이름을 가져옵니다. 선택이 안 되어있으면 경고를 띄웁니다.
        /// </summary>
        private string GetTargetSheetName()
        {
            if (cmbSheets.SelectedItem == null)
            {
                return "Sheet1"; // 예외 방지용 기본값
            }
            return cmbSheets.SelectedItem.ToString();
        }

        // 📁 CSV 파일 선택 및 데이터 그리드 자동 표시
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

        // 📤 구글 시트에 업로드 버튼 기능 (콤보박스에 선택된 탭으로 업로드)
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
                string userSheetName = GetTargetSheetName(); // 콤보박스에서 선택된 탭 이름
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
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 업로드 실패 에러:\n" + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🌐 구글 시트 데이터 원격 불러오기 기능 (콤보박스에 선택된 탭에서 다운로드)
        private void btnLoadGoogleSheet_Click(object sender, RoutedEventArgs e)
        {
            if (cmbSheets.SelectedItem == null)
            {
                MessageBox.Show("불러올 대상 구글 시트 탭을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadDataFromGoogleSheet();
        }

        private void LoadDataFromGoogleSheet()
        {
            try
            {
                var service = GetSheetsService();
                string userSheetName = GetTargetSheetName(); // 콤보박스에서 선택된 탭 이름
                string targetRange = $"{userSheetName}!A1:Z1000";

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
                    MessageBox.Show($"구글 클라우드 [{userSheetName}] 탭에서 데이터를 실시간 수신했습니다.", "동기화 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show($"[{userSheetName}] 시트에 데이터가 비어 있습니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                    dataGridIngredients.ItemsSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("구글 시트 불러오기 실패 에러:\n" + ex.Message, "에러", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            if (dataGridIngredients.ItemsSource == null) LoadDataFromGoogleSheet();
            else MessageBox.Show("현재 그리드에 동기화된 데이터 조회가 완료되었습니다.", "조회 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void btnFilter_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("성분 필터링 기능이 준비 중입니다.", "필터", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private SheetsService GetSheetsService()
        {
            UserCredential credential;
            using (var stream = new FileStream(clientSecretPath, FileMode.Open, FileAccess.Read))
            {
                string credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".credentials/sheets.googleapis.com-MyWorkspace.json");

                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { SheetsService.Scope.Spreadsheets },
                    "user",
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            return new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GoogleSheetCSVApp",
            });
        }
    }
}