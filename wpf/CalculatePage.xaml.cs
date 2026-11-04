using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
namespace wpf;
public partial class CalculatePage : Page
{
    private readonly GoogleSheetsService service=AppServices.Require();
    private List<SavedCost> costs=[];
    private readonly Dictionary<string,HashSet<string>> personalKeys=new();
    private ObservableCollection<CostDraft> drafts=new();
    private List<IngredientCatalogItem> ingredientCatalog=[];
    private List<IngredientItem> result=[];
    private List<SavedMealCalculation> savedCalculations=[];
    private MealCostCalculation? calculation;
    private string savedReadError="";
    private bool busy;
    private readonly Dictionary<string,string[]> displayMenus = new();
    public CalculatePage(){InitializeComponent();Loaded+=async(_,_)=>await Load();}
    private void ClearResult(){result=[];calculation=null;if(dgCalculationResult==null)return;dgCalculationResult.ItemsSource=null;txtTotalCount.Text="재료 —";txtCostPerPerson.Text="1인당 —";txtTotalCost.Text="총 원가 —";UpdateSavedStatus();}
    private void QueueCalculate()=>Dispatcher.BeginInvoke(new Action(()=>{if(!busy)Calculate(false);}));
    private void InputsChanged(object sender,TextChangedEventArgs e){ClearResult();if(IsInitialized)QueueCalculate();}
    private void CostEdited(object sender,DataGridCellEditEndingEventArgs e){ClearResult();QueueCalculate();}
    private async void Refresh_Click(object sender,RoutedEventArgs e){if(!busy)await Load(true);}
    private async Task<bool> Load(bool keepSelection=false)
    {
        string? selected=keepSelection?(cbTargetMenu.SelectedItem as StoredMeal)?.Identity:null;
        string head=keepSelection?txtHeadCount.Text:"";
        string loadError="";
        busy=true;EditorRoot.IsEnabled=false;ClearResult();
        using var activity=AppActivity.Begin("저장된 식단과 원가 자료를 불러오는 중입니다…");
        try {
            var meals=await service.GetStoredMealsAsync();
            displayMenus.Clear(); foreach(var m in await service.DisplayMealsAsync(meals)) if(m.Stored != null) displayMenus[m.Stored.Identity] = m.Menus;
            costs=await service.GetMealCostsAsync(true);
            savedReadError="";
            try{savedCalculations=await service.GetSavedMealCalculationsAsync();}catch(Exception ex){savedCalculations=[];savedReadError=ex.Message;}
            personalKeys.Clear();
            foreach(string age in meals.Select(m=>m.AgeGroup.Length==0?"3-5":m.AgeGroup).Distinct())personalKeys[age]=(await service.ReadPersonalRecipesAsync(age)).Select(r=>r.Menu.Key).ToHashSet();
            txtHeadCount.Text=head.Length>0?head:(service.CurrentFacility?.Diners ?? (await service.GetDinersAsync()).Count).ToString();
            cbTargetMenu.ItemsSource=meals.OrderByDescending(m=>m.Date).ToList();
            var monday=WeeklyMealPlanner.Monday(DateTime.Today);
            cbTargetMenu.SelectedItem=meals.FirstOrDefault(m=>m.Identity==selected)??meals.FirstOrDefault(m=>m.Date>=monday&&m.Date<monday.AddDays(7))??meals.LastOrDefault();
            if(meals.Count==0)TxtDataStatus.Text="저장된 식단이 없습니다. 식단 만들기에서 먼저 식단을 저장해 주세요.";
        }catch(Exception ex){ if(cbTargetMenu.Items.Count>0 && cbTargetMenu.SelectedItem==null)cbTargetMenu.SelectedIndex=0; loadError="일부 자료 조회 실패: "+ex.Message; }
        finally{busy=false;EditorRoot.IsEnabled=true;Calculate(false);if(loadError.Length>0)TxtDataStatus.Text=loadError;}
        return loadError.Length==0;
    }
    private void MenuChanged(object sender,SelectionChangedEventArgs e)
    {
        ClearResult();
        if(cbTargetMenu.SelectedItem is not StoredMeal meal){CostInputs.ItemsSource=null;return;}
        try{
            drafts=new(MealCostData.Build(meal,costs,personalKeys.GetValueOrDefault(meal.AgeGroup.Length==0?"3-5":meal.AgeGroup)));
            var labels = displayMenus.GetValueOrDefault(meal.Identity);
            foreach(var draft in drafts) { int index = Array.IndexOf(meal.Menus, draft.Menu); if(labels != null && index >= 0) draft.MenuLabel = labels[index]; }
            drafts.CollectionChanged+=(_,_)=>{ClearResult();QueueCalculate();};CostInputs.Tag=meal.Menus.Where(n=>n.Length>0).Distinct().ToArray();CostInputs.ItemsSource=drafts;
            MealSummary.Text=string.Join(" · ",displayMenus.GetValueOrDefault(meal.Identity, meal.Menus.Select(n=>MealPresentation.Label(n,null)).ToArray()).Where(n=>n.Length>0));
            TxtDataStatus.Text=$"재료 {drafts.Count}행 · 단가 미입력 {drafts.Count(d=>d.Price.Length==0)}행. 재료·분량을 확인하고 구매 단가를 입력해 주세요.";
            if(!busy)QueueCalculate();
            UpdateSavedStatus();
        }catch(Exception ex){TxtDataStatus.Text="레시피 연결 실패: "+ex.Message;}
    }
    private async void OpenStandards_Click(object sender,RoutedEventArgs e){if(busy)return;new IngredientStandardsWindow(service){Owner=Window.GetWindow(this)}.ShowDialog();await Load(true);}
    private void Commit(){if(!CostInputs.CommitEdit(DataGridEditingUnit.Cell,true)||!CostInputs.CommitEdit(DataGridEditingUnit.Row,true))throw new InvalidOperationException("편집 중인 입력값을 확인해 주세요.");}
    private void btnRunCalculate_Click(object sender,RoutedEventArgs e)=>Calculate(true);
    private void Calculate(bool interactive)
    {
        if(CostInputs==null||dgCalculationResult==null)return;
        try{
            Commit();ClearResult();
            if(cbTargetMenu.SelectedItem is not StoredMeal meal){if(interactive)TxtDataStatus.Text="저장된 식단을 선택해 주세요.";return;}
            if(!int.TryParse(txtHeadCount.Text,out int people)||people<=0)throw new InvalidOperationException("식사 인원은 1명 이상으로 입력해 주세요.");
            calculation=MealCostCalculation.Create(meal,drafts.ToList(),people);result=calculation.Items.ToList();
            dgCalculationResult.ItemsSource=result;txtTotalCount.Text=$"재료 {result.Count}행 · 식수 {people}명";
            txtCostPerPerson.Text=$"한 끼 1인당 {(calculation.Complete?"원가":"임시 소계")} ₩ {calculation.PerPersonCost:N2}";
            txtTotalCost.Text=$"한 끼 총 {(calculation.Complete?"원가":"임시 소계")} ₩ {calculation.TotalCost:N2}";
            TxtDataStatus.Text=calculation.Complete?"입력값으로 자동 계산했습니다. ‘한 끼 원가 계산·저장’을 누르면 재료별 비용과 한 끼 합계가 저장됩니다.":$"구매가격 미입력 {calculation.MissingPrices}행 · 폐기율 미입력 {calculation.MissingWaste}행. 빈 단가는 제외하고 빈 폐기율은 0%로 임시 계산했습니다. 전체 원가는 미확정입니다.";
            if(calculation.Complete&&!string.IsNullOrWhiteSpace(txtBudgetPerPerson.Text)){decimal budget=CostCalculator.Number(txtBudgetPerPerson.Text,"인당 목표 급식비",true);TxtDataStatus.Text+=$" 목표 대비 {calculation.PerPersonCost/budget*100:0.##}%"+(calculation.PerPersonCost>budget?" · 목표 초과":" · 목표 이내");}
            UpdateSavedStatus();
        }catch(Exception ex){TxtDataStatus.Text="계산 전 확인: "+ex.Message;}
    }
    private void UpdateSavedStatus()
    {
        if(CalculationSaveStatus==null)return;
        if(savedReadError.Length>0){CalculationSaveStatus.Text="저장된 원가 조회 실패: "+savedReadError;return;}
        var saved=cbTargetMenu?.SelectedItem is StoredMeal meal?savedCalculations.LastOrDefault(s=>s.Identity==meal.Identity):null;
        if(saved==null){CalculationSaveStatus.Text="아직 저장된 한 끼 원가가 없습니다. 입력만으로 Google 시트에 자동 전송되지는 않습니다.";return;}
        string when=DateTimeOffset.TryParse(saved.SavedAt,out var date)?date.ToLocalTime().ToString("yyyy-MM-dd HH:mm"):saved.SavedAt;
        CalculationSaveStatus.Text=$"저장 기록 {when} · 1인당 {saved.PerPersonCost:N2}원 · {saved.HeadCount}명 총 {saved.TotalCost:N2}원\n"+
            (calculation?.Fingerprint==saved.Fingerprint?"현재 식단·입력값과 일치합니다. 내 시트의 ‘원가계산결과’에 재료별 내역과 합계가 저장되어 있습니다.":"현재 식단·입력값과 다르거나 계산 대기 중입니다. 이전 원가를 현재 원가로 사용하지 말고 다시 계산·저장해 주세요.");
    }
    private async void SaveCosts_Click(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        try{Commit();if(cbTargetMenu.SelectedItem is not StoredMeal meal)throw new InvalidOperationException("식단을 선택해 주세요.");busy=true;EditorRoot.IsEnabled=false;using var activity=AppActivity.Begin("재료 분량과 단가를 저장하는 중입니다…");await service.SaveMealCostsAsync(meal,drafts.ToList());if(await Load(true))TxtDataStatus.Text="재료·공통 구매 기준을 저장했습니다. 한 끼 원가까지 저장하려면 ‘한 끼 원가 계산·저장’을 누르세요.";else TxtDataStatus.Text="저장은 완료했지만 재조회에 실패했습니다. "+TxtDataStatus.Text;}
        catch(Exception ex){TxtDataStatus.Text="저장 실패: "+ex.Message;}finally{busy=false;EditorRoot.IsEnabled=true;}
    }
    private async void SaveCalculation_Click(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        bool saved=false;
        try
        {
            Commit();
            if(cbTargetMenu.SelectedItem is not StoredMeal meal)throw new InvalidOperationException("식단을 선택해 주세요.");
            if(!int.TryParse(txtHeadCount.Text,out int people))throw new InvalidOperationException("식수는 1명 이상의 정수로 입력해 주세요.");
            var snapshot=drafts.Select(MealCostData.Clone).ToArray();MealCostCalculation.Create(meal,snapshot,people,true);
            busy=true;EditorRoot.IsEnabled=false;
            using var activity=AppActivity.Begin("재료별 비용과 한 끼 원가를 저장하는 중입니다…");
            await service.SaveMealCalculationAsync(meal,snapshot,people);saved=true;
            bool refreshed=await Load(true);
            if(refreshed&&savedReadError.Length==0)TxtDataStatus.Text="재료·구매 기준과 재료별 1인분 비용·발주량·한 끼 합계를 내 시트에 저장했습니다.";
            else TxtDataStatus.Text="저장은 완료했지만 재조회에 실패했습니다. "+(savedReadError.Length>0?savedReadError:TxtDataStatus.Text);
        }
        catch(Exception ex){TxtDataStatus.Text=(saved?"저장은 완료했지만 재조회에 실패했습니다: ":"원가 저장을 완료하지 못했습니다. 입력값과 연결을 확인한 뒤 다시 저장해 주세요: ")+ex.Message;}
        finally{busy=false;EditorRoot.IsEnabled=true;}
    }
    private void ImportIngredientCsv_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new OpenFileDialog{Filter="CSV 파일|*.csv",Multiselect=false};
        if(dialog.ShowDialog()!=true)return;
        try { using var reader=new StreamReader(dialog.FileName,detectEncodingFromByteOrderMarks:true); ingredientCatalog=IngredientCsv.Parse(reader,Path.GetFileName(dialog.FileName)); RefreshIngredientSearch(); }
        catch(Exception ex){IngredientSearchStatus.Text="CSV 불러오기 실패: "+ex.Message;}
    }
    private void IngredientSearchChanged(object sender,TextChangedEventArgs e)=>RefreshIngredientSearch();
    private void RefreshIngredientSearch()
    {
        if(ingredientCatalog.Count==0){IngredientSearchGrid.ItemsSource=null;return;}
        string query=TxtIngredientSearch.Text.Trim();
        var found=ingredientCatalog.Where(i=>query.Length==0||i.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||i.Product.Contains(query,StringComparison.OrdinalIgnoreCase)).Take(200).ToList();
        IngredientSearchGrid.ItemsSource=found;
        IngredientSearchStatus.Text=$"{ingredientCatalog.Count:N0}개 원재료를 불러왔습니다. 검색 결과 {found.Count:N0}개"+(found.Count==200?" (최대 200개 표시)":"");
    }
    private void btnExportExcel_Click(object sender,RoutedEventArgs e)
    {
        if(result.Count==0){TxtDataStatus.Text="먼저 원가를 계산해 주세요.";return;}
        var dialog=new SaveFileDialog{Filter="CSV 파일|*.csv",FileName=$"원가명세표_{DateTime.Now:yyyyMMdd_HHmm}.csv"};if(dialog.ShowDialog()!=true)return;
        string Csv(string s)=>"\""+s.Replace("\"","\"\"")+"\"";
        try {var lines=new List<string>{"메뉴명,식재료명,1인분 정미량,폐기율,1인분 구매량,환산 기준량,g·ml당 단가,1인분 재료비,발주량,재료 총 원가"};lines.AddRange(result.Select(r=>string.Join(",",new[]{r.Menu,r.Name,r.UnitWeightDisplay,r.WasteDisplay,r.GrossWeightDisplay,r.PurchaseBasisDisplay,r.BaseUnitPriceDisplay,r.PerPersonCostDisplay,r.TotalWeightDisplay,r.TotalPriceDisplay}.Select(Csv))));if(calculation!=null)lines.Add(string.Join(",",new[]{"합계",calculation.Complete?"계산 완료":"임시 소계","식수",calculation.HeadCount.ToString(),"한 끼 1인당 원가",calculation.PerPersonCost.ToString(CultureInfo.InvariantCulture),"한 끼 총 원가",calculation.TotalCost.ToString(CultureInfo.InvariantCulture)}.Select(Csv)));File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));TxtDataStatus.Text="CSV 파일을 저장했습니다.";}
        catch(Exception ex){TxtDataStatus.Text="파일 저장 실패: "+ex.Message;}
    }
}
