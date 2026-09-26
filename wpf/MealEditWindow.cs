using System.Windows;
using System.Windows.Controls;
namespace wpf;
public sealed class MealEditWindow : Window
{
    private readonly List<ComboBox> boxes=[];
    public TrackMenu[] Selection => boxes.Select(b=>(TrackMenu)b.SelectedItem).ToArray();
    public MealEditWindow(StoredMeal meal,IReadOnlyList<TrackMenu> catalog)
    {
        Title="끼니 수정 · "+meal;Width=620;Height=720;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/wpf;component/PlannerStyles.xaml",UriKind.Relative)});
        var root=new StackPanel{Margin=new Thickness(28)};Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock{Text=meal.ToString(),FontSize=22,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
        root.Children.Add(new TextBlock{Text="메뉴를 바꾸면 해당 끼니의 영양 합계를 다시 계산합니다. 영양정보가 없는 메뉴를 유지하면 합계는 미완성으로 표시됩니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)});
        var keys=meal.Value(25).Split(" / ");
        for(int i=0;i<6;i++) {
            string category=WeeklyMealPlanner.Categories[i];var list=catalog.Where(m=>m.Category==category).OrderBy(m=>m.Name).ToList();
            var chosen=list.FirstOrDefault(m=>i<keys.Length&&m.Key==keys[i])??list.FirstOrDefault(m=>m.Name==meal.Menus[i]);
            if(chosen==null){chosen=new TrackMenu("기존",meal.Menus[i],category,"",null,null,null,null,"");list.Insert(0,chosen);}
            root.Children.Add(new TextBlock{Text=category,Margin=new Thickness(0,8,0,5)});
            var box=new ComboBox{ItemsSource=list,DisplayMemberPath="Name",SelectedItem=chosen};boxes.Add(box);root.Children.Add(box);
        }
        var save=new Button{Content="변경한 식단 저장",Style=(Style)FindResource("PrimaryButton"),Margin=new Thickness(0,22,0,0)};
        save.Click+=(_,_)=>{if(boxes.All(b=>b.SelectedItem is TrackMenu m && m.Name.Length>0))DialogResult=true;};root.Children.Add(save);
    }
}
