using System.Windows;
using System.Windows.Controls;

namespace wpf;

public partial class Menu_table : Page
{
    private readonly GoogleSheetsService _service;
    private readonly Dictionary<string, ComboBox> _fixed = new();
    private TrackCatalog? _catalog;
    private List<WeeklyMeal> _results = [];
    private bool _ready;
    private bool _busy;
    private int _loadVersion;
    private string Age => (CboAgeGroup.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "3-5";

    public Menu_table(string ageGroup = "", DateTime? selectedDate = null)
    {
        InitializeComponent();
        _service = AppServices.Require();
        WeekPicker.SelectedDate = WeeklyMealPlanner.Monday(selectedDate ?? DateTime.Today);
        CboAgeGroup.SelectedIndex = ageGroup is "6-18" or "6-11" or "12-18" ? 1 : 0;
        foreach (string category in WeeklyMealPlanner.Categories)
        {
            var panel = new StackPanel { Width = 235, Margin = new Thickness(0, 12, 16, 0) };
            panel.Children.Add(new TextBlock { Text = category, Margin = new Thickness(0,0,0,8) });
            var combo = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Key" };
            panel.Children.Add(combo);
            _fixed.Add(category, combo);
            FixedPanel.Children.Add(panel);
        }
        Loaded += async (_, _) => { if (!_ready) { _ready = true; await ReloadAsync(); } };
    }

    private async void AgeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready || _busy) return;
        await ReloadAsync();
    }

    private async Task ReloadAsync(bool forceRefresh = false)
    {
        using var activity = AppActivity.Begin("선택한 연령의 메뉴를 불러오는 중입니다…");
        int version = ++_loadVersion;
        _catalog = null;
        foreach (var box in _fixed.Values) box.ItemsSource = null;
        DataStatus.Text = "트랙 메뉴를 불러오는 중입니다…";
        try
        {
            var catalog = await _service.GetTrackCatalogAsync(Age, forceRefresh);
            if (version != _loadVersion) return;
            _catalog = catalog;
            foreach (var pair in _fixed)
            {
                pair.Value.ItemsSource = new[] { new TrackMenu("", "프로그램이 골라주기", pair.Key, "", null, null, null, null, "") }
                    .Concat(catalog.Menus.Where(m => m.Category == pair.Key && m.HasNutrition)).ToList();
                pair.Value.SelectedIndex = 0;
            }
            ShowCatalog(catalog);
        }
        catch (Exception ex) { if (version == _loadVersion) DataStatus.Text = "메뉴 불러오기 실패: " + ex.Message; }
    }

    private void ShowCatalog(TrackCatalog catalog)
    {
        int ready=catalog.Menus.Count(m=>m.HasNutrition);
        DataStatus.Text = $"{catalog.Title} · 메뉴 {catalog.Menus.Count:N0}개 · 영양정보 등록 {ready:N0}개" +
            (ready==0 ? "\n아직 메뉴별 영양정보가 준비되지 않았습니다. 영양정보 등록 후 식단을 만들 수 있어요." : "\n등록된 1인분 영양정보로 조합하고, 완성된 식단은 내 구글 시트에 저장합니다.");
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await ReloadAsync(true);

    private MealTargets ReadTargets()
    {
        var c = WeeklyMealPlanner.Number(TxtCarb.Text);
        var p = WeeklyMealPlanner.Number(TxtProtein.Text);
        var f = WeeklyMealPlanner.Number(TxtFat.Text);
        var t = WeeklyMealPlanner.Number(TxtTolerance.Text);
        if (c is null or <= 0 || p is null or <= 0 || f is null or <= 0 || t is null or > 100)
            throw new InvalidOperationException("탄수화물·단백질·지방은 0보다 큰 수, 허용 편차는 0~100%로 입력해 주세요.");
        return new(c.Value, p.Value, f.Value, t.Value);
    }

    private async void Execute_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        _busy = true;
        using var activity = AppActivity.Begin("식단을 만들고 내 자료에 저장하는 중입니다…");
        SettingsPanel.IsEnabled = false;
        RetrySave.Visibility = Visibility.Collapsed;
        try
        {
            var targets = ReadTargets();
            var days = DayPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => (DayOfWeek)int.Parse(c.Tag.ToString()!)).ToArray();
            var meals = MealPanel.Children.OfType<CheckBox>().Where(c => c.IsChecked == true).Select(c => c.Tag.ToString()!).ToArray();
            if (days.Length == 0 || meals.Length == 0) throw new InvalidOperationException("급식 요일과 끼니를 한 개 이상 선택해 주세요.");
            if (!WeekPicker.SelectedDate.HasValue) throw new InvalidOperationException("대상 주의 날짜를 선택해 주세요.");
            var week = WeeklyMealPlanner.Monday(WeekPicker.SelectedDate.Value);
            WeekPicker.SelectedDate = week;
            string age = Age;
            var fixedIds = _fixed.Where(p => p.Value.SelectedItem is TrackMenu m && m.Id.Length > 0)
                .ToDictionary(p => p.Key, p => ((TrackMenu)p.Value.SelectedItem).Key);
            SaveStatus.Text = "최신 메뉴와 영양정보를 확인하는 중입니다…";
            var catalog = await _service.GetTrackCatalogAsync(age);
            _catalog = catalog;
            ShowCatalog(catalog);
            var allergens = ExcludeAllergens.IsChecked == true ? await _service.GetPlannerAllergensAsync() : new HashSet<string>();
            var eligible = catalog.Menus.Where(m => !GoogleSheetsService.MatchesRegisteredAllergen(m, allergens)).ToList();
            SaveStatus.Text = "주간 식단을 조합하는 중입니다…";
            var generated = await Task.Run(() => WeeklyMealPlanner.Generate(eligible, week, days, meals, age, catalog.Title, targets, fixedIds));
            _results = generated;
            GridDietResult.ItemsSource = _results;
            await SaveAsync();
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "새 식단을 생성하지 못했습니다. " + ex.Message;
        }
        finally { _busy = false; SettingsPanel.IsEnabled = true; }
    }

    private async Task SaveAsync()
    {
        SaveStatus.Text = "개인 구글 시트에 저장하는 중입니다…";
        try
        {
            await _service.SaveWeeklyMealsAsync(_results);
            int outside = _results.Count(m => !m.WithinTolerance);
            SaveStatus.Text = $"'{AppConfig.WeeklyMenuSheetName}'에 {_results.Count}끼 저장 완료. 목표 허용범위 밖 {outside}끼." +
                (outside > 0 ? " 후보 메뉴로 목표를 충족하지 못한 끼니의 편차를 확인해 주세요." : "");
            RetrySave.Visibility = Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            SaveStatus.Text = "식단 생성 완료 / 저장 확인 실패: " + ex.Message + "\n같은 결과로 재시도하면 식단 ID를 확인해 중복 저장을 피합니다.";
            RetrySave.Visibility = Visibility.Visible;
        }
    }

    private async void RetrySave_Click(object sender, RoutedEventArgs e)
    {
        if (_busy || _results.Count == 0) return;
        _busy = true; SettingsPanel.IsEnabled = false; RetrySave.IsEnabled = false;
        try { await SaveAsync(); }
        finally { _busy = false; SettingsPanel.IsEnabled = true; RetrySave.IsEnabled = true; }
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        TxtCarb.Clear(); TxtProtein.Clear(); TxtFat.Clear(); TxtTolerance.Text = "15";
        WeekPicker.SelectedDate = WeeklyMealPlanner.Monday(DateTime.Today);
        foreach (var c in DayPanel.Children.OfType<CheckBox>()) c.IsChecked = c.Tag.ToString() is not ("0" or "6");
        foreach (var c in MealPanel.Children.OfType<CheckBox>()) c.IsChecked = c.Tag.ToString() == "중식";
        foreach (var c in _fixed.Values) c.SelectedIndex = 0;
        ExcludeAllergens.IsChecked = true;
    }
}
