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

internal static class NovemberPreview
{
    public static void Run(string folder)
    {
        System.IO.Directory.CreateDirectory(folder);
        var catalog = FixtureCatalog(); typeof(OfficialCatalog).GetField("current", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, catalog);
        var app = new Application(); app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml", UriKind.Relative));
        var http = new NovemberSheets();
        var service = new GoogleSheetsService(null!); service.SetUserSpreadsheetId("november-isolated-test");
        typeof(GoogleSheetsService).GetField("_sheetsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service, new SheetsService(new BaseClientService.Initializer { HttpClientFactory = new NovemberFactory(http) })); AppServices.Sheets = service;
        var main = new wpf.Main(service);
        main.Loaded -= (RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler), main, typeof(wpf.Main).GetMethod("Main_Loaded", BindingFlags.Instance | BindingFlags.NonPublic)!);
        main.Title = "배불뚝이 · 기능 검증용 가상자료 | F1 대상자 F2 식단생성 F3 반찬수정 F4 원가 F5 PDF F6 공통단가";
        main.WindowState = WindowState.Normal; main.Width = 1260; main.Height = 850; main.MinWidth = 1000; main.MinHeight = 600; main.Left = 0; main.Top = 0;
        ((TextBlock)main.FindName("SideAccountName")).Text = "기능 검증용 계정"; ((TextBlock)main.FindName("SideAccountEmail")).Text = "실계정 연결 없음"; ((TextBlock)main.FindName("FacilityCount")).Text = "가상 급식소 · 30명";
        var frame = (Frame)main.FindName("MainFrame");
        void Navigate(Page page, string title) { ((TextBlock)main.FindName("TxtPageTitle")).Text = title; ((TextBlock)main.FindName("TxtPageSubtitle")).Text = "검증용 가상 식재료·영양값 · 실제 앱 코드 · 실제 Google 전송 없음"; frame.Navigate(page); }
        void Planner() { var page = new Menu_table("3-5"); ((DatePicker)page.FindName("WeekPicker")).SelectedDate = new(2026, 11, 2); Navigate(page, "식단 만들기 · 우유·참깨 제외 검증"); }
        main.PreviewKeyDown += async (_, e) => {
            if (Keyboard.Modifiers != ModifierKeys.None) return;
            try {
                if (e.Key == Key.F1) { Navigate(new AllergyManagementPage(), "피급식자 · 우유·참깨 테스트"); e.Handled = true; }
                if (e.Key == Key.F2) { Planner(); e.Handled = true; }
                if (e.Key == Key.F3) {
                    e.Handled = true; var meal = (await service.GetStoredMealsAsync()).FirstOrDefault() ?? throw new InvalidOperationException("한 주 식단을 먼저 생성해 주세요.");
                    var patients = await service.GetMealReviewPatientsAsync(); var window = new MealEditWindow(meal, OfficialCatalog.Menus("3-5"), patients: patients) { Owner = main };
                    if (window.ShowDialog() == true && window.Result == MealEditResult.Save && ManualAllergyReview.Confirm(window.Selection, patients, main)) {
                        await service.ChangeStoredMealAsync(meal, window.Selection); MessageBox.Show(main, $"저장 완료 · 반찬 {window.Selection.Count(m => MenuClassification.IsSide(m))}개", "검증용 저장");
                    }
                }
                if (e.Key == Key.F4) { Navigate(new CalculatePage(), "원가 · 소요량 계산 · 5개 반찬 검증"); e.Handled = true; }
                if (e.Key == Key.F5) {
                    e.Handled = true; var meals = await service.GetStoredMealsAsync(); var displays = await service.DisplayMealsAsync(meals);
                    string file = System.IO.Path.Combine(folder, "반찬5개_검증용_식단표.pdf"); MealPlanPdf.Save(file, "검증용 가상 급식소", new(2026, 11, 2), displays);
                    var pages = MealPlanPdf.CreatePages("검증용 가상 급식소", new(2026, 11, 2), displays);
                    var stack = new StackPanel(); foreach (var page in pages) stack.Children.Add(new Viewbox { Child = page, Width = 720, Stretch = System.Windows.Media.Stretch.Uniform });
                    new Window { Title = "저장한 식단 PDF 미리보기 · 반찬 5개 · 가상자료", Width = 780, Height = 850, Owner = main, Content = new ScrollViewer { Content = stack, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } }.Show();
                    System.IO.File.WriteAllText(System.IO.Path.Combine(folder, "검증_저장상태.json"), JsonSerializer.Serialize(new { Days = meals.Count, FirstMealMenuCount = meals[0].Menus.Length, MainPerMeal = meals.Select(m => m.SavedItems!.Count(i => i.Category == "주찬" && i.ProteinGrade == "고단백")), HttpCalls = http.Calls, ActualGoogleCalls = 0 }, new JsonSerializerOptions { WriteIndented = true }));
                }
                if (e.Key == Key.F6) { new IngredientStandardsWindow(service) { Owner = main }.Show(); e.Handled = true; }
            } catch (Exception ex) { MessageBox.Show(main, ex.Message, "검증 확인"); }
        };
        main.Closed += (_, _) => Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
        Navigate(new AllergyManagementPage(), "피급식자 · 우유·참깨 테스트"); main.Show(); Dispatcher.Run();
    }
    private static OfficialCatalogData FixtureCatalog()
    {
        var data = new OfficialCatalogData { SchemaVersion = 1, Version = "2026.11.3.999", Source = "기능 검증용 가상자료" };
        string[][] names = [["쌀밥", "기장밥", "보리밥", "현미밥", "수수밥"], ["무국", "배추국", "미역국", "감자국", "애호박국"], ["닭살구이", "생선구이", "쇠고기볶음", "돼지고기구이", "달걀찜"], ["당근무침", "애호박볶음", "시금치무침", "오이무침", "브로콜리무침"], ["배추김치", "백김치", "무김치", "오이김치", "열무김치"], ["사과", "바나나", "귤", "배", "포도"]];
        for (int c = 0; c < names.Length; c++) foreach (string name in names[c]) {
            var menu = NovemberChecks.Reviewed(name, WeeklyMealPlanner.Categories[c]); data.Menus.Add(new() { Track = "A", Menu = menu });
            data.Recipes.Add(new() { Track = "A", Key = menu.Key, Name = name, Category = menu.Category, Source = "기능 검증용 가상 재료·분량", Ingredients = [new() { Name = "검증재료", Amount = 30, Unit = "g" }] });
        }
        foreach (var tuple in new[] { ("우유감자볶음", "2", "없음 확인"), ("참깨채소무침", "없음", "포함") }) {
            var menu = NovemberChecks.Reviewed(tuple.Item1, "부찬", allergens: tuple.Item2) with { Sesame = tuple.Item3 };
            data.Menus.Add(new() { Track = "A", Menu = menu }); data.Recipes.Add(new() { Track = "A", Key = menu.Key, Name = menu.Name, Category = "부찬", Source = "기능 검증용 가상 재료", Ingredients = [new() { Name = "검증재료", Amount = 30, Unit = "g" }] });
        }
        return data;
    }
}
internal sealed class NovemberFactory(NovemberSheets handler) : Google.Apis.Http.IHttpClientFactory { public Google.Apis.Http.ConfigurableHttpClient CreateHttpClient(Google.Apis.Http.CreateHttpClientArgs args) => new(new Google.Apis.Http.ConfigurableMessageHandler(handler)); }
internal sealed class NovemberSheets : HttpMessageHandler
{
    public int Calls;
    private readonly Dictionary<string, List<IList<object>>> tables = new() {
        [AppConfig.WeeklyMenuSheetName] = [GoogleSheetsService.WeeklyHeaders.Cast<object>().ToArray()],
        [AppConfig.PatientSheetName] = [new object[] { "ID", "성명", "구분", "특이 알러지 성분", "비고", "알레르기 상태" }, new object[] { 1, "테스트-우유참깨", "일반", "2,참깨", "화면 검증용 가상 대상자", "있음 확인" }],
        [AppConfig.CostSheetName] = [GoogleSheetsService.CostHeaders.Cast<object>().ToArray(), new object[] { IngredientStandards.Marker, "검증재료", "재료 공통 기준", "", "", "5000", "1", "kg", "", "MASTER:검증재료", "10", "100" }],
        [AppConfig.EventSheetName] = [new object[] { "날짜", "내용" }],
        ["식수인원"] = [new object[] { "이름", "나이", "성별", "특이사항" }, new object[] { "테스트-우유참깨", "5", "", "" }]
    };
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++; string path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath); object response;
        if (!path.StartsWith("/v4/spreadsheets/november-isolated-test")) throw new InvalidOperationException("검증 환경에서 외부 접근을 차단했습니다.");
        if (request.Method == HttpMethod.Post) {
            var bytes = await request.Content!.ReadAsByteArrayAsync(ct); string body;
            if (bytes.Length > 1 && bytes[0] == 0x1f && bytes[1] == 0x8b) { using var zip = new System.IO.Compression.GZipStream(new System.IO.MemoryStream(bytes), System.IO.Compression.CompressionMode.Decompress); using var reader = new System.IO.StreamReader(zip); body = await reader.ReadToEndAsync(ct); } else body = Encoding.UTF8.GetString(bytes);
            using var doc = JsonDocument.Parse(body);
            List<IList<object>> Table(int id) => tables.ElementAt(id - 1).Value;
            IList<object> Values(JsonElement row) => row.GetProperty("values").EnumerateArray().Select(c => { if (!c.TryGetProperty("userEnteredValue", out var v)) return (object)""; return v.TryGetProperty("numberValue", out var n) ? (object)n.GetDouble() : v.GetProperty("stringValue").GetString()!; }).ToList();
            foreach (var r in doc.RootElement.GetProperty("requests").EnumerateArray()) {
                if (r.TryGetProperty("appendCells", out var append)) Table(append.GetProperty("sheetId").GetInt32()).AddRange(append.GetProperty("rows").EnumerateArray().Select(Values));
                if (r.TryGetProperty("updateCells", out var update)) { var pos = update.GetProperty("start"); var table = Table(pos.GetProperty("sheetId").GetInt32()); int index = pos.GetProperty("rowIndex").GetInt32(); foreach (var row in update.GetProperty("rows").EnumerateArray()) { while (table.Count <= index) table.Add([]); var values = Values(row); while (table[index].Count < values.Count) table[index] = table[index].Append<object>("").ToList(); for (int c = 0; c < values.Count; c++) table[index][c] = values[c]; index++; } }
                if (r.TryGetProperty("deleteDimension", out var delete)) { var range = delete.GetProperty("range"); if (range.GetProperty("dimension").GetString() == "ROWS") Table(range.GetProperty("sheetId").GetInt32()).RemoveRange(range.GetProperty("startIndex").GetInt32(), range.GetProperty("endIndex").GetInt32() - range.GetProperty("startIndex").GetInt32()); }
            }
            response = new { };
        } else if (path.Contains("/values/")) {
            string range = path.Split("/values/")[1]; string title = range.Split('!')[0].Trim('\''); response = new { values = tables.GetValueOrDefault(title, []) };
        } else response = new { sheets = tables.Keys.Select((title, i) => new { properties = new { title, sheetId = i + 1, gridProperties = new { columnCount = 50, rowCount = 2000 } } }) };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(response), Encoding.UTF8, "application/json") };
    }
}
