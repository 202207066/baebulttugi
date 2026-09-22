using System.Windows;
using System.Windows.Controls;
using System.Globalization;
namespace wpf;
public partial class MenuInputPage : Page
{
    private readonly GoogleSheetsService service=AppServices.Require();
    private List<TrackMenu> menus=[];
    private Dictionary<string,string> ingredients=new();
    private TrackMenu? selected;
    private bool ready,busy;
    private int version;
    private string Age => (Track.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "3-5";
    public MenuInputPage(){InitializeComponent();CategoryBox.ItemsSource=WeeklyMealPlanner.Categories;CategoryBox.SelectedIndex=0;Track.SelectedIndex=0;Loaded+=async(_,_)=>{if(!ready){ready=true;await Load();}};}
    private async void TrackChanged(object sender,SelectionChangedEventArgs e){if(ready&&!busy){Clear();await Load();}}
    private async Task Load(bool force=false){int current=++version;string age=Age;Status.Text="메뉴를 불러오는 중입니다…";try{using var activity=AppActivity.Begin(Status.Text);var catalog=await service.GetTrackCatalogAsync(age,force);var personal=await service.ReadPersonalRecipesAsync(age);if(current!=version)return;menus=catalog.Menus;ingredients=personal.ToDictionary(x=>x.Menu.Key,x=>x.Ingredients);Filter();Status.Text=$"메뉴 {menus.Count:N0}개 · 내 레시피 {personal.Count}개. 목록에서 메뉴를 선택하거나 새로 입력하세요.";}catch(Exception ex){if(current==version)Status.Text="조회 실패: "+ex.Message;}}
    private void Filter(){if(MenuGrid!=null)MenuGrid.ItemsSource=menus.Where(m=>m.Name.Contains(SearchBox.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToList();}
    private void SearchChanged(object sender,TextChangedEventArgs e)=>Filter();
    private void MenuSelected(object sender,SelectionChangedEventArgs e){if(MenuGrid.SelectedItem is not TrackMenu m)return;selected=m;NameBox.Text=m.Name;CategoryBox.SelectedItem=m.Category;AllergyBox.Text=m.Allergens;CarbBox.Text=m.Carb?.ToString(CultureInfo.InvariantCulture)??"";ProteinBox.Text=m.Protein?.ToString(CultureInfo.InvariantCulture)??"";FatBox.Text=m.Fat?.ToString(CultureInfo.InvariantCulture)??"";EnergyBox.Text=m.Calories?.ToString(CultureInfo.InvariantCulture)??"";SourceBox.Text=m.Source;IngredientsBox.Text=ingredients.GetValueOrDefault(m.Key,"");}
    private void Clear(){selected=null;MenuGrid.SelectedItem=null;foreach(var box in new[]{NameBox,AllergyBox,CarbBox,ProteinBox,FatBox,EnergyBox,IngredientsBox,SourceBox})box.Clear();CategoryBox.SelectedIndex=0;}
    private void NewClick(object sender,RoutedEventArgs e)=>Clear();
    private async void RefreshClick(object sender,RoutedEventArgs e){if(!busy)await Load(true);}
    private async void SaveClick(object sender,RoutedEventArgs e){if(busy)return;busy=true;Root.IsEnabled=false;try{
        double? Number(TextBox box){if(string.IsNullOrWhiteSpace(box.Text))return null;return WeeklyMealPlanner.Number(box.Text)??throw new InvalidOperationException("영양량은 0 이상의 숫자로 입력해 주세요.");}
        var menu=new TrackMenu(selected?.Id??("USER-"+Guid.NewGuid().ToString("N")),NameBox.Text.Trim(),CategoryBox.SelectedItem?.ToString()??"",AllergyBox.Text.Trim(),Number(CarbBox),Number(ProteinBox),Number(FatBox),Number(EnergyBox),SourceBox.Text.Trim());
        using var activity=AppActivity.Begin("내 레시피를 저장하는 중입니다…");await service.SavePersonalRecipeAsync(Age,menu,IngredientsBox.Text.Trim());await Load();Clear();Status.Text="저장했습니다. 내 식단 조합에 적용됩니다.";
    }catch(Exception ex){Status.Text="저장 실패: "+ex.Message;}finally{busy=false;Root.IsEnabled=true;}}
}
