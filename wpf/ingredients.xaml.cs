using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace wpf // 메인 윈도우와 일치하도록 네임스페이스를 wpf로 변경
{
    /// <summary>
    /// Ingredients.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Ingredients : Page // CS8981 경고 해결을 위해 대문자 'Ingredients'로 변경
    {
        public Ingredients()
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

        // 📤 구글 시트에 업로드 버튼 기능
        private void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            // 예외 처리: 파일을 선택하지 않은 상태에서 업로드 버튼을 누른 경우
            if (string.IsNullOrEmpty(txtFilePath.Text) || txtFilePath.Text == "선택된 파일이 없습니다.")
            {
                MessageBox.Show("먼저 업로드할 CSV 파일을 선택해주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            MessageBox.Show("선택한 CSV 파일을 구글 시트에 업로드하는 로직을 실행합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);

            // 👉 조원들과 할 일: 기존 test11.cs에 있던 UploadCsvToGoogleSheet(txtFilePath.Text); 코드를 
            // 이곳으로 가져와 연결해 주시면 됩니다!
        }

        // 🌐 구글 시트 불러오기 기능  
        private void btnLoadGoogleSheet_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("구글 스프레드시트 연동 로직을 실행하여 최신 데이터를 가져옵니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 🔍 성분 조회 버튼 기능  
        private void btnSearch_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("식재료 및 성분 DB 데이터를 새로고침하여 목록에 조회합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ⏳ 필터 적용 버튼 기능  
        private void btnFilter_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("설정된 필터 조건에 맞게 데이터 목록을 필터링 및 정렬합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}