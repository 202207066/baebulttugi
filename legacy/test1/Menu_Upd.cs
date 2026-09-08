using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace test1
{
    public partial class Menu_Upd : Form
    {
        // JSON 키 파일 경로
        private readonly string credPath = @"C:\google\Google_key.json";
        public Menu_Upd()
        {
            InitializeComponent();
            // 폼 로드 이벤트 등록
            this.Load += Menu_Upd_Load;
        }

        // 폼이 로드될 때 실행되는 메서드
        private void Menu_Upd_Load(object sender, EventArgs e)
        {
            LoadIngredientsFromGoogleSheet();
        }

        // 구글 시트에서 식재료 목록 불러오기
        private void LoadIngredientsFromGoogleSheet()
        {
            // 1. JSON 키 파일로 인증 객체 생성
            GoogleCredential credential;
            using (var stream = new FileStream(credPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(SheetsService.Scope.Spreadsheets);
            }

            // 2. SheetsService 객체 생성
            var service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "FoodProject",
            });

            // 3. 스프레드시트 ID와 범위 지정
            string spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8"; // URL에서 확인 가능
            string range = "FoodItem!A2:A"; // A열에 식재료 목록 있다고 가정

            // 4. 데이터 읽기 요청
            var request = service.Spreadsheets.Values.Get(spreadsheetId, range);
            var response = request.Execute();
            var values = response.Values;

            // 5. CheckedListBox에 데이터 추가
            if (values != null && values.Count > 0)
            {
                foreach (var row in values)
                {
                    checkedListBox1.Items.Add(row[0].ToString());
                }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            // 사용자가 체크한 항목 가져오기
            var selectedItems = checkedListBox1.CheckedItems;
            string result = string.Join(", ", selectedItems.Cast<string>());
            MessageBox.Show("선택된 재료: " + result);
        }

        private void button2_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

        }
    }
}
