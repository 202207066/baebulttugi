using System.Reflection;
using System.Windows.Controls;
using wpf;
internal sealed class PreviewRequests : ICatalogRequestsClient
{
 public bool Fail,WrongReceipt,Admin=true; public int Submitted; public List<CatalogRequestItem> Items=[];
 public Task<CatalogRequestList> ListAsync(bool all,string? before=null)=>Task.FromResult(new CatalogRequestList{Items=Items.ToList(),IsAdmin=all&&Admin});
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
   client.Admin=false;var denied=new CatalogRequestsPage(true,client,account);Invoke(denied,"LoadAsync",false);check(Field<DataGrid>(denied,"grid").Items.Count==0&&Field<TextBlock>(denied,"status").Text.Contains("관리자 계정"),"Non-admin response does not populate the admin inbox");
  }finally{CatalogRequestDraftStore.Clear(account);directory.SetValue(null,originalDirectory);}
 }
}
