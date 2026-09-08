using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// 구글 API 선언
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace wpf
{
    public partial class AllergyManagementPage : Page
    {
        // 1. 본인의 구글 시트 ID 및 시트 범위 설정
        private static string _spreadsheetId => AppConfig.SpreadsheetId;
        // ID, 성명, 구분, 알러지내역, 비고
        private static string _sheetRange => $"'{AppConfig.PatientSheetName}'!A:E";

        private SheetsService _sheetsService;

        // 💡 메모리 DB 대신, 프로그램 내부에서 구글 시트 데이터를 담고 있을 실시간 리스트입니다.
        private List<PatientModel> _patientList = new List<PatientModel>();
        private List<MenuModel> _menuList = new List<MenuModel>();

        public AllergyManagementPage()
        {
            InitializeComponent();

            InitGoogleSheetsService();

            Loaded += AllergyManagementPage_Loaded;

            // 버튼 이벤트 연결
            BtnSavePatient.Click += BtnSavePatient_Click;
            BtnSearchPatient.Click += BtnSearchPatient_Click;
            BtnUpdatePatient.Click += BtnUpdatePatient_Click;
            BtnDeletePatient.Click += BtnDeletePatient_Click;
            BtnScanAllergy.Click += BtnScanAllergy_Click;
            BtnManualMenuInput.Click += BtnManualMenuInput_Click;
            BtnConfirmAlternativeMenu.Click += BtnConfirmAlternativeMenu_Click;

            // 리스트 행 선택 시 입력창 바인딩 이벤트
            DgPatients.SelectionChanged += DgPatients_SelectionChanged;
        }

        private async void AllergyManagementPage_Loaded(object sender, RoutedEventArgs e)
        {
            SeedInitialSampleData();
            RefreshDailyMenuGrid();

            if (DpMonitorDate != null) DpMonitorDate.SelectedDate = DateTime.Today;

            // 💡 프로그램이 켜지자마자 구글 시트 DB 서버에서 데이터를 원격으로 긁어옵니다!
            await LoadPatientsFromGoogleSheetAsync();
        }

        // ==========================================
        // 🟢 구글 시트 서비스 초기화 (비밀키 파일 연동)
        // ==========================================
        private void InitGoogleSheetsService()
        {
            try
            {
                string keyPath = AppConfig.CredentialsPath;

                if (File.Exists(keyPath))
                {
                    using (var stream = new FileStream(keyPath, FileMode.Open, FileAccess.Read))
                    {
                        GoogleCredential credential = GoogleCredential.FromStream(stream)
                            .CreateScoped(SheetsService.Scope.Spreadsheets);

                        _sheetsService = new SheetsService(new BaseClientService.Initializer()
                        {
                            HttpClientInitializer = credential,
                            ApplicationName = "MealCareAllergyApp"
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"구글 시트 초기화 실패: {ex.Message}");
            }
        }

        // ========================================================
        // 🟢 [핵심 추가] 구글 시트 서버에서 기존 환자 데이터 전부 원격으로 읽어오기
        // ========================================================
        private async Task LoadPatientsFromGoogleSheetAsync()
        {
            if (_sheetsService == null)
            {
                MessageBox.Show("구글 인증 서비스가 연결되지 않아 서버 데이터를 불러올 수 없습니다.", "구글 미연동", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                _patientList.Clear();

                var response = await Task.Run(() => {
                    return _sheetsService.Spreadsheets.Values.Get(_spreadsheetId, _sheetRange).Execute();
                });

                var values = response.Values;
                if (values != null && values.Count > 0)
                {
                    // 첫 번째 행이 헤더(ID, 성명...)라면 i = 1 부터 시작, 데이터부터 시작하면 i = 0 부터 시작
                    // 안전하게 첫 행이 숫자가 아니면 헤더로 취급하여 건너뜁니다.
                    int startIndex = 0;
                    if (!int.TryParse(values[0][0]?.ToString(), out _))
                    {
                        startIndex = 1;
                    }

                    for (int i = startIndex; i < values.Count; i++)
                    {
                        var row = values[i];
                        if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]?.ToString())) continue;

                        int id = int.TryParse(row[0]?.ToString(), out int parsedId) ? parsedId : i;
                        string name = row.Count > 1 ? row[1]?.ToString() ?? "" : "";
                        string category = row.Count > 2 ? row[2]?.ToString() ?? "" : "일반";
                        string allergies = row.Count > 3 ? row[3]?.ToString() ?? "" : "";
                        string note = row.Count > 4 ? row[4]?.ToString() ?? "" : "";

                        _patientList.Add(new PatientModel
                        {
                            Id = id,
                            Name = name,
                            Category = category,
                            Allergies = allergies,
                            Note = note
                        });
                    }
                }

                UpdatePatientGrid(_patientList);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 서버 로드 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ========================================================
        // 🟢 [핵심 추가] 구글 시트 서버의 전체 데이터를 갱신(덮어쓰기)하는 로직
        // ========================================================
        private async Task<bool> SaveAllPatientsToGoogleSheetAsync()
        {
            if (_sheetsService == null) return false;

            try
            {
                await Task.Run(() =>
                {
                    // 1. 기존 시트 데이터를 완전히 깨끗하게 비웁니다.
                    var clearRequest = _sheetsService.Spreadsheets.Values.Clear(new ClearValuesRequest(), _spreadsheetId, _sheetRange);
                    clearRequest.Execute();

                    // 2. 헤더 구성 및 현재 메모리 리스트에 있는 모든 환자 정보 준비
                    var values = new List<IList<object>> {
                        new List<object> { "ID", "성명", "구분", "특이 알러지 성분", "비고(메모)" } // 타이틀 헤더 행
                    };

                    foreach (var p in _patientList)
                    {
                        values.Add(new List<object> { p.Id, p.Name, p.Category, p.Allergies, p.Note });
                    }

                    var valueRange = new ValueRange { Values = values };

                    // 3. 통째로 구글 시트에 밀어 넣습니다.
                    var updateRequest = _sheetsService.Spreadsheets.Values.Update(valueRange, _spreadsheetId, _sheetRange);
                    updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                    updateRequest.Execute();
                });

                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 서버 동기화 실패:\n{ex.Message}", "연동 오류", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void UpdatePatientGrid(List<PatientModel> list)
        {
            DgPatients.ItemsSource = null;
            DgPatients.ItemsSource = list.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                Allergies = p.Allergies,
                p.Note
            }).ToList();
        }

        // ==========================================
        // 🟢 데이터그리드 선택 시 왼쪽 입력창 복원
        // ==========================================
        private void DgPatients_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DgPatients.SelectedItem == null) return;

            dynamic selectedPatient = DgPatients.SelectedItem;
            string name = selectedPatient.Name;
            string category = selectedPatient.Category;
            string allergiesText = selectedPatient.Allergies;
            string note = selectedPatient.Note;

            TxtPatientName.Text = name;
            TxtPatientNote.Text = note;

            foreach (ComboBoxItem item in CmbPatientCategory.Items)
            {
                if (item.Content?.ToString() == category)
                {
                    CmbPatientCategory.SelectedItem = item;
                    break;
                }
            }

            var allergyList = allergiesText.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToHashSet();
            var currentContent = this.Content as DependencyObject;
            if (currentContent != null)
            {
                foreach (var child in FindVisualChildren<CheckBox>(currentContent))
                {
                    if (child != null && child.Content != null)
                    {
                        string checkBoxName = child.Content.ToString()!;
                        child.IsChecked = allergyList.Contains(checkBoxName);
                    }
                }
            }
        }

        // ==========================================
        // 🟢 피급식자 등록 기능 (구글 시트 서버에 추가)
        // ==========================================
        private async void BtnSavePatient_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtPatientName.Text))
            {
                MessageBox.Show("피급식자의 성명을 입력해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputName = TxtPatientName.Text.Trim();
            List<string> selectedAllergies = new List<string>();

            if (this.Content is DependencyObject currentContent)
            {
                foreach (var child in FindVisualChildren<CheckBox>(currentContent))
                {
                    if (child?.IsChecked == true && child.Content != null)
                    {
                        selectedAllergies.Add(child.Content.ToString() ?? "");
                    }
                }
            }

            string category = CmbPatientCategory?.SelectedItem is ComboBoxItem item ? (item.Content?.ToString() ?? "일반") : "일반";

            // 고유 ID 부여 (가장 큰 ID + 1)
            int nextId = _patientList.Count > 0 ? _patientList.Max(p => p.Id) + 1 : 1;

            var newPatient = new PatientModel
            {
                Id = nextId,
                Name = inputName,
                Category = category,
                Allergies = string.Join(", ", selectedAllergies),
                Note = TxtPatientNote?.Text?.Trim() ?? string.Empty
            };

            // 내부 목록에 추가 후 서버 동기화
            _patientList.Add(newPatient);

            bool isSuccess = await SaveAllPatientsToGoogleSheetAsync();

            if (isSuccess)
            {
                UpdatePatientGrid(_patientList);
                ClearInputFields();
                MessageBox.Show($"구글 서버에 '{inputName}' 피급식자 등록이 완료되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 실패 시 롤백
                _patientList.Remove(newPatient);
            }
        }

        // ==========================================
        // 🟢 피급식자 정보 수정 기능
        // ==========================================
        private async void BtnUpdatePatient_Click(object sender, RoutedEventArgs e)
        {
            if (DgPatients.SelectedItem == null)
            {
                MessageBox.Show("수정할 대상을 목록에서 먼저 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic selectedItem = DgPatients.SelectedItem;
            int selectedId = selectedItem.Id;

            var target = _patientList.FirstOrDefault(p => p.Id == selectedId);
            if (target != null)
            {
                List<string> updatedAllergies = new List<string>();
                if (this.Content is DependencyObject currentContent)
                {
                    foreach (var child in FindVisualChildren<CheckBox>(currentContent))
                    {
                        if (child?.IsChecked == true && child.Content != null)
                        {
                            updatedAllergies.Add(child.Content.ToString() ?? "");
                        }
                    }
                }

                // 값 업데이트
                target.Name = TxtPatientName.Text.Trim();
                target.Category = CmbPatientCategory.SelectedItem is ComboBoxItem item ? (item.Content?.ToString() ?? "일반") : "일반";
                target.Allergies = string.Join(", ", updatedAllergies);
                target.Note = TxtPatientNote?.Text?.Trim() ?? string.Empty;

                // 구글 시트 전면 동기화
                bool isSuccess = await SaveAllPatientsToGoogleSheetAsync();
                if (isSuccess)
                {
                    UpdatePatientGrid(_patientList);
                    ClearInputFields();
                    MessageBox.Show("선택한 정보가 구글 시트 데이터베이스 서버에 실시간 수정되었습니다.", "수정 완료");
                }
            }
        }

        // ==========================================
        // 🟢 피급식자 정보 삭제 기능
        // ==========================================
        private async void BtnDeletePatient_Click(object sender, RoutedEventArgs e)
        {
            if (DgPatients.SelectedItem == null)
            {
                MessageBox.Show("삭제할 대상을 목록에서 먼저 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic selectedItem = DgPatients.SelectedItem;
            int selectedId = selectedItem.Id;

            if (MessageBox.Show("선택한 피급식자 데이터를 구글 서버에서 영구 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var target = _patientList.FirstOrDefault(p => p.Id == selectedId);
                if (target != null)
                {
                    _patientList.Remove(target);

                    // 구글 서버에 변경 상태 업로드
                    bool isSuccess = await SaveAllPatientsToGoogleSheetAsync();
                    if (isSuccess)
                    {
                        UpdatePatientGrid(_patientList);
                        ClearInputFields();
                        MessageBox.Show("구글 서버에서 삭제 처리가 완료되었습니다.", "삭제 완료");
                    }
                }
            }
        }

        // ==========================================
        // 🟢 검색 기능
        // ==========================================
        private void BtnSearchPatient_Click(object sender, RoutedEventArgs e)
        {
            string searchKeyword = TxtSearchPatient.Text.Trim();
            if (string.IsNullOrWhiteSpace(searchKeyword))
            {
                UpdatePatientGrid(_patientList);
                return;
            }

            var filtered = _patientList.Where(p => p.Name.Contains(searchKeyword)).ToList();
            UpdatePatientGrid(filtered);
        }

        // ==========================================
        // 🟢 알러지 스캔 매칭 기능
        // ==========================================
        private void BtnScanAllergy_Click(object sender, RoutedEventArgs e)
        {
            DateTime selectedDate = DpMonitorDate?.SelectedDate ?? DateTime.Today;
            var dailyMenus = _menuList.Where(m => m.ServingDate.Date == selectedDate.Date).ToList();

            var conflictMatches = new List<object>();
            foreach (var patient in _patientList)
            {
                var patientAllergySet = patient.Allergies.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries).Select(a => a.Trim()).ToHashSet();
                foreach (var menu in dailyMenus)
                {
                    var matchedRisks = menu.Ingredients.Where(name => patientAllergySet.Contains(name)).ToList();
                    if (matchedRisks.Any())
                    {
                        conflictMatches.Add(new
                        {
                            PatientName = patient.Name,
                            RiskIngredients = string.Join(", ", matchedRisks),
                            SuggestedAlternative = $"💡 [{menu.MenuName}] 제한 -> 대체 식단 교체 권장"
                        });
                    }
                }
            }
            DgAllergicMatches.ItemsSource = conflictMatches;
            MessageBox.Show($"스캔 완료: 총 {conflictMatches.Count}건의 알러지 교차 위험 요인이 검출되었습니다.");
        }

        private void SeedInitialSampleData()
        {
            if (!_menuList.Any())
            {
                _menuList.Add(new MenuModel { MenuName = "땅콩소스 닭강정", ServingDate = DateTime.Today, Ingredients = new List<string> { "닭고기", "땅콩" } });
                _menuList.Add(new MenuModel { MenuName = "소고기 미역국", ServingDate = DateTime.Today, Ingredients = new List<string> { "소고기" } });
            }
        }

        private void RefreshDailyMenuGrid()
        {
            DateTime targetDate = DpMonitorDate?.SelectedDate ?? DateTime.Today;
            var filteredMenus = _menuList.Where(m => m.ServingDate.Date == targetDate.Date).ToList();

            DgDailyMenu.ItemsSource = filteredMenus.Select(m => new
            {
                m.MenuName,
                Ingredients = string.Join(", ", m.Ingredients)
            }).ToList();
        }

        private void BtnManualMenuInput_Click(object sender, RoutedEventArgs e)
        {
            _menuList.Add(new MenuModel { MenuName = "수동 추가 메밀국수", ServingDate = DpMonitorDate?.SelectedDate ?? DateTime.Today, Ingredients = new List<string> { "메밀", "밀가루" } });
            RefreshDailyMenuGrid();
        }

        private void BtnConfirmAlternativeMenu_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("대체 식단 규칙이 확정 반영되었습니다.");
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject? child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t) yield return t;
                    if (child != null)
                    {
                        foreach (T childOfChild in FindVisualChildren<T>(child)) yield return childOfChild;
                    }
                }
            }
        }

        private void ClearInputFields()
        {
            if (TxtPatientName != null) TxtPatientName.Text = string.Empty;
            if (TxtPatientNote != null) TxtPatientNote.Text = string.Empty;
            var currentContent = this.Content as DependencyObject;
            if (currentContent != null)
            {
                foreach (var child in FindVisualChildren<CheckBox>(currentContent))
                {
                    if (child != null) child.IsChecked = false;
                }
            }
            if (DgPatients != null) DgPatients.SelectedItem = null;
        }
    }

    // ==========================================
    // 💡 구글 시트 연동 전용 단순화 모델 클래스
    // ==========================================
    public class PatientModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Allergies { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }

    public class MenuModel
    {
        public string MenuName { get; set; } = string.Empty;
        public DateTime ServingDate { get; set; }
        public List<string> Ingredients { get; set; } = new List<string>();
    }
}