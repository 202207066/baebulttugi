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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace wpf
{
    /// <summary>
    /// MenuInputPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MenuInputPage : Page // ◀ Window에서 Page로 변경하여 Frame 연동이 가능하도록 수정했습니다.
    {
        // 등록된 메뉴들을 임시 저장할 메모리 리스트
        private List<MenuDataModel> _menuList;

        public MenuInputPage()
        {
            InitializeComponent();

            // 화면이 로드될 때 기본 예시 식단 데이터를 표에 뿌려줍니다.
            LoadInitialRecipes();
        }

        /// <summary>
        /// 초기 예시 레시피 데이터를 로드하고 표(DataGrid)에 바인딩합니다.
        /// </summary>
        private void LoadInitialRecipes()
        {
            _menuList = new List<MenuDataModel>
            {
                new MenuDataModel { MenuName = "발아현미밥", Calories = "320 kcal", Ingredients = "쌀, 현미", Allergies = "없음" },
                new MenuDataModel { MenuName = "땅콩 소스 닭강정", Calories = "450 kcal", Ingredients = "닭고기, 땅콩, 대두, 밀", Allergies = "땅콩, 대두, 밀" },
                new MenuDataModel { MenuName = "새우 완탕 국", Calories = "180 kcal", Ingredients = "새우, 밀, 계란", Allergies = "새우, 계란, 밀" }
            };

            DgMenuList.ItemsSource = _menuList;
        }

        /// <summary>
        /// [레시피 저장하기] 버튼 클릭 이벤트 처리
        /// </summary>
        private void BtnSaveMenu_Click(object sender, RoutedEventArgs e)
        {
            // 간단한 입력 유효성 검사 (메뉴명 필수)
            if (string.IsNullOrWhiteSpace(TxtMenuName.Text))
            {
                MessageBox.Show("메뉴명을 입력해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 새로운 메뉴 데이터 객체 생성 및 텍스트 포맷팅
            var newMenu = new MenuDataModel
            {
                MenuName = TxtMenuName.Text.Trim(),
                Calories = string.IsNullOrWhiteSpace(TxtCalories.Text) ? "- kcal" : TxtCalories.Text.Trim() + " kcal",
                Ingredients = string.IsNullOrWhiteSpace(TxtIngredients.Text) ? "미지정" : TxtIngredients.Text.Trim(),
                Allergies = string.IsNullOrWhiteSpace(TxtAllergies.Text) ? "없음" : TxtAllergies.Text.Trim()
            };

            // 리스트에 추가 후 그리드 동적 새로고침
            _menuList.Add(newMenu);
            DgMenuList.ItemsSource = null;
            DgMenuList.ItemsSource = _menuList;

            // 입력창 초기화
            ClearInputForms();

            MessageBox.Show($"[{newMenu.MenuName}] 레시피가 성공적으로 등록되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// 입력 폼 리셋 함수
        /// </summary>
        private void ClearInputForms()
        {
            TxtMenuName.Clear();
            TxtCalories.Clear();
            TxtIngredients.Clear();
            TxtAllergies.Clear();
        }
    }

    /// <summary>
    /// 메뉴 레시피 데이터를 담기 위한 구조 설계 클래스
    /// </summary>
    public class MenuDataModel
    {
        public string MenuName { get; set; }
        public string Calories { get; set; }
        public string Ingredients { get; set; }
        public string Allergies { get; set; }
    }
}