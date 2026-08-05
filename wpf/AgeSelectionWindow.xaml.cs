using System.Windows;

namespace wpf
{
    /// <summary>
    /// Interaction logic for AgeSelectionWindow.xaml
    /// </summary>
    public partial class AgeSelectionWindow : Window
    {
        public AgeSelectionWindow()
        {
            InitializeComponent();
        }

        private void AgeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn)
            {
                var ageTag = btn.Tag?.ToString() ?? btn.Content?.ToString() ?? string.Empty;
                // 선택한 나이대를 애플리케이션 속성에 저장합니다. 나중에 Main에서 참조 가능
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
