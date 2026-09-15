using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace wpf
{
    /// <summary>
    /// 처음 실행하는 사용자에게 "왜 구글 로그인을 하는지", "내 드라이브에 어떤 파일이
    /// 만들어지는지"를 설명하는 3단계 안내 창.
    ///
    /// - 앱을 처음 켤 때 로그인 창보다 먼저 한 번 뜹니다.
    /// - 그 뒤로는 메인 화면의 [도움말] 버튼으로 언제든 다시 볼 수 있습니다.
    /// </summary>
    public partial class WelcomeWindow : Window
    {
        private sealed class Step
        {
            public string Counter = "";
            public string Title = "";
            public string Body = "";
            public string HighlightIcon = "";
            public string Highlight = "";
        }

        private static readonly List<Step> Steps = new List<Step>
        {
            new Step
            {
                Counter = "1 / 3",
                Title = "설치도, 회원가입도 없습니다",
                Body =
                    "이 프로그램은 급식소의 식단과 알러지 정보를 관리합니다.\n\n" +
                    "그런데 그 정보를 보관할 서버가 따로 없습니다. 대신 선생님이 이미 쓰고 계신 " +
                    "구글 계정의 드라이브를 데이터베이스로 사용합니다. 그래서 별도 회원가입 없이 " +
                    "구글 로그인 한 번이면 바로 시작할 수 있습니다.",
                HighlightIcon = "🔑",
                Highlight =
                    "구글 로그인은 신원 확인용이 아니라, 선생님의 드라이브에 " +
                    "데이터 파일을 만들고 읽기 위한 권한을 받는 절차입니다."
            },
            new Step
            {
                Counter = "2 / 3",
                Title = "내 드라이브에 전용 DB가 만들어집니다",
                Body =
                    "처음 로그인하면 팀이 준비한 스프레드시트 템플릿이 선생님의 구글 드라이브로 " +
                    "복사됩니다. 파일 이름은 «나만의 스마트 식단 및 알러지 DB» 입니다.\n\n" +
                    "이 파일 안에 메뉴, 식재료, 피급식자 알러지 정보, 만들어진 식단이 모두 " +
                    "기록됩니다. 프로그램을 꺼도 구글 시트에서 직접 열어 확인하고 수정할 수 있습니다.",
                HighlightIcon = "📄",
                Highlight =
                    "복사는 최초 1회만 일어납니다. 두 번째 실행부터는 이미 만들어진 " +
                    "그 파일을 계속 사용합니다."
            },
            new Step
            {
                Counter = "3 / 3",
                Title = "데이터는 선생님 계정 안에만 있습니다",
                Body =
                    "만들어진 스프레드시트의 주인은 선생님입니다. 팀이나 제3자가 그 파일을 " +
                    "들여다볼 수 없고, 프로그램도 그 파일 하나만 읽고 씁니다.\n\n" +
                    "로그인 기록은 이 컴퓨터 안에만 저장되며, 권한을 취소하고 싶으면 구글 계정 " +
                    "설정의 «보안 → 타사 앱 및 서비스» 에서 언제든 연결을 해제할 수 있습니다.",
                HighlightIcon = "🔒",
                Highlight =
                    "공용 PC를 쓰신다면 사용 후 실행 폴더의 token.json 폴더를 지우면 " +
                    "로그인 기록이 남지 않습니다."
            }
        };

        private int _index;

        /// <summary>안내를 끝까지 봤거나 건너뛰었으면 true.</summary>
        public bool Completed { get; private set; }

        public WelcomeWindow()
        {
            InitializeComponent();
            Render();
        }

        private void Render()
        {
            Step step = Steps[_index];

            TxtStepCounter.Text = step.Counter;
            TxtStepTitle.Text = step.Title;
            TxtStepBody.Text = step.Body;
            TxtHighlightIcon.Text = step.HighlightIcon;
            TxtHighlight.Text = step.Highlight;

            // 왼쪽 단계 표시 갱신
            var active = Brushes.White;
            var inactiveDot = new SolidColorBrush(Color.FromRgb(0x6E, 0x9A, 0x72));
            var inactiveText = new SolidColorBrush(Color.FromRgb(0xBF, 0xE0, 0xC2));

            Dot1.Fill = _index == 0 ? active : inactiveDot;
            Dot2.Fill = _index == 1 ? active : inactiveDot;
            Dot3.Fill = _index == 2 ? active : inactiveDot;

            StepLabel1.Foreground = _index == 0 ? active : inactiveText;
            StepLabel2.Foreground = _index == 1 ? active : inactiveText;
            StepLabel3.Foreground = _index == 2 ? active : inactiveText;

            BtnPrev.Visibility = _index == 0 ? Visibility.Hidden : Visibility.Visible;
            BtnNext.Content = _index == Steps.Count - 1 ? "시작하기" : "다음";
            BtnSkip.Visibility = _index == Steps.Count - 1 ? Visibility.Hidden : Visibility.Visible;
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            if (_index > 0)
            {
                _index--;
                Render();
            }
        }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            if (_index < Steps.Count - 1)
            {
                _index++;
                Render();
                return;
            }

            Finish();
        }

        private void BtnSkip_Click(object sender, RoutedEventArgs e) => Finish();

        private void Finish()
        {
            Completed = true;
            UserPrefs.HasSeenTutorial = true;

            // 도움말로 다시 열었을 때는 DialogResult를 설정할 수 없으므로
            // ShowDialog로 띄운 경우에만 지정합니다.
            try
            {
                DialogResult = true;
            }
            catch (System.InvalidOperationException)
            {
                // Show()로 띄운 경우 - 그냥 닫습니다.
            }

            Close();
        }
    }
}
