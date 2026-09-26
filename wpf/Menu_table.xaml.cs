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
    private MealTargets? _profileTargets;
    private List<SavedCost> _savedCosts = [];
    private string Age => (CboAgeGroup.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "3-5";
    private string NutritionAge => (CboNutritionAge.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "3-5";
    private string Sex => (CboSex.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "공통";

    public Menu_table(string ageGroup = "", DateTime? selectedDate = null)
    {
        InitializeComponent();
        _service = AppServices.Require();
        WeekPicker.SelectedDate = WeeklyMealPlanner.Monday(selectedDate ?? DateTime.Today);
        CboAgeGroup.SelectedIndex = ageGroup is "6-18" or "6-11" or "12-18" ? 1 : 0;
        CboNutritionAge.SelectedIndex = ageGroup is "6-18" or "6-11" or "12-18" ? 1 : 0;
        CboSex.SelectedIndex = 0;
        foreach (string category in WeeklyMealPlanner.Categories)
        {
            var panel = new StackPanel { Width = 235, Margin = new Thickness(0, 12, 16, 0) };
            panel.Children.Add(new TextBlock { Text = category, Margin = new Thickness(0,0,0,8) });
            var combo = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Key" };
            combo.SelectionChanged += (_, _) => UpdateFixedMenuStatus();
            panel.Children.Add(combo);
            _fixed.Add(category, combo);
            FixedPanel.Children.Add(panel);
        }
        Loaded += async (_, _) => { if (!_ready) { _ready = true; ApplyTrackAgeOptions(); ApplyNutritionProfile(); await Task.WhenAll(ReloadAsync(), LoadWeekAsync()); } };
    }

    private async void AgeChanged(object sender, SelectionChangedEventArgs e)
    {
        ApplyTrackAgeOptions();
        if (!_ready || _busy) return;
        await Task.WhenAll(ReloadAsync(), LoadWeekAsync());
    }

    private void ApplyTrackAgeOptions()
    {
        var options = CboNutritionAge.Items.OfType<ComboBoxItem>().ToList();
        if (Age == "3-5")
        {
            for (int i = 0; i < options.Count; i++) options[i].IsEnabled = i == 0;
            CboNutritionAge.SelectedIndex = 0;
            CboSex.SelectedIndex = 0;
        }
        else
        {
            for (int i = 0; i < options.Count; i++) options[i].IsEnabled = i > 0;
            if (CboNutritionAge.SelectedIndex <= 0) CboNutritionAge.SelectedIndex = 1;
        }
    }

    private void NutritionProfileChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_ready) return;
        ApplyNutritionProfile();
    }

    private void MealCountChanged(object sender, RoutedEventArgs e)
    {
        if (_ready) ApplyNutritionProfile();
    }

    private void ApplyNutritionProfile()
    {
        try
        {
            if (NutritionAge == "3-5") CboSex.SelectedIndex = 0;
            var profile = NutritionProfiles.Get(NutritionAge, Sex);
            // 일주일에 점심만 제공하더라도 영양 목표는 하루 섭취기준의 한 끼 몫이다.
            // 선택한 제공 끼니 수로 나누면 1끼 선택 시 하루 1,600 kcal가 그대로
            // 배정되는 문제가 생기므로, 항상 하루 3끼 기준으로 계산한다.
            const int dailyMealCount = 3;
            _profileTargets = profile.PerMeal(dailyMealCount, WeeklyMealPlanner.Number(TxtTolerance.Text) ?? 15);
            TxtCarb.Text = _profileTargets.Carb.ToString("0.0");
            TxtProtein.Text = _profileTargets.Protein.ToString("0.0");
            TxtFat.Text = _profileTargets.Fat.ToString("0.0");
            NutritionProfileStatus.Text = $"{profile.AgeBand}세 {profile.Sex} · 하루 {profile.DailyCalories:N0} kcal 기준 / 하루 3끼 기준 한 끼 {_profileTargets.Calories:N0} kcal. 탄수 57.5% · 단백질 15% · 지방 22.5%를 자동 적용했습니다.";
            UpdateFixedMenuStatus();
        }
        catch (Exception ex) { NutritionProfileStatus.Text = "영양 기준 설정 실패: " + ex.Message; }
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
            try { _savedCosts = await _service.GetMealCostsAsync(); }
            catch { _savedCosts = []; }
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
        double? calories = _profileTargets?.Calories;
        return new(c.Value, p.Value, f.Value, t.Value, calories);
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
            await LoadWeekAsync();
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
        TxtTargetCost.Text = "5000";
        WeekPicker.SelectedDate = WeeklyMealPlanner.Monday(DateTime.Today);
        foreach (var c in DayPanel.Children.OfType<CheckBox>()) c.IsChecked = c.Tag.ToString() is not ("0" or "6");
        foreach (var c in MealPanel.Children.OfType<CheckBox>()) c.IsChecked = c.Tag.ToString() == "중식";
        foreach (var c in _fixed.Values) c.SelectedIndex = 0;
        ExcludeAllergens.IsChecked = true;
        ApplyNutritionProfile();
    }

    private void UpdateFixedMenuStatus()
    {
        if (FixedMenuStatus == null) return;
        var selected = _fixed.Values.Select(x => x.SelectedItem as TrackMenu).Where(x => x?.Id.Length > 0).Cast<TrackMenu>().ToList();
        if (selected.Count == 0) { FixedMenuStatus.Text = "메뉴를 선택하면 열량·탄수화물·단백질·지방 오차를 표시합니다. 원가는 원가 화면에 저장한 구매 단가 기준으로 계산합니다."; return; }
        var targets = _profileTargets;
        if (targets == null) return;
        double carb=selected.Sum(x=>x.Carb!.Value), protein=selected.Sum(x=>x.Protein!.Value), fat=selected.Sum(x=>x.Fat!.Value), kcal=selected.Sum(x=>x.Energy);
        var costs = selected.Select(m => m.Source.Contains("사용자", StringComparison.OrdinalIgnoreCase) ? (decimal?)null : MealCostData.EstimateMenuCost(m, Age, _savedCosts)).ToList();
        string costText;
        if (costs.Any(c => !c.HasValue)) costText = "원가: 단가 미입력 재료가 있어 계산 대기";
        else if (WeeklyMealPlanner.Number(TxtTargetCost.Text) is double targetCost)
        {
            decimal total = costs.Sum(c => c!.Value);
            costText = $"원가 ₩{total:N0} ({total - (decimal)targetCost:+0;-0;0})";
        }
        else costText = $"원가 ₩{costs.Sum(c => c!.Value):N0}";
        FixedMenuStatus.Text = $"선택 {selected.Count}/6개 · {kcal:N0} kcal ({kcal-targets.Calories!.Value:+0.0;-0.0;0.0}) · 탄수 {carb:N1} g ({carb-targets.Carb:+0.0;-0.0;0.0}) · 단백질 {protein:N1} g ({protein-targets.Protein:+0.0;-0.0;0.0}) · 지방 {fat:N1} g ({fat-targets.Fat:+0.0;-0.0;0.0}) · {costText}";
    }
    private void TargetCostChanged(object sender, TextChangedEventArgs e) => UpdateFixedMenuStatus();

    private int _weekVersion;
    private async void WeekChanged(object? sender, SelectionChangedEventArgs e) { if (_ready && !_busy) await LoadWeekAsync(); }
    private async void LoadWeek_Click(object sender, RoutedEventArgs e) { if (!_busy) await LoadWeekAsync(); }
    private async Task LoadWeekAsync()
    {
        int version=++_weekVersion;
        var week=WeeklyMealPlanner.Monday(WeekPicker.SelectedDate??DateTime.Today);string age=Age;
        WeekTitle.Text=$"{week:M월 d일} ~ {week.AddDays(6):M월 d일} 식단";
        GridDietResult.ItemsSource=null;
        try { using var activity=AppActivity.Begin("저장된 한 주의 식단을 불러오는 중입니다…");var meals=await _service.GetStoredMealsAsync(week,age);if(version!=_weekVersion)return;GridDietResult.ItemsSource=meals; }
        catch(Exception ex) { if(version==_weekVersion)SaveStatus.Text="주간 식단 조회 실패: "+ex.Message; }
    }
    private async void EditMeal_Click(object sender,RoutedEventArgs e)
    {
        if(_busy)return;
        if(GridDietResult.SelectedItem is not StoredMeal meal){SaveStatus.Text="표에서 저장된 끼니를 선택해 주세요.";return;}
        _busy=true;SettingsPanel.IsEnabled=false;
        try {
            using var activity=AppActivity.Begin("수정할 메뉴를 준비하는 중입니다…");
            var catalog=await _service.GetTrackCatalogAsync(meal.AgeGroup.Length==0?Age:meal.AgeGroup);
            var dialog=new MealEditWindow(meal,catalog.Menus){Owner=Window.GetWindow(this)};
            if(dialog.ShowDialog()!=true)return;
            await _service.ChangeStoredMealAsync(meal,dialog.Selection);await LoadWeekAsync();SaveStatus.Text="선택한 끼니를 수정했습니다.";
        } catch(Exception ex){SaveStatus.Text="수정 실패: "+ex.Message;}
        finally{_busy=false;SettingsPanel.IsEnabled=true;}
    }
    private async void DeleteMeal_Click(object sender,RoutedEventArgs e)
    {
        if(_busy)return;
        if(GridDietResult.SelectedItem is not StoredMeal meal){SaveStatus.Text="표에서 삭제할 끼니를 선택해 주세요.";return;}
        if(MessageBox.Show($"{meal} 식단을 삭제할까요?", "식단 삭제",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
        _busy=true;SettingsPanel.IsEnabled=false;
        try{using var activity=AppActivity.Begin("식단을 삭제하는 중입니다…");await _service.ChangeStoredMealAsync(meal,null);await LoadWeekAsync();SaveStatus.Text="선택한 끼니를 삭제했습니다.";}
        catch(Exception ex){SaveStatus.Text="삭제 실패: "+ex.Message;}
        finally{_busy=false;SettingsPanel.IsEnabled=true;}
    }
    private async void DeleteWeekMeals_Click(object sender, RoutedEventArgs e)
    {
        if (_busy) return;
        var week = WeeklyMealPlanner.Monday(WeekPicker.SelectedDate ?? DateTime.Today);
        if (MessageBox.Show($"{week:M월 d일} ~ {week.AddDays(6):M월 d일}에 화면에 표시된 식단을 모두 삭제할까요?", "이번 주 식단 전체 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        _busy = true; SettingsPanel.IsEnabled = false;
        try
        {
            using var activity = AppActivity.Begin("이번 주 식단을 삭제하는 중입니다…");
            int count = await _service.DeleteStoredMealsForWeekAsync(week, Age);
            await LoadWeekAsync();
            SaveStatus.Text = count == 0 ? "삭제할 식단이 없습니다." : $"이번 주 식단 {count}끼를 삭제했습니다.";
        }
        catch (Exception ex) { SaveStatus.Text = "이번 주 식단 삭제 실패: " + ex.Message; }
        finally { _busy = false; SettingsPanel.IsEnabled = true; }
    }
}
