using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
namespace wpf;
public sealed class CatalogRequestsPage : Page
{
    public event EventHandler? RequestsChanged;
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
    private readonly DataGrid grid = new() { IsReadOnly = true, AutoGenerateColumns = false, Height = 226, RowHeight = 44, ColumnHeaderHeight = 42, SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, CanUserAddRows = false };
    private readonly TextBox selectedDetail = Input(20000,150), adminNote = Input(2000,80);
    private readonly ComboBox reviewState = new() { ItemsSource = CatalogRequestDraft.States, SelectedIndex = 0, Height = 38 };
    private readonly List<Button> buttons = [];
    private string? nextBefore;
    private bool busy;
    private const int PageSize = 4;
    private int pageIndex;
    private readonly TextBox search = Input(120);
    private readonly ComboBox stateFilter = new() { ItemsSource = new[]{"전체 상태"}.Concat(CatalogRequestDraft.States).ToArray(), SelectedIndex = 0, Width = 150, Height = 38, Margin = new Thickness(8,0,8,10) };
    private string appliedSearch = "", appliedState = "전체 상태";
    private readonly Dictionary<string,(string State,string Note)> reviewDrafts = new();
    private List<CatalogRequestItem> VisibleRequests() => items.Where(x =>
        (appliedState == "전체 상태" || x.Status == appliedState) &&
        (appliedSearch.Length == 0 || x.Name.Contains(appliedSearch,StringComparison.OrdinalIgnoreCase))).ToList();

    private readonly Button previousPage = new() { Content = "◀ 이전", Padding = new Thickness(16,8,16,8), Margin = new Thickness(0,0,8,10) };
    private readonly Button nextPage = new() { Content = "다음 ▶", Padding = new Thickness(16,8,16,8), Margin = new Thickness(8,0,8,10) };
    private readonly TextBlock pageLabel = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0,0,0,10) };
    private readonly TabControl sections = new() { Background = Brushes.Transparent, BorderThickness = new Thickness(0) };

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
        root.Children.Add(status);
        if(!admin)root.Children.Add(sections);
        if (!admin)
        {
            var form = new StackPanel { Margin = new Thickness(12) };
            sections.Items.Add(new TabItem { Header = "요청 작성", Padding = new Thickness(20,10,20,10), Content = form });
            AddField(form,"요청 종류",kind); AddField(form,"적용 연령대",track); AddField(form,"메뉴·식재료 이름 *",name); AddField(form,"기존 메뉴 ID 또는 식재료 번호 (선택)",reference);
            AddField(form,"추가·수정할 내용 * — 재료명, 분량과 단위, 알레르기 정보 등을 적어 주세요",details);
            AddField(form,"요청 이유 *",reason); AddField(form,"참고 자료·출처 (선택)",source); form.Children.Add(consent);
            var actions=new WrapPanel(); form.Children.Add(actions);
            AddButton(actions,"이 PC에 임시 저장",()=>RunAsync(()=>{ReadForm();CatalogRequestDraftStore.Save(account,draft);status.Text="이 PC에 임시 저장했습니다. 아직 관리자에게 보내지 않았습니다.";return Task.CompletedTask;}));
            AddButton(actions,"공식 반영 요청 보내기",SubmitAsync,true);
            FillForm();
        }
        var history = new StackPanel { Margin = new Thickness(12) };
        if(admin)root.Children.Add(history);
        else sections.Items.Add(new TabItem { Header = "내 요청 내역", Padding = new Thickness(20,10,20,10), Content = history });
        root = history;
        root.Children.Add(new TextBlock { Text=admin?"전체 접수 내역":"내 요청 내역", FontSize=20,FontWeight=FontWeights.Bold,Margin=new Thickness(0,12,0,12) });
        var filters = new WrapPanel(); root.Children.Add(filters);
        search.Width=240;search.Margin=new Thickness(0,0,0,10);search.ToolTip="메뉴·식재료 이름 검색";
        filters.Children.Add(new TextBlock{Text="이름 검색",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,10)});
        filters.Children.Add(search);filters.Children.Add(stateFilter);
        AddButton(filters,"검색 · 필터 적용",ApplyFilterAsync);
        AddButton(filters,"전체 보기",()=>RunAsync(()=>{search.Clear();stateFilter.SelectedIndex=0;appliedSearch="";appliedState="전체 상태";pageIndex=0;ShowPage();return Task.CompletedTask;}));
        search.KeyDown+=async(_,e)=>{if(e.Key==System.Windows.Input.Key.Enter){e.Handled=true;await ApplyFilterAsync();}};
        var listActions=new WrapPanel();root.Children.Add(listActions);AddButton(listActions,"새로고침",()=>LoadAsync());
        listActions.Children.Add(previousPage); listActions.Children.Add(pageLabel); listActions.Children.Add(nextPage);
        previousPage.Click += async (_,_) => await MovePageAsync(-1);
        nextPage.Click += async (_,_) => await MovePageAsync(1);
        grid.PreviewMouseWheel += WeekMealTable.ScrollDashboard;
        foreach (var combo in new[]{kind,track,reviewState,stateFilter}) combo.PreviewMouseWheel += (sender,e) => { if(!combo.IsDropDownOpen)WeekMealTable.ScrollDashboard(sender,e); };
        UpdatePager();
        grid.Columns.Add(new DataGridTextColumn{Header="접수일",Binding=new Binding("DateDisplay"),Width=145});
        grid.Columns.Add(new DataGridTextColumn{Header="종류",Binding=new Binding("Kind"),Width=100});grid.Columns.Add(new DataGridTextColumn{Header="이름",Binding=new Binding("Name"),Width=new DataGridLength(1,DataGridLengthUnitType.Star)});
        grid.Columns.Add(new DataGridTextColumn{Header="상태",Binding=new Binding("Status"),Width=100});
        if(admin)grid.Columns.Add(new DataGridTextColumn{Header="보낸 계정",Binding=new Binding("Email"),Width=210});
        root.Children.Add(grid);selectedDetail.IsReadOnly=true;AddField(root,"선택한 요청 내용과 답변",selectedDetail);
        grid.SelectionChanged+=(_,e)=>{
            if(admin && e.RemovedItems.OfType<CatalogRequestItem>().FirstOrDefault() is { } old){
                var state=reviewState.SelectedItem?.ToString()??old.Status;
                if(state!=old.Status || adminNote.Text!=old.AdminNote)reviewDrafts[old.Id]=(state,adminNote.Text);
                else reviewDrafts.Remove(old.Id);
            }
            if(grid.SelectedItem is CatalogRequestItem row){
                selectedDetail.Text=row.DetailDisplay;
                if(admin && reviewDrafts.TryGetValue(row.Id,out var saved)){reviewState.SelectedItem=saved.State;adminNote.Text=saved.Note;}
                else {reviewState.SelectedItem=row.Status;adminNote.Text=row.AdminNote;}
                MarkSelectedRead();
            }
            else {selectedDetail.Text="요청을 선택하면 상세 내용과 관리자 답변이 표시됩니다."; reviewState.SelectedIndex=0; adminNote.Clear();}
        };
        if(admin){root.Children.Add(new TextBlock{Text="다른 요청으로 이동해도 이 화면의 답변 초안은 유지됩니다. 관리자에게 저장하려면 검토 결과 저장을 누르세요.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkGreen,Margin=new Thickness(0,10,0,0)});AddField(root,"검토 상태",reviewState);AddField(root,"요청자에게 표시할 답변",adminNote);var reviewButtons=new WrapPanel();root.Children.Add(reviewButtons);AddButton(reviewButtons,"검토 결과 저장",ReviewAsync,true);}
        Loaded+=async(_,_)=>await LoadAsync();
        selectedDetail.IsVisibleChanged+=(_,_)=>MarkSelectedRead();
        ShowPage();
    }
    private void MarkSelectedRead()
    {
        if(IsLoaded && selectedDetail.IsVisible && grid.SelectedItem is CatalogRequestItem row)
            CatalogRequestReadState.MarkRead(account,admin,row);
    }
    private static void AddField(Panel parent,string label,FrameworkElement input){parent.Children.Add(new TextBlock{Text=label,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,6),FontSize=14,FontWeight=FontWeights.SemiBold});parent.Children.Add(input);}
    private void AddButton(Panel parent,string text,Func<Task> action,bool primary=false){var button=new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,10,10)};if(primary){button.Background=Brushes.DarkGreen;button.Foreground=Brushes.White;}button.Click+=async(_,_)=>await action();parent.Children.Add(button);buttons.Add(button);}
    private void ReadForm(){draft.Kind=kind.SelectedItem?.ToString()??"";draft.Track=track.SelectedItem?.ToString()??"";draft.Name=name.Text.Trim();draft.ReferenceId=reference.Text.Trim();draft.Details=details.Text.Trim();draft.Reason=reason.Text.Trim();draft.Source=source.Text.Trim();}
    private void FillForm(){kind.SelectedItem=draft.Kind;track.SelectedItem=draft.Track;name.Text=draft.Name;reference.Text=draft.ReferenceId;details.Text=draft.Details;reason.Text=draft.Reason;source.Text=draft.Source;consent.IsChecked=false;}
    private async Task RunAsync(Func<Task> action){if(busy)return;busy=true;UpdatePager();grid.IsEnabled=false;foreach(var b in buttons)b.IsEnabled=false;foreach(var field in new Control[]{kind,track,name,reference,details,reason,source,consent,reviewState,adminNote,search,stateFilter})field.IsEnabled=false;try{await action();}catch(Exception ex){status.Text=ex is TaskCanceledException?"응답 시간이 지났습니다. 작성 내용은 유지됩니다. 다시 시도해 주세요.":ex.Message;}finally{busy=false;grid.IsEnabled=true;UpdatePager();foreach(var b in buttons)b.IsEnabled=true;foreach(var field in new Control[]{kind,track,name,reference,details,reason,source,consent,reviewState,adminNote,search,stateFilter})field.IsEnabled=true;}}
    private Task SubmitAsync()=>RunAsync(async()=>{
        ReadForm();draft.Validate();if(consent.IsChecked!=true)throw new InvalidOperationException("전달할 내용과 계정 안내를 확인해 주세요.");
        CatalogRequestDraftStore.Save(account,draft);status.Text="요청을 보내는 중입니다…";
        var result=await client.SubmitAsync(draft);if(result.Id!=draft.Id)throw new InvalidOperationException("접수 번호를 확인하지 못했습니다. 작성 내용은 유지됩니다.");
        status.Text="요청이 접수됐습니다. 검토 후 공식 업데이트 반영 여부를 안내합니다. 접수 번호: "+result.Id;
        try{CatalogRequestDraftStore.Clear(account);}catch{ }
        draft=new();FillForm();
        RequestsChanged?.Invoke(this,EventArgs.Empty);
        try{var list=await client.ListAsync(false);items.Clear();foreach(var row in list.Items)items.Add(row);nextBefore=list.NextBefore;pageIndex=0;ShowPage();sections.SelectedIndex=sections.Items.Count-1;}catch{status.Text+="\n목록은 새로고침 버튼으로 다시 확인해 주세요.";}
    });
    private Task LoadAsync(bool more=false)=>RunAsync(async()=>{
        if(more&&nextBefore==null){status.Text="더 불러올 요청이 없습니다.";return;}
        var list=await client.ListAsync(admin,more?nextBefore:null);if(admin&&!list.IsAdmin)throw new InvalidOperationException("관리자 계정으로 로그인해 주세요.");
        if(!more){items.Clear();appliedSearch="";appliedState="전체 상태";search.Clear();stateFilter.SelectedIndex=0;}foreach(var row in list.Items)if(!items.Any(x=>x.Id==row.Id))items.Add(row);nextBefore=list.NextBefore;pageIndex=0;ShowPage();
        status.Text=items.Count==0?"아직 접수된 요청이 없습니다.":$"불러온 요청 {items.Count}건 · 한 번에 4건씩 표시합니다.";
    });
    private void UpdatePager()
    {
        int total = VisibleRequests().Count;
        previousPage.IsEnabled = !busy && pageIndex > 0;
        nextPage.IsEnabled = !busy && ((pageIndex + 1) * PageSize < total || nextBefore != null);
        pageLabel.Text = total == 0 ? "요청 없음" : $"{pageIndex + 1}쪽 · {pageIndex * PageSize + 1}–{Math.Min((pageIndex + 1) * PageSize, total)}건";
    }
    private void ShowPage()
    {
        var visible = VisibleRequests();
        pageIndex = Math.Clamp(pageIndex, 0, Math.Max(0, (visible.Count - 1) / PageSize));
        grid.ItemsSource = visible.Skip(pageIndex * PageSize).Take(PageSize).ToList();
        grid.SelectedIndex = grid.Items.Count > 0 ? 0 : -1;
        if (grid.Items.Count == 0) { selectedDetail.Text = "표시할 요청이 없습니다. 검색어와 상태 조건을 확인해 주세요."; adminNote.Clear(); }
        UpdatePager();
    }
    private Task MovePageAsync(int direction) => RunAsync(async () =>
    {
        int target = Math.Max(0, pageIndex + direction);
        var visited = new HashSet<string>();
        while (direction > 0 && items.Count < (target + 1) * PageSize && nextBefore != null)
        {
            var cursor = nextBefore;
            if (!visited.Add(cursor)) throw new InvalidOperationException("다음 요청을 불러오지 못했습니다. 새로고침 후 다시 시도해 주세요.");
            var list = await client.ListAsync(admin, cursor);
            if (admin && !list.IsAdmin) throw new InvalidOperationException("관리자 계정으로 로그인해 주세요.");
            foreach (var row in list.Items) if (!items.Any(x => x.Id == row.Id)) items.Add(row);
            nextBefore = list.NextBefore;
        }
        if (target * PageSize < VisibleRequests().Count) pageIndex = target;
        ShowPage();
    });
    private Task ApplyFilterAsync() => RunAsync(async () =>
    {
        status.Text="이전 요청까지 확인하며 검색하는 중입니다…";
        var visited=new HashSet<string>();
        while(nextBefore!=null){
            var cursor=nextBefore;
            if(!visited.Add(cursor))throw new InvalidOperationException("전체 요청을 확인하지 못했습니다. 새로고침 후 다시 검색해 주세요.");
            var list=await client.ListAsync(admin,cursor);
            if(admin&&!list.IsAdmin)throw new InvalidOperationException("관리자 계정으로 로그인해 주세요.");
            foreach(var row in list.Items)if(!items.Any(x=>x.Id==row.Id))items.Add(row);
            nextBefore=list.NextBefore;
        }
        appliedSearch=search.Text.Trim();appliedState=stateFilter.SelectedItem?.ToString()??"전체 상태";
        pageIndex=0;ShowPage();status.Text=$"검색 결과 {VisibleRequests().Count}건 · 한 번에 4건씩 표시합니다.";
    });
    private Task ReviewAsync()=>RunAsync(async()=>{
        if(grid.SelectedItem is not CatalogRequestItem selected)throw new InvalidOperationException("검토할 요청을 선택해 주세요.");
        var savedState=reviewState.SelectedItem?.ToString()??"";var savedNote=adminNote.Text;
        await client.ReviewAsync(selected,savedState,savedNote);
        selected.Status=savedState;selected.AdminNote=savedNote;reviewDrafts.Remove(selected.Id);
        selectedDetail.Text=selected.DetailDisplay;
        CatalogRequestReadState.MarkRead(account,admin,selected);
        RequestsChanged?.Invoke(this,EventArgs.Empty);
        status.Text="검토 결과를 저장했습니다. 공식 자료 변경은 업데이트할 때 반영해 주세요.";
        try {
            var list=await client.ListAsync(true);
            if(!list.IsAdmin)throw new InvalidOperationException("관리자 권한을 확인해 주세요.");
            items.Clear();foreach(var row in list.Items)items.Add(row);nextBefore=list.NextBefore;pageIndex=0;appliedSearch="";appliedState="전체 상태";search.Clear();stateFilter.SelectedIndex=0;ShowPage();
        } catch { status.Text += "\n저장은 완료됐지만 목록을 갱신하지 못했습니다. 새로고침으로 최신 상태를 확인해 주세요."; }

    });
}
