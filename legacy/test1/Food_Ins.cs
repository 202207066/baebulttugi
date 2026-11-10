using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;

// 구글 API 라이브러리
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;

namespace test1
{
    public partial class Food_Ins : Form
    {
        // 1. 설정 정보 (credPath는 이제 필요 없으므로 제거하거나 주석 처리합니다)
        // private readonly string credPath = @"C:\google\Google_key.json"; 
        private readonly string spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";

        public Food_Ins()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                SaveToGoogleSheet();
                MessageBox.Show("성공적으로 저장되었습니다!");
                ClearInputs();
            }
            catch (Exception ex)
            {
                // 인증 정보를 못 찾을 경우 여기서 에러 메시지가 출력됩니다.
                MessageBox.Show($"오류 발생: {ex.Message}");
            }
        }

        private void SaveToGoogleSheet()
        {
            // 2. ADC 방식으로 구글 API 인증 세팅
            // 파일 스트림을 여는 과정 없이, 시스템 환경(gcloud login 등)에서 인증 정보를 자동으로 가져옵니다.
            GoogleCredential credential = GoogleCredential.GetApplicationDefault()
                .CreateScoped(SheetsService.Scope.Spreadsheets);

            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Food Inventory App",
            });

            // 3. 데이터 조립 (동일)
            var rowData = new List<object>
            {
                textBox1.Text,      // 식재료 이름
                textBox2.Text,      // 단가
                textBox3.Text,      // 탄수화물
                textBox4.Text,      // 단백질
                textBox5.Text,      // 지방
                comboBox1.Text       // 알러지 여부
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
            textBox1.Focus();
        }
    }
}