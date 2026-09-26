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
    private bool busy;
    public CalculatePage(){InitializeComponent();Loaded+=async(_,_)=>await Load();}
    private void ClearResult(){result=[];if(dgCalculationResult==null)return;dgCalculationResult.ItemsSource=null;txtTotalCount.Text="재료 —";txtCostPerPerson.Text="1인당 —";txtTotalCost.Text="총 원가 —";}
    private void InputsChanged(object sender,TextChangedEventArgs e)=>ClearResult();
    private void CostEdited(object sender,DataGridCellEditEndingEventArgs e)=>ClearResult();
    private async void Refresh_Click(object sender,RoutedEventArgs e){if(!busy)await Load();}
    private async Task Load()
    {
        busy=true;EditorRoot.IsEnabled=false;ClearResult();
        using var activity=AppActivity.Begin("저장된 식단과 원가 자료를 불러오는 중입니다…");
        try {
            var meals=await service.GetStoredMealsAsync();
            cbTargetMenu.ItemsSource=meals.OrderByDescending(m=>m.Date).ToList();
            costs=await service.GetMealCostsAsync();
            personalKeys.Clear();
            foreach(string age in meals.Select(m=>m.AgeGroup.Length==0?"3-5":m.AgeGroup).Distinct())personalKeys[age]=(await service.ReadPersonalRecipesAsync(age)).Select(r=>r.Menu.Key).ToHashSet();
            txtHeadCount.Text=(service.CurrentFacility?.Diners ?? (await service.GetDinersAsync()).Count).ToString();
            cbTargetMenu.ItemsSource=meals.OrderByDescending(m=>m.Date).ToList();
            var monday=WeeklyMealPlanner.Monday(DateTime.Today);
            cbTargetMenu.SelectedItem=meals.FirstOrDefault(m=>m.Date>=monday&&m.Date<monday.AddDays(7))??meals.LastOrDefault();
            if(meals.Count==0)TxtDataStatus.Text="저장된 식단이 없습니다. 식단 만들기에서 먼저 식단을 저장해 주세요.";
        }catch(Exception ex){ if(cbTargetMenu.Items.Count>0 && cbTargetMenu.SelectedItem==null)cbTargetMenu.SelectedIndex=0; TxtDataStatus.Text="일부 자료 조회 실패: "+ex.Message; }
        finally{busy=false;EditorRoot.IsEnabled=true;}
    }
    private void MenuChanged(object sender,SelectionChangedEventArgs e)
    {
        ClearResult();
        if(cbTargetMenu.SelectedItem is not StoredMeal meal){CostInputs.ItemsSource=null;return;}
        try{
            drafts=new(MealCostData.Build(meal,costs,personalKeys.GetValueOrDefault(meal.AgeGroup.Length==0?"3-5":meal.AgeGroup)));
            drafts.CollectionChanged+=(_,_)=>ClearResult();CostInputs.ItemsSource=drafts;
            MealSummary.Text=string.Join(" · ",meal.Menus.Where(n=>n.Length>0));
            TxtDataStatus.Text=$"재료 {drafts.Count}행 · 단가 미입력 {drafts.Count(d=>d.Price.Length==0)}행. 재료·분량을 확인하고 구매 단가를 입력해 주세요.";
        }catch(Exception ex){TxtDataStatus.Text="레시피 연결 실패: "+ex.Message;}
    }
    private void Commit(){if(!CostInputs.CommitEdit(DataGridEditingUnit.Cell,true)||!CostInputs.CommitEdit(DataGridEditingUnit.Row,true))throw new InvalidOperationException("편집 중인 입력값을 확인해 주세요.");}
    private void btnRunCalculate_Click(object sender,RoutedEventArgs e)
    {
        try{
            Commit();ClearResult();
            if(cbTargetMenu.SelectedItem is not StoredMeal meal)throw new InvalidOperationException("저장된 식단을 선택해 주세요.");
            if(!int.TryParse(txtHeadCount.Text,out int people)||people<=0)throw new InvalidOperationException("식사 인원은 1명 이상으로 입력해 주세요.");
            decimal budget=CostCalculator.Number(txtBudgetPerPerson.Text,"인당 목표 급식비",true);
            var validated=MealCostData.Validate(meal,drafts,true);result=validated.Select(r=>new IngredientItem(r,people)).ToList();
            dgCalculationResult.ItemsSource=result;txtTotalCount.Text=$"재료 {result.Count}행";txtCostPerPerson.Text=$"1인당 ₩ {result.Sum(r=>r.PerPersonCost):N2}";txtTotalCost.Text=$"총 원가 ₩ {result.Sum(r=>r.TotalCost):N2}";
            TxtDataStatus.Text=result.Sum(r=>r.PerPersonCost)>budget?"계산 완료 · 인당 목표 급식비를 초과했습니다.":"계산 완료 · 목표 급식비 이내입니다.";
        }catch(Exception ex){TxtDataStatus.Text="계산 전 확인: "+ex.Message;}
    }
    private async void SaveCosts_Click(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        try{Commit();if(cbTargetMenu.SelectedItem is not StoredMeal meal)throw new InvalidOperationException("식단을 선택해 주세요.");busy=true;EditorRoot.IsEnabled=false;using var activity=AppActivity.Begin("재료 분량과 단가를 저장하는 중입니다…");await service.SaveMealCostsAsync(meal,drafts.ToList());costs=await service.GetMealCostsAsync();TxtDataStatus.Text="재료·단가를 내 원가 시트에 저장했습니다.";}
        catch(Exception ex){TxtDataStatus.Text="저장 실패: "+ex.Message;}finally{busy=false;EditorRoot.IsEnabled=true;}
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
        try {var lines=new List<string>{"식재료명,1인당 소요량,총 소요량,단가 기준,총 소요금액"};lines.AddRange(result.Select(r=>string.Join(",",new[]{r.Name,r.UnitWeightDisplay,r.TotalWeightDisplay,r.UnitPriceDisplay,r.TotalPriceDisplay}.Select(Csv))));File.WriteAllLines(dialog.FileName,lines,new UTF8Encoding(true));TxtDataStatus.Text="CSV 파일을 저장했습니다.";}
        catch(Exception ex){TxtDataStatus.Text="파일 저장 실패: "+ex.Message;}
    }
}
