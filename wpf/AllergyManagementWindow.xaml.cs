using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace wpf
{
    /// <summary>
    /// AllergyManagementPage.xaml에 대한 상호 작용 논리
    /// </summary>
    // 1. 기존 AllergyManagementWindow : Window 부분을 아래와 같이 변경합니다.
    public partial class AllergyManagementPage : Page
    {
        // 2. 생성자 이름도 클래스명과 동일하게 AllergyManagementPage로 변경합니다.
        public AllergyManagementPage()
        {
            InitializeComponent(); // 이제 오류 없이 정상적으로 연결됩니다!

            // 페이지 로드 시 샘플 데이터 주입
            Loaded += AllergyManagementPage_Loaded;
        }

        private void AllergyManagementPage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSampleData();
        }

        private void LoadSampleData()
        {
            // (이전 답변과 동일한 샘플 데이터 주입 로직...)
            var patientList = new List<PatientSample>
            {
                new PatientSample { Id = 1, Name = "김철수", Category = "일반", Allergies = "땅콩", Note = "견과류 일체 알러지 반응 있음" }
            };
            DgPatients.ItemsSource = patientList;
        }
    }

    public class PatientSample
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Allergies { get; set; }
        public string Note { get; set; }
    }
}