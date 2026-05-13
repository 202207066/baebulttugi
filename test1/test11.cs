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
        private string jsonKeyPath = "your-key-file.json"; // 구글에서 받은 인증키 파일 경로

        public test11()
        {
            InitializeComponent();
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog { Filter = "CSV 파일 (*.csv)|*.csv" };
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                UploadCsvToGoogleSheet(openFileDialog.FileName);
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            LoadDataFromGoogleSheet();
        }

        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            DataTable dt = (DataTable)dataGridView1.DataSource;
            if (dt == null) return;

            string searchText = txtSearch.Text.Replace("'", "''"); // 작은따옴표 입력 에러 방지

            // 검색어가 없으면 전체 리스트 표시
            if (string.IsNullOrWhiteSpace(searchText))
            {
                dt.DefaultView.RowFilter = "";
                return;
            }

            string filter = "";
            foreach (DataColumn column in dt.Columns)
            {
                if (filter.Length > 0) filter += " OR ";

                // 핵심 수정: Convert(열이름, 'System.String')를 사용하여 숫자/날짜 열에서도 LIKE 검색이 가능하게 함
                filter += string.Format("Convert([{0}], 'System.String') LIKE '%{1}%'", column.ColumnName, searchText);
            }

            try
            {
                dt.DefaultView.RowFilter = filter;
            }
            catch (Exception ex)
            {
                // 필터 형식이 잘못되었을 경우 에러 메시지 출력 (디버깅용)
                Console.WriteLine("검색 필터 오류: " + ex.Message);
            }
        }

        private void UploadCsvToGoogleSheet(string filePath)
        {
            try
            {
                var service = GetSheetsService();
                // 한글 인코딩(ANSI) 지원을 위해 euc-kr 사용
                var lines = File.ReadAllLines(filePath, Encoding.GetEncoding("euc-kr"));

                var valueRange = new ValueRange { Values = new List<IList<object>>() };
                foreach (var line in lines)
                {
                    valueRange.Values.Add(line.Split(',').Cast<object>().ToList());
                }

                var updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetId, "Sheet1!A1");
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                updateRequest.Execute();

                MessageBox.Show("구글 시트 업로드 성공!");
            }
            catch (Exception ex) { MessageBox.Show("업로드 에러: " + ex.Message); }
        }

        private void    LoadDataFromGoogleSheet()
        {
            try
            {
                var service = GetSheetsService();
                var request = service.Spreadsheets.Values.Get(spreadsheetId, "Sheet1!A1:Z1000");
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
                }
                dataGridView1.DataSource = dt;
                dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
                dataGridView1.AllowUserToAddRows = false;
            }
            catch (Exception ex) { MessageBox.Show("불러오기 에러: " + ex.Message); }
        }

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

        private void test11_Load(object sender, EventArgs e)
        {

        }

        private void btnUpload_Click_1(object sender, EventArgs e)
        {

        }
    }
}