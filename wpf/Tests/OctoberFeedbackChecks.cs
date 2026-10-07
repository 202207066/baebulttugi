using System.Windows;
using System.Windows.Controls;
using wpf;
internal static class OctoberFeedbackChecks
{
    public static void Run(string folder)
    {
        var app = new App(); app.InitializeComponent();
        void Check(bool ok, string label) { if(!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
        Check(DashboardAgeFilter.Matches("3-5","3-5") && !DashboardAgeFilter.Matches("3-5","6-18") && DashboardAgeFilter.Matches("12-18","6-18") && DashboardAgeFilter.Matches("9-11","6-18"), "Dashboard age groups do not mix tracks and include legacy ages");
        Check(!DashboardAgeFilter.Matches("","3-5") && DashboardAgeFilter.Matches("","unknown"), "Unspecified ages remain separate");
        foreach(var icon in new[]{"👨‍👩‍👧‍👦","🇰🇷","👍🏽","🎉","❤️"}) Check(EventIconText.LeadingIcon(icon+" 행사")==icon && EventIconText.Compose(icon,"행사")==icon+" 행사", "Emoji grapheme preserved: " + icon);
        Check(EventIconText.Compose("📌","🎂 생일")=="🎂 생일", "Typing emoji in event content does not duplicate prefix");
        Check(MealPresentation.Matches("쇠고기 미역국", "고기미역"), "Search matches partial names ignoring whitespace");
        Check(!MealPresentation.Matches("미역국", "두부"), "Unmatched search excluded");
        var menu = new TrackMenu("1", "미역국", "국류", "", 10, 2, 1, null, "test");
        Check(menu.DisplayName == "미역국 · 57 kcal", "Calories derive from full macros when explicit energy absent");
        Check((menu with { Carb=null }).DisplayName.Contains("열량 미확인"), "Missing energy is not shown as zero");
        Check((menu with { Calories=0 }).DisplayName.EndsWith("0 kcal"), "Known zero calories preserved");
        var first = new StoredMeal { Date = new DateTime(2026,10,5), Raw = new List<object>{"a","2026-10-05","중식","밥","국","주찬","부찬","김치","후식"} };
        var second = new StoredMeal { Date = first.Date, Raw = new List<object>{"b","2026-10-05","석식","밥","국","주찬","부찬","김치","후식"} };
        var grid = new DataGrid { IsReadOnly=true, AutoGenerateColumns=false, CanUserAddRows=false };
        MealPresentation.Show(grid, new[] { new MealDisplayColumn("10/5 (월) · 중식", Enumerable.Repeat(menu.DisplayName,6).ToArray(),342,first), new MealDisplayColumn("10/5 (월) · 석식", Enumerable.Repeat(menu.DisplayName,6).ToArray(),342,second) });
        Check(grid.Columns.Count==3 && grid.Items.Count==7, "Transposed grid has meal columns and six categories plus total");
        Check(!grid.CanUserSortColumns && grid.Columns.All(c => !c.CanUserSort), "Every column including categories rejects sorting");
        grid.Measure(new Size(1100,500)); grid.Arrange(new Rect(0,0,1100,500)); grid.UpdateLayout();
        grid.SelectedCells.Clear();
        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[2],grid.Columns[2]));
        Check(ReferenceEquals(MealPresentation.Selected(grid), second), "Edit/delete selects the meal column, including multiple meals on one date");
        System.IO.Directory.CreateDirectory(folder);
        FeedbackChecks.Render(new Page{Content=grid}, System.IO.Path.Combine(folder,"weekly-feedback.png"),1100,500);
        var catalog = WeeklyMealPlanner.Categories.Select((c,i)=>menu with {Id=i.ToString(),Category=c,Name=c+" 메뉴"}).ToList();
        var picker = new AllergenPicker { Text="①,5,16,참깨" };
        Check(picker.Text=="1,5,16,참깨", "Allergy selector round-trips codes and preserves custom allergens");
        picker.Clear();Check(picker.Text=="", "New recipe clears all allergy selections");
        var editor = new MealEditWindow(first,catalog);
        var editorRoot=(StackPanel)((ScrollViewer)editor.Content).Content;
        var section=editorRoot.Children.OfType<Expander>().First();section.IsExpanded=true;
        var searchPanel=(Grid)((StackPanel)section.Content).Children[0];
        var search=(TextBox)searchPanel.Children[0];var original=editor.Selection[0];
        search.Text="존재하지않는메뉴";
        Check(editor.Selection[0]==original, "Unmatched search preserves saved menu");
        search.Text="";
        var emoji=new Emoji.Wpf.TextBlock {Text="🎂 🎉 ❤️ 👍🏽",FontSize=44};
        FeedbackChecks.Render(new Page{Content=emoji,Background=System.Windows.Media.Brushes.White},System.IO.Path.Combine(folder,"color-emoji.png"),400,100);
        FeedbackChecks.Render(new Page{Content=Detach(editor),Background=System.Windows.Media.Brushes.White},System.IO.Path.Combine(folder,"edit-feedback.png"),620,1000);
        var calendar = new Calendar { SelectedDate=first.Date, DisplayDate=first.Date };
        calendar.Resources.MergedDictionaries.Add(new ResourceDictionary { Source=new Uri("/wpf;component/CleanCalendar.xaml",UriKind.Relative) });
        var mark = CalendarDates.Mark(first.Date,null,true) with { HasMeal=true, EventIcon="🎂" };
        var day = new System.Windows.Controls.Primitives.CalendarDayButton { Content="5", Tag=mark, Style=(Style)calendar.Resources["CleanDay"], Width=100, Height=64 };
        FeedbackChecks.Render(new Page{Content=day,Background=System.Windows.Media.Brushes.White},System.IO.Path.Combine(folder,"calendar-mark.png"),140,100);
        Check(mark.HasMeal && mark.HasEvent && mark.EventIcon=="🎂", "Meal and custom event markers coexist");
        calendar.Style = (Style)calendar.Resources["CleanCalendar"];
        calendar.Width=620; calendar.Height=500;
        foreach(var month in new[] {new DateTime(2026,2,1),new DateTime(2026,10,1),new DateTime(2027,1,1)}) {
            calendar.DisplayDate=month;
            calendar.Measure(new Size(620,500)); calendar.Arrange(new Rect(0,0,620,500)); calendar.UpdateLayout();
            var days=Descendants<System.Windows.Controls.Primitives.CalendarDayButton>(calendar).Where(d=>d.DataContext is DateTime).ToList();
            Check(calendar.FirstDayOfWeek==DayOfWeek.Sunday && days.Count==42 && days.All(d=>Grid.GetColumn(d)==(int)((DateTime)d.DataContext).DayOfWeek), "Calendar dates match Sunday-first columns: " + month.ToString("yyyy-MM"));
        }
        calendar.DisplayDate=new DateTime(2026,10,1);
        FeedbackChecks.Render(new Page{Content=calendar,Background=System.Windows.Media.Brushes.White},System.IO.Path.Combine(folder,"calendar-alignment.png"),660,540);
        AppServices.Sheets = new GoogleSheetsService(null!);
        var allergyPage = new AllergyManagementPage();
        foreach(var name in new[]{"DgPatients","DgDailyMenu","DgAllergicMatches"})
            if(allergyPage.FindName(name) is DataGrid table) Check(table.IsReadOnly&&!table.CanUserAddRows&&!table.CanUserDeleteRows, "Read-only allergy table: "+name);
        var eventPage = new EventCalendar();
        eventPage.Measure(new Size(1200,800)); eventPage.Arrange(new Rect(0,0,1200,800)); eventPage.UpdateLayout();
        var left=(FrameworkElement)eventPage.FindName("CalendarCard");
        double before=left.ActualHeight;
        ((ItemsControl)eventPage.FindName("lstTodayMeals")).ItemsSource=Enumerable.Range(0,40).Select(i=>"점심 · 테스트 식단\n현미밥 (180 kcal) · 미역국 (40 kcal)").ToArray();
        eventPage.Measure(new Size(1200,800)); eventPage.Arrange(new Rect(0,0,1200,800)); eventPage.UpdateLayout();
        Check(Math.Abs(left.ActualHeight-before)<0.1, "Long right meal list leaves left calendar height unchanged");
        var eventCal=(Calendar)eventPage.FindName("MainCalendar"); eventCal.DisplayDate=new DateTime(2026,10,1);eventCal.UpdateLayout();
        var planned=new HashSet<DateTime>(Enumerable.Range(5,4).Select(d=>new DateTime(2026,10,d)));
        ((HashSet<DateTime>)typeof(EventCalendar).GetField("_mealDates",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(eventPage)!).UnionWith(planned);
        ((Dictionary<DateTime,string>)typeof(EventCalendar).GetField("_eventIcons",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(eventPage)!)[new DateTime(2026,10,7)]="🎂";
        foreach(var button in Descendants<System.Windows.Controls.Primitives.CalendarDayButton>(eventCal)) if(button.DataContext is DateTime date) button.Tag=CalendarDates.Mark(date,null,date.Day==7) with {HasMeal=planned.Contains(date),ConnectPrevious=date.DayOfWeek!=DayOfWeek.Sunday&&planned.Contains(date.AddDays(-1)),ConnectNext=date.DayOfWeek!=DayOfWeek.Saturday&&planned.Contains(date.AddDays(1)),EventIcon=date.Day==7?"🎂":""};
        FeedbackChecks.Render(eventPage,System.IO.Path.Combine(folder,"calendar-independent.png"),1200,800);
        var mid=Descendants<System.Windows.Controls.Primitives.CalendarDayButton>(eventCal).First(d=>d.DataContext is DateTime dt && dt==new DateTime(2026,10,6));
        Check(mid.Tag is CalendarDateMark {HasMeal:true,ConnectPrevious:true,ConnectNext:true}, "Consecutive meals connect pill segments");
        var pill=(FrameworkElement)mid.Template.FindName("MealPill",mid);
        Check(pill.Visibility==Visibility.Visible && pill.ActualWidth>40,"Meal pill spans the date cell");
        var planner=new Menu_table();
        var guide=(Expander)planner.FindName("NutritionGuide"); guide.IsExpanded=true;
        ((Panel)guide.Parent).Children.Remove(guide);
        FeedbackChecks.Render(new Page{Content=guide,Background=System.Windows.Media.Brushes.White},System.IO.Path.Combine(folder,"nutrition-guide.png"),520,480);
        MealPresentation.Show(grid,Array.Empty<MealDisplayColumn>());
        Check(grid.Items.Count==0 && MealPresentation.Selected(grid)==null,"Refresh clears stale meal selection");
        var preview = new DataGrid { IsReadOnly=true, AutoGenerateColumns=false, CanUserAddRows=false };
        string[][] names = [ ["현미밥","쇠고기미역국","닭살채소볶음","시금치나물","배추김치","사과"], ["잡곡밥","맑은두부국","돼지고기불고기","애호박볶음","깍두기","플레인요구르트"], ["기장밥","감자수제비국","생선구이","브로콜리무침","배추김치","바나나"], ["흑미밥","콩나물국","두부조림","당근달걀볶음","깍두기","배"], ["쌀밥","된장국","닭고기카레","오이무침","배추김치","귤"] ];
        MealPresentation.Show(preview, names.Select((n,i)=>new MealDisplayColumn($"10/{5+i} ({"월화수목금"[i]})\n점심 · 3–5세",n.Select((name,j)=>MealPresentation.Label(name,new double[]{180,45,130,35,10,60}[j])).ToArray(),460)).ToList());
        var printMeals = names.Select((n,i)=> {
            var raw = new List<object>{"id"+i,$"2026-10-{5+i:D2}","중식"};raw.AddRange(n);raw.Add("월");raw.Add("3-5");raw.Add("A");raw.AddRange(new object[]{65,20,15,475});
            var saved=new StoredMeal {Date=first.Date.AddDays(i),Raw=raw};
            return new MealDisplayColumn("",n.Select((name,j)=>MealPresentation.Label(name,new double[]{180,45,130,35,10,75}[j])).ToArray(),475,saved);
        }).ToList();
        var methods=CookingMethods.Parse(new List<IList<object>> {new List<object>{"메뉴명","조리방법"},new List<object>{"현미 밥","① 쌀과 현미를 불립니다. ② 물을 넣어 밥을 짓습니다."},new List<object>{"배추김치","준비한 김치를 적당한 크기로 썰어 제공합니다."}});
        Check(methods.ContainsKey(CookingMethods.Key("현미밥"))&&!methods.ContainsKey(CookingMethods.Key("현미죽")),"Cooking matches whitespace differences without substituting different dishes");
        var recipePages=MealPlanPdf.CreatePages("검증용 급식소",first.Date,printMeals,methods);
        MealPlanPdf.Save(System.IO.Path.Combine(folder,"weekly-recipes.pdf"),"검증용 급식소",first.Date,printMeals,methods);
        foreach(var page in recipePages){page.Measure(new Size(MealPlanPdf.Width,MealPlanPdf.Height));page.Arrange(new Rect(0,0,MealPlanPdf.Width,MealPlanPdf.Height));page.UpdateLayout();}
        Check(!Descendants<TextBlock>(recipePages[0]).Any(t=>t.Text.Contains("조리방법")),"Meal page never includes recipes");
        Check(recipePages.Count>1 && Descendants<TextBlock>(recipePages[1]).Any(t=>t.Text.Contains("조리방법")),"Recipes start on page two");
        var longMethod=string.Concat(Enumerable.Repeat("재료를 손질하고 충분히 익혀 제공합니다. ",600));
        var longPages=MealPlanPdf.CreatePages("검증",first.Date,printMeals,new Dictionary<string,string>{{CookingMethods.Key("현미밥"),longMethod}});
        Check(longPages.Count>1,"Long cooking methods continue onto extra pages");
        foreach(var page in longPages){page.Measure(new Size(MealPlanPdf.Width,MealPlanPdf.Height));page.Arrange(new Rect(0,0,MealPlanPdf.Width,MealPlanPdf.Height));page.UpdateLayout();}
        var allText=string.Concat(longPages.SelectMany(p=>Descendants<TextBlock>(p)).Where(t=>System.Windows.Media.VisualTreeHelper.GetParent(t) is Border b && b.Padding.Left==12 && t.Inlines.FirstInline is System.Windows.Documents.Run title && (title.Text=="현미밥" || title.Text=="현미밥 (계속)")).Select(t=>string.Concat(t.Inlines.OfType<System.Windows.Documents.Run>().Skip(1).Select(r=>r.Text))[1..]));
        Check(allText==longMethod,"Cooking pagination retains the complete instructions");
        var pages=MealPlanPdf.CreatePages("검증용 급식소",first.Date,printMeals);
        Check(pages.Count==1,"Weekly lunch PDF fits one A4 page");

        MealPlanPdf.Save(System.IO.Path.Combine(folder,"weekly-print.pdf"),"검증용 급식소",first.Date,printMeals);
        FeedbackChecks.Render(new Page{Content=pages[0]},System.IO.Path.Combine(folder,"weekly-print.png"),1120,1584);
        var mixed=printMeals.Concat(printMeals.Select(m=>m with {Stored=new StoredMeal {Date=m.Stored!.Date,Raw=m.Stored.Raw.Select((x,i)=>i==10?(object)"6-18":x).ToList()}})).ToList();
        Check(MealPlanPdf.CreatePages("검증",first.Date,mixed).Count==2,"PDF separates age groups");
        var recipe=new MenuInputPage();recipe.Measure(new Size(1100,850));recipe.Arrange(new Rect(0,0,1100,850));recipe.UpdateLayout();
        var input=(TextBox)recipe.FindName("IngredientsBox");input.Text=string.Join("\n",Enumerable.Range(1,40).Select(i=>$"{i}. 재료와 조리법"));input.UpdateLayout();
        var host=(ScrollViewer)input.Template.FindName("PART_ContentHost",input);input.ScrollToEnd();
        input.UpdateLayout();
        Check(host.ScrollableHeight>0 && host.VerticalOffset>0,"Recipe input has a bounded, scrollable multiline viewport");
        allergyPage.Measure(new Size(1200,900));allergyPage.Arrange(new Rect(0,0,1200,900));allergyPage.UpdateLayout();
        var allergyNames=Descendants<CheckBox>(allergyPage).Select(c=>c.Content?.ToString()??"").Where(t=>char.IsDigit(t.FirstOrDefault())).Select(t=>int.Parse(t.Split('.')[0])).ToArray();
        Check(allergyNames.Length>=19 && allergyNames.SequenceEqual(allergyNames.OrderBy(n=>n)),"Patient allergy choices sorted by code");
        var card = new System.Windows.Controls.Border { Background=System.Windows.Media.Brushes.White, Padding=new Thickness(24), CornerRadius=new CornerRadius(18), Margin=new Thickness(20), Child=preview };
        FeedbackChecks.Render(new Page{Content=card,Background=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(244,247,243))},System.IO.Path.Combine(folder,"styled-weekly.png"),1200,720);
        app.Dispatcher.InvokeShutdown();
    }
    private static object Detach(Window window) { var content=window.Content;window.Content=null;return content; }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T:DependencyObject {
        for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);i++) {
            var child=System.Windows.Media.VisualTreeHelper.GetChild(parent,i);
            if(child is T found) yield return found;
            foreach(var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
}
