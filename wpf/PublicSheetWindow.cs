using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
namespace wpf;
public sealed class PublicSheetWindow : Window
{
    private readonly Func<Task<IReadOnlyList<PublicSheet>>> load;
    private readonly Action<PublicSheet> open;
    private IReadOnlyList<PublicSheet> sheets=[];
    private readonly TextBox search=new(){Name="SheetSearch",Margin=new Thickness(0,8,0,8),ToolTip="시트 이름 검색"};
    private readonly ListBox list=new(){Name="SheetList",DisplayMemberPath="Title",MinHeight=100};
    private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)};
    private readonly Button openButton=new(){Name="OpenSheetButton",Content="선택한 시트 열기",IsEnabled=false,Padding=new Thickness(16,10,16,10),Margin=new Thickness(0,0,10,0)};
    private readonly Button refresh=new(){Content="새로고침",Padding=new Thickness(12,10,12,10)};
    private bool loading;
    public PublicSheetWindow(Func<Task<IReadOnlyList<PublicSheet>>> load,Action<PublicSheet> open)
    {
        this.load=load;this.open=open;
        Title="공공 메뉴 자료 · 시트 선택";EditorWindowSizing.Apply(this,560,560);WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var root=new DockPanel{Margin=new Thickness(22)};Content=root;
        var heading=new StackPanel();DockPanel.SetDock(heading,Dock.Top);root.Children.Add(heading);
        heading.Children.Add(new TextBlock{Text="열어 볼 시트를 선택하세요",FontSize=22,FontWeight=FontWeights.Bold});
        heading.Children.Add(new TextBlock{Text="시트를 선택하고 열기를 누르면 해당 시트가 브라우저에서 열립니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,4)});
        heading.Children.Add(search);
        var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);footer.Children.Add(status);
        var buttons=new WrapPanel();buttons.Children.Add(openButton);buttons.Children.Add(refresh);footer.Children.Add(buttons);root.Children.Add(list);
        search.TextChanged+=(_,_)=>Filter();list.SelectionChanged+=(_,_)=>openButton.IsEnabled=!loading&&list.SelectedItem is PublicSheet;
        openButton.Click+=(_,_)=>OpenSelected();refresh.Click+=async(_,_)=>await ReloadAsync();
        list.MouseDoubleClick+=(_,e)=>{if(ItemsControl.ContainerFromElement(list,e.OriginalSource as DependencyObject) is ListBoxItem)OpenSelected();};
        list.KeyDown+=(_,e)=>{if(e.Key==Key.Enter){OpenSelected();e.Handled=true;}};
        Loaded+=async(_,_)=>await ReloadAsync();
    }
    public async Task ReloadAsync()
    {
        if(loading)return;loading=true;refresh.IsEnabled=false;openButton.IsEnabled=false;list.IsEnabled=false;
        status.Text="공공 메뉴 자료의 시트 목록을 불러오는 중입니다…";
        try { sheets=await load();Filter(); }
        catch(Exception ex){sheets=[];Filter();status.Text="목록을 불러오지 못했습니다. 새로고침으로 다시 시도해 주세요. "+ex.Message;}
        finally {loading=false;refresh.IsEnabled=true;list.IsEnabled=true;openButton.IsEnabled=list.SelectedItem is PublicSheet;}
    }
    private void Filter()
    {
        var selected=list.SelectedItem as PublicSheet;
        var matches=sheets.Where(sheet=>sheet.Title.Contains(search.Text.Trim(),StringComparison.OrdinalIgnoreCase)).ToArray();
        list.ItemsSource=matches;list.SelectedItem=matches.FirstOrDefault(sheet=>sheet.Id==selected?.Id);
        status.Text=sheets.Count==0?"열 수 있는 시트가 없습니다.":matches.Length==0?"검색 결과가 없습니다.":$"시트 {matches.Length}개 · 열려는 시트를 선택해 주세요.";
    }
    private void OpenSelected()
    {
        if(loading||list.SelectedItem is not PublicSheet sheet)return;
        try{open(sheet);}catch(Exception ex){status.Text="시트를 열지 못했습니다. "+ex.Message;}
    }
}
