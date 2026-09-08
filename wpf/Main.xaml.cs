using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace wpf
{
    public partial class Main : Window
    {
        private GoogleSheetsService? _sheetsService;
        private readonly string _sheetIdFilePath = "user_sheet_id.txt";

        public Main()
        {
            InitializeComponent();
            this.Loaded += Main_Loaded;
        }

        // 🚀 프로그램 시작 시 실행되는 이벤트 (로그인 및 DB 복사)
        private async void Main_Loaded(object sender, RoutedEventArgs e)
        {
            LoginWindow loginWindow = new LoginWindow();

            if (loginWindow.ShowDialog() == true && loginWindow.UserCredential != null)
            {
                _sheetsService = new GoogleSheetsService(loginWindow.UserCredential);

                if (File.Exists(_sheetIdFilePath))
                {
                    string savedId = File.ReadAllText(_sheetIdFilePath);
                    _sheetsService.SetUserSpreadsheetId(savedId);
                }
                else
                {
                    try
                    {
                        string newId = await _sheetsService.SetupUserDatabaseAsync();
                        File.WriteAllText(_sheetIdFilePath, newId);
                        MessageBox.Show("개인 구글 계정에 전용 엑셀 DB 배포가 완료되었습니다!", "초기화 성공", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"DB 배포 중 오류가 발생했습니다: {ex.Message}", "초기화 실패", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                ResetAllButtonsActive();
                SetButtonActive(BtnDashboard, true);

                await LoadDefaultDashboardAsync();
            }
            else
            {
                MessageBox.Show("프로그램을 사용하려면 구글 로그인이 필요합니다.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
                Application.Current.Shutdown();
            }
        } // 💡 꼬였던 괄호 문제 완벽 해결!

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
            ResetAllButtonsActive();
            SetButtonActive(BtnMenuTable, true);
            MainFrame.Navigate(new Uri("Menu_table.xaml", UriKind.Relative));
        }

        // 🍳 3. 메뉴(레시피) 관리 버튼 클릭 이벤트
        private void BtnMenuManage_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnMenuManage, true);
            // 만약 MenuInputPage가 없다면 임시로 주석 처리하시거나 해당 클래스를 생성하세요
            // MainFrame.Navigate(new MenuInputPage());
        }

        // 🌿 4. 식재료 원천 DB 버튼 클릭 이벤트
        private void BtnIngredientsDb_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnIngredientsDb, true);
            MainFrame.Navigate(new Uri("Ingredients.xaml", UriKind.Relative));
        }

        // 👥 5. 피급식자 알러지 관리 버튼 클릭 이벤트
        private void BtnManagement_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnManagement, true);
            // 만약 AllergyManagementPage가 없다면 임시로 주석 처리하시거나 해당 클래스를 생성하세요
            // MainFrame.Navigate(new AllergyManagementPage());
        }

        // 💵 6. 원가 / 소요량 계산 버튼 클릭 이벤트
        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnCalculate, true);
            try
            {
                MainFrame.Navigate(new Uri("CalculatePage.xaml", UriKind.Relative));
            }
            catch (Exception ex)
            {
                MessageBox.Show("CalculatePage.xaml 페이지를 로드하는 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 📅 7. 이벤트 및 절기 달력 버튼 클릭 이벤트
        private void BtnCalendar_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnCalendar, true);
            try
            {
                MainFrame.Navigate(new Uri("EventCalendar.xaml", UriKind.Relative));
            }
            catch (Exception ex)
            {
                MessageBox.Show("EventCalendar.xaml 페이지를 전환하는 중 오류가 발생했습니다.\n" + ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
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
            // 💡 로그인 안된 상태에서 로드 방지
            if (_sheetsService == null) return;

            DashboardData dbData = await _sheetsService.GetDashboardDataAsync();

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
            string defaultMenuText = (dbData.TodayMenu != null && dbData.TodayMenu.Count > 0) ? string.Join(", ", dbData.TodayMenu) : "조회된 식단 데이터가 없습니다.";
            string altMenuText = (dbData.TodayAlternativeMenu != null && dbData.TodayAlternativeMenu.Count > 0) ? string.Join(", ", dbData.TodayAlternativeMenu) : "조회된 대치 식단 데이터가 없습니다.";
            allergyList.Items.Add(CreateAllergyCard("견과류 알러지", "대상자: 김철수 외 4명", $"기본 메뉴: {defaultMenuText}", $"대치 메뉴: {altMenuText}"));
            allergyList.Items.Add(CreateAllergyCard("갑각류 알러지", "대상자: 이영희 외 2명", $"기본 메뉴: {defaultMenuText}", $"대치 메뉴: {altMenuText}"));
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