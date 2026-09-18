using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace wpf
{
    /// <summary>
    /// 메뉴(레시피) 관리 화면.
    ///
    /// 예전에는 이 파일 안에 LocalGoogleSheetsService라는 별도 연동 클래스를 두고
    /// credentials.json을 직접 열어 서비스 계정으로 인증했습니다. 앱의 다른 부분은
    /// OAuth로 로그인하는데 같은 파일을 다른 포맷으로 읽는 셈이라 한쪽은 반드시
    /// 실패했습니다. 이제 로그인 때 만든 공용 서비스(AppServices)를 사용합니다.
    /// </summary>
    public partial class MenuInputPage : Page
    {
        private readonly GoogleSheetsService _sheetsService;

        /// <summary>
        /// 새로 등록한 메뉴가 쌓이는 시트.
        ///
        /// 식품영양학과에서 정리한 메뉴풀(트랙A/트랙B)은 읽기 전용으로 두고,
        /// 앱에서 추가하는 메뉴는 별도 시트에 모읍니다. 원본 데이터에 섞이면
        /// 출처 관리가 깨지기 때문입니다.
        /// </summary>
        private string _userMenuSheet = AppConfig.UserMenuSheetName;
        private string UserMenuRange => $"'{_userMenuSheet}'!A2:H";

        private List<MenuDataModel> _menuList = new List<MenuDataModel>();

        public MenuInputPage()
        {
            InitializeComponent();

            _sheetsService = AppServices.Require();

            this.Loaded += MenuInputPage_Loaded;
        }

        private async void MenuInputPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshGridFromGoogleSheetAsync();
        }

        /// <summary>
        /// MenuDatabase 시트에서 레시피를 읽어 표를 갱신합니다.
        /// </summary>
        private async Task RefreshGridFromGoogleSheetAsync()
        {
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                var pool = await _sheetsService.GetMenuPoolAsync(GoogleSheetsService.AgeTrack.All);

                var list = pool
                    .Select(m => new MenuDataModel
                    {
                        MenuName = m.Name,
                        Calories = m.Calories > 0 ? $"{m.Calories:0} kcal" : "-",
                        Ingredients = string.IsNullOrWhiteSpace(m.Materials) ? "매칭 전" : m.Materials,
                        Allergies = string.IsNullOrWhiteSpace(m.Allergy) ? "없음" : m.Allergy
                    })
                    .ToList();

                _menuList = list;
                DgMenuList.ItemsSource = null;
                DgMenuList.ItemsSource = _menuList;

                // 예전에는 시트가 비어 있으면 예시 레시피 3개를 화면에 채워 넣어서
                // 실제 데이터인지 더미인지 구분할 수 없었습니다. 이제는 비었다고 알립니다.
                if (_menuList.Count == 0)
                {
                    MessageBox.Show(
                        "등록된 메뉴가 없습니다.\n아래 입력란에서 첫 메뉴를 등록해 보세요.",
                        "안내", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 데이터를 불러오는 중 오류 발생:\n{ex.Message}",
                                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// [레시피 저장하기] 버튼
        /// </summary>
        private async void BtnSaveMenu_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtMenuName.Text))
            {
                MessageBox.Show("메뉴명을 입력해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtMenuName.Focus();
                return;
            }

            string menuName = TxtMenuName.Text.Trim();

            // 같은 이름이 이미 있으면 중복 등록을 막습니다.
            foreach (var existing in _menuList)
            {
                if (string.Equals(existing.MenuName, menuName, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show($"'{menuName}' 은(는) 이미 등록된 메뉴입니다.",
                                    "중복", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            var newMenu = new MenuDataModel
            {
                MenuName = menuName,
                Calories = FormatCalories(TxtCalories.Text),
                Ingredients = string.IsNullOrWhiteSpace(TxtIngredients.Text) ? "미지정" : TxtIngredients.Text.Trim(),
                Allergies = string.IsNullOrWhiteSpace(TxtAllergies.Text) ? "없음" : TxtAllergies.Text.Trim()
            };

            Mouse.OverrideCursor = Cursors.Wait;
            try
            {
                _userMenuSheet = await _sheetsService.EnsureUserMenuSheetAsync();

                await _sheetsService.AppendRowAsync(UserMenuRange, new List<object>
                {
                    newMenu.MenuName, newMenu.Calories, newMenu.Ingredients, newMenu.Allergies,
                    "", "", "", ""   // 분류 · 탄수 · 단백 · 지방 (필요하면 시트에서 채웁니다)
                });

                Mouse.OverrideCursor = null;

                MessageBox.Show($"[{newMenu.MenuName}] 메뉴를 «{_userMenuSheet}» 시트에 저장했습니다.",
                                "완료", MessageBoxButton.OK, MessageBoxImage.Information);

                ClearInputForms();
                await RefreshGridFromGoogleSheetAsync();
            }
            catch (Exception ex)
            {
                Mouse.OverrideCursor = null;

                // 예전에는 실패를 bool로 삼켜서 "저장 실패" 한 줄만 보였습니다.
                // 원인(권한/네트워크/시트 이름)을 그대로 보여 줍니다.
                MessageBox.Show(
                    "구글 스프레드시트에 저장하지 못했습니다.\n\n" + ex.Message,
                    "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>"350" → "350 kcal", 이미 단위가 있으면 그대로.</summary>
        private static string FormatCalories(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "- kcal";

            string text = raw.Trim();
            return text.Contains("kcal", StringComparison.OrdinalIgnoreCase) ? text : text + " kcal";
        }

        private void ClearInputForms()
        {
            TxtMenuName.Clear();
            TxtCalories.Clear();
            TxtIngredients.Clear();
            TxtAllergies.Clear();
        }
    }

    /// <summary>
    /// 메뉴 레시피 데이터 모델
    /// </summary>
    public class MenuDataModel
    {
        public string MenuName { get; set; } = string.Empty;
        public string Calories { get; set; } = string.Empty;
        public string Ingredients { get; set; } = string.Empty;
        public string Allergies { get; set; } = string.Empty;
    }
}
