using System.Reflection;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;
internal static class ReleasePreview
{
 public static void Run()
 {
  var app=new Application();app.Resources=(ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml",UriKind.Relative));
  typeof(CatalogRequestDraftStore).GetField("directoryPath",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,System.IO.Path.Combine(AppContext.BaseDirectory,"GuideDrafts"));
  var service=new GoogleSheetsService(null!);service.SetUserSpreadsheetId("preview-only");
  typeof(GoogleSheetsService).GetField("_sheetsService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new SheetsService(new BaseClientService.Initializer{HttpClientFactory=new PreviewSheetFactory()}));AppServices.Sheets=service;
  var main=new wpf.Main(service);var load=(RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler),main,typeof(wpf.Main).GetMethod("Main_Loaded",BindingFlags.Instance|BindingFlags.NonPublic)!);main.Loaded-=load;
  main.Title="배불뚝이 2026.10.30 · 설명용 가상자료 | F2 요청 F3 관리자 F4 레시피 F5 대상자 F6 원가 F7 식단수정 F8 공식자료 F9 로그인";main.WindowState=WindowState.Normal;main.MinWidth=1000;main.MinHeight=600;main.Width=1260;main.Height=720;main.Left=0;main.Top=0;
  ((TextBlock)main.FindName("SideAccountName")).Text="설명용 계정";((TextBlock)main.FindName("SideAccountEmail")).Text="example@example.invalid";((TextBlock)main.FindName("SideAccountInitial")).Text="예";((TextBlock)main.FindName("FacilityCount")).Text="설명용 급식소 · 30명";
  var frame=(Frame)main.FindName("MainFrame");var client=new PreviewRequests();
  void Navigate(Page page,string title,string button){((TextBlock)main.FindName("TxtPageTitle")).Text=title;((TextBlock)main.FindName("TxtPageSubtitle")).Text="설명용 가상자료 · 실제 앱 화면";if(main.FindName(button) is RadioButton radio)radio.IsChecked=true;frame.Navigate(page);}
  void Requests(bool admin){var page=new CatalogRequestsPage(admin,client,"guide-preview@example.invalid");if(!admin)RequestUiChecks.Fill(page);Navigate(page,admin?"관리자 요청함":"메뉴·식재료 요청",admin?"BtnCatalogAdmin":"BtnCatalogRequests");}
  main.PreviewKeyDown+=(_,e)=>{
   if(Keyboard.Modifiers!=ModifierKeys.None)return;
   if(e.Key==Key.F2){Requests(false);e.Handled=true;}
   if(e.Key==Key.F3){if(client.Items.Count==0)client.Items.Add(new(){Id=Guid.NewGuid().ToString(),Name="두부 채소전",Kind="메뉴 추가",Details="두부 40g, 당근 10g, 달걀 15g",Reason="설명용 가상 요청",Track="3-5",Status="접수",Email="example@example.invalid",Revision=1,CreatedAt=DateTimeOffset.Now.ToUnixTimeMilliseconds()});((RadioButton)main.FindName("BtnCatalogAdmin")).Visibility=Visibility.Visible;Requests(true);e.Handled=true;}
   if(e.Key==Key.F4){Navigate(new MenuInputPage(),"메뉴(레시피) 관리","BtnMenuManage");e.Handled=true;}
   if(e.Key==Key.F5){Navigate(new AllergyManagementPage(),"피급식자 · 알러지","");e.Handled=true;}
   if(e.Key==Key.F6){Navigate(new CalculatePage(),"원가 · 소요량 계산","BtnCalculate");e.Handled=true;}
   if(e.Key==Key.F7){new MealEditWindow(PreviewMeal(),OfficialCatalog.Menus("3-5")){Owner=main}.Show();e.Handled=true;}
   if(e.Key==Key.F8){new PublicSheetWindow(()=>service.GetPublicSheetsAsync(),sheet=>new OfficialCatalogWindow(sheet){Owner=main}.Show()){Owner=main}.Show();e.Handled=true;}
   if(e.Key==Key.F9){new LoginWindow{Owner=main}.Show();e.Handled=true;}
  };
  main.Closed+=(_,_)=>Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
  Requests(false);main.Show();Dispatcher.Run();
 }
 public static StoredMeal PreviewMeal(){var catalog=OfficialCatalog.Menus("3-5");var selected=WeeklyMealPlanner.Categories.Select(c=>catalog.First(m=>m.Category==c&&m.HasNutrition&&!GoogleSheetsService.NeedsAllergenReview(m))).ToArray();var old=ManagementChecks.Fixture();old.Raw[1]="2026-10-30";return new(){Raw=GoogleSheetsService.EditedMealValues(old,selected).ToList(),Date=new(2026,10,30),RowIndex=1};}
 private sealed class PreviewSheetFactory:Google.Apis.Http.IHttpClientFactory{public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args)=>new(new Google.Apis.Http.ConfigurableMessageHandler(new PreviewSheets()));}
 private sealed class PreviewSheets:HttpMessageHandler
 {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   if(request.Method!=HttpMethod.Get)throw new InvalidOperationException("설명용 화면에서는 실제 구글 저장을 하지 않습니다.");
   string url=Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);object result;
   if(url.Contains("/values/")){
    List<IList<object>> rows=[];
    if(url.Contains("알러지")||url.Contains("알레르기"))rows=[new object[]{"ID","성명","구분","특이 알러지 성분","비고","알레르기 상태"},new object[]{1,"대상자-0001","일반","2","가상 예시","있음 확인"},new object[]{2,"대상자-0002","일반","","가상 예시","미확인"}];
    else if(url.Contains(AppConfig.CostSheetName))rows=[GoogleSheetsService.CostHeaders.Cast<object>().ToArray()];
    else if(url.Contains(AppConfig.WeeklyMenuSheetName))rows=[GoogleSheetsService.WeeklyHeaders.Cast<object>().ToArray(),PreviewMeal().Raw];
    else if(url.Contains("식수"))rows=[new object[]{"이름","나이","성별","특이사항"}];
    result=new{values=rows};
   }else result=new{sheets=new[]{AppConfig.WeeklyMenuSheetName,AppConfig.PatientSheetName,AppConfig.CostSheetName,"식수인원"}.Select((title,i)=>new{properties=new{title,sheetId=i+1,gridProperties=new{columnCount=40,rowCount=100}}})};
   return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(result),Encoding.UTF8,"application/json")});
  }
 }
}
