using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace wpf
{
    public partial class Main : Window
    {
        private readonly GoogleSheetsService _sheetsService;
        private readonly string _ageGroup;

        /// <summary>
        /// 로그인과 개인 DB 준비는 App.xaml.cs에서 이미 끝난 상태로 들어옵니다.
        /// 여기서 다시 로그인 창을 띄우지 않습니다(예전에는 인증 창이 두 번 떴습니다).
        /// </summary>
        public Main(GoogleSheetsService sheetsService, string ageGroup = "")
        {
            InitializeComponent();

            _sheetsService = sheetsService ?? throw new ArgumentNullException(nameof(sheetsService));
            _ageGroup = ageGroup ?? string.Empty;

            this.Loaded += Main_Loaded;
        }

        private async void Main_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                ResetAllButtonsActive();
                SetButtonActive(BtnDashboard, true);

                await LoadDefaultDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("대시보드를 불러오는 중 오류가 발생했습니다:\n" + ex.Message,
                                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 🏠 1. 종합 대시보드 홈 버튼 클릭 이벤트
        private async void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnDashboard, true);
            await LoadDefaultDashboardAsync();
        }

        // ⚙️ 2. 식단 자동 조합 버튼 클릭 이벤트
        private void BtnMenuTable_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(BtnMenuTable, () => new Menu_table(_ageGroup), "식단 자동 조합");
        }

        // 🍳 3. 메뉴(레시피) 관리 버튼 클릭 이벤트
        private void BtnMenuManage_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(BtnMenuManage, () => new MenuInputPage(), "메뉴(레시피) 관리");
        }

        // 🌿 4. 식재료 원천 DB 버튼 클릭 이벤트
        private void BtnIngredientsDb_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(BtnIngredientsDb, () => new Ingredients(), "식재료 원천 DB");
        }

        // 👥 5. 피급식자 알러지 관리 버튼 클릭 이벤트
        private void BtnManagement_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(BtnManagement, () => new AllergyManagementPage(), "피급식자 알러지 관리");
        }

        // 💵 6. 원가 / 소요량 계산 버튼 클릭 이벤트
        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(BtnCalculate, () => new CalculatePage(), "원가 / 소요량 계산");
        }

        // 📅 7. 이벤트 및 절기 달력 버튼 클릭 이벤트
        private void BtnCalendar_Click(object sender, RoutedEventArgs e)
        {
            NavigateTo(BtnCalendar, () => new EventCalendar(), "이벤트 및 절기 달력");
        }

        /// <summary>
        /// 사이드바 버튼 상태를 정리하고 페이지를 띄웁니다.
        ///
        /// Uri 대신 인스턴스를 만들어 Navigate합니다. 이렇게 해야 나이대 같은 값을
        /// 페이지에 넘길 수 있고, 페이지 생성자에서 난 예외도 여기서 잡힙니다.
        /// </summary>
        private void NavigateTo(Button sourceButton, Func<Page> pageFactory, string pageTitle)
        {
            ResetAllButtonsActive();
            SetButtonActive(sourceButton, true);

            try
            {
                MainFrame.Navigate(pageFactory());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"[{pageTitle}] 화면을 여는 중 오류가 발생했습니다.\n\n{ex.Message}",
                                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────
        // 헬퍼 함수: 버튼 스타일 제어 및 대시보드 빌더
        // ──────────────────────────────────────────

        private void ResetAllButtonsActive()
        {
            SetButtonActive(BtnDashboard, false);
            SetButtonActive(BtnMenuTable, false);
            SetButtonActive(BtnMenuManage, false);
            SetButtonActive(BtnIngredientsDb, false);
            SetButtonActive(BtnManagement, false);
            SetButtonActive(BtnCalculate, false);
            SetButtonActive(BtnCalendar, false);
        }

        private void SetButtonActive(Button button, bool isActive)
        {
            if (isActive)
            {
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CB0"));
                if (button.Content is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                        if (child is TextBlock tb) tb.FontWeight = FontWeights.SemiBold;
                }
            }
            else
            {
                button.Background = Brushes.Transparent;
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));
                if (button.Content is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                        if (child is TextBlock tb) tb.FontWeight = FontWeights.Normal;
                }
            }
        }

        private async Task LoadDefaultDashboardAsync()
        {
            DashboardData dbData = await _sheetsService.GetDashboardDataAsync();

            // 인원 수와 알러지 현황은 Dashboard 시트의 요약값 대신
            // 알러지 명단 시트를 직접 집계해 실제 데이터와 어긋나지 않게 합니다.
            List<PatientModel> patients;
            List<(string Allergen, List<string> Names)> allergyGroups;
            try
            {
                patients = await _sheetsService.GetPatientsAsync();
                allergyGroups = await _sheetsService.GetAllergyGroupsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[대시보드 명단 집계 실패] {ex.Message}");
                patients = new List<PatientModel>();
                allergyGroups = new List<(string, List<string>)>();
            }

            if (patients.Count > 0)
            {
                int allergyPatientCount = patients.Count(
                    p => GoogleSheetsService.SplitAllergens(p.Allergies).Any());

                dbData.TotalPatients = $"{patients.Count} 명";
                dbData.AllergyPatients = $"{allergyPatientCount} 명";
            }

            Page dashboardPage = new Page { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F6FA")) };
            ScrollViewer scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid mainGrid = new Grid { Margin = new Thickness(30) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid titleGrid = new Grid { Margin = new Thickness(0, 0, 0, 25) };
            titleGrid.Children.Add(new TextBlock { Text = "대시보드", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A202C")) });

            string todayText = DateTime.Now.ToString("yyyy년 MM월 dd일 (ddd)");
            titleGrid.Children.Add(new TextBlock { Text = $"오늘 날짜: {todayText}", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, FontSize = 14, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")) });
            Grid.SetRow(titleGrid, 0);
            mainGrid.Children.Add(titleGrid);

            Grid cardGrid = new Grid { Margin = new Thickness(0, 0, 0, 25) };
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            cardGrid.Children.Add(CreateWidgetCard("총 관리 피급식자 수", dbData.TotalPatients, "#2D3748", new Thickness(0, 0, 15, 0), 0));
            cardGrid.Children.Add(CreateWidgetCard("주의 필요 알러지 환자", dbData.AllergyPatients, "#E53E3E", new Thickness(10, 0, 10, 0), 1));
            cardGrid.Children.Add(CreateWidgetCard("금일 식단 구성 상태", dbData.DietStatus, "#3182CE", new Thickness(15, 0, 0, 0), 2, true));
            Grid.SetRow(cardGrid, 1);
            mainGrid.Children.Add(cardGrid);

            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

            Border leftCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(25), Margin = new Thickness(0, 0, 15, 0), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")), BorderThickness = new Thickness(1) };
            Grid leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftGrid.Children.Add(new TextBlock { Text = "🍱 오늘의 기본 식단", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748")), Margin = new Thickness(0, 0, 0, 20) });

            StackPanel menuStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            if (dbData.TodayMenu != null && dbData.TodayMenu.Count > 0)
            {
                foreach (string menu in dbData.TodayMenu)
                {
                    string colorHex = (menu.Contains("닭") || menu.Contains("고기") || menu.Contains("새우") || menu.Contains("탕")) ? "#DD6B20" : "#2D3748";
                    menuStack.Children.Add(CreateMenuBadge(menu, colorHex));
                }
            }
            else
            {
                menuStack.Children.Add(CreateMenuBadge("조회된 식단 데이터가 없습니다.", "#A0AEC0"));
            }

            Grid.SetRow(menuStack, 1);
            leftGrid.Children.Add(menuStack);
            leftCard.Child = leftGrid;
            Grid.SetColumn(leftCard, 0);
            contentGrid.Children.Add(leftCard);

            Border rightCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(25), Margin = new Thickness(15, 0, 0, 0), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")), BorderThickness = new Thickness(1) };
            Grid rightGrid = new Grid();
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            rightGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel rightTitleStack = new StackPanel { Margin = new Thickness(0, 0, 0, 15) };
            rightTitleStack.Children.Add(new TextBlock { Text = "⚠️ 알러지 환자별 자동 대치 식단", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748")) });
            rightTitleStack.Children.Add(new TextBlock { Text = "시스템 규칙 및 구글 DB 기반으로 위험 요소를 필터링한 결과입니다.", FontSize = 12, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AEC0")), Margin = new Thickness(0, 4, 0, 0) });
            rightGrid.Children.Add(rightTitleStack);

            ListBox allergyList = new ListBox { BorderThickness = new Thickness(0), Background = Brushes.Transparent };
            ScrollViewer.SetHorizontalScrollBarVisibility(allergyList, ScrollBarVisibility.Disabled);

            string defaultMenuText = (dbData.TodayMenu != null && dbData.TodayMenu.Count > 0)
                ? string.Join(", ", dbData.TodayMenu) : "조회된 식단 데이터가 없습니다.";
            string altMenuText = (dbData.TodayAlternativeMenu != null && dbData.TodayAlternativeMenu.Count > 0)
                ? string.Join(", ", dbData.TodayAlternativeMenu) : "조회된 대치 식단 데이터가 없습니다.";

            // 예전에는 "김철수 외 4명", "이영희 외 2명"이 코드에 박혀 있어
            // 실제 등록 인원과 아무 관계가 없었습니다. 명단에서 집계합니다.
            if (allergyGroups.Count > 0)
            {
                foreach (var (allergen, names) in allergyGroups)
                {
                    allergyList.Items.Add(CreateAllergyCard(
                        $"{allergen} 알러지",
                        "대상자: " + FormatNames(names),
                        $"기본 메뉴: {defaultMenuText}",
                        $"대치 메뉴: {altMenuText}"));
                }
            }
            else
            {
                allergyList.Items.Add(CreateAllergyCard(
                    "등록된 알러지 없음",
                    "‘피급식자 알러지 관리’ 화면에서 대상자를 등록해 주세요.",
                    $"기본 메뉴: {defaultMenuText}",
                    "대치 메뉴: 해당 없음"));
            }

            Grid.SetRow(allergyList, 1);
            rightGrid.Children.Add(allergyList);
            rightCard.Child = rightGrid;
            Grid.SetColumn(rightCard, 1);
            contentGrid.Children.Add(rightCard);

            Grid.SetRow(contentGrid, 2);
            mainGrid.Children.Add(contentGrid);

            scrollViewer.Content = mainGrid;
            dashboardPage.Content = scrollViewer;

            MainFrame.Navigate(dashboardPage);
        }

        /// <summary>"김철수, 이영희 외 3명" 형태로 줄여 표시합니다.</summary>
        private static string FormatNames(List<string> names)
        {
            if (names == null || names.Count == 0) return "없음";
            if (names.Count <= 2) return string.Join(", ", names);

            return $"{names[0]}, {names[1]} 외 {names.Count - 2}명";
        }

        private Border CreateWidgetCard(string title, string value, string colorHex, Thickness margin, int column, bool isCompact = false)
        {
            Border card = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(20), Margin = margin, BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")), BorderThickness = new Thickness(1) };
            StackPanel stack = new StackPanel();
            stack.Children.Add(new TextBlock { Text = title, FontSize = 14, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")) });
            stack.Children.Add(new TextBlock { Text = value, FontSize = isCompact ? 24 : 28, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)), Margin = new Thickness(0, isCompact ? 13 : 10, 0, 0) });
            card.Child = stack;
            Grid.SetColumn(card, column);
            return card;
        }

        private Border CreateMenuBadge(string text, string colorHex)
        {
            return new Border
            {
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F7FAFC")),
                Padding = new Thickness(15),
                Margin = new Thickness(0, 5, 0, 5),
                CornerRadius = new CornerRadius(5),
                Child = new TextBlock { Text = text, FontSize = 15, FontWeight = FontWeights.Medium, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex)) }
            };
        }

        private ListBoxItem CreateAllergyCard(string label, string targetedUsers, string defaultMenu, string alternativeMenu)
        {
            ListBoxItem item = new ListBoxItem { Margin = new Thickness(0, 0, 0, 12), Padding = new Thickness(0) };
            Border border = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5F5")), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FED7D7")), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(15), Width = 580 };
            StackPanel mainStack = new StackPanel();

            StackPanel headerStack = new StackPanel { Orientation = Orientation.Horizontal };
            Border labelTag = new Border { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32")), CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 2, 6, 2), Child = new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.White } };
            headerStack.Children.Add(labelTag);
            headerStack.Children.Add(new TextBlock { Text = targetedUsers, FontSize = 13, FontWeight = FontWeights.Bold, Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center });
            mainStack.Children.Add(headerStack);

            StackPanel detailsStack = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
            detailsStack.Children.Add(new TextBlock { Text = defaultMenu, FontSize = 13, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")), TextDecorations = TextDecorations.Strikethrough });
            detailsStack.Children.Add(new TextBlock { Text = " ▶ ", FontSize = 13, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#A0AEC0")), Margin = new Thickness(10, 0, 10, 0) });
            detailsStack.Children.Add(new TextBlock { Text = alternativeMenu, FontSize = 13, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#38A169")) });
            mainStack.Children.Add(detailsStack);

            border.Child = mainStack;
            item.Content = border;
            return item;
        }

        private void MainFrame_Navigated(object sender, System.Windows.Navigation.NavigationEventArgs e)
        {
        }
    }
}