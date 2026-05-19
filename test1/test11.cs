using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;

namespace test1
{
    public partial class test11 : Form
    {
        // 구글 시트 정보 (본인의 것으로 변경 필요)
        private string spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";
        private string jsonKeyPath = @"C:\google\Google_key.json"; // 구글에서 받은 인증키 파일 경로

        public test11()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        // [공통 로직] txtSearch 텍스트박스에서 사용자가 입력한 시트 이름을 가져옵니다.
        private string GetTargetSheetName()
        {
            // 실제 이름인 txtSearch를 반영했습니다.
            string sheetName = txtSearch.Text.Trim();

            // 만약 아무것도 입력하지 않았다면 경고를 띄우고 기본값으로 Sheet1을 지정합니다.
            if (string.IsNullOrEmpty(sheetName))
            {
                MessageBox.Show("시트 이름을 입력하지 않아 기본값('Sheet1')으로 진행합니다.", "안내");
                return "Sheet1";
            }
            return sheetName;
        }

        // 1. 시트 불러오기 버튼 클릭 이벤트
        private void btnLoad_Click(object sender, EventArgs e)
        {
            LoadDataFromGoogleSheet();
        }

        // 2. CSV 업로드 버튼 클릭 이벤트
        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "CSV 파일 (*.csv)|*.csv" };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UploadCsvToGoogleSheet(openFileDialog.FileName);
            }
        }

        // 구글 시트에 내가 입력한 시트 이름으로 CSV 업로드
        private void UploadCsvToGoogleSheet(string filePath)
        {
            try
            {
                var service = GetSheetsService();
                var lines = File.ReadAllLines(filePath, Encoding.GetEncoding("euc-kr"));

                // txtSearch에 입력한 시트 이름을 가져옴
                string userSheetName = GetTargetSheetName();

                var valueRange = new ValueRange { Values = new List<IList<object>>() };
                foreach (var line in lines)
                {
                    valueRange.Values.Add(line.Split(',').Cast<object>().ToList());
                }

                // 입력한 시트 이름의 A1 셀부터 데이터 저장 (예: "과일목록!A1")
                string targetRange = $"{userSheetName}!A1";

                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, targetRange);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                updateRequest.Execute();

                MessageBox.Show($"구글 시트 [{userSheetName}] 탭에 업로드 성공!");
            }
            catch (Exception ex)
            {
                MessageBox.Show("업로드 에러: " + ex.Message + "\n\n구글 시트에 해당 이름의 탭이 실제로 존재하는지 확인해 주세요.");
            }
        }

        // 구글 시트에서 내가 입력한 시트 이름의 데이터 가져오기
        private void LoadDataFromGoogleSheet()
        {
            try
            {
                var service = GetSheetsService();

                // txtSearch에 입력한 시트 이름을 가져옴
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
                MessageBox.Show("불러오기 에러: " + ex.Message + "\n\n구글 시트에 해당 이름의 탭이 실제로 존재하는지 확인해 주세요.");
            }
        }

        // 구글 인증 서비스 생성
        private SheetsService GetSheetsService()
        {
            GoogleCredential credential;
            using (var stream = new FileStream(jsonKeyPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream).CreateScoped(SheetsService.Scope.Spreadsheets);
            }
            return new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "GoogleSheetCSVApp",
            });
        }
    }
}