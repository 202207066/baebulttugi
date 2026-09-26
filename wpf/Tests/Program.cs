using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;

internal static class Program
{
    private static int count;
    private static void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
    private static void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { Check(true, name); return; } catch (InvalidOperationException) { Check(true, name); return; } throw new Exception(name); }

    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception ex) { Console.Error.WriteLine(ex); Environment.ExitCode = 1; }
    }

    private static void Run(string[] args)
    {
        if (args.Length > 1 && args[0] == "sidebar-preview")
        {
            var previewApp = new wpf.App(); previewApp.InitializeComponent();
            var window = new wpf.Main(new GoogleSheetsService(null!));
            var root = (FrameworkElement)window.Content;
            ((System.Windows.Controls.TextBlock)window.FindName("SideAccountName")).Text = "배불뚝이";
            ((System.Windows.Controls.TextBlock)window.FindName("SideAccountEmail")).Text = "example@gmail.com";
            ((System.Windows.Controls.TextBlock)window.FindName("SideAccountInitial")).Text = "배";
            ((System.Windows.Controls.TextBlock)window.FindName("FacilityCount")).Text = "추가 버튼으로 급식소를 등록하세요";
            root.Measure(new Size(1480,880)); root.Arrange(new Rect(0,0,1480,880)); root.UpdateLayout();
            var selector = (System.Windows.Controls.ComboBox)window.FindName("FacilityBox");
            var toggle = (FrameworkElement)selector.Template.FindName("SelectorToggle", selector);
            Check(toggle.ActualWidth >= selector.ActualWidth - 2, "Selector background fills available width");
            selector.ItemsSource = new[] { new Facility("default", "유한대", "preview", 150, true), new Facility("second", "두 번째 급식소", "preview-2", 80, false) };
            selector.SelectedIndex = 0; root.UpdateLayout();
            var selectedName = (System.Windows.Controls.TextBlock)selector.Template.FindName("SelectedFacilityName", selector);
            Check(selectedName.Text == "유한대", "Selected facility displays its name instead of record contents");
            selector.SelectedIndex = 1; root.UpdateLayout();
            Check(selectedName.Text == "두 번째 급식소", "Selected name updates when switching facilities");
            selector.SelectedIndex = -1; root.UpdateLayout();
            Check(string.IsNullOrEmpty(selectedName.Text), "Empty selection clears previous facility name");
            selector.SelectedIndex = 0; root.UpdateLayout();
            var bitmap = new RenderTargetBitmap(1480,880,96,96,PixelFormats.Pbgra32); bitmap.Render(root);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var output = File.Create(args[1]); encoder.Save(output);
            return;
        }
        if (args.Contains("facilities")) { FacilityChecks.Run(); return; }
        var menus = WeeklyMealPlanner.Categories.Select((c, i) => new TrackMenu("A" + i, "Menu" + i, c, "", 10, 3, 2, null, "test fixture")).ToList();
        var target = new MealTargets(60, 18, 12, 0);
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
        List<WeeklyMeal> Generate(TrackMenu[]? custom = null, DayOfWeek[]? days = null, string[]? meals = null, MealTargets? goals = null) =>
            WeeklyMealPlanner.Generate(custom ?? menus.ToArray(), new DateTime(2026, 9, 20), days ?? weekdays, meals ?? ["중식"], "3-5", "Track A", goals ?? target, seed: 42);
        var week = Generate();
        Check(week.Count == 5 && week[0].Date == new DateTime(2026,9,14) && week[^1].Date.DayOfWeek == DayOfWeek.Friday, "Sunday selection resolves to Monday-Friday");
        Check(week.All(m => m.Items.Length == 6 && m.Carb == 60 && m.Protein == 18 && m.Fat == 12 && m.Calories == 420 && m.WithinTolerance), "Six actual portions sum correctly, including macro-derived calories");
        Check(Generate(days: Enum.GetValues<DayOfWeek>(), meals: ["조식","중식","석식"]).Count == 21, "Seven days and three meals yield 21 rows");
        Check(WeeklyMealPlanner.TrackForAge("6-11") == "B" && WeeklyMealPlanner.TrackForAge("12-18") == "B" && WeeklyMealPlanner.TrackForAge("3-5") == "A", "Age routing");
        Reject(() => WeeklyMealPlanner.TrackForAge("adult"), "Unsupported age rejected");
        Reject(() => Generate(days: []), "No selected days rejected");
        Reject(() => Generate(meals: []), "No selected meals rejected");
        Reject(() => Generate(goals: target with { Carb = double.NaN }), "Nonfinite target rejected");
        Reject(() => Generate(goals: target with { Fat = 0 }), "Zero target rejected");
        Reject(() => Generate(custom: menus.Select(m => m with { Carb = null }).ToArray()), "Missing nutrition never treated as zero");
        Check(WeeklyMealPlanner.Number("0") == 0 && WeeklyMealPlanner.Number("") == null && WeeklyMealPlanner.Number("-1") == null && WeeklyMealPlanner.Number("NaN") == null && WeeklyMealPlanner.Number("12.5 g") == 12.5, "Strict numeric parsing preserves true zero");
        Check(Generate(goals: new MealTargets(500, 500, 500, 15)).All(m => !m.WithinTolerance), "Unattainable goals labelled outside tolerance");
        var boy = NutritionProfiles.Get("9-11", "남").PerMeal(3);
        Check(boy.Calories == 666.7 && boy.Carb == 95.8 && boy.Protein == 25 && boy.Fat == 16.7, "Age and sex profile calculates per-meal energy and macro targets from KDRI ratios");
        Check(NutritionProfiles.Get("3-5", "여").DailyCalories == 1400, "Preschool profile uses shared 3-5 age standard");
        Check(GoogleSheetsService.MatchesRegisteredAllergen(menus[0] with { Allergens = "⑤⑥⑩⑬" }, ["대두"]), "Circled allergy codes map to names");
        Check(GoogleSheetsService.MatchesRegisteredAllergen(menus[0] with { Allergens = "1,5,6,13" }, ["달걀"]), "Comma-separated allergy codes map to synonyms");
        Check(!GoogleSheetsService.MatchesRegisteredAllergen(menus[0] with { Allergens = "⑬" }, ["메밀"]), "Code 13 does not match code 3");
        if (args.Length > 0 && File.Exists(args[0]))
        {
            var fixture = JsonSerializer.Deserialize<List<List<List<JsonElement>>>>(File.ReadAllText(args[0]))!;
            foreach (var track in fixture)
            {
                var rows = track.Select(r => (IList<object>)r.Select(c => (object)c.ToString()).ToList()).ToList();
                var parsed = WeeklyMealPlanner.Parse(rows);
                Check(parsed.Count > 1000 && parsed.All(m => !m.HasNutrition), "Real track headers parsed and missing nutrients detected");
                Reject(() => WeeklyMealPlanner.Generate(parsed, DateTime.Today, weekdays, ["중식"], "3-5", "Track", target), "Real unpopulated catalog does not generate fabricated nutrition");
            }
        }
        var http = new FakeSheets();
        var service = new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(service,
            new SheetsService(new BaseClientService.Initializer { HttpClientFactory = new Factory(http), ApplicationName = "Local tests" }));
        service.SetUserSpreadsheetId("personal-test");
        service.SaveWeeklyMealsAsync(week).GetAwaiter().GetResult();
        Check(http.BatchCount == 1 && http.SavedIds.Count == 5, "Weekly save uses one atomic batch for all rows");
        using (var batch = JsonDocument.Parse(http.LastBatch))
        {
            var requests = batch.RootElement.GetProperty("requests");
            Check(requests[0].GetProperty("appendDimension").GetProperty("length").GetInt32() == GoogleSheetsService.WeeklyHeaders.Length - 9, "Existing narrow sheet expanded before writing");
            var cells = requests[2].GetProperty("appendCells").GetProperty("rows")[0].GetProperty("values");
            Check(cells[12].GetProperty("userEnteredValue").GetProperty("numberValue").GetDouble() == 60, "Nutrition saved as numeric cells");
        }
        service.SaveWeeklyMealsAsync(week).GetAwaiter().GetResult();
        Check(http.BatchCount == 2 && http.SavedIds.Count == 5, "Save retry updates rows without duplicating IDs");
        var replacement = new WeeklyMeal {Date=week[0].Date,Meal=week[0].Meal,AgeGroup=week[0].AgeGroup,Track=week[0].Track,Targets=target,Items=week[0].Items.Select((m,i)=>i==0?m with {Name="변경한 밥"}:m).ToArray()};
        service.SaveWeeklyMealsAsync([replacement]).GetAwaiter().GetResult();
        using(var update=JsonDocument.Parse(http.LastBatch)) {
            var requests=update.RootElement.GetProperty("requests").EnumerateArray().ToList();
            Check(requests.All(r=>!r.TryGetProperty("appendCells",out _)) && http.LastBatch.Contains("변경한 밥"),"Regeneration replaces same date/meal/age instead of appending");
        }
        var secondAge=week.Select(m=>new WeeklyMeal{Date=m.Date,Meal=m.Meal,AgeGroup="6-18",Track="B",Targets=m.Targets,Items=m.Items}).ToList();
        service.SaveWeeklyMealsAsync(secondAge).GetAwaiter().GetResult();
        Check(http.SavedIds.Count==10 && http.SavedIds.Count(id=>id==week[0].Id)==2,"Same date and meal ID keeps both age tracks in separate rows");
        http.Mismatch = true;
        Reject(() => service.SaveWeeklyMealsAsync(Generate()).GetAwaiter().GetResult(), "Unknown output schema cannot overwrite data");
        service.SetUserSpreadsheetId("");
        Reject(() => service.SaveWeeklyMealsAsync(week).GetAwaiter().GetResult(), "No personal DB cannot write original template");
        Check(week[0].Id == "260914L", "Meal ID uses yyMMdd and lunch L");
        var three = Generate(days: [DayOfWeek.Monday], meals: ["조식","중식","석식"]);
        Check(three.Select(m=>m.Id).SequenceEqual(new[]{"260914M","260914L","260914N"}), "Breakfast M and dinner N IDs");
        var placeholder = new TrackMenu("", "프로그램이 골라주기", "밥류", "", null,null,null,null,"");
        Check(placeholder.ToString() == placeholder.Name, "Accessibility name of empty-nutrient placeholder never calculates energy");
        var varied = menus.SelectMany(m=>Enumerable.Range(0,5).Select(i=>m with { Id=m.Id+i,Name=m.Name+"-"+i })).ToArray();
        var diverse = Generate(custom:varied);
        Check(WeeklyMealPlanner.Categories.All(c=>diverse.SelectMany(x=>x.Items).Where(x=>x.Category==c).Select(x=>x.Name).Distinct().Count()==5),"Five-day plan avoids repetitions when five dishes are available");
        var fixedWeek=WeeklyMealPlanner.Generate(varied,new DateTime(2026,9,21),weekdays,["중식"],"3-5","A",target,new Dictionary<string,string>{{"밥류",varied[0].Key}},seed:42);
        Check(fixedWeek.All(x=>x.Items[0].Key==varied[0].Key),"Fixed menu remains fixed while other dishes vary");
        Check(DinerCsv.Parse(new StringReader("이름,나이,성별,특이사항\n홍길동,5,남,\"우유,난류\"\n"))[0].Notes=="우유,난류","Quoted CSV fields preserve commas");
        var ingredients = IngredientCsv.Parse(new StringReader("식재료명,규격,단가\n쌀,20kg,56000\n당근,10kg,23000\n"), "purchase.csv");
        Check(ingredients.Count == 2 && ingredients[0].Name == "쌀" && ingredients[1].Price == "23000", "Ingredient CSV accepts a named source and searchable columns");
        Reject(() => IngredientCsv.Parse(new StringReader("가격\n3000\n"), "bad.csv"), "Ingredient CSV rejects missing ingredient-name header");
        Reject(()=>DinerCsv.Parse(new StringReader("이름,나이\n아이,-1")),"Invalid ages rejected before any upload");
        Reject(()=>DinerCsv.Parse(new StringReader("나이\n5")),"Missing CSV name header rejected");
        EntryChecks.Run(Check, args.Length > 2 ? args[2] : null);
        FeedbackChecks.Run(Check, Reject);
        var app = new wpf.App();
        app.InitializeComponent();
        AppServices.Sheets = new GoogleSheetsService(null!);
        var page = new Menu_table("6-18");
        Check(((System.Windows.Controls.ComboBoxItem)((System.Windows.Controls.ComboBox)page.FindName("CboAgeGroup")).SelectedItem).Tag.ToString() == "6-18", "WPF page constructs and selects track B");
        if (args.Length > 1)
        {
            page.Measure(new Size(1140, 1000)); page.Arrange(new Rect(0, 0, 1140, 1000)); page.UpdateLayout();
            var bmp = new RenderTargetBitmap(1140,1000,96,96,PixelFormats.Pbgra32); bmp.Render(page);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp));
            using var file = File.Create(args[1]); encoder.Save(file);
            string folder = Path.GetDirectoryName(args[1])!;
            ManagementChecks.Run(Check, Reject, folder);
            FeedbackChecks.Render(new MenuInputPage(), Path.Combine(folder,"recipe-ui.png"),1140,960);
            var fixedPanel = (System.Windows.Controls.WrapPanel)page.FindName("FixedPanel");
            var combo = ((System.Windows.Controls.StackPanel)fixedPanel.Children[0]).Children.OfType<System.Windows.Controls.ComboBox>().Single();
            combo.ItemsSource = new[]{placeholder}.Concat(varied).ToList(); combo.SelectedIndex=0;
            Check(System.Windows.Controls.VirtualizingPanel.GetIsVirtualizing(combo),"Fixed-menu list uses virtualization");
            var peer = new System.Windows.Automation.Peers.ComboBoxAutomationPeer(combo);
            Check(new System.Windows.Automation.Peers.ListBoxItemAutomationPeer(placeholder,peer).GetName() == placeholder.Name,"Fixed-menu accessibility peers read names without crashing");
            FeedbackChecks.Render(new CalculatePage(), Path.Combine(folder,"cost-ui.png"),1140,1250);
            var calendarPage = new EventCalendar();
            FeedbackChecks.Render(calendarPage, Path.Combine(folder,"calendar-ui.png"),1140,900);
            var calendar = (System.Windows.Controls.Calendar)calendarPage.FindName("MainCalendar");
            var calendarItem = (System.Windows.Controls.Primitives.CalendarItem)calendar.Template.FindName("PART_CalendarItem",calendar);
            var next = (System.Windows.Controls.Button)calendarItem.Template.FindName("PART_NextButton",calendarItem);
            var previous = (System.Windows.Controls.Button)calendarItem.Template.FindName("PART_PreviousButton",calendarItem);
            DateTime initialMonth=calendar.DisplayDate;
            next.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(calendar.DisplayDate.Month==initialMonth.AddMonths(1).Month,"Custom calendar next-month button works");
            previous.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(calendar.DisplayDate.Month==initialMonth.Month && calendar.FirstDayOfWeek==DayOfWeek.Monday && calendar.Language.IetfLanguageTag.Equals("ko-KR",StringComparison.OrdinalIgnoreCase),"Custom calendar previous month and Korean Monday-first layout work");
            var heading=(System.Windows.Controls.Button)calendarItem.Template.FindName("PART_HeaderButton",calendarItem);
            heading.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            Check(calendar.DisplayMode==System.Windows.Controls.CalendarMode.Year,"Custom calendar retains month picker navigation");
            var login = new LoginWindow();
            FeedbackChecks.Render((FrameworkElement)login.Content, Path.Combine(folder,"login-ui.png"),480,310);
            login.Close();
            var screenHandler = new FakeScreens();
            var screenService = new GoogleSheetsService(null!);
            typeof(GoogleSheetsService).GetField("_sheetsService", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(screenService,
                new SheetsService(new BaseClientService.Initializer { HttpClientFactory = new ScreenFactory(screenHandler), ApplicationName = "Screen tests" }));
            screenService.SetUserSpreadsheetId("personal-test");
            var main = new wpf.Main(screenService);
            var dashboard = ((Task<System.Windows.Controls.Page>)typeof(wpf.Main).GetMethod("LoadDefaultDashboardAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main,null)!).GetAwaiter().GetResult();
            FeedbackChecks.Render(dashboard,Path.Combine(folder,"dashboard-ui.png"),1140,820);
            FeedbackChecks.Render((FrameworkElement)main.Content, Path.Combine(folder,"main-ui.png"),1440,900);
            main.Close();
            Check(true, "Changed WPF screens construct and render with application resources");
        }
        Console.WriteLine($"{count} checks passed. No live Google API calls were made.");
    }
}

internal sealed class ScreenFactory(FakeScreens handler) : IHttpClientFactory
{
    public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args) => new(new ConfigurableMessageHandler(handler));
}
internal sealed class FakeScreens : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string url=Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        string json;
        if(url.Contains("/values/")) json = url.Contains("메뉴") ? "{\"values\":[[\"식단 ID\",\"날짜\",\"끼니\",\"밥류\"]]}" : "{\"values\":[[\"이름\",\"연령\",\"성별\",\"특이사항\",\"대체식품\"]]}";
        else json = "{\"sheets\":[{\"properties\":{\"title\":\"메뉴\",\"sheetId\":42}},{\"properties\":{\"title\":\"알러지인원\",\"sheetId\":43}}]}";
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json,Encoding.UTF8,"application/json") });
    }
}

internal sealed class Factory(FakeSheets handler) : IHttpClientFactory
{
    public ConfigurableHttpClient CreateHttpClient(CreateHttpClientArgs args) => new(new ConfigurableMessageHandler(handler));
}

internal sealed class FakeSheets : HttpMessageHandler
{
    public bool CostMode;
    public int BatchCount;
    public bool Mismatch;
    public string LastBatch = "";
    public readonly List<string> SavedIds = [];
    public readonly List<object[]> SavedRows = [];
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string json;
        if (request.Method == HttpMethod.Post)
        {
            var bytes = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > 1 && bytes[0] == 0x1f && bytes[1] == 0x8b)
            {
                using var zipped = new System.IO.Compression.GZipStream(new MemoryStream(bytes), System.IO.Compression.CompressionMode.Decompress);
                using var reader = new StreamReader(zipped); LastBatch = await reader.ReadToEndAsync(cancellationToken);
            }
            else LastBatch = Encoding.UTF8.GetString(bytes);
            using var body = JsonDocument.Parse(LastBatch);
            foreach (var r in body.RootElement.GetProperty("requests").EnumerateArray())
                if (r.TryGetProperty("appendCells", out var append))
                    foreach (var row in append.GetProperty("rows").EnumerateArray()) {
                        SavedIds.Add(row.GetProperty("values")[0].GetProperty("userEnteredValue").GetProperty("stringValue").GetString()!);
                        SavedRows.Add(row.GetProperty("values").EnumerateArray().Select(c => { var v=c.GetProperty("userEnteredValue"); return v.TryGetProperty("stringValue",out var s) ? (object)s.GetString()! : v.GetProperty("numberValue").GetDouble(); }).ToArray());
                    }
            BatchCount++; json = "{}";
        }
        else if (request.RequestUri!.AbsolutePath.Contains("/values/"))
        {
            var rows = new List<object[]> { Mismatch ? ["메뉴명","칼로리"] : ["식단 ID","날짜","구분","밥","국","메인","사이드1","사이드2","후식"] };
            if(CostMode) rows = [GoogleSheetsService.CostHeaders.Take(8).Cast<object>().ToArray()];
            rows.AddRange(SavedRows);
            json = JsonSerializer.Serialize(new { values = rows });
        }
        else json = "{\"sheets\":[{\"properties\":{\"title\":\"메뉴\",\"sheetId\":42,\"gridProperties\":{\"columnCount\":9,\"rowCount\":1000}}}]}";
        if(CostMode && request.Method != HttpMethod.Post && !request.RequestUri!.AbsolutePath.Contains("/values/"))
            json=JsonSerializer.Serialize(new{sheets=new[]{new{properties=new{title=AppConfig.CostSheetName,sheetId=42,gridProperties=new{columnCount=8,rowCount=1000}}}}});
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json,Encoding.UTF8,"application/json") };
    }
}
