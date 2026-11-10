using System.Windows;
using System.Windows.Controls;
using System.Globalization;
using System.Collections.ObjectModel;
namespace wpf;
public partial class MenuInputPage : Page
{
    private readonly GoogleSheetsService service=AppServices.Require();
    private List<TrackMenu> menus=[];
    private Dictionary<string,string> ingredients=new();
    private TrackMenu? selected;
    private Dictionary<string,string> origins=new();
    private List<SavedCost> costs=[];
    private HashSet<string> personalKeys=[];
    private ObservableCollection<CostDraft> recipeDrafts=[];
    private bool ready,busy,loading;
    private int version;
    private string Age => (Track.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "3-5";
    public MenuInputPage(){InitializeComponent();RecipeIngredients.PreviewMouseWheel+=WeekMealTable.Wheel;MenuGrid.PreviewMouseWheel+=WeekMealTable.Wheel;CategoryBox.ItemsSource=WeeklyMealPlanner.Categories;CategoryBox.SelectedIndex=0;Track.SelectedIndex=0;Loaded+=async(_,_)=>{if(!ready){ready=true;await Load();}};}
    private async void TrackChanged(object sender,SelectionChangedEventArgs e){if(ready&&!busy&&!loading){Clear();menus=[];ingredients.Clear();Filter();await Load();}}
    private async Task Load(bool force=false)
    {
        if (loading) return;
        loading=true;
        Root.IsEnabled=false;
        int current=++version;
        string age=Age;
        Status.Text="메뉴를 불러오는 중입니다…";
        try
        {
            using var activity=AppActivity.Begin(Status.Text);
            var catalog=await service.GetTrackCatalogAsync(age,force);
            costs=await service.GetMealCostsAsync(force);
            personalKeys=catalog.PersonalRecipes.Select(x=>x.Menu.Key).ToHashSet();
            if(current!=version)return;
            menus=catalog.Menus;
            origins=catalog.PersonalRecipes.ToDictionary(x=>x.Menu.Key,x=>x.OriginKey);
            ingredients=catalog.PersonalRecipes.ToDictionary(x=>x.Menu.Key,x=>x.Ingredients);
            Filter();
            Status.Text=$"메뉴 {menus.Count:N0}개 · 내 레시피 {catalog.PersonalRecipes.Count}개. 목록에서 메뉴를 선택하거나 새로 입력하세요.";
        }
        catch(Exception ex)
        {
            if(current==version)Status.Text="조회 실패: "+ex.Message;
        }
        finally
        {
            loading=false;
            Root.IsEnabled=!busy;
        }
    }
    private async void OpenStandards_Click(object sender,RoutedEventArgs e){if(busy||loading)return;new IngredientStandardsWindow(service){Owner=Window.GetWindow(this)}.ShowDialog();await Load(true);}
    private void Filter(){if(MenuGrid!=null)MenuGrid.ItemsSource=menus.Where(m=>m.Name.Contains(SearchBox.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();}
    private void SearchChanged(object sender,TextChangedEventArgs e)=>Filter();
    private void MenuSelected(object sender,SelectionChangedEventArgs e){if(MenuGrid.SelectedItem is not TrackMenu m)return;selected=m;NameBox.Text=m.Name;CategoryBox.SelectedItem=m.Category;AllergyBox.Text=MenuIngredientReview.EffectiveAllergens(m);ServingBox.Text=m.ServingBasis.StartsWith("완성 음식")?m.ServingGrams?.ToString(CultureInfo.InvariantCulture)??"":"";IngredientsConfirmedBox.IsChecked=false;ReviewSummary.Text=$"{m.ProteinGrade} · 100g당 단백질 {m.ProteinPer100g?.ToString("0.##")??"미확인"}g · {m.ServingBasis} · {m.ProteinReview} · {m.AllergyReview}";CarbBox.Text=m.Carb?.ToString(CultureInfo.InvariantCulture)??"";ProteinBox.Text=m.Protein?.ToString(CultureInfo.InvariantCulture)??"";FatBox.Text=m.Fat?.ToString(CultureInfo.InvariantCulture)??"";EnergyBox.Text=m.Calories?.ToString(CultureInfo.InvariantCulture)??"";SourceBox.Text=m.Source;IngredientsBox.Text=ingredients.GetValueOrDefault(m.Key,OfficialCatalog.Current.Cooking.GetValueOrDefault(CookingMethods.Key(m.Name),""));recipeDrafts=new(MealCostData.Build(MealCostData.CostMeal(m,Age),costs,personalKeys));RecipeIngredients.ItemsSource=recipeDrafts;}
    private void Clear(){recipeDrafts=new();RecipeIngredients.ItemsSource=recipeDrafts;selected=null;MenuGrid.SelectedItem=null;foreach(var box in new[]{NameBox,CarbBox,ProteinBox,FatBox,EnergyBox,IngredientsBox,SourceBox,ServingBox})box.Clear();IngredientsConfirmedBox.IsChecked=false;ReviewSummary.Text=""; AllergyBox.Clear();CategoryBox.SelectedIndex=0;}
    private void NewClick(object sender,RoutedEventArgs e)=>Clear();
    private async void RefreshClick(object sender,RoutedEventArgs e){if(!busy)await Load(true);}
    private async void SaveClick(object sender,RoutedEventArgs e){if(busy||loading)return;busy=true;Root.IsEnabled=false;try{
        if(!RecipeIngredients.CommitEdit(DataGridEditingUnit.Cell,true)||!RecipeIngredients.CommitEdit(DataGridEditingUnit.Row,true))throw new InvalidOperationException("식재료 입력을 확인해 주세요.");
        double? Number(TextBox box){if(string.IsNullOrWhiteSpace(box.Text))return null;return WeeklyMealPlanner.Number(box.Text)??throw new InvalidOperationException("영양량은 0 이상의 숫자로 입력해 주세요.");}
        var menu=new TrackMenu(selected?.Id??("USER-"+Guid.NewGuid().ToString("N")),NameBox.Text.Trim(),CategoryBox.SelectedItem?.ToString()??"",AllergyBox.Text.Trim(),Number(CarbBox),Number(ProteinBox),Number(FatBox),Number(EnergyBox),SourceBox.Text.Trim());
        var ingredientRows=recipeDrafts.Where(d=>d.Name.Trim().Length>0||d.Amount.Trim().Length>0).ToList();foreach(var d in ingredientRows){d.Menu=menu.Name;d.Category=menu.Category;}
        menu=MenuIngredientReview.Apply(menu,ingredientRows.Select(d=>new PortionIngredient{Name=d.Name,Amount=CostCalculator.Number(d.Amount,d.Name+" 소요량"),Unit=d.Unit}));
        if(!string.IsNullOrWhiteSpace(ServingBox.Text)) menu=menu with{ServingGrams=Number(ServingBox),ServingBasis="완성 음식 제공량 · 사용자 입력"};
        if(IngredientsConfirmedBox.IsChecked==true){string Flag(string allergen)=>GoogleSheetsService.MatchesRegisteredAllergen(menu,new[]{allergen})?"포함":"없음 확인";menu=menu with{Almond=Flag("아몬드"),Sesame=Flag("참깨"),OtherBeans=Flag("콩류(기타)"),AllergyReview=""};}
        menu=MenuClassification.Official(menu);
        foreach(var d in ingredientRows)d.Category=menu.Category;
        var costMeal=MealCostData.CostMeal(menu,Age);if(ingredientRows.Count>0)MealCostData.Validate(costMeal,ingredientRows,false);
        using var activity=AppActivity.Begin("내 레시피를 저장하는 중입니다…");await service.SavePersonalRecipeAsync(Age,menu,IngredientsBox.Text.Trim(),selected==null?"":origins.GetValueOrDefault(selected.Key,selected.Key));
        try{await service.SaveMealCostsAsync(costMeal,ingredientRows,true);}catch(Exception ex){Status.Text="메뉴 정보는 저장됐지만 식재료 표는 저장하지 못했습니다. 작성 내용은 유지합니다. 다시 저장해 주세요. "+ex.Message;return;}
        await Load();Clear();Status.Text="내 레시피와 식재료를 저장했습니다. 식단 조합과 원가 계산에 적용됩니다.";
    }catch(Exception ex){Status.Text="저장 실패: "+ex.Message;}finally{busy=false;Root.IsEnabled=true;}}
}
