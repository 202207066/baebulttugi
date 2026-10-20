using System.Windows;
using System.Windows.Controls;
using System.Net;
using System.Net.Http;
using System.Reflection;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;
internal static class UiSelectionChecks
{
    public static void Run(Action<bool,string> check, bool preview)
    {
        var meal=ManagementChecks.Fixture();
        var editor=new MealEditWindow(meal,[],2);
        var root=(StackPanel)((ScrollViewer)editor.Content).Content;
        var section=root.Children.OfType<Expander>().Single();
        double compactWidth=editor.Width,compactHeight=editor.Height;
        check(!section.IsExpanded && compactWidth<=520 && compactHeight<=380,"Single-menu editor starts compact with all six selections preserved");
        if(preview)Render(editor,"editor-compact.png",false);
        section.IsExpanded=true;
        check(editor.IsMenuExpanded && editor.Width>=compactWidth && editor.Height>compactHeight && editor.Height<=SystemParameters.WorkArea.Height,"Plus expands editor within screen work area");
        if(preview)Render(editor,"editor-expanded.png",false);
        section.IsExpanded=false;
        check(editor.Width==compactWidth&&editor.Height==compactHeight&&editor.Selection.Length==6,"Collapse restores compact size without changing meal selections");
        var day=new DayMealEditWindow(meal.Date,[meal],[]);
        var tabs=((DockPanel)day.Content).Children.OfType<TabControl>().Single();tabs.SelectedIndex=0;
        var dayRoot=(StackPanel)((ScrollViewer)((TabItem)tabs.Items[0]).Content).Content;
        var daySection=dayRoot.Children.OfType<Expander>().First();double dayHeight=day.Height;
        daySection.IsExpanded=true;
        check(day.Height>dayHeight&&day.Changes.Count==0,"Day editor grows when its embedded menu expands without changing food data");
        daySection.IsExpanded=false;check(day.Height==dayHeight,"Day editor returns to compact size");
        int opened=-1,loads=0;bool fail=false;
        var chooser=new PublicSheetWindow(()=>{
            loads++;if(fail)throw new InvalidOperationException("test offline");
            return Task.FromResult<IReadOnlyList<PublicSheet>>([new(0,"트랙 A 메뉴"),new(42,"트랙 B 영양정보"),new(97,"사용 설명")]);
        },sheet=>opened=sheet.Id);
        chooser.ReloadAsync().GetAwaiter().GetResult();
        var dock=(DockPanel)chooser.Content;
        var list=dock.Children.OfType<ListBox>().Single();
        var search=dock.Children.OfType<StackPanel>().First().Children.OfType<TextBox>().Single();
        var footer=dock.Children.OfType<StackPanel>().Last();
        var open=footer.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().First();
        check(list.Items.Count==3&&opened==-1&&!open.IsEnabled,"Public sheets load inside app without launching browser");
        search.Text="영양";check(list.Items.Count==1&&loads==1,"Sheet search filters locally without extra API reads");
        list.SelectedIndex=0;open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        check(opened==42&&PublicSheet.Url("test-book",42).EndsWith("/edit#gid=42"),"Open targets the selected sheet gid");
        search.Text="없는 시트";check(list.Items.Count==0&&!open.IsEnabled,"No results disables open rather than opening whole database");
        search.Text="";if(preview)Render(chooser,"public-sheet-picker.png",false);
        fail=true;chooser.ReloadAsync().GetAwaiter().GetResult();check(list.Items.Count==0&&!open.IsEnabled,"Failed refresh clears stale sheet choices");
        fail=false;chooser.ReloadAsync().GetAwaiter().GetResult();check(list.Items.Count==3,"Sheet list retries successfully after a read failure");
        var handler=new PublicMetadataHttp();var service=new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new SheetsService(new BaseClientService.Initializer{HttpClientFactory=new MetadataFactory(handler)}));
        var sheets=service.GetPublicSheetsAsync().GetAwaiter().GetResult();
        check(sheets.Count==2&&sheets[0].Id==0&&sheets[1].Id==42&&handler.Calls==1,"Metadata returns real visible sheet IDs including gid zero in one GET");
        check(handler.Path.EndsWith("/"+AppConfig.TemplateSpreadsheetId),"Public sheet lookup targets shared template, not personal database");
    }
    private static void Render(Window window,string name,bool unused)
    {
        var content=window.Content;window.Content=null;
        var page=new Page{Content=content,Background=System.Windows.Media.Brushes.White};
        FeedbackChecks.Render(page,System.IO.Path.Combine("outputs",name),(int)window.Width,(int)window.Height);
        page.Content=null;window.Content=content;
    }
    private sealed class MetadataFactory(PublicMetadataHttp handler):Google.Apis.Http.IHttpClientFactory
    {
        public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args)=>new(new Google.Apis.Http.ConfigurableMessageHandler(handler));
    }
    private sealed class PublicMetadataHttp:HttpMessageHandler
    {
        public int Calls;public string Path="";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            if(request.Method!=HttpMethod.Get)throw new Exception("Unexpected write");
            Calls++;Path=request.RequestUri!.AbsolutePath;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"sheets\":[{\"properties\":{\"sheetId\":0,\"title\":\"메뉴\"}},{\"properties\":{\"sheetId\":42,\"title\":\"영양\"}},{\"properties\":{\"sheetId\":99,\"title\":\"숨김\",\"hidden\":true}}]}")});
        }
    }
}

