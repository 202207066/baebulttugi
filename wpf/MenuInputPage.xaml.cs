using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace wpf
{
    /// <summary>
    /// MenuInputPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MenuInputPage : Page
    {
        // 🟢 내부 구글 시트 전용 서비스 객체
        private LocalGoogleSheetsService _sheetsService;

        // 등록된 메뉴들을 임시 저장 및 바인딩할 메모리 리스트
        private List<MenuDataModel> _menuList;

        public MenuInputPage()
        {
            InitializeComponent();

            try
            {
                // 여기서 구글 시트 서비스를 바로 초기화합니다.
                _sheetsService = new LocalGoogleSheetsService();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 연동 엔진 초기화 실패:\n{ex.Message}\n\n*주의: 프로젝트 폴더 내에 credentials.json 파일이 있는지 확인하세요.*", "초기화 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            // 화면이 완전히 로드되면 구글 시트에서 데이터를 비동기로 긁어옵니다.
            this.Loaded += MenuInputPage_Loaded;
        }

        /// <summary>
        /// 페이지가 화면에 켜질 때 구글 시트 데이터를 가져옵니다.
        /// </summary>
        private async void MenuInputPage_Loaded(object sender, RoutedEventArgs e)
        {
            await RefreshGridFromGoogleSheetAsync();
        }

        /// <summary>
        /// 구글 스프레드시트의 'MenuDatabase' 시트에서 데이터를 가져와 표(DataGrid)를 갱신합니다.
        /// </summary>
        private async Task RefreshGridFromGoogleSheetAsync()
        {
            if (_sheetsService == null) return;

            // 로딩 중 마우스 커서 변경
            Mouse.OverrideCursor = Cursors.Wait;

            try
            {
                // 구글 시트 데이터 원격 호출
                _menuList = await _sheetsService.GetMenuListAsync();

                // 만약 구글 시트에 데이터가 아예 없다면 로컬 기본 예시 데이터를 배치합니다.
                if (_menuList == null || _menuList.Count == 0)
                {
                    _menuList = new List<MenuDataModel>
                    {
                        new MenuDataModel { MenuName = "발아현미밥", Calories = "320 kcal", Ingredients = "쌀, 현미", Allergies = "없음" },
                        new MenuDataModel { MenuName = "땅콩 소스 닭강정", Calories = "450 kcal", Ingredients = "닭고기, 땅콩, 대두, 밀", Allergies = "땅콩, 대두, 밀" },
                        new MenuDataModel { MenuName = "새우 완탕 국", Calories = "180 kcal", Ingredients = "새우, 밀, 계란", Allergies = "새우, 계란, 밀" }
                    };
                }

                // DataGrid 새로고침 바인딩
                DgMenuList.ItemsSource = null;
                DgMenuList.ItemsSource = _menuList;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 데이터를 불러오는 중 오류 발생:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // 마우스 커서 정상화
                Mouse.OverrideCursor = null;
            }
        }

        /// <summary>
        /// [레시피 저장하기] 버튼 클릭 시 작동하는 로직
        /// </summary>
        private async void BtnSaveMenu_Click(object sender, RoutedEventArgs e)
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
                Calories = string.IsNullOrWhiteSpace(TxtCalories.Text) ? "- kcal" :
                           (TxtCalories.Text.Contains("kcal") ? TxtCalories.Text.Trim() : TxtCalories.Text.Trim() + " kcal"),
                Ingredients = string.IsNullOrWhiteSpace(TxtIngredients.Text) ? "미지정" : TxtIngredients.Text.Trim(),
                Allergies = string.IsNullOrWhiteSpace(TxtAllergies.Text) ? "없음" : TxtAllergies.Text.Trim()
            };

            if (_sheetsService != null)
            {
                Mouse.OverrideCursor = Cursors.Wait;

                // 🟢 구글 시트에 비동기로 신규 데이터 1줄 추가 요청
                bool isSuccess = await _sheetsService.AppendMenuAsync(newMenu);

                Mouse.OverrideCursor = null;

                if (isSuccess)
                {
                    MessageBox.Show($"[{newMenu.MenuName}] 레시피가 구글 시트에 영구 저장되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);

                    // 저장 성공 후 구글 시트의 최신 데이터를 다시 긁어와서 UI 갱신
                    await RefreshGridFromGoogleSheetAsync();

                    // 입력창 초기화
                    ClearInputForms();
                }
                else
                {
                    MessageBox.Show("구글 스프레드시트에 저장하지 못했습니다.\n네트워크 상태나 시트 권한 설정을 확인하세요.", "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                MessageBox.Show("구글 연동 서비스가 켜지지 않아 로컬에만 추가합니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                _menuList.Add(newMenu);
                DgMenuList.ItemsSource = null;
                DgMenuList.ItemsSource = _menuList;
                ClearInputForms();
            }
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

    /// <summary>
    /// 🟢 딴 파일 안 거치게 내부에 내장시킨 구글 시트 연동 모듈 클래스
    /// </summary>
    public class LocalGoogleSheetsService
    {
        // 스프레드시트 ID는 appsettings.json에서 읽습니다.
        private static string _spreadsheetId => AppConfig.SpreadsheetId;
        private readonly SheetsService _service;

        public LocalGoogleSheetsService()
        {
            string credentialPath = AppConfig.CredentialsPath;

            if (!File.Exists(credentialPath))
            {
                throw new FileNotFoundException($"인증 키 파일({credentialPath})이 없습니다.");
            }

            GoogleCredential credential;
            using (var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(SheetsService.Scope.Spreadsheets); // 읽기/쓰기 통합 권한 부여
            }

            _service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "WPF Diet Management System",
            });
        }

        /// <summary>
        /// 구글 시트의 'MenuDatabase' 혹은 'Sheet1' 탭에서 레시피 전체 데이터를 읽어옵니다.
        /// </summary>
        public async Task<List<MenuDataModel>> GetMenuListAsync()
        {
            var list = new List<MenuDataModel>();
            try
            {
                // ⚠️ 구글 시트 좌측 하단 탭 이름이 'MenuDatabase' 또는 'Sheet1' 등 실제 이름과 똑같아야 합니다.
                string range = "MenuDatabase!A2:D";
                var request = _service.Spreadsheets.Values.Get(_spreadsheetId, range);
                var response = await request.ExecuteAsync();
                var values = response.Values;

                if (values != null && values.Count > 0)
                {
                    foreach (var row in values)
                    {
                        list.Add(new MenuDataModel
                        {
                            MenuName = row.Count > 0 ? row[0]?.ToString() ?? "" : "",
                            Calories = row.Count > 1 ? row[1]?.ToString() ?? "" : "",
                            Ingredients = row.Count > 2 ? row[2]?.ToString() ?? "" : "",
                            Allergies = row.Count > 3 ? row[3]?.ToString() ?? "" : ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[구글 시트 읽기 실패] {ex.Message}");
                // 만약 MenuDatabase 탭을 못 찾으면 시도할 예외 처리용 코드나 메시지 영역
            }
            return list;
        }

        /// <summary>
        /// 구글 시트 맨 아래 빈 줄에 새 레시피 추가하기
        /// </summary>
        public async Task<bool> AppendMenuAsync(MenuDataModel menu)
        {
            try
            {
                string range = "MenuDatabase!A2:D";
                var valueRange = new ValueRange
                {
                    Values = new List<IList<object>>
                    {
                        new List<object> { menu.MenuName, menu.Calories, menu.Ingredients, menu.Allergies }
                    }
                };

                var appendRequest = _service.Spreadsheets.Values.Append(valueRange, _spreadsheetId, range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;

                await appendRequest.ExecuteAsync();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[구글 시트 쓰기 실패] {ex.Message}");
                return false;
            }
        }
    }
}