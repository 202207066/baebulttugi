using System.Windows;
using System.Windows.Controls;
namespace wpf;
public enum MealEditResult { Cancel, Save, Delete }
public sealed class MealEditWindow:Window
{
    private readonly List<TrackMenu> selections=[];
    public event Action<bool>? ExpansionChanged;
    public bool IsMenuExpanded{get;private set;}
    public MealEditResult Result{get;private set;}=MealEditResult.Cancel;
    public TrackMenu[] Selection=>selections.ToArray();
    public MealEditWindow(StoredMeal meal,IReadOnlyList<TrackMenu> catalog,int? menuIndex=null,bool showSave=true,IReadOnlyList<PatientModel>? patients=null)
    {
        patients ??= [];
        Title="끼니 수정 · "+meal;EditorWindowSizing.Apply(this,520,menuIndex.HasValue?380:580);MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Resources.MergedDictionaries.Add(new ResourceDictionary{Source=new Uri("/wpf;component/PlannerStyles.xaml",UriKind.Relative)});
        var root=new StackPanel{Margin=new Thickness(20)};Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        root.Children.Add(new TextBlock{Text=meal.ToString(),FontSize=22,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
        root.Children.Add(new TextBlock{Text="메뉴를 바꾸면 해당 끼니의 영양 합계를 다시 계산합니다. 반찬은 주찬·부찬·김치를 합쳐 최대 5개까지 구성할 수 있습니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,16)});
        var keys=meal.MenuKeys;
        for(int i=0;i<meal.Menus.Length;i++){
            var chosen=meal.SavedItems?.ElementAtOrDefault(i)??catalog.FirstOrDefault(m=>i<keys.Length&&m.Key==keys[i])??catalog.FirstOrDefault(m=>m.Name==meal.Menus[i]&&m.Category==MealSnapshot.CategoryAt(i));
            selections.Add(chosen??new TrackMenu("기존",meal.Menus[i],MealSnapshot.CategoryAt(i),"",null,null,null,null,""));
        }
        void ResizeEditor(){IsMenuExpanded=root.Children.OfType<Expander>().Any(s=>s.IsExpanded);if(showSave)EditorWindowSizing.SetHeight(this,IsMenuExpanded?(menuIndex.HasValue?620:740):menuIndex.HasValue?380:580);ExpansionChanged?.Invoke(IsMenuExpanded);}
        var allergyStatus=new TextBlock{Foreground=System.Windows.Media.Brushes.DarkRed,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,0)};
        void ShowWarnings(){var warnings=ManualAllergyReview.Describe(selections,patients);allergyStatus.Text=warnings.Count==0?"등록 대상자와 겹치는 알레르기 없음":string.Join("\n",warnings.Take(4))+(warnings.Count>4?$"\n외 {warnings.Count-4}건 · 저장 시 다시 확인합니다.":"");}
        root.Children.Add(allergyStatus);
        var add=new Button{Content="+ 반찬 추가",Margin=new Thickness(0,16,0,0)};root.Children.Add(add);
        void RenderSections(int? expand=null){
            foreach(var old in root.Children.OfType<Expander>().ToList())root.Children.Remove(old);
            int insert=2;
            for(int i=0;i<selections.Count;i++){
                int index=i;if(menuIndex.HasValue&&menuIndex.Value!=i&&i<6)continue;
                var chosen=selections[i];string category=MealSnapshot.CategoryAt(i);
                var list=catalog.Where(m=>m.Category==category).OrderBy(m=>m.Name).ToList();
                if(!list.Contains(chosen))list.Insert(0,chosen);
                var section=new Expander{Header=MealSnapshot.LabelAt(i)+" · "+chosen.DisplayName,Style=(Style)FindResource("GuideExpander"),Margin=new Thickness(0,8,0,0)};
                var panel=new StackPanel();section.Content=panel;root.Children.Insert(insert++,section);
                var search=new TextBox{ToolTip="메뉴 이름 일부를 입력하세요",Margin=new Thickness(0,0,0,5)};panel.Children.Add(search);
                var box=new ListBox{ItemsSource=list,DisplayMemberPath="DisplayName",MaxHeight=180,MinHeight=80};panel.Children.Add(box);
                void Choose(){if(box.SelectedItem is not TrackMenu selected||selected.Id=="PENDING")return;if(!selected.HasNutrition&&MessageBox.Show("이 메뉴는 영양정보가 미완성입니다. 식단 합계를 확정할 수 없습니다. 그래도 선택할까요?","영양정보 확인",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;if(!ManualAllergyReview.Confirm([selected],patients,Window.GetWindow(root)))return;selections[index]=selected;ShowWarnings();section.Header=MealSnapshot.LabelAt(index)+" · "+selected.DisplayName;section.IsExpanded=false;}
                box.PreviewMouseLeftButtonUp+=(_,e)=>{if(ItemsControl.ContainerFromElement(box,e.OriginalSource as DependencyObject) is ListBoxItem)Choose();};
                box.KeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){Choose();e.Handled=true;}};
                search.PreviewKeyDown+=(_,e)=>{if(e.Key==System.Windows.Input.Key.Down&&box.Items.Count>0){box.SelectedIndex=0;box.Focus();e.Handled=true;}};
                section.Expanded+=(_,_)=>{foreach(var other in root.Children.OfType<Expander>().Where(x=>x!=section))other.IsExpanded=false;ResizeEditor();search.Focus();};section.Collapsed+=(_,_)=>ResizeEditor();
                var hint=new TextBlock{Text="메뉴명 검색 · 일부 단어로도 찾을 수 있습니다",FontSize=12,Margin=new Thickness(0,4,0,0)};panel.Children.Add(hint);
                search.TextChanged+=(_,_)=>{var matches=list.Where(m=>MealPresentation.Matches(m.Name,search.Text)).ToList();box.ItemsSource=matches;hint.Text=matches.Count==0?"일치하는 메뉴가 없습니다.":$"검색 결과 {matches.Count}개 · 목록에서 선택하세요";};
                if(i>=6){var remove=new Button{Content="이 추가 반찬 제외",Margin=new Thickness(0,10,0,0)};remove.Click+=(_,_)=>{selections.RemoveAt(index);RenderSections();};panel.Children.Add(remove);}
                if(expand==index)section.IsExpanded=true;
            }
            add.IsEnabled=selections.Count<8;ResizeEditor();ShowWarnings();
        }
        add.Click+=(_,_)=>{if(selections.Count>=8)return;selections.Add(new TrackMenu("PENDING","추가할 반찬을 선택하세요","부찬","",null,null,null,null,""));RenderSections(selections.Count-1);};
        if(showSave){var save=new Button{Content="변경한 식단 저장",Style=(Style)FindResource("PrimaryButton"),Margin=new Thickness(0,22,0,0)};save.Click+=(_,_)=>{try{MealSnapshot.Validate(selections);Result=MealEditResult.Save;DialogResult=true;}catch(Exception ex){MessageBox.Show(ex.Message,"식단 확인");}};root.Children.Add(save);
            var actions=new WrapPanel{Margin=new Thickness(0,16,0,0)};var delete=new Button{Content="이 끼니 전체 삭제",Style=(Style)FindResource("DangerButton"),Margin=new Thickness(0,0,16,0)};
            delete.Click+=(_,_)=>{if(MessageBox.Show(this,$"{meal} 식단의 메뉴 {selections.Count}개를 모두 삭제할까요? 삭제 후 되돌릴 수 없습니다.","식단 삭제",MessageBoxButton.YesNo,MessageBoxImage.Warning,MessageBoxResult.No)!=MessageBoxResult.Yes)return;Result=MealEditResult.Delete;DialogResult=true;};actions.Children.Add(delete);actions.Children.Add(new Button{Content="취소",IsCancel=true});root.Children.Add(actions);
        }
        RenderSections();
    }
}
