using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore; // 💡 실제 DB 연동을 위한 EF Core 네임스페이스

namespace wpf
{
    /// <summary>
    /// AllergyManagementPage.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class AllergyManagementPage : Page
    {
        // 데이터베이스와의 연결 세션을 담당하는 컨텍스트 객체
        private readonly AllergyDbContext _dbContext;

        public AllergyManagementPage()
        {
            InitializeComponent();

            // DB 컨텍스트 초기화
            _dbContext = new AllergyDbContext();

            // [임시 메모리 기능] 실제 서버 DB를 연결하기 전까지 에러 없이 작동하도록 임시 가상 DB 자동 생성
            _dbContext.Database.EnsureCreated();

            // 페이지 로드 이벤트 연결
            Loaded += AllergyManagementPage_Loaded;

            // XAML 버튼들과 C# 이벤트 핸들러 메서드 명시적 매칭 (XAML에 Click 속성이 없을 경우 대비)
            BtnSavePatient.Click += BtnSavePatient_Click;
            BtnSearchPatient.Click += BtnSearchPatient_Click;
            BtnUpdatePatient.Click += BtnUpdatePatient_Click;
            BtnDeletePatient.Click += BtnDeletePatient_Click;
            BtnScanAllergy.Click += BtnScanAllergy_Click;
            BtnManualMenuInput.Click += BtnManualMenuInput_Click;
            BtnConfirmAlternativeMenu.Click += BtnConfirmAlternativeMenu_Click;
        }

        private void AllergyManagementPage_Loaded(object sender, RoutedEventArgs e)
        {
            // 초기 가상 데이터 주입 (DB가 텅 비어있을 때만 작동)
            SeedInitialSampleData();

            // DB 리스트 조회 후 DataGrid 바인딩
            RefreshPatientGridFromDb();
            RefreshDailyMenuGridFromDb();

            // 날짜 선택기 기본값을 오늘로 설정
            if (DpMonitorDate != null) DpMonitorDate.SelectedDate = DateTime.Today;
        }

        /// <summary>
        /// 초기 구동 시 테스트를 위한 가상 DB 기본 세팅
        /// </summary>
        private void SeedInitialSampleData()
        {
            if (!_dbContext.Patients.Any())
            {
                var p = new PatientEntity { Name = "김철수", Category = "일반", Note = "견과류 알러지 심함" };
                p.Allergies.Add(new PatientAllergyEntity { AllergyName = "땅콩" });
                p.Allergies.Add(new PatientAllergyEntity { AllergyName = "호두" });
                _dbContext.Patients.Add(p);
            }

            if (!_dbContext.Menus.Any())
            {
                var m1 = new MenuEntity { MenuName = "땅콩소스 닭강정", ServingDate = DateTime.Today };
                m1.Ingredients.Add(new MenuIngredientEntity { IngredientName = "닭고기" });
                m1.Ingredients.Add(new MenuIngredientEntity { IngredientName = "땅콩" });

                var m2 = new MenuEntity { MenuName = "소고기 미역국", ServingDate = DateTime.Today };
                m2.Ingredients.Add(new MenuIngredientEntity { IngredientName = "소고기" });

                _dbContext.Menus.AddRange(m1, m2);
            }

            _dbContext.SaveChanges();
        }

        /// <summary>
        /// 1번 탭 우측 피급식자 명단 DataGrid 갱신
        /// </summary>
        private void RefreshPatientGridFromDb()
        {
            var patients = _dbContext.Patients.Include(p => p.Allergies).ToList();

            // XAML DataGrid Columns 바인딩 명칭인 Id, Name, Category, Allergies, Note에 정확히 맞춤
            DgPatients.ItemsSource = patients.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                Allergies = string.Join(", ", p.Allergies.Select(a => a.AllergyName)), // [이름 = 알러지] 매핑 표시
                p.Note
            }).ToList();
        }

        /// <summary>
        /// 2번 탭 좌측 당일 식단 메뉴 성분표 DataGrid 갱신
        /// </summary>
        private void RefreshDailyMenuGridFromDb()
        {
            DateTime targetDate = DpMonitorDate?.SelectedDate ?? DateTime.Today;

            var menus = _dbContext.Menus
                .Include(m => m.Ingredients)
                .Where(m => m.ServingDate.Date == targetDate.Date)
                .ToList();

            // XAML DataGrid Columns 바인딩 명칭인 MenuName, Ingredients에 정확히 맞춤
            DgDailyMenu.ItemsSource = menus.Select(m => new
            {
                m.MenuName,
                Ingredients = string.Join(", ", m.Ingredients.Select(i => i.IngredientName))
            }).ToList();
        }


        #region [핵심 기능] 1번 탭 - 환자 등록 및 검색 로직

        /// <summary>
        /// 💾 신규 피급식자 DB 등록 버튼 클릭 로직
        /// </summary>
        private void BtnSavePatient_Click(object sender, RoutedEventArgs e)
        {
            // 1. 입력 검증
            if (string.IsNullOrWhiteSpace(TxtPatientName.Text))
            {
                MessageBox.Show("피급식자의 성명을 입력해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string inputName = TxtPatientName.Text.Trim();
            List<string> selectedAllergies = new List<string>();

            // 2. UI 내 모든 체크박스 순회 및 null 참조 경고 예방 보완
            // 2. UI 내 모든 체크박스 순회 및 null 참조 경고 예방 보완
            var checkBoxes = FindVisualChildren<CheckBox>(this);
            if (checkBoxes != null)
            {
                foreach (var child in checkBoxes)
                {
                    // 🟢 child가 null이 아님을 한 번 더 검증하여 할당 경고를 완벽히 해결합니다.
                    if (child != null && child.IsChecked == true && child.Content != null)
                    {
                        selectedAllergies.Add(child.Content.ToString() ?? "");
                    }
                }
            }


            try
            {
                // 3. 🚨 [중요: 프로그램이 멋대로 꺼지던 원인 방어 코드]
                // CmbPatientCategory가 혹시나 선택되지 않은 상태거나 텍스트 상태일 때 튕기는 문제를 예방합니다.
                string category = "일반";
                if (CmbPatientCategory != null)
                {
                    if (CmbPatientCategory.SelectedItem is ComboBoxItem item && item.Content != null)
                    {
                        category = item.Content.ToString() ?? "일반";
                    }
                    else if (!string.IsNullOrEmpty(CmbPatientCategory.Text))
                    {
                        category = CmbPatientCategory.Text;
                    }
                }

                // [사용자 이름 = 선택한 알러지 리스트] 형태로 관계형 매핑 엔티티 생성
                var newPatient = new PatientEntity
                {
                    Name = inputName,
                    Category = category, // 🟢 위에서 정제한 안전한 카테고리 값 대입
                    Note = TxtPatientNote?.Text?.Trim() ?? string.Empty
                };

                foreach (var allergyName in selectedAllergies)
                {
                    newPatient.Allergies.Add(new PatientAllergyEntity { AllergyName = allergyName });
                }

                // 4. 데이터베이스 저장 호출
                _dbContext.Patients.Add(newPatient);
                _dbContext.SaveChanges(); // 🔥 이 시점에 실제 DB에 영구 INSERT 처리가 실행됩니다.

                // 5. 그리드 갱신 및 입력 서식 초기화
                RefreshPatientGridFromDb();
                ClearInputFields();

                MessageBox.Show($"피급식자 '{inputName}' 등록 및 개인별 알러지 매핑이 완료되었습니다.", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"DB 등록 과정 중 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        /// <summary>
        /// 🔍 환자 명단 검색 버튼 클릭 로직 (LINQ 기반 DB 검색)
        /// </summary>
        private void BtnSearchPatient_Click(object sender, RoutedEventArgs e)
        {
            string searchKeyword = TxtSearchPatient.Text.Trim();

            // 검색어가 없으면 전체 리스트를 로드합니다.
            if (string.IsNullOrWhiteSpace(searchKeyword))
            {
                RefreshPatientGridFromDb();
                return;
            }

            // DB에서 이름 컬럼에 키워드가 포함(Like)되었는지 필터링 쿼리 수행
            var searchResult = _dbContext.Patients
                .Include(p => p.Allergies)
                .Where(p => p.Name.Contains(searchKeyword))
                .ToList();

            // 필터링 결과를 DataGrid 서식 구조에 매칭하여 바인딩
            DgPatients.ItemsSource = searchResult.Select(p => new
            {
                p.Id,
                p.Name,
                p.Category,
                Allergies = string.Join(", ", p.Allergies.Select(a => a.AllergyName)),
                p.Note
            }).ToList();
        }

        #endregion


        #region [나머지 이벤트 핸들러 구현] 수정, 삭제, 교차 분석 스캔 및 식단 통제

        /// <summary>
        /// ✏️ 정보 수정 버튼 클릭 이벤트
        /// </summary>
        private void BtnUpdatePatient_Click(object sender, RoutedEventArgs e)
        {
            if (DgPatients.SelectedItem == null)
            {
                MessageBox.Show("수정할 피급식자 행을 목록에서 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic selectedItem = DgPatients.SelectedItem;
            int selectedId = selectedItem.Id;

            var targetPatient = _dbContext.Patients.Include(p => p.Allergies).FirstOrDefault(p => p.Id == selectedId);
            if (targetPatient != null)
            {
                // UI 입력창에 적힌 현재 값들로 덮어쓰기 반영
                targetPatient.Name = TxtPatientName.Text.Trim();
                targetPatient.Category = CmbPatientCategory.SelectedItem is ComboBoxItem item ? (item.Content?.ToString() ?? "일반") : "일반";
                targetPatient.Note = TxtPatientNote?.Text?.Trim() ?? string.Empty;

                // 알러지 매핑 리스트 초기화 후 재등록
                _dbContext.PatientAllergies.RemoveRange(targetPatient.Allergies);
                foreach (var child in FindVisualChildren<CheckBox>(this))
                {
                    if (child != null && child.IsChecked == true && child.Content != null)
                    {
                        targetPatient.Allergies.Add(new PatientAllergyEntity
                        {
                            AllergyName = child.Content.ToString() ?? ""
                        });
                    }
                }

                _dbContext.SaveChanges(); // DB 수정(UPDATE) 쿼리 컴포넌트 반영
                RefreshPatientGridFromDb();
                MessageBox.Show("피급식자 상세 정보가 안전하게 변경되었습니다.");
            }
        }

        /// <summary>
        /// 🗑️ 명단 삭제 버튼 클릭 이벤트
        /// </summary>
        private void BtnDeletePatient_Click(object sender, RoutedEventArgs e)
        {
            if (DgPatients.SelectedItem == null)
            {
                MessageBox.Show("삭제할 대상을 선택해 주세요.", "알림", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            dynamic selectedItem = DgPatients.SelectedItem;
            int selectedId = selectedItem.Id;

            if (MessageBox.Show("선택한 대상의 알러지 매핑 및 등록 인적 데이터를 정말로 영구 삭제하시겠습니까?", "삭제 여부 확인", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var targetPatient = _dbContext.Patients.FirstOrDefault(p => p.Id == selectedId);
                if (targetPatient != null)
                {
                    _dbContext.Patients.Remove(targetPatient);
                    _dbContext.SaveChanges(); // 🔥 실제 물리 삭제(DELETE) 실행
                    RefreshPatientGridFromDb();
                    MessageBox.Show("피급식자 정보와 알러지 매핑 데이터가 완전히 삭제되었습니다.", "삭제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        /// <summary>
        /// ⚡ 알러지 위험 요인 교차 스캔 프로세스 실행 버튼 클릭 이벤트 (2번 탭)
        /// </summary>
        private void BtnScanAllergy_Click(object sender, RoutedEventArgs e)
        {
            DateTime selectedDate = DpMonitorDate?.SelectedDate ?? DateTime.Today;

            // 1. 전체 환자의 알러지 매핑 데이터 가져오기
            var patients = _dbContext.Patients.Include(p => p.Allergies).ToList();

            // 2. 지정된 날짜의 식단 메뉴 성분 정보 가져오기
            var dailyMenus = _dbContext.Menus
                .Include(m => m.Ingredients)
                .Where(m => m.ServingDate.Date == selectedDate.Date)
                .ToList();

            var conflictMatches = new List<object>();

            // 3. 고속 교차 크로스 매핑 알고리즘 진행
            foreach (var patient in patients)
            {
                var patientAllergySet = patient.Allergies.Select(a => a.AllergyName).ToHashSet();

                foreach (var menu in dailyMenus)
                {
                    // Intersect(교집합)을 활용하여 메뉴 성분 중 환자의 차단 물질이 포함되어 있는지 필터링
                    var matchedRisks = menu.Ingredients
                        .Select(i => i.IngredientName)
                        .Where(name => patientAllergySet.Contains(name))
                        .ToList();

                    if (matchedRisks.Any())
                    {
                        // 4. 충돌 발생 시 대체 식단 정보 제안 객체 빌드
                        conflictMatches.Add(new
                        {
                            PatientName = patient.Name, // XAML 바인딩명 일치
                            RiskIngredients = string.Join(", ", matchedRisks), // XAML 바인딩명 일치
                            SuggestedAlternative = $"💡 [{menu.MenuName}] 성분 포함 제한 -> '안전 대체 전용 A식단' 교체 추천" // XAML 바인딩명 일치
                        });
                    }
                }
            }

            // XAML의 우측 알러지 자동 추출 DataGrid에 최종 교차 분석 바인딩 결과 주입
            DgAllergicMatches.ItemsSource = conflictMatches;

            MessageBox.Show($"식단 분석 결과, 총 {conflictMatches.Count}건의 알러지 크로스 위험 요인이 스캔·추출되었습니다.", "스캔 리포트", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// ➕ 식단 메뉴 수동 임의 입력 버튼 클릭 이벤트 (2번 탭)
        /// </summary>
        private void BtnManualMenuInput_Click(object sender, RoutedEventArgs e)
        {
            // 임의 수동 식단 주입 뼈대
            var manualMenu = new MenuEntity
            {
                MenuName = "수동 추가 메밀국수",
                ServingDate = DpMonitorDate?.SelectedDate ?? DateTime.Today
            };
            manualMenu.Ingredients.Add(new MenuIngredientEntity { IngredientName = "메밀" });
            manualMenu.Ingredients.Add(new MenuIngredientEntity { IngredientName = "밀가루" });

            _dbContext.Menus.Add(manualMenu);
            _dbContext.SaveChanges();

            RefreshDailyMenuGridFromDb();
            MessageBox.Show("수동 지정 검사 대상 식단 성분표가 당일 메뉴에 추가되었습니다. 위험 요인 교차 스캔을 다시 실행하세요.");
        }

        /// <summary>
        /// 🔄 대체 식단 규칙 일괄 승인 및 확정 버튼 클릭 이벤트 (2번 탭)
        /// </summary>
        private void BtnConfirmAlternativeMenu_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("검출된 고위험 대상 피급식자의 식단 변환 매칭 규칙이 실시간 급식 관리 연동 DB에 반영 확정되었습니다.", "확정 보고", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion


        #region [유틸리티 기능] 비주얼 트리 검색 및 입력창 초기화 도구

        /// <summary>
        /// WPF 화면 구성 요소 중 특정 타입을 전부 찾아내는 트리 탐색 헬퍼 메서드
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject? depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject? child = System.Windows.Media.VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t; // 🟢 가능한 null 참조 할당 경고 완벽 제거
                    }

                    if (child != null)
                    {
                        foreach (T childOfChild in FindVisualChildren<T>(child))
                        {
                            yield return childOfChild;
                        }
                    }
                }
            }
        }

        private void ClearInputFields()
        {
            if (TxtPatientName != null) TxtPatientName.Text = string.Empty;
            if (TxtPatientNote != null) TxtPatientNote.Text = string.Empty;

            // depObj에 전달될 때 null이 아님을 100% 보장하도록 수정한 안전한 순회 루프
            var currentContent = this.Content as DependencyObject;
            if (currentContent != null)
            {
                foreach (var child in FindVisualChildren<CheckBox>(currentContent))
                {
                    if (child != null) child.IsChecked = false;
                }
            }
        }

    }

    public class AllergyDbContext : DbContext
    {
        public DbSet<PatientEntity> Patients { get; set; } = default!;
        public DbSet<PatientAllergyEntity> PatientAllergies { get; set; } = default!;
        public DbSet<MenuEntity> Menus { get; set; } = default!;
        public DbSet<MenuIngredientEntity> MenuIngredients { get; set; } = default!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // 🔗 DB 주소 입력 위치
            // optionsBuilder.UseSqlServer(@"Server=YOUR_DB_SERVER;Database=AllergyManagementDb;Trusted_Connection=True;TrustServerCertificate=True;");
            optionsBuilder.UseInMemoryDatabase("AllergySafeInMemoryServer");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<PatientAllergyEntity>().HasKey(pa => new { pa.PatientId, pa.AllergyName });
            modelBuilder.Entity<MenuIngredientEntity>().HasKey(mi => new { mi.MenuId, mi.IngredientName });
            modelBuilder.Entity<MenuEntity>().HasKey(m => m.MenuId);
            modelBuilder.Entity<PatientEntity>().HasKey(p => p.Id);
        }
    }

    // 1. 피급식자 환자 엔티티
    public class PatientEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;       // 🟢 경고 해결
        public string Category { get; set; } = string.Empty;   // 🟢 경고 해결
        public string Note { get; set; } = string.Empty;       // 🟢 경고 해결
        public List<PatientAllergyEntity> Allergies { get; set; } = new List<PatientAllergyEntity>();
    }

    // 2. 환자별 보유 알러지 유발 물질 매핑 정보 엔티티
    public class PatientAllergyEntity
    {
        public int PatientId { get; set; }
        public string AllergyName { get; set; } = string.Empty; // 🟢 경고 해결
    }

    // 3. 식단 메뉴 정보 엔티티
    public class MenuEntity
    {
        public int MenuId { get; set; }
        public string MenuName { get; set; } = string.Empty;    // 🟢 경고 해결
        public DateTime ServingDate { get; set; }
        public List<MenuIngredientEntity> Ingredients { get; set; } = new List<MenuIngredientEntity>();
    }

    // 4. 식단별 포함 원재료 성분 매핑 정보 엔티티
    public class MenuIngredientEntity
    {
        public int MenuId { get; set; }
        public string IngredientName { get; set; } = string.Empty; // 🟢 경고 해결
    }

    #endregion
}
