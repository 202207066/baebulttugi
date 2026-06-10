using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace wpf
{
    /// <summary>
    /// Menu_table.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Menu_table : Page
    {
        public Menu_table()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 식단 자동 조합 실행 버튼 클릭 이벤트
        /// </summary>
        private void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            // 1. 입력값 유효성 검증 (빈칸, 문자열 입력 차단 및 음수 체크)
            if (!double.TryParse(TxtCarb.Text, out double carb) || carb < 0)
            {
                MessageBox.Show("탄수화물 적정량을 올바른 숫자로 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCarb.Focus();
                return;
            }

            if (!double.TryParse(TxtProtein.Text, out double protein) || protein < 0)
            {
                MessageBox.Show("단백질 적정량을 올바른 숫자로 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtProtein.Focus();
                return;
            }

            if (!double.TryParse(TxtFat.Text, out double fat) || fat < 0)
            {
                MessageBox.Show("지방 적정량을 올바른 숫자로 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtFat.Focus();
                return;
            }

            if (!double.TryParse(TxtCalories.Text, out double calories) || calories < 0)
            {
                MessageBox.Show("총 칼로리 권장량을 올바른 숫자로 입력해주세요.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtCalories.Focus();
                return;
            }

            // 2. 검증 통과 시 입력된 값으로 조합 알고리즘 호출
            RunMenuRecommendationAlgorithm(carb, protein, fat, calories);
        }

        /// <summary>
        /// 식단 매칭 및 조합 알고리즘 수행부
        /// </summary>
        private void RunMenuRecommendationAlgorithm(double carb, double protein, double fat, double calories)
        {
            // 입력 데이터 확인용 임시 메시지 박스 출력함
            // 추후 이 위치에 DB 연동 및 조합 로직 구현 예정
            string resultMessage = $"[식단 조합 조건 입력 완료]\n\n" +
                                   $"▶ 탄수화물: {carb}g\n" +
                                   $"▶ 단백질: {protein}g\n" +
                                   $"▶ 지방: {fat}g\n" +
                                   $"▶ 총 칼로리: {calories}kcal\n\n" +
                                   $"설정된 기준량으로 조합을 진행함.";

            MessageBox.Show(resultMessage, "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 초기화 버튼 클릭 이벤트 (입력 필드 전체 삭제)
        /// </summary>
        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            TxtCarb.Clear();
            TxtProtein.Clear();
            TxtFat.Clear();
            TxtCalories.Clear();
        }
    }
}