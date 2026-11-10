using System.Reflection;
using System.Windows.Controls;
using wpf;
internal sealed class PreviewRequests : ICatalogRequestsClient
{
 public bool Fail,WrongReceipt,Admin=true; public int Submitted; public int BatchSize=100; public bool ListFail; public List<CatalogRequestItem> Items=[];
 public Task<CatalogRequestList> ListAsync(bool all,string? before=null){if(ListFail)throw new InvalidOperationException("목록 연결 실패");int offset=before==null?0:int.Parse(before);return Task.FromResult(new CatalogRequestList{Items=Items.Skip(offset).Take(BatchSize).ToList(),NextBefore=offset+BatchSize<Items.Count?(offset+BatchSize).ToString():null,IsAdmin=all&&Admin});}

 public Task<CatalogRequestReceipt> SubmitAsync(CatalogRequestDraft d){Submitted++;if(Fail)throw new InvalidOperationException("연결 실패 예시");if(WrongReceipt)return Task.FromResult(new CatalogRequestReceipt{Id=Guid.NewGuid().ToString()});if(!Items.Any(i=>i.Id==d.Id))Items.Add(new(){Id=d.Id,Kind=d.Kind,Name=d.Name,Track=d.Track,Details=d.Details,Reason=d.Reason,Source=d.Source,Email="example@example.invalid",Status="접수",Revision=1,CreatedAt=DateTimeOffset.Now.ToUnixTimeMilliseconds()});return Task.FromResult(new CatalogRequestReceipt{Id=d.Id,Status="접수"});}
 public Task ReviewAsync(CatalogRequestItem item,string status,string note){item.Status=status;item.AdminNote=note;item.Revision++;return Task.CompletedTask;}
}
internal static class RequestUiChecks
{
 public static T Field<T>(object instance,string name)=>(T)instance.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(instance)!;
 private static void Invoke(CatalogRequestsPage page,string method,params object[] args)=>((Task)typeof(CatalogRequestsPage).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(page,args)!).GetAwaiter().GetResult();
 public static void Fill(CatalogRequestsPage page){Field<TextBox>(page,"name").Text="두부 채소전";Field<TextBox>(page,"details").Text="두부 40g, 당근 10g, 달걀 15g / 1인분\n알레르기: 난류, 대두\n영양값은 출처 확인 후 기재 요청";Field<TextBox>(page,"reason").Text="실습용 채소 메뉴를 공식 목록에 제안합니다.";Field<TextBox>(page,"source").Text="설명용 가상 예시";}
 public static void Run(Action<bool,string> check)
 {
  string account="request-check-"+Guid.NewGuid().ToString("N")+"@example.invalid";
  var directory=typeof(CatalogRequestDraftStore).GetField("directoryPath",BindingFlags.NonPublic|BindingFlags.Static)!;var originalDirectory=directory.GetValue(null);directory.SetValue(null,System.IO.Path.Combine(AppContext.BaseDirectory,"RequestCheckDrafts"));
  try{
   var client=new PreviewRequests();var page=new CatalogRequestsPage(false,client,account);Fill(page);
   Invoke(page,"SubmitAsync");check(client.Submitted==0&&Field<TextBox>(page,"name").Text.Length>0,"Request submission requires explicit content/account confirmation");
   Field<CheckBox>(page,"consent").IsChecked=true;client.Fail=true;string id=Field<CatalogRequestDraft>(page,"draft").Id;Invoke(page,"SubmitAsync");
   check(Field<TextBox>(page,"name").Text=="두부 채소전"&&CatalogRequestDraftStore.Read(account)?.Id==id&&!Field<TextBlock>(page,"status").Text.Contains("접수됐"),"Failed request keeps the draft and never reports receipt");
   client.Fail=false;client.WrongReceipt=true;Invoke(page,"SubmitAsync");check(Field<CatalogRequestDraft>(page,"draft").Id==id&&Field<TextBox>(page,"name").Text.Length>0,"Mismatched server receipt does not clear the request");
   client.WrongReceipt=false;Invoke(page,"SubmitAsync");check(client.Items.Count==1&&client.Items[0].Id==id&&Field<TextBox>(page,"name").Text.Length==0&&CatalogRequestDraftStore.Read(account)==null,"Retry uses the same request ID and clears only after confirmed receipt");
   var admin=new CatalogRequestsPage(true,client,account);Invoke(admin,"LoadAsync",false);Field<DataGrid>(admin,"grid").SelectedIndex=0;Field<ComboBox>(admin,"reviewState").SelectedItem="검토 중";Field<TextBox>(admin,"adminNote").Text="분량 자료를 확인하겠습니다.";Invoke(admin,"ReviewAsync");check(client.Items[0].Status=="검토 중"&&client.Items[0].Revision==2,"Admin reply and status are returned in the shared request history");
   var pagedClient=new PreviewRequests { BatchSize=6, Items=Enumerable.Range(1,9).Select(i=>new CatalogRequestItem{Id=i.ToString(),Name="요청 "+i,Status="접수"}).ToList() };
   var paged=new CatalogRequestsPage(true,pagedClient,account);Invoke(paged,"LoadAsync",false);
   var listGrid=Field<DataGrid>(paged,"grid");
   check(listGrid.Items.Cast<CatalogRequestItem>().Select(x=>x.Id).SequenceEqual(new[]{"1","2","3","4"}),"Request list starts with four rows");
   Invoke(paged,"MovePageAsync",1);
   check(listGrid.Items.Cast<CatalogRequestItem>().Select(x=>x.Id).SequenceEqual(new[]{"5","6","7","8"}),"Request paging crosses server batch boundary without skipping rows");
   Invoke(paged,"MovePageAsync",1);
   check(listGrid.Items.Count==1&&((CatalogRequestItem)listGrid.Items[0]).Id=="9"&&!Field<Button>(paged,"nextPage").IsEnabled,"Last request page disables next arrow");
   Invoke(paged,"MovePageAsync",-1);
   check(listGrid.Items.Count==4&&((CatalogRequestItem)listGrid.Items[0]).Id=="5","Previous arrow restores earlier requests");
   Field<TextBox>(paged,"adminNote").Text="저장한 답변";pagedClient.ListFail=true;Invoke(paged,"ReviewAsync");
   check(pagedClient.Items[4].AdminNote=="저장한 답변"&&Field<TextBlock>(paged,"status").Text.Contains("저장은 완료"),"Saved review is reported correctly when subsequent list refresh fails");
   pagedClient.ListFail=false;Invoke(paged,"LoadAsync",false);
   Field<TextBox>(paged,"adminNote").Text="임시 답변";Field<ComboBox>(paged,"reviewState").SelectedItem="보류";
   listGrid.SelectedIndex=1;Field<TextBox>(paged,"adminNote").Text="다른 답변";listGrid.SelectedIndex=0;
   check(Field<TextBox>(paged,"adminNote").Text=="임시 답변"&&Field<ComboBox>(paged,"reviewState").SelectedItem?.ToString()=="보류","Switching requests retains each review draft and state");
   Field<TextBox>(paged,"search").Text="요청 9";Invoke(paged,"ApplyFilterAsync");
   check(listGrid.Items.Count==1&&((CatalogRequestItem)listGrid.Items[0]).Id=="9","Search finds requests beyond the first server batch");
   Field<TextBox>(paged,"search").Clear();Field<ComboBox>(paged,"stateFilter").SelectedItem="보류";Invoke(paged,"ApplyFilterAsync");
   check(listGrid.Items.Count==0,"Status filter uses saved status, not unsaved review drafts");
   Field<ComboBox>(paged,"stateFilter").SelectedIndex=0;Invoke(paged,"ApplyFilterAsync");
   check(Field<TextBox>(paged,"adminNote").Text=="임시 답변","Filtering away and returning retains unsaved reply");
   var readDirectory=typeof(CatalogRequestReadState).GetField("DirectoryPath",BindingFlags.Static|BindingFlags.NonPublic)!;
   var originalReadDirectory=readDirectory.GetValue(null);readDirectory.SetValue(null,System.IO.Path.Combine(AppContext.BaseDirectory,"ReadChecks-"+Guid.NewGuid().ToString("N")));
   try {
    var row=new CatalogRequestItem{Id="notice",Email=account,Status="접수",Revision=1};
    check(CatalogRequestReadState.CountUnread(account,true,new[]{row})==1&&CatalogRequestReadState.CountUnread(account,false,new[]{row})==0,"New request notifies administrator but is not a reply notification");
    CatalogRequestReadState.MarkRead(account,true,row);
    check(CatalogRequestReadState.CountUnread(account,true,new[]{row})==0,"Opening request clears its unread count");
    row.AdminNote="답변";row.Status="검토 중";row.Revision=2;
    check(CatalogRequestReadState.CountUnread(account,false,new[]{row})==1,"New reply produces an unread notification");
    CatalogRequestReadState.MarkRead(account,false,row);
    check(CatalogRequestReadState.CountUnread(account,false,new[]{row})==0&&CatalogRequestReadState.CountUnread("another@example.invalid",false,new[]{row})==1,"Read state persists separately for each account");
    row.Revision=3;row.AdminNote="추가 답변";
    check(CatalogRequestReadState.CountUnread(account,false,new[]{row})==1,"Updated reply becomes unread again");
   } finally {readDirectory.SetValue(null,originalReadDirectory);}
   client.Admin=false;var denied=new CatalogRequestsPage(true,client,account);Invoke(denied,"LoadAsync",false);check(Field<DataGrid>(denied,"grid").Items.Count==0&&Field<TextBlock>(denied,"status").Text.Contains("관리자 계정"),"Non-admin response does not populate the admin inbox");
  }finally{CatalogRequestDraftStore.Clear(account);directory.SetValue(null,originalDirectory);}
 }
}
