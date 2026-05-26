using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading; // <-- OAuth 비동기 처리를 위해 추가
using System.Windows.Forms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store; // <-- 인증 토큰 저장을 위해 추가
using Microsoft.VisualBasic.FileIO;

namespace test1
{
    public partial class test11 : Form
    {
        private string spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";

        // [수정] 서비스 계정 키 대신 구글 클라우드에서 받은 '클라이언트 비밀번호 JSON' 경로를 적어줍니다.
        private string clientSecretPath = @"C:\google\client_secret.json";

        public test11()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private string GetTargetSheetName()
        {
            string sheetName = txtSearch.Text.Trim();
            if (string.IsNullOrEmpty(sheetName))
            {
                MessageBox.Show("시트 이름을 입력하지 않아 기본값('Sheet1')으로 진행합니다.", "안내");
                return "Sheet1";
            }
            return sheetName;
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            LoadDataFromGoogleSheet();
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "CSV 파일 (*.csv)|*.csv" };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UploadCsvToGoogleSheet(openFileDialog.FileName);
            }
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
                        if (fields != null)
                        {
                            valueRange.Values.Add(fields.Cast<object>().ToList());
                        }
                    }
                }

                string targetRange = $"{userSheetName}!A1";
                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, targetRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                updateRequest.Execute();

                MessageBox.Show($"구글 시트 [{userSheetName}] 탭에 업로드 성공!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("업로드 에러: " + ex.Message);
            }
        }

        private void LoadDataFromGoogleSheet()
        {
            try
            {
                var service = GetSheetsService();
                string userSheetName = GetTargetSheetName();
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

                    dataGridView1.DataSource = dt;
                    dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                    dataGridView1.AllowUserToAddRows = false;
                }
                else
                {
                    MessageBox.Show($"[{userSheetName}] 시트에 가져올 데이터가 없습니다.");
                    dataGridView1.DataSource = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("불러오기 에러: " + ex.Message);
            }
        }

        // [핵심 핵심 핵심수정] 구글 OAuth 2.0 사용자 인증 서비스 생성
        private SheetsService GetSheetsService()
        {
            UserCredential credential;

            // 구글 클라우드에서 다운로드한 client_secret.json 파일을 읽습니다.
            using (var stream = new FileStream(clientSecretPath, FileMode.Open, FileAccess.Read))
            {
                // 로그인 완료 후 발급받은 액세스 토큰을 저장할 PC 내부 폴더 경로입니다.
                // "MyWorkspace" 부분은 프로그램에 맞게 자유롭게 이름을 바꾸셔도 됩니다.
                string credPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), ".credentials/sheets.googleapis.com-MyWorkspace.json");

                // 브라우저를 열어 구글 로그인을 진행하고 인증을 획득합니다.
                credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                    GoogleClientSecrets.FromStream(stream).Secrets,
                    new[] { SheetsService.Scope.Spreadsheets }, // 시트 읽기/쓰기 권한 요청
                    "user", // 필요 시 "user" 대신 로그인한 사원ID 등을 넣으면 계정별 토큰 분리 관리가 가능합니다.
                    CancellationToken.None,
                    new FileDataStore(credPath, true)).Result;
            }

            // 인증된 사용자의 권한으로 구글 시트 서비스 객체를 생성하여 반환합니다.
            return new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GoogleSheetCSVApp",
            });
        }
    }
}