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
    /// Main.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class Main : Window
    {
        public Main()
        {
            InitializeComponent();

            // 프로그램이 실행될 때 첫 화면으로 메인 대시보드 UI를 동적으로 로드합니다.
            LoadDefaultDashboard();
        }

        // 🏠 1. 종합 대시보드 홈 버튼 클릭 이벤트
        private void BtnDashboard_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnDashboard, true);

            // 대시보드 화면 새로고침/전환
            LoadDefaultDashboard();
        }

        // ⚙️ 2. 식단 자동 조합 버튼 클릭 이벤트
        private void BtnMenuTable_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnMenuTable, true);

            // Main.xaml에 배치된 Frame(MainFrame)에 Menu_table.xaml 페이지를 주입합니다.
            MainFrame.Navigate(new Uri("Menu_table.xaml", UriKind.Relative));
        }

        // 🍳 3. 메뉴(레시피) 관리 버튼 클릭 이벤트
        private void BtnMenuManage_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnMenuManage, true);

            // 알림 팝업을 지우고, 새로 작성한 메뉴 입력 페이지(Page)를 주입합니다.
            MainFrame.Navigate(new MenuInputPage());
        }

        // 🌿 4. 식재료 원천 DB 버튼 클릭 이벤트
        private void BtnIngredientsDb_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnIngredientsDb, true);

            // MainFrame 영역에 원래 사용하던 Ingredients.xaml 페이지를 주입합니다.
            MainFrame.Navigate(new Uri("Ingredients.xaml", UriKind.Relative));
        }

        // 👥 5. 피급식자 알러지 관리 버튼 클릭 이벤트
        private void BtnManagement_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnManagement, true);

            // MainFrame 영역에 리디자인한 AllergyManagementPage를 생성하여 주입합니다.
            MainFrame.Navigate(new AllergyManagementPage());
        }

        // 💵 6. 원가 / 소요량 계산 버튼 클릭 이벤트
        private void BtnCalculate_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnCalculate, true);

            MessageBox.Show("원가 및 소요량 계산 페이지를 로드합니다. (준비 중)", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // 📅 7. 이벤트 및 절기 달력 버튼 클릭 이벤트 (★EventCalendar 연동 수정 완료★)
        private void BtnCalendar_Click(object sender, RoutedEventArgs e)
        {
            ResetAllButtonsActive();
            SetButtonActive(BtnCalendar, true);

            try
            {
                // 팝업 창을 띄우는 대신, 새로 만든 EventCalendar.xaml 페이지를 프레임에 주입합니다.
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

        /// <summary>
        /// 모든 사이드바 버튼의 스타일을 기본(비활성화) 상태로 일괄 리셋합니다.
        /// </summary>
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

        /// <summary>
        /// 사이드바 내비게이션 버튼의 스타일을 동적으로 변경합니다.
        /// </summary>
        private void SetButtonActive(Button button, bool isActive)
        {
            if (isActive)
            {
                // 선택되었을 때: 연한 파란색 배경 + 진한 청색 글씨
                button.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EBF8FF"));
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2B6CB0"));

                // 버튼 내부 StackPanel 안에 있는 TextBlock들의 폰트를 두껍게 변경
                if (button.Content is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                    {
                        if (child is TextBlock tb) tb.FontWeight = FontWeights.SemiBold;
                    }
                }
            }
            else
            {
                // 해제되었을 때: 투명 배경 + 회색 글씨
                button.Background = Brushes.Transparent;
                button.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#4A5568"));

                // 버튼 내부 StackPanel 안에 있는 TextBlock들의 폰트를 일반 두께로 변경
                if (button.Content is StackPanel sp)
                {
                    foreach (var child in sp.Children)
                    {
                        if (child is TextBlock tb) tb.FontWeight = FontWeights.Normal;
                    }
                }
            }
        }

        /// <summary>
        /// 기존 Main.xaml의 대시보드 레이아웃 구조를 무너뜨리지 않고 Page 형태로 랩핑하여 Frame에 렌더링합니다.
        /// </summary>
        private void LoadDefaultDashboard()
        {
            Page dashboardPage = new Page { Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F5F6FA")) };

            ScrollViewer scrollViewer = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid mainGrid = new Grid { Margin = new Thickness(30) };
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 1. 타이틀 그리드 (대시보드 타이틀 & 날짜 표기)
            Grid titleGrid = new Grid { Margin = new Thickness(0, 0, 0, 25) };
            titleGrid.Children.Add(new TextBlock { Text = "대시보드", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A202C")) });
            titleGrid.Children.Add(new TextBlock { Text = "오늘 날짜: 2026년 5월 24일 (일)", HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, FontSize = 14, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#718096")) });
            Grid.SetRow(titleGrid, 0);
            mainGrid.Children.Add(titleGrid);

            // 2. 상단 요약 현황 카드 그리드 (3개 컬럼 배치)
            Grid cardGrid = new Grid { Margin = new Thickness(0, 0, 0, 25) };
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            cardGrid.Children.Add(CreateWidgetCard("총 관리 피급식자 수", "128 명", "#2D3748", new Thickness(0, 0, 15, 0), 0));
            cardGrid.Children.Add(CreateWidgetCard("주의 필요 알러지 환자", "24 명", "#E53E3E", new Thickness(10, 0, 10, 0), 1));
            cardGrid.Children.Add(CreateWidgetCard("금일 식단 구성 상태", "구성 완료 (대치 3건)", "#3182CE", new Thickness(15, 0, 0, 0), 2, true));
            Grid.SetRow(cardGrid, 1);
            mainGrid.Children.Add(cardGrid);

            // 3. 하단 메인 콘텐츠 그리드 (오늘의 식단 2 : 알러지 대치 3 비율)
            Grid contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });

            // 3-1. ① ① 왼쪽: 오늘의 기본 식단 구성
            Border leftCard = new Border { Background = Brushes.White, CornerRadius = new CornerRadius(10), Padding = new Thickness(25), Margin = new Thickness(0, 0, 15, 0), BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")), BorderThickness = new Thickness(1) };
            Grid leftGrid = new Grid();
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            leftGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            leftGrid.Children.Add(new TextBlock { Text = "🍱 오늘의 기본 식단", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2D3748")), Margin = new Thickness(0, 0, 0, 20) });

            StackPanel menuStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            menuStack.Children.Add(CreateMenuBadge("🍚 밥 : 발아현미밥", "#2D3748"));
            menuStack.Children.Add(CreateMenuBadge("🍖 메인 : 땅콩 소스 닭강정", "#DD6B20"));
            menuStack.Children.Add(CreateMenuBadge("🥗 반찬 : 삼색나물무침", "#2D3748"));
            menuStack.Children.Add(CreateMenuBadge("🍲 국 : 새우 완탕 국", "#DD6B20"));
            menuStack.Children.Add(CreateMenuBadge("🍮 디저트 : 요구르트 푸딩", "#2D3748"));
            Grid.SetRow(menuStack, 1);
            leftGrid.Children.Add(menuStack);
            leftCard.Child = leftGrid;
            Grid.SetColumn(leftCard, 0);
            contentGrid.Children.Add(leftCard);

            // 3-2. ② 오른쪽: 알러지 환자 대치 식단 목록 구성
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
            allergyList.Items.Add(CreateAllergyCard("견과류 알러지", "대상자: 김철수 외 4명", "기본 메뉴: 땅콩 소스 닭강정", "대치 메뉴: 달콤 마늘 닭강정"));
            allergyList.Items.Add(CreateAllergyCard("갑각류 알러지", "대상자: 이영희 외 2명", "기본 메뉴: 새우 완탕 국", "대치 메뉴: 맑은 계란 완탕 국"));
            Grid.SetRow(allergyList, 1);
            rightGrid.Children.Add(allergyList);
            rightCard.Child = rightGrid;
            Grid.SetColumn(rightCard, 1);
            contentGrid.Children.Add(rightCard);

            Grid.SetRow(contentGrid, 2);
            mainGrid.Children.Add(contentGrid);

            scrollViewer.Content = mainGrid;
            dashboardPage.Content = scrollViewer;

            // 최종적으로 완전하게 빌드된 Page 노드를 프레임에 로드합니다.
            MainFrame.Navigate(dashboardPage);
        }

        // [컴포넌트 빌더 내부 함수] 대시보드용 위젯 카드 객체를 코드로 생성합니다.
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

        // [컴포넌트 빌더 내부 함수] 식단표 내부의 행 요소를 연회색 둥근 배지로 빌드합니다.
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

        // [컴포넌트 빌더 내부 함수] 알러지 대치용 ListBox 내부 커스텀 아이템 블록을 생성합니다.
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
    }
}