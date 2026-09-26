using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Text.Json;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using wpf;

internal static class ManagementChecks
{
    public static StoredMeal Fixture()
    {
        var row=Enumerable.Repeat<object>("",27).ToList();
        row[0]="old-id";row[1]="2026-09-21";row[2]="중식";row[10]="3-5";row[11]="트랙A";
        string[] names=["밥","국","주찬","부찬","김치","과일"];
        for(int i=0;i<6;i++)row[i+3]=names[i];row[16]=60;row[17]=18;row[18]=12;row[26]=15;
        return new StoredMeal{Date=new(2026,9,21),Raw=row,RowIndex=1};
    }
    public static void Run(Action<bool,string> check,Action<Action,string> reject,string folder)
    {
        var meal=Fixture();var http=new FakeSheets();http.SavedRows.Add(meal.Raw.ToArray());
        var extra=meal.Raw.ToArray();extra[1]="2026-09-28";http.SavedRows.Add(extra);
        var service=new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(service,new SheetsService(new BaseClientService.Initializer{HttpClientFactory=new Factory(http),ApplicationName="Management tests"}));service.SetUserSpreadsheetId("personal-test");
        var week=service.GetStoredMealsAsync(new DateTime(2026,9,27),"3-5").GetAwaiter().GetResult();
        check(week.Count==1&&week[0].Date==new DateTime(2026,9,21),"Sunday selection reloads saved Monday-Sunday meals, excluding next Monday");
        check(service.GetStoredMealsAsync(new DateTime(2026,9,21),"6-18").GetAwaiter().GetResult().Count==0,"Saved-week load respects selected age");
        var items=WeeklyMealPlanner.Categories.Select((c,i)=>new TrackMenu("TEST"+i,"수정"+i,c,"",10,3,2,null,"")).ToArray();
        var edited=GoogleSheetsService.EditedMealValues(week[0],items);
        check(edited[0].ToString()=="260921L" && Convert.ToDouble(edited[12])==60 && Convert.ToDouble(edited[15])==420,"Meal editing updates deterministic ID and nutrition totals");
        var incomplete=GoogleSheetsService.EditedMealValues(week[0],items.Select((m,i)=>i==0?m with{Carb=null}:m).ToArray());
        check(incomplete[12].ToString()==""&&incomplete[22].ToString()=="영양정보 미완성","Unknown edit nutrients clear stale totals instead of making up values");
        service.ChangeStoredMealAsync(week[0],items).GetAwaiter().GetResult();
        using(var doc=JsonDocument.Parse(http.LastBatch))check(doc.RootElement.GetProperty("requests").EnumerateArray().Any(r=>r.TryGetProperty("updateCells",out var u)&&u.GetProperty("start").GetProperty("rowIndex").GetInt32()==1),"Editing updates the selected saved row");
        service.ChangeStoredMealAsync(week[0],null).GetAwaiter().GetResult();
        using(var doc=JsonDocument.Parse(http.LastBatch)){var range=doc.RootElement.GetProperty("requests")[0].GetProperty("deleteDimension").GetProperty("range");check(range.GetProperty("startIndex").GetInt32()==1&&range.GetProperty("endIndex").GetInt32()==2,"Delete removes only the selected logical meal row");}
        http.SavedRows[0][3]="다른 곳에서 변경";
        reject(()=>service.ChangeStoredMealAsync(week[0],items).GetAwaiter().GetResult(),"Concurrent sheet edits block stale overwrite");
        var drafts=meal.Menus.Select((name,i)=>new CostDraft{Menu=name,Name="재료"+i,Amount="100",Unit="g",Price="3000",PriceQuantity="1",PriceUnit="kg"}).ToList();
        var costs=MealCostData.Validate(meal,drafts,true);
        check(costs.Sum(CostCalculator.CostPerPerson)==1800,"Whole saved meal costs all six menus with unit conversion");
        drafts[0].Price="";reject(()=>MealCostData.Validate(meal,drafts,true),"Missing purchase price cannot silently count as zero");
        check(MealCostData.Validate(meal,drafts,false).Count==6,"Partial price entry can be saved before calculation");
        var fixedMenu=new TrackMenu("C1","고정 메뉴","밥류","",10,3,2,60,"test");
        var fixedCost=MealCostData.EstimateMenuCost(fixedMenu,"3-5",[new SavedCost("3-5",fixedMenu.Key,new CostDraft{Menu="고정 메뉴",Name="쌀",Category="밥류",Amount="100",Unit="g",Price="3000",PriceQuantity="1",PriceUnit="kg"})]);
        check(fixedCost==300,"Selected-menu cost uses saved purchase price instead of a fabricated value");
        http.CostMode=true;http.SavedRows.Clear();
        http.SavedRows.Add(new object[]{"다른 메뉴","다른 재료","밥류",100,"g",5000,1,"kg","3-5",""});
        http.SavedRows.Add(new object[]{meal.Rice,"이전 재료","밥류",100,"g",4000,1,"kg","3-5",""});
        service.SaveMealCostsAsync(meal,drafts).GetAwaiter().GetResult();
        using(var saved=JsonDocument.Parse(http.LastBatch)) {
            var requests=saved.RootElement.GetProperty("requests").EnumerateArray().ToList();
            check(requests[0].GetProperty("appendDimension").GetProperty("length").GetInt32()==2,"Eight-column cost sheet expands before storing age and menu key");
            var removed=requests.Where(r=>r.TryGetProperty("deleteDimension",out _)).ToList();
            check(removed.Count==1&&removed[0].GetProperty("deleteDimension").GetProperty("range").GetProperty("startIndex").GetInt32()==2,"Saving costs replaces selected recipe rows and preserves unrelated recipes");
            var newRows=requests.Single(r=>r.TryGetProperty("appendCells",out _)).GetProperty("appendCells").GetProperty("rows");
            check(newRows.GetArrayLength()==6&&newRows[0].GetProperty("values")[5].GetProperty("userEnteredValue").GetProperty("stringValue").GetString()=="","Cost save retains missing prices as blanks across all six menus");
        }
        reject(()=>MealCostData.Validate(meal,drafts.Skip(1),false),"A missing menu cannot produce a partial meal total");
        var linked=new StoredMeal{Date=meal.Date,Raw=meal.Raw.ToList()};linked.Raw[3]="가지짜장밥";linked.Raw[25]="A-0002:밥류:가지짜장밥";
        check(MealCostData.Build(linked,[]).Any(r=>r.Menu=="가지짜장밥"&&r.Amount.Length>0),"Saved menu key connects to actual bundled recipe portions");
        check(MealCostData.Build(linked,[],new HashSet<string>{"A-0002:밥류:가지짜장밥"}).Where(r=>r.Menu=="가지짜장밥").All(r=>r.Amount.Length==0),"Personal recipe override never borrows an obsolete shared portion");
        check(CalendarDates.Holidays(2026).ContainsKey(new(2026,12,25))&&CalendarDates.Holidays(2026).ContainsKey(new(2026,5,1)),"Christmas and updated 2026 Labour Day load without API key");
        check(CalendarDates.Holidays(2026).ContainsKey(new(2026,9,25))&&CalendarDates.Holidays(2026).ContainsKey(new(2026,10,5)),"Lunar Chuseok and verified substitute holiday dates");
        check(CalendarDates.Special(new(2026,7,15)).Contains("초복")&&CalendarDates.Special(new(2026,8,14)).Contains("말복"),"Verified 2026 special-meal days");

        var calendarPage=new EventCalendar();var calendar=(Calendar)calendarPage.FindName("MainCalendar");calendar.DisplayDate=new DateTime(2026,12,1);calendar.SelectedDate=new DateTime(2026,12,25);
        ((HashSet<DateTime>)typeof(EventCalendar).GetField("_eventDates",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(calendarPage)!).Add(new(2026,12,24));
        FeedbackChecks.Render(calendarPage,System.IO.Path.Combine(folder,"calendar-holidays.png"),1200,850);
        var buttons=Descendants<CalendarDayButton>(calendar).Where(b=>b.DataContext is DateTime).ToList();
        var christmas=buttons.Single(b=>(DateTime)b.DataContext==new DateTime(2026,12,25));
        var saturday=buttons.Single(b=>(DateTime)b.DataContext==new DateTime(2026,12,26));
        check(((SolidColorBrush)christmas.Foreground).Color==Color.FromRgb(196,67,67),"Selected Christmas number stays red");
        check(((SolidColorBrush)saturday.Foreground).Color==Color.FromRgb(36,116,182),"Saturday numbers are blue");
        var eventDay=buttons.Single(b=>(DateTime)b.DataContext==new DateTime(2026,12,24));
        check(eventDay.Tag is CalendarDateMark{HasEvent:true}&&((FrameworkElement)eventDay.Template.FindName("EventDot",eventDay)).Visibility==Visibility.Visible,"Saved user event renders a calendar dot");
        calendar.DisplayDate=new DateTime(2026,7,1);calendar.SelectedDate=new DateTime(2026,7,15);
        FeedbackChecks.Render(calendarPage,System.IO.Path.Combine(folder,"calendar-specials.png"),1200,850);
        var bok=Descendants<CalendarDayButton>(calendar).Single(b=>b.DataContext is DateTime date&&date==new DateTime(2026,7,15));
        check(bok.Tag is CalendarDateMark{HasSpecial:true}&&((FrameworkElement)bok.Template.FindName("SpecialDot",bok)).Visibility==Visibility.Visible,"Boknal special-meal date renders its marker");
        var page=new Menu_table("3-5");((DataGrid)page.FindName("GridDietResult")).ItemsSource=week;
        FeedbackChecks.Render(page,System.IO.Path.Combine(folder,"weekly-saved.png"),1250,1550); check(((DataGrid)page.FindName("GridDietResult")).Columns.Skip(3).Count(c=>c.ActualWidth>=88)==6,"All six menu columns retain visible widths");
        var costPage=new CalculatePage();((ComboBox)costPage.FindName("cbTargetMenu")).ItemsSource=new[]{meal};((ComboBox)costPage.FindName("cbTargetMenu")).SelectedIndex=0;
        FeedbackChecks.Render(costPage,System.IO.Path.Combine(folder,"cost-connected.png"),1250,1000);
        check(((DataGrid)costPage.FindName("CostInputs")).Items.Count>0,"Saved meal selection populates cost editor");
        var main=new wpf.Main(service);check(main.WindowState==WindowState.Maximized,"Main window starts maximized");main.Close();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var c=VisualTreeHelper.GetChild(root,i);if(c is T t)yield return t;foreach(var n in Descendants<T>(c))yield return n;}}
}
