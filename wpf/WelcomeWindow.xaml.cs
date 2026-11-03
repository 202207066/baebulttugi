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

        private static readonly List<Step> Steps = new()
        {
            new Step { Counter="1 / 3", Title="내 구글 계정으로 시작합니다",
                Body="급식소의 식단과 대상자 정보를 본인의 구글 시트에 저장합니다.\n\n공식 메뉴·식재료 자료는 앱에 포함되어 있습니다. 원본 자료에 대한 공유 승인을 받을 필요가 없습니다.",
                HighlightIcon="🔑", Highlight="구글 로그인은 내 자료를 만들고 읽고 쓰기 위한 절차입니다. 요청함에서는 같은 로그인 계정으로 본인의 요청을 확인합니다." },
            new Step { Counter="2 / 3", Title="내 드라이브에 자료가 생깁니다",
                Body="첫 로그인 시 ‘배불뚝이 · 내 식단 자료’라는 빈 구글 시트를 만듭니다. 급식소를 추가하면 급식소별 시트에 식단과 대상자, 내 레시피가 저장됩니다.\n\n이전에 만든 자료가 있으면 다시 사용합니다. 다른 사람의 이름이나 식단을 복사하지 않습니다.",
                HighlightIcon="📄", Highlight="공식 자료를 업데이트해도 내 레시피 수정 내용과 저장된 식단은 유지됩니다." },
            new Step { Counter="3 / 3", Title="내 자료와 공식 요청은 구분됩니다",
                Body="개인 식단·대상자 명단은 본인의 구글 시트에 저장됩니다. 앱이 사용하도록 허용된 파일을 읽고 씁니다.\n\n‘메뉴·식재료 요청’에서 직접 보내는 제안 내용과 계정 이메일만 관리자 요청함으로 전달됩니다. 대상자의 이름·건강정보는 요청에 적지 마세요.",
                HighlightIcon="🔒", Highlight="공용 PC에서는 사용 후 앱의 ‘로그아웃’을 누르세요. 브라우저의 구글 로그인은 별도로 확인해 주세요." }
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
