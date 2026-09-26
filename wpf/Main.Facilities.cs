using System.Windows;
using System.Windows.Controls;
namespace wpf;
public partial class Main
{
    private async Task RestoreAccountAsync()
    {
        try {
            var user = await _sheetsService.LoadCurrentUserAsync();
            AccountName.Text = SideAccountName.Text = user.FriendlyName;
            AccountEmail.Text = SideAccountEmail.Text = user.Email;
            SideAccountInitial.Text = user.Initial;
        }
        catch { AccountName.Text = SideAccountName.Text = "로그인됨"; AccountEmail.Text = SideAccountEmail.Text = ""; }
    }
    private Page ShowFacilitySetup()
    {
        var panel = new StackPanel { Margin = new Thickness(32) };
        panel.Children.Add(new TextBlock { Text = "설정해 주세요", FontSize = 28, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = "급식소를 추가하면 식수, 명단, 식단과 일정을 관리할 수 있습니다.", Margin = new Thickness(0,12,0,24), TextWrapping = TextWrapping.Wrap });
        var add = new Button { Content = "급식소 설정", Style = (Style)FindResource("PrimaryButton"), HorizontalAlignment = HorizontalAlignment.Left };
        add.Click += async (_, _) => {
            var initial = (await _sheetsService.GetFacilitiesAsync()).FirstOrDefault(f => !f.Deleted && f.Id == "default" && f.Name == "기본 급식소");
            await EditFacilityAsync(initial);
        };
        panel.Children.Add(add);
        var page = new Page { Content = panel, Background = (System.Windows.Media.Brush)FindResource("Canvas") };
        MainFrame.Navigate(page);
        return page;
    }
    private bool _facilityLoading = true;
    private async Task LoadFacilitiesAsync()
    {
        var all = (await _sheetsService.GetFacilitiesAsync()).Where(f => !f.Deleted && !(f.Id == "default" && f.Name == "기본 급식소")).ToList();
        var current = all.FirstOrDefault(f => f.Id == _sheetsService.CurrentFacility?.Id) ?? all.FirstOrDefault(f => f.Selected) ?? all.FirstOrDefault();
        if (current == null) { _sheetsService = _sheetsService.FacilityRegistry; AppServices.Sheets = _sheetsService; }
        else if (_sheetsService.CurrentFacility == null)
        {
            _sheetsService = _sheetsService.ForFacility(current);
            AppServices.Sheets = _sheetsService;
        }
        FacilityBox.ItemsSource = all;
        FacilityBox.SelectedItem = current;
        FacilityCount.Text = current == null ? "추가 버튼으로 급식소를 등록하세요" : $"선택 급식소 식수: {current.Diners:N0}명";
        FacilityCount.TextWrapping = TextWrapping.Wrap;
        FacilityEditButton.IsEnabled = FacilityDeleteButton.IsEnabled = current != null;
        _facilityLoading = false;
    }
    private async void FacilityChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_facilityLoading || FacilityBox.SelectedItem is not Facility f || f.Id == _sheetsService.CurrentFacility?.Id) return;
        await ChangeFacilityAsync(f, f.Name, f.Diners);
    }
    private async void FacilityAdd_Click(object sender, RoutedEventArgs e) => await EditFacilityAsync(null);
    private async void FacilityEdit_Click(object sender, RoutedEventArgs e) => await EditFacilityAsync(_sheetsService.CurrentFacility);
    private async Task EditDinerCountAsync()
    {
        var facility = _sheetsService.CurrentFacility;
        if (facility == null) return;
        var dialog = new Window { Owner = this, Title = "식수 인원 수정", Width = 400,
            SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = System.Windows.Media.Brushes.White };
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = facility.Name, FontSize = 20, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock { Text = "배식 인원 (명)", Margin = new Thickness(0,16,0,8) });
        var input = new TextBox { Text = facility.Diners.ToString(), FontSize = 18, Padding = new Thickness(12,8,12,8) };
        panel.Children.Add(input);
        var error = new TextBlock { Foreground = System.Windows.Media.Brushes.Firebrick, Margin = new Thickness(0,8,0,0), TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(error);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0,16,0,0) };
        var cancel = new Button { Content = "취소", IsCancel = true, MinWidth = 80, Margin = new Thickness(0,0,8,0) };
        var save = new Button { Content = "저장", IsDefault = true, MinWidth = 80 };
        int count = facility.Diners;
        save.Click += (_, _) => {
            if (!int.TryParse(input.Text.Trim(), out count) || count < 0) { error.Text = "0 이상의 정수를 입력해 주세요."; input.Focus(); return; }
            dialog.DialogResult = true;
        };
        buttons.Children.Add(cancel); buttons.Children.Add(save); panel.Children.Add(buttons);
        dialog.Content = panel;
        dialog.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        if (dialog.ShowDialog() == true && count != facility.Diners)
            await ChangeFacilityAsync(facility, facility.Name, count);
    }
    private async Task EditFacilityAsync(Facility? existing)
    {
        var name = PromptDialog.Show(this,"급식소 " + (existing == null ? "추가" : "수정"), "급식소 이름", existing == null || existing.Name == "기본 급식소" ? null : new[] { existing.Name });
        if (name == null) return;
        var number = PromptDialog.Show(this,"급식소 식수", "배식 인원을 입력해 주세요.", new[] { (existing?.Diners ?? 0).ToString() });
        if (number == null) return;
        if (!int.TryParse(number, out int count) || count < 0) { MessageBox.Show(this,"식수는 0 이상의 정수로 입력해 주세요."); return; }
        await ChangeFacilityAsync(existing,name,count);
    }
    private async void FacilityDelete_Click(object sender, RoutedEventArgs e)
    {
        var f = _sheetsService.CurrentFacility;
        if (f == null) return;
        if (MessageBox.Show(this,$"'{f.Name}'의 식수, 명단, 식단, 일정과 저장된 자료를 모두 삭제합니다.\n삭제 후 앱에서 되돌릴 수 없습니다. 삭제할까요?","급식소 전체 삭제",MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        await ChangeFacilityAsync(f,f.Name,f.Diners,true);
    }
    private async Task ChangeFacilityAsync(Facility? existing,string name,int count,bool delete=false)
    {
        if (AppActivity.Message != null) { MessageBox.Show(this,"진행 중인 작업이 끝난 후 급식소를 변경해 주세요."); return; }
        IsEnabled = false;
        try
        {
            var selected = await _sheetsService.SaveFacilityAsync(existing,name,count,delete);
            var service = selected == null ? _sheetsService.FacilityRegistry : _sheetsService.ForFacility(selected);
            AppServices.Sheets = service;
            var next = new Main(service);
            Application.Current.MainWindow = next;
            next.Show();
            if (delete) MessageBox.Show(next, $"'{name}'의 급식소 정보와 저장된 자료를 삭제했습니다.", "삭제 완료", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex) { MessageBox.Show(this,"급식소 저장 실패: " + ex.Message); }
        finally { IsEnabled = true; }
    }
}
