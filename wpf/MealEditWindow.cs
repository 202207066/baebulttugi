using System.Windows;
using System.Windows.Controls;
namespace wpf;
public enum MealEditResult { Cancel, Save, Delete }
public sealed class MealEditWindow : Window
{
    private readonly List<TrackMenu> selections=[];
    public event Action<bool>? ExpansionChanged;
    public bool IsMenuExpanded { get; private set; }
    public MealEditResult Result { get; private set; } = MealEditResult.Cancel;
    public TrackMenu[] Selection => selections.ToArray();
    public MealEditWindow(StoredMeal meal,IReadOnlyList<TrackMenu> catalog,int? menuIndex=null,bool showSave=true)
    {
        Title="끼니 수정 · "+meal;EditorWindowSizing.Apply(this,520,menuIndex.HasValue?380:580);MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/wpf;component/PlannerStyles.xaml",UriKind.Relative)});
        var root=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock{Text=meal.ToString(),FontSize=22,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
        root.Children.Add(new TextBlock{Text="메뉴를 바꾸면 해당 끼니의 영양 합계를 다시 계산합니다. 영양정보가 없는 메뉴를 유지하면 합계는 미완성으로 표시됩니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)});
        void ResizeEditor()
        {
            IsMenuExpanded=root.Children.OfType<Expander>().Any(section=>section.IsExpanded);
            if(showSave)EditorWindowSizing.SetHeight(this,IsMenuExpanded?(menuIndex.HasValue?620:740):menuIndex.HasValue?380:580);
            ExpansionChanged?.Invoke(IsMenuExpanded);
        }
        var keys=meal.Value(25).Split(" / ");
        for(int i=0;i<6;i++) {
            string category=WeeklyMealPlanner.Categories[i];var list=catalog.Where(m=>m.Category==category).OrderBy(m=>m.Name).ToList();
            var chosen=list.FirstOrDefault(m=>i<keys.Length&&m.Key==keys[i])??list.FirstOrDefault(m=>m.Name==meal.Menus[i]);
            if(chosen==null){chosen=new TrackMenu("기존",meal.Menus[i],category,"",null,null,null,null,"");list.Insert(0,chosen);}
            int index=i; selections.Add(chosen);
            if(menuIndex.HasValue && menuIndex.Value!=i)continue;
            var section=new Expander { Header=category+" · "+chosen.DisplayName, Style=(Style)FindResource("GuideExpander"),Margin=new Thickness(0,8,0,0) };
            var panel=new StackPanel(); section.Content=panel; root.Children.Add(section);
            var search = new TextBox { ToolTip = "메뉴 이름 일부를 입력하세요", Margin = new Thickness(0,0,0,5) };
            search.Padding = new Thickness(12,0,40,0);
            var searchPanel = new Grid(); searchPanel.Children.Add(search);
            searchPanel.Children.Add(new System.Windows.Shapes.Path {
                Data = System.Windows.Media.Geometry.Parse("M 7,1 A 6,6 0 1 1 6.99,1 M 11,11 L 17,17"),
                Stroke = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(98,130,108)), StrokeThickness = 1.7,
                Width = 18, Height = 18, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0,0,14,5), IsHitTestVisible = false });
            panel.Children.Add(searchPanel);
            var box=new ListBox{ItemsSource=list,DisplayMemberPath="DisplayName",MaxHeight=180,MinHeight=80};panel.Children.Add(box);
            void Choose() { if(box.SelectedItem is TrackMenu selected) { selections[index]=selected; section.Header=category+" · "+selected.DisplayName;section.IsExpanded=false; } }
            box.PreviewMouseLeftButtonUp += (_,e)=> { if(ItemsControl.ContainerFromElement(box,e.OriginalSource as DependencyObject) is ListBoxItem) Choose(); };
            box.KeyDown += (_,e)=> { if(e.Key==System.Windows.Input.Key.Enter) {Choose();e.Handled=true;} };
            search.PreviewKeyDown += (_,e)=> {if(e.Key==System.Windows.Input.Key.Down && box.Items.Count>0) {box.SelectedIndex=0;box.Focus();e.Handled=true;}};
            section.Expanded += (_,_)=>{foreach(var other in root.Children.OfType<Expander>().Where(x=>x!=section))other.IsExpanded=false;ResizeEditor();search.Focus();};
            section.Collapsed += (_,_)=>ResizeEditor();
            var hint = new TextBlock { FontSize = 12, Text = "메뉴명 검색 · 일부 단어로도 찾을 수 있습니다", Margin = new Thickness(0,4,0,0) };
            panel.Children.Add(hint);
            search.TextChanged += (_,_) => {

                var matches = list.Where(m => MealPresentation.Matches(m.Name, search.Text)).ToList();
                box.ItemsSource = matches;

                hint.Text = matches.Count == 0 ? "일치하는 메뉴가 없습니다." : $"검색 결과 {matches.Count}개 · 목록에서 선택하세요";

            };
        }
        // The existing + control expands the menu list and window together.
        var save=new Button{Content="변경한 식단 저장",Style=(Style)FindResource("PrimaryButton"),Margin=new Thickness(0,22,0,0)};
        save.Click+=(_,_)=>{if(selections.All(m=>m.Name.Length>0)){Result=MealEditResult.Save;DialogResult=true;}};if(showSave)root.Children.Add(save);
        if(showSave)
        {
            var actions = new WrapPanel { Margin = new Thickness(0,16,0,0) };
            var delete = new Button { Content = "이 끼니 전체 삭제", Style = (Style)FindResource("DangerButton"), Margin = new Thickness(0,0,16,0) };
            delete.Click += (_,_) =>
            {
                if(MessageBox.Show(this, $"{meal} 식단의 메뉴 6개를 모두 삭제할까요? 삭제 후 되돌릴 수 없습니다.", "식단 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
                Result = MealEditResult.Delete; DialogResult = true;
            };
            var cancel = new Button { Content = "취소", IsCancel = true };
            actions.Children.Add(delete); actions.Children.Add(cancel); root.Children.Add(actions);
        }
    }
}

