using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

// 구글 API 라이브러리 (NuGet에서 Google.Apis.Sheets.v4 설치 필요)
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;

namespace test1
{
    public partial class Food_Ins : Form
    {
        // 1. 설정 정보
        private readonly string credPath = @"C:\google\Google_key.json";
        private readonly string spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";

        public Food_Ins()
        {
            InitializeComponent();
        }

        // '보내기' 버튼 클릭 시 실행되는 이벤트
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                SaveToGoogleSheet();
                MessageBox.Show("성공적으로 저장되었습니다!");

                // 입력 칸 초기화 (선택 사항)
                ClearInputs();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"오류 발생: {ex.Message}");
            }
        }

        private void SaveToGoogleSheet()
        {
            // 2. 구글 API 인증 세팅
            GoogleCredential credential;
            using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Food Inventory App",
            });

            // 3. 데이터 조립 (ValueRange)
            var rowData = new List<object>
            {
                textBox1.Text,      // 식재료 이름
                textBox2.Text,      // 단가
                textBox3.Text,      // 탄수화물
                textBox4.Text,      // 단백질
                textBox5.Text,      // 지방
                comboBox1.Text,     // 알러지 여부
               // DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") // 입력 시간
            };

            var valueRange = new ValueRange { Values = new List<IList<object>> { rowData } };

            // 4. 데이터 전송 (Append)
            var appendRequest = service.Spreadsheets.Values.Append(valueRange, spreadsheetId, "FoodItem!A:G");
            appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            appendRequest.Execute();
        }

        private void ClearInputs()
        {
            textBox1.Clear();
            textBox2.Clear();
            textBox3.Clear();
            textBox4.Clear();
            textBox5.Clear();
            comboBox1.SelectedIndex = -1;
            textBox1.Focus(); // 다시 입력하기 편하게 이름 칸으로 커서 이동
        }
    }
}