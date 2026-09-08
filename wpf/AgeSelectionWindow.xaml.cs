using System.Windows;

namespace wpf
{
    /// <summary>
    /// Interaction logic for AgeSelectionWindow.xaml
    /// </summary>
    public partial class AgeSelectionWindow : Window
    {
        /// <summary>
        /// 선택된 나이대. AgeSelectionWindow.xaml의 Button Tag 값과 같습니다.
        /// ("3-5", "6-11", "12-18", "adult")
        ///
        /// 예전에는 Application.Current.Properties에 넣어 두기만 하고
        /// 아무 화면도 읽지 않아 선택이 버려졌습니다.
        /// 지금은 App이 이 값을 Main → 식단 자동 조합 화면까지 전달합니다.
        /// </summary>
        public static string SelectedAgeGroup { get; private set; } = string.Empty;

        public AgeSelectionWindow()
        {
            InitializeComponent();
        }

        private void AgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                var ageTag = btn.Tag?.ToString() ?? btn.Content?.ToString() ?? string.Empty;

                SelectedAgeGroup = ageTag;
                Application.Current.Properties["SelectedAgeGroup"] = ageTag;

                this.DialogResult = true;
                this.Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
