using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;

internal static class FixChecks
{
    public static void Run(Action<bool,string> check)
    {
        var menu = new TrackMenu("x","15곡밥","밥류","",10,2,1,60,"");
        check(!GoogleSheetsService.MatchesRegisteredAllergen(menu,["5","15"]),"메뉴 이름의 숫자는 알레르기 코드와 일치하지 않습니다");
        foreach(var code in new[]{"1,2,5","①②⑤","난류,우유,대두"})
            check(GoogleSheetsService.MatchesRegisteredAllergen(menu with {Allergens=code},["달걀"]) && GoogleSheetsService.MatchesRegisteredAllergen(menu with {Allergens=code},["우유"]),"알레르기 구분 형식: "+code);
        check(GoogleSheetsService.NeedsAllergenReview(menu with {Allergens="125,0,20"}) && GoogleSheetsService.InvalidAllergenCodes("①②⑤").Count==0,"잘못된 숫자는 확인 필요이며 원문자 코드는 유효합니다");
        check(!AllergyAlternatives.KnownAllergens("1,125"),"혼합된 잘못된 코드는 확인된 대체식으로 취급하지 않습니다");
        check(GoogleSheetsService.MatchesRegisteredAllergen(menu with {Allergens="아몬드밀크"},["밀"]),"한 글자 부분 일치 현상을 재현하며 정책을 유지합니다");
        var calendarPage=new EventCalendar();
        var calendar=(Calendar)calendarPage.FindName("MainCalendar");
        calendar.DisplayDate=new DateTime(2026,10,1);
        void Wheel(int delta) { var e=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,delta){RoutedEvent=UIElement.PreviewMouseWheelEvent};calendar.RaiseEvent(e);check(e.Handled,"달력 휠은 바깥 스크롤로 전달되지 않습니다"); }
        Wheel(60);check(calendar.DisplayDate.Month==10,"미세 휠 입력은 누적합니다");
        Wheel(60);check(calendar.DisplayDate.Month==9,"120 단위에서 이전 달로 이동합니다");
        calendar.DisplayMode=CalendarMode.Year;Wheel(-120);check(calendar.DisplayDate.Year==2027,"연 보기에서 한 해 이동합니다");
        calendar.DisplayMode=CalendarMode.Decade;Wheel(-120);check(calendar.DisplayDate.Year==2037,"10년 보기에서 10년 이동합니다");
        calendar.DisplayDateEnd=new DateTime(2037,12,31);Wheel(-120);check(calendar.DisplayDate==calendar.DisplayDateEnd,"달력 범위 밖으로 이동하지 않습니다");
        var stored=ManagementChecks.Fixture();
        var row=new WeekMealRow("밥류",["밥","—"],[stored,null],0);
        check(MealTableEditing.CanEdit(row,1,true)&&!MealTableEditing.CanEdit(row,0,true)&&!MealTableEditing.CanEdit(row,2,true)&&!MealTableEditing.CanEdit(row with{MenuIndex=6},1,true)&&!MealTableEditing.CanEdit(row,1,false),"첫 열·빈 칸·열량·대시보드는 편집 반응이 없습니다");
        CacheChecks(check).GetAwaiter().GetResult();
    }

    private static async Task CacheChecks(Action<bool,string> check)
    {
        var handler=new MetadataHandler();
        var service=new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new SheetsService(new BaseClientService.Initializer{HttpClientFactory=handler}));
        service.SetUserSpreadsheetId("cache-test");
        var results=await Task.WhenAll(Enumerable.Range(0,8).Select(_=>service.GetSheetTitlesAsync())).ConfigureAwait(false);
        check(handler.Reads==1,"동시 탭 목록 요청 8개는 API 1회로 합칩니다");
        results[0].Clear();check((await service.GetSheetTitlesAsync().ConfigureAwait(false)).Count==1,"반환 목록 수정은 캐시를 오염시키지 않습니다");
        await service.GetSheetTitlesAsync(true).ConfigureAwait(false);check(handler.Reads==2,"강제 갱신은 캐시를 우회합니다");
        await service.AddSheetAsync("새 탭").ConfigureAwait(false);
        check((await service.GetSheetTitlesAsync().ConfigureAwait(false)).Contains("새 탭")&&handler.Reads==3,"탭 생성 성공 직후 목록을 갱신합니다");
        service.SetUserSpreadsheetId("other-book");await service.GetSheetTitlesAsync().ConfigureAwait(false);check(handler.Reads==4,"책 전환은 캐시를 무효화합니다");
        var scoped=service.ForFacility(new Facility("f","시설","other-book",1,true));await scoped.GetSheetTitlesAsync().ConfigureAwait(false);check(handler.Reads==4,"동일 책의 급식소 인스턴스는 캐시를 공유합니다");
        var cache=(Dictionary<string,(DateTime Time,string[] Titles)>)typeof(GoogleSheetsService).GetField("_sheetTitlesCache",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(service)!;
        cache["other-book"]=(DateTime.UtcNow.AddMinutes(-1),["만료된 탭"]);
        await service.GetSheetTitlesAsync().ConfigureAwait(false);check(handler.Reads==5,"TTL이 지난 목록은 다시 읽습니다");
        handler.Failures=1;await service.GetSheetTitlesAsync(true).ConfigureAwait(false);check(handler.Reads==7,"429 응답은 한 번 재시도합니다");
        handler.Failures=2;
        bool rejected=false;try { await service.GetSheetTitlesAsync(true).ConfigureAwait(false); } catch(Google.GoogleApiException) { rejected=true; }
        check(rejected && handler.Reads==9,"429 재시도 실패는 무한 반복하지 않습니다");
    }

    private sealed class MetadataHandler : HttpMessageHandler, IHttpClientFactory
    {
        public int Reads; public int Failures; private bool added;
        public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args)=>new(new ConfigurableMessageHandler(this));
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)
        {
            if(request.Method==HttpMethod.Post){added=true;return Json("{}");}
            Interlocked.Increment(ref Reads);await Task.Delay(20,cancellationToken).ConfigureAwait(false);
            if(Failures>0){Failures--;return new HttpResponseMessage(HttpStatusCode.TooManyRequests){Content=new StringContent("{\"error\":{\"code\":429,\"message\":\"테스트 제한\"}}",Encoding.UTF8,"application/json")};}
            return Json("{\"sheets\":[{\"properties\":{\"title\":\"기존 탭\"}}"+(added?",{\"properties\":{\"title\":\"새 탭\"}}":"")+"]}");
        }
        private static HttpResponseMessage Json(string body)=>new(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
    }
}
