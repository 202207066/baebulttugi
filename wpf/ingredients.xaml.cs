using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace test1
{
    // 클래스 이름 소문자 유지
    public partial class ingredients : Page
    {
        public ingredients()
        {
            InitializeComponent();
        }

        // 📁 CSV 파일 선택 기능  
        private void btnSelectFile_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV 파일 (*.csv)|*.csv|모든 파일 (*.*)|*.*";

            if (openFileDialog.ShowDialog() == true)
            {
                txtFilePath.Text = openFileDialog.FileName;
            }
        }

        // 📤 구글 시트에 업로드 버튼 기능 (새로 추가됨!)
        private void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            // 예외 처리: 파일을 선택하지 않은 상태에서 업로드 버튼을 누른 경우
            if (string.IsNullOrEmpty(txtFilePath.Text) || txtFilePath.Text == "선택된 파일이 없습니다.")
            {
                MessageBox.Show("먼저 업로드할 CSV 파일을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("선택한 CSV 파일을 구글 시트에 업로드하는 로직을 실행합니다.");

            // 👉 조원들과 할 일: 기존 test11.cs에 있던 UploadCsvToGoogleSheet(txtFilePath.Text); 코드를 
            // 이곳으로 가져와 연결해 주시면 됩니다!
        }

        // 🌐 구글시트불러오기 기능  
        private void btnLoadGoogleSheet_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("구글 시트 연동 로직을 실행합니다.");
        }

        // 🔍 조회 버튼 기능  
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("데이터를 새로 고쳐 조회합니다.");
        }

        // ⏳ 필터 적용 버튼 기능  
        private void btnFilter_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("필터 조건에 맞게 엑셀 표를 정렬합니다.");
        }
    }
}