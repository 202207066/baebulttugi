using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace wpf;
public sealed class CatalogRequestsPage : Page
{
    private readonly ICatalogRequestsClient client;
    private readonly bool admin;
    private readonly string account;
    private CatalogRequestDraft draft;
    private readonly ComboBox kind = new() { ItemsSource = CatalogRequestDraft.Kinds, SelectedIndex = 0, Height = 38 };
    private readonly ComboBox track = new() { ItemsSource = new[] { "공통", "3-5", "6-18" }, SelectedIndex = 0, Height = 38 };
    private readonly TextBox name = Input(120), reference = Input(160), details = Input(4000,130), reason = Input(1500,90), source = Input(800);
    private readonly CheckBox consent = new() { Content = new TextBlock{Text="요청 내용과 로그인 계정을 관리자에게 전달합니다. 개인 명단·건강정보는 포함하지 않았습니다.",TextWrapping=TextWrapping.Wrap}, Margin = new Thickness(0,14,0,12) };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12), Foreground = Brushes.DarkGreen };
    private readonly ObservableCollection<CatalogRequestItem> items = [];
    private readonly DataGrid grid = new() { IsReadOnly = true, AutoGenerateColumns = false, MinHeight = 160, MaxHeight = 300, CanUserAddRows = false };
    private readonly TextBox selectedDetail = Input(20000,180), adminNote = Input(2000,100);
    private readonly ComboBox reviewState = new() { ItemsSource = CatalogRequestDraft.States, SelectedIndex = 0, Height = 38 };
    private readonly List<Button> buttons = [];
    private string? nextBefore;
    private bool busy;
    private static TextBox Input(int max,int height=38) => new() { Style = null, MaxLength = max, MinHeight = height, Height = height, Padding = new Thickness(10,6,10,6), FontSize = 14, TextWrapping = TextWrapping.Wrap, AcceptsReturn = height > 38, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderBrush = new SolidColorBrush(Color.FromRgb(205,220,209)), BorderThickness = new Thickness(1) };
    public CatalogRequestsPage(bool administrator = false, ICatalogRequestsClient? requestClient = null, string? currentAccount = null)
    {
        admin = administrator; client = requestClient ?? new CatalogRequestsClient(AppServices.Require());
        account = currentAccount ?? AppServices.Require().FacilityRegistry.CurrentUser?.Email ?? "";
        draft = admin ? new() : CatalogRequestDraftStore.Read(account) ?? new();
        Background = new SolidColorBrush(Color.FromRgb(245,248,245));
        var root = new StackPanel { Margin = new Thickness(28), MaxWidth = 1120, HorizontalAlignment = HorizontalAlignment.Stretch };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = admin ? "공식 자료 반영 요청 검토" : "메뉴·식재료 요청", FontSize = 26, FontWeight = FontWeights.Bold, Foreground = Brushes.DarkGreen });
        root.Children.Add(new TextBlock { Text = admin ? "접수된 요청을 검토하고 답변합니다. 상태 변경만으로 공식 자료가 수정되지는 않습니다." : "개인 메뉴는 메뉴(레시피) 관리에서 바로 저장할 수 있습니다. 이 화면은 공식 자료에 추가·수정을 제안하는 곳입니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,10,0,20), FontSize = 14 });
        if (!admin)
        {
            AddField(root,"요청 종류",kind); AddField(root,"적용 연령대",track); AddField(root,"메뉴·식재료 이름 *",name); AddField(root,"기존 메뉴 ID 또는 식재료 번호 (선택)",reference);
            AddField(root,"추가·수정할 내용 * — 재료명, 분량과 단위, 알레르기 정보 등을 적어 주세요",details);
            AddField(root,"요청 이유 *",reason); AddField(root,"참고 자료·출처 (선택)",source); root.Children.Add(consent);
            var actions=new WrapPanel(); root.Children.Add(actions);
            AddButton(actions,"이 PC에 임시 저장",()=>RunAsync(()=>{ReadForm();CatalogRequestDraftStore.Save(account,draft);status.Text="이 PC에 임시 저장했습니다. 아직 관리자에게 보내지 않았습니다.";return Task.CompletedTask;}));
            AddButton(actions,"공식 반영 요청 보내기",SubmitAsync,true);
            FillForm();
        }
        root.Children.Add(status);
        root.Children.Add(new TextBlock { Text=admin?"전체 접수 내역":"내 요청 내역", FontSize=20,FontWeight=FontWeights.Bold,Margin=new Thickness(0,12,0,12) });
        var listActions=new WrapPanel();root.Children.Add(listActions);AddButton(listActions,"새로고침",()=>LoadAsync());AddButton(listActions,"이전 요청 더 보기",()=>LoadAsync(true));
        grid.ItemsSource=items;grid.Columns.Add(new DataGridTextColumn{Header="접수일",Binding=new Binding("DateDisplay"),Width=145});
        grid.Columns.Add(new DataGridTextColumn{Header="종류",Binding=new Binding("Kind"),Width=100});grid.Columns.Add(new DataGridTextColumn{Header="이름",Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        grid.Columns.Add(new DataGridTextColumn{Header="상태",Binding=new Binding("Status"),Width=100});
        if(admin)grid.Columns.Add(new DataGridTextColumn{Header="보낸 계정",Binding=new Binding("Email"),Width=210});
        root.Children.Add(grid);selectedDetail.IsReadOnly=true;AddField(root,"선택한 요청 내용과 답변",selectedDetail);
        grid.SelectionChanged+=(_,_)=>{if(grid.SelectedItem is CatalogRequestItem row){selectedDetail.Text=row.DetailDisplay;reviewState.SelectedItem=row.Status;adminNote.Text=row.AdminNote;}};
        if(admin){AddField(root,"검토 상태",reviewState);AddField(root,"요청자에게 표시할 답변",adminNote);var reviewButtons=new WrapPanel();root.Children.Add(reviewButtons);AddButton(reviewButtons,"검토 결과 저장",ReviewAsync,true);}
        Loaded+=async(_,_)=>await LoadAsync();
    }
    private static void AddField(Panel parent,string label,FrameworkElement input){parent.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,6),FontSize=14,FontWeight=FontWeights.SemiBold});parent.Children.Add(input);}
    private void AddButton(Panel parent,string text,Func<Task> action,bool primary=false){var button=new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,10,10)};if(primary){button.Background=Brushes.DarkGreen;button.Foreground=Brushes.White;}button.Click+=async(_,_)=>await action();parent.Children.Add(button);buttons.Add(button);}
    private void ReadForm(){draft.Kind=kind.SelectedItem?.ToString()??"";draft.Track=track.SelectedItem?.ToString()??"";draft.Name=name.Text.Trim();draft.ReferenceId=reference.Text.Trim();draft.Details=details.Text.Trim();draft.Reason=reason.Text.Trim();draft.Source=source.Text.Trim();}
    private void FillForm(){kind.SelectedItem=draft.Kind;track.SelectedItem=draft.Track;name.Text=draft.Name;reference.Text=draft.ReferenceId;details.Text=draft.Details;reason.Text=draft.Reason;source.Text=draft.Source;consent.IsChecked=false;}
    private async Task RunAsync(Func<Task> action){if(busy)return;busy=true;foreach(var b in buttons)b.IsEnabled=false;foreach(var field in new Control[]{kind,track,name,reference,details,reason,source,consent,reviewState,adminNote})field.IsEnabled=false;try{await action();}catch(Exception ex){status.Text=ex is TaskCanceledException?"응답 시간이 지났습니다. 작성 내용은 유지됩니다. 다시 시도해 주세요.":ex.Message;}finally{busy=false;foreach(var b in buttons)b.IsEnabled=true;foreach(var field in new Control[]{kind,track,name,reference,details,reason,source,consent,reviewState,adminNote})field.IsEnabled=true;}}
    private Task SubmitAsync()=>RunAsync(async()=>{
        ReadForm();draft.Validate();if(consent.IsChecked!=true)throw new InvalidOperationException("전달할 내용과 계정 안내를 확인해 주세요.");
        CatalogRequestDraftStore.Save(account,draft);status.Text="요청을 보내는 중입니다…";
        var result=await client.SubmitAsync(draft);if(result.Id!=draft.Id)throw new InvalidOperationException("접수 번호를 확인하지 못했습니다. 작성 내용은 유지됩니다.");
        status.Text="요청이 접수됐습니다. 검토 후 공식 업데이트 반영 여부를 안내합니다. 접수 번호: "+result.Id;
        try{CatalogRequestDraftStore.Clear(account);}catch{ }
        draft=new();FillForm();
        try{var list=await client.ListAsync(false);items.Clear();foreach(var row in list.Items)items.Add(row);nextBefore=list.NextBefore;}catch{status.Text+="\n목록은 새로고침 버튼으로 다시 확인해 주세요.";}
    });
    private Task LoadAsync(bool more=false)=>RunAsync(async()=>{
        if(more&&nextBefore==null){status.Text="더 불러올 요청이 없습니다.";return;}
        var list=await client.ListAsync(admin,more?nextBefore:null);if(admin&&!list.IsAdmin)throw new InvalidOperationException("관리자 계정으로 로그인해 주세요.");
        if(!more)items.Clear();foreach(var row in list.Items)if(!items.Any(x=>x.Id==row.Id))items.Add(row);nextBefore=list.NextBefore;
        status.Text=items.Count==0?"아직 접수된 요청이 없습니다.":$"요청 {items.Count}건을 표시합니다.";
    });
    private Task ReviewAsync()=>RunAsync(async()=>{
        if(grid.SelectedItem is not CatalogRequestItem selected)throw new InvalidOperationException("검토할 요청을 선택해 주세요.");
        await client.ReviewAsync(selected,reviewState.SelectedItem?.ToString()??"",adminNote.Text);var list=await client.ListAsync(true);items.Clear();foreach(var row in list.Items)items.Add(row);nextBefore=list.NextBefore;status.Text="검토 결과를 저장했습니다. 공식 자료 변경은 업데이트할 때 반영해 주세요.";
    });
}
