using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using wpf;

internal static class WeekFeedbackChecks
{
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var child=VisualTreeHelper.GetChild(root,i);if(child is T found)yield return found;foreach(var nested in Children<T>(child))yield return nested;}
    }
    public static void Run(string folder)
    {
        var app=new App();app.InitializeComponent();
        var start=new DateTime(2026,10,4);
        IList<IList<object>> events=new List<IList<object>>{new List<object>{"날짜","내용"},new List<object>{"2026-10-06","🎂 생일 행사"},new List<object>{"2026-10-07","📌 [휴무] 시설 점검"},new List<object>{"2026-10-08","휴무 안내 회의"}};
        var days=WeekSchedule.Build(start,events,new Dictionary<DateTime,string>{{start.AddDays(1),"대체공휴일"},{start.AddDays(5),"한글날"}});
        Check(days[0].Date==start&&days[6].Date==start.AddDays(6),"Sunday through Saturday use consecutive dates");
        Check(days[1].IsClosed&&days[3].IsClosed&&days[5].IsClosed,"Holiday and explicit facility closure disable generation");
        Check(!days[2].IsClosed&&!days[4].IsClosed&&days[2].Description.Contains("생일"),"Normal events remain selectable and visible");
        Check(WeekDayNotice.IsClosure("📌 휴무"),"Legacy closure event with emoji is recognized");
        var catalog=WeeklyMealPlanner.Categories.Select((c,i)=>new TrackMenu(i.ToString(),c+" 메뉴",c,"",20,5,3,127,"검증")).ToList();
        var generated=WeeklyMealPlanner.Generate(catalog,start,days.Where(d=>!d.IsClosed).Select(d=>d.Date.DayOfWeek).ToArray(),new[]{"중식"},"3-5","A",new MealTargets(120,30,18,20),seed:1,sundayFirst:true);
        Check(generated.Count==4&&generated.All(m=>m.Date>=start&&m.Date<=start.AddDays(6)&&!days.Single(d=>d.Date==m.Date).IsClosed),"Only selected open dates generate within the Sunday week");
        StoredMeal Stored(DateTime date,string meal)
        {
            var raw=new List<object>{"id",date.ToString("yyyy-MM-dd"),meal};raw.AddRange(catalog.Select(m=>(object)m.Name));raw.Add("월");raw.Add("3-5");raw.Add("A");raw.AddRange(new object[]{120,30,18,762});
            return new StoredMeal{Date=date,Raw=raw};
        }
        var monday=Stored(start.AddDays(1),"중식");var tuesday=Stored(start.AddDays(2),"중식");
        var meals=new[]{monday,tuesday}.Select(m=>new MealDisplayColumn("",catalog.Select(c=>c.DisplayName).ToArray(),762,m)).ToList();
        var grid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false,MaxHeight=330};
        WeekMealTable.Show(grid,meals,days);
        var outer=new ScrollViewer{Content=new StackPanel{Children={new Border{Height=250},grid,new Border{Height=600}}}};
        outer.Measure(new Size(1200,700));outer.Arrange(new Rect(0,0,1200,700));outer.UpdateLayout();
        Check(grid.Columns.Count==8&&((MealTableHeading)grid.Columns[1].Header).Title.StartsWith("일요일"),"Table always includes seven Sunday-first day headers");
        grid.SelectedCells.Add(new DataGridCellInfo(grid.Items[2],grid.Columns[3]));
        var selected=WeekMealTable.Selected(grid);
        Check(selected.Meal==tuesday&&selected.Index==2,"Cell selection resolves exact date and menu index");
        WeekMealTable.Show(grid,meals.Where(m=>m.Stored!=monday).ToList(),days);
        Check(grid.Columns.Count==8&&((WeekMealRow)grid.Items[0]).Meals[1]==null,"Deleting Monday keeps its empty column");
        WeekMealTable.Show(grid,Array.Empty<MealDisplayColumn>(),days);
        Check(grid.Columns.Count==8&&grid.Items.Count==7,"Entirely empty weeks preserve all weekday columns");
        WeekMealTable.Show(grid,meals,days);outer.UpdateLayout();
        var inner=Children<ScrollViewer>(grid).First();
        var wheel=new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent};
        WeekMealTable.Wheel(grid,wheel);outer.UpdateLayout();
        Check(wheel.Handled&&inner.VerticalOffset>0,"Wheel scrolls the table viewport");
        inner.ScrollToEnd();outer.UpdateLayout();double before=outer.VerticalOffset;
        WeekMealTable.Wheel(grid,new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});outer.UpdateLayout();
        Check(outer.VerticalOffset>before,"Wheel continues into page at table boundary");
        var editor=new MealEditWindow(tuesday,catalog,2);
        var root=(StackPanel)((ScrollViewer)editor.Content).Content;
        Check(root.Children.OfType<Expander>().Count()==1&&editor.Selection.Length==6,"Single-menu editor exposes only the selected category and retains all others");
        var replacement=catalog[2] with {Name="새 주찬",Id="new"};var replacements=editor.Selection;replacements[2]=replacement;
        var values=GoogleSheetsService.EditedMealValues(tuesday,replacements);
        Check(Enumerable.Range(0,6).All(i=>values[3+i].ToString()==(i==2?"새 주찬":tuesday.Menus[i])),"Saving one menu preserves the other five dishes");
        var dayEditor=new DayMealEditWindow(tuesday.Date,new[]{tuesday,Stored(tuesday.Date,"석식")},catalog);
        var tabs=((DockPanel)dayEditor.Content).Children.OfType<TabControl>().Single();
        Check(tabs.Items.Count==2&&dayEditor.Changes.Count==0,"Day editor has only that day's meal tabs and skips unchanged meals");
        var http=new FakeSheets();
        var dinner=Stored(tuesday.Date,"석식");
        http.SavedRows.Add(monday.Raw.ToArray());http.SavedRows.Add(tuesday.Raw.ToArray());http.SavedRows.Add(dinner.Raw.ToArray());
        var service=new GoogleSheetsService(null!);
        typeof(GoogleSheetsService).GetField("_sheetsService",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(service,new Google.Apis.Sheets.v4.SheetsService(new Google.Apis.Services.BaseClientService.Initializer{HttpClientFactory=new Factory(http),ApplicationName="Week tests"}));
        service.SetUserSpreadsheetId("personal-test");
        service.ChangeStoredDayAsync(new (StoredMeal,IReadOnlyList<TrackMenu>)[]{(tuesday,replacements),(dinner,catalog)}).GetAwaiter().GetResult();
        using(var batch=System.Text.Json.JsonDocument.Parse(http.LastBatch))
        {
            var updated=batch.RootElement.GetProperty("requests").EnumerateArray().Where(r=>r.TryGetProperty("updateCells",out _)).Select(r=>r.GetProperty("updateCells").GetProperty("start").GetProperty("rowIndex").GetInt32()).ToArray();
            Check(http.BatchCount==1&&updated.SequenceEqual(new[]{2,3}),"Day save uses one atomic batch and touches only the selected day's rows");
        }
        http.SavedRows[2][3]="외부 수정";
        bool rejected=false;
        try{service.ChangeStoredDayAsync(new (StoredMeal,IReadOnlyList<TrackMenu>)[]{(tuesday,replacements),(dinner,catalog)}).GetAwaiter().GetResult();}
        catch(InvalidOperationException){rejected=true;}
        Check(rejected&&http.BatchCount==1,"Conflicting day edits block all writes without partial changes");
        AppServices.Sheets=new GoogleSheetsService(null!);
        var planner=new Menu_table();
        var toggle=typeof(Menu_table).GetMethod("EditMeal_Click",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        var editButton=(Button)planner.FindName("EditModeButton");var plannerGrid=(DataGrid)planner.FindName("GridDietResult");
        Check(!MealTableEditing.GetIsEditing(plannerGrid)&&editButton.Content.ToString()=="수정하기","Editor initially requires explicit edit mode");
        toggle.Invoke(planner,new object[]{editButton,new RoutedEventArgs()});
        Check(MealTableEditing.GetIsEditing(plannerGrid)&&editButton.Content.ToString()=="수정종료","Edit toggle enables editing and changes button label");
        toggle.Invoke(planner,new object[]{editButton,new RoutedEventArgs()});
        Check(!MealTableEditing.GetIsEditing(plannerGrid)&&editButton.Content.ToString()=="수정하기","Finish editing returns to read-only mode");
        Check(((DatePicker)planner.FindName("WeekPicker")).FirstDayOfWeek==DayOfWeek.Sunday,"Date picker begins on Sunday");
        var apply=typeof(Menu_table).GetMethod("ApplyDays",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        apply.Invoke(planner,new object[]{days});
        var choices=((WrapPanel)planner.FindName("DayPanel")).Children.OfType<CheckBox>().ToList();
        Check(choices.Where(c=>c.Tag.ToString() is "1" or "3" or "5").All(c=>!c.IsEnabled&&c.IsChecked==false),"Holiday checkboxes are disabled and unchecked");
        Check(((TextBlock)planner.FindName("ScheduleStatus")).Text.Contains("시설 점검"),"Disabled dates have a visible explanation");
        apply.Invoke(planner,new object[]{WeekSchedule.Build(start.AddDays(7),new List<IList<object>>(),new Dictionary<DateTime,string>())});
        Check(choices.Single(c=>c.Tag.ToString()=="1").IsEnabled&&choices.Single(c=>c.Tag.ToString()=="1").IsChecked==true,"Next open week restores the previously selected weekday");
        var pdf=MealPlanPdf.CreatePages("검증",start,meals,sundayFirst:true);
        pdf[0].Measure(new Size(MealPlanPdf.Width,MealPlanPdf.Height));pdf[0].Arrange(new Rect(0,0,MealPlanPdf.Width,MealPlanPdf.Height));pdf[0].UpdateLayout();
        Check(Children<TextBlock>(pdf[0]).Any(t=>t.Text=="4일 (일)")&&Children<TextBlock>(pdf[0]).Any(t=>t.Text=="10일 (토)"),"PDF export uses the same Sunday-through-Saturday interval");
        inner.ScrollToTop();outer.ScrollToVerticalOffset(200);outer.UpdateLayout();
        System.IO.Directory.CreateDirectory(folder);
        var page=new Page{Content=outer,Background=Brushes.White};
        FeedbackChecks.Render(page,System.IO.Path.Combine(folder,"fixed-week-table.png"),1200,700);
        MealTableEditing.SetIsEditing(grid,true);grid.UpdateLayout();
        Check(Children<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid).Any(h=>h.Background is SolidColorBrush b&&b.Color==Color.FromRgb(251,230,181)),"Edit mode paints actual table headers amber");
        FeedbackChecks.Render(page,System.IO.Path.Combine(folder,"editing-week-table.png"),1200,700);
        MealTableEditing.SetIsEditing(grid,false);grid.UpdateLayout();
        Check(Children<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid).Any(h=>h.Background is SolidColorBrush b&&b.Color==Color.FromRgb(234,243,237)),"Leaving edit mode restores green headers");
        var dashboardGrid=new DataGrid{IsReadOnly=true,AutoGenerateColumns=false,CanUserAddRows=false};
        WeekMealTable.ShowDashboard(dashboardGrid,meals,days);
        var dashboard=new Page{Background=Brushes.White,Content=new Viewbox{Child=dashboardGrid,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly}};
        FeedbackChecks.Render(dashboard,System.IO.Path.Combine(folder,"dashboard-week.png"),1120,850);
        Check(dashboardGrid.Columns.Count==8&&Children<ScrollViewer>(dashboardGrid).All(v=>v.ScrollableHeight==0&&v.ScrollableWidth==0),"Dashboard exposes all seven days without internal scrolling");
        var dashboardContent=(UIElement)dashboard.Content;dashboard.Content=null;
        var allergyCard=new Border{Child=new TextBlock{Text="알레르기 대상자"},Height=120};
        var allergyList=new ListBox{Items={allergyCard}};
        ScrollViewer.SetVerticalScrollBarVisibility(allergyList,ScrollBarVisibility.Disabled);
        allergyList.PreviewMouseWheel+=WeekMealTable.ScrollDashboard;
        var dashboardScroll=new ScrollViewer{Content=new StackPanel{Children={dashboardContent,allergyList,new Border{Height=900}}}};
        dashboardScroll.Measure(new Size(1120,500));dashboardScroll.Arrange(new Rect(0,0,1120,500));dashboardScroll.UpdateLayout();
        dashboardScroll.ScrollToVerticalOffset(300);dashboardScroll.UpdateLayout();
        var source=Children<TextBlock>(dashboardGrid).First();
        source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent});dashboardScroll.UpdateLayout();
        Check(dashboardScroll.VerticalOffset==180,"Wheel over a dashboard cell scrolls the page upward");
        source.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});dashboardScroll.UpdateLayout();
        Check(dashboardScroll.VerticalOffset==300,"Wheel over a dashboard cell scrolls the page downward");
        allergyCard.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,120){RoutedEvent=UIElement.PreviewMouseWheelEvent});dashboardScroll.UpdateLayout();
        Check(dashboardScroll.VerticalOffset==180,"Wheel over an allergy card scrolls the dashboard upward");
        allergyCard.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,0,-120){RoutedEvent=UIElement.PreviewMouseWheelEvent});dashboardScroll.UpdateLayout();
        Check(dashboardScroll.VerticalOffset==300,"Wheel over an allergy card scrolls the dashboard downward");
        Check(((MealTableHeading)grid.Columns[2].Header).Color=="#C43F3F"&&((MealTableHeading)grid.Columns[7].Header).Color=="#2867B2","Screen uses holiday red and Saturday blue");
        MealTableEditing.SetIsEditing(grid,true);grid.UpdateLayout();
        Check(Children<Border>(grid).Where(b=>b.Name=="Outline").All(b=>b.BorderBrush==Brushes.Transparent),"Amber mode preserves flat table cells without outlined cards");
        var schedulePdf=MealPlanPdf.CreatePages("검증",start,meals,sundayFirst:true,schedule:days);
        var pdfPage=new Page{Content=schedulePdf[0],Background=Brushes.White};
        FeedbackChecks.Render(pdfPage,System.IO.Path.Combine(folder,"pdf-schedule-preview.png"),1120,1584);
        Check(Children<TextBlock>(schedulePdf[0]).Any(t=>t.Text.Contains("생일 행사")),"PDF includes the registered event on its date");
        Check(Children<TextBlock>(schedulePdf[0]).Any(t=>t.Text.Contains("대체공휴일")&&t.Foreground is SolidColorBrush b&&b.Color==Color.FromRgb(196,63,63)),"PDF holiday date is red");
        Check(Children<TextBlock>(schedulePdf[0]).Any(t=>t.Text.Contains("10일 (토)")&&t.Foreground is SolidColorBrush b&&b.Color==Color.FromRgb(40,103,178)),"PDF Saturday date is blue");
        var soy=catalog[0] with{Allergens="5",Name="대두밥"};
        var close=soy with{Id="close",Name="찰밥",Allergens="없음",Carb=21};
        var milk=soy with{Id="milk",Name="우유밥",Allergens="2"};
        var far=close with{Id="far",Name="다른밥",Carb=60,Calories=330};
        var blank=soy with{Id="unknown",Name="성분미확인밥",Allergens=""};
        var sameNutritionWrongCategory=close with{Category="국류",Carb=20};
        var alternatives=new[]{far,milk,blank,sameNutritionWrongCategory,close};
        Check(AllergyAlternatives.Choose(soy,alternatives,new[]{"대두","우유"})==close,"Alternative selects nearest nutrition in same category excluding every registered allergen");
        Check(AllergyAlternatives.Choose(soy,new[]{milk,blank},new[]{"대두","우유"})==null,"Unknown allergen data and conflicting candidates cannot become safe alternatives");
        Check(AllergyAlternatives.Choose(soy with{Carb=null},alternatives,new[]{"대두"})==null,"Missing original nutrition cannot claim a similar replacement");
        var originalCatalog=catalog.Select((m,i)=>i==0?soy:m with{Allergens="없음"}).Concat(alternatives.Where(m=>m.Category==soy.Category)).ToArray();
        var allergyMeal=Stored(tuesday.Date,"중식");allergyMeal.Raw[3]=soy.Name;
        var rows=AllergyAlternatives.Build(new[]{allergyMeal},new[]{new PatientModel{Name="대상",Allergies="대두, 우유"}},new Dictionary<string,IReadOnlyList<TrackMenu>>{{"3-5",originalCatalog}});
        Check(rows.Count==1&&rows[0].SuggestedAlternative.Contains("대두밥 → 찰밥")&&rows[0].SuggestedAlternative.Contains("탄 "),"Patient meal contains concrete replacement and macro comparisons");
        Check(allergyMeal.Menus[0]=="대두밥"&&catalog.Skip(1).All(m=>rows[0].SuggestedAlternative.Contains(m.Name)),"Alternative generation preserves original meal and unaffected dishes");
        Check(!AllergyAlternatives.KnownAllergens("미확인")&&!AllergyAlternatives.KnownAllergens("-")&&AllergyAlternatives.KnownAllergens("없음"),"Unknown markers are never treated as verified allergen-free data");
        var monitor=new AllergyManagementPage();
        monitor.Measure(new Size(1500,900));monitor.Arrange(new Rect(0,0,1500,900));monitor.UpdateLayout();
        Children<TabControl>(monitor).First().SelectedIndex=1;monitor.UpdateLayout();
        ((DataGrid)monitor.FindName("DgAllergicMatches")).ItemsSource=rows;
        ((TextBlock)monitor.FindName("MonitorStatus")).Text="자동 대조 완료 · 대상자별 대체식단을 생성했습니다.";
        ((DataGrid)monitor.FindName("DgDailyMenu")).ItemsSource=catalog.Select(m=>new{DisplayName=m.DisplayName,Ingredients=m.Allergens}).ToArray();
        FeedbackChecks.Render(monitor,System.IO.Path.Combine(folder,"allergy-alternative-monitor.png"),1500,900);
        Check(Children<TextBlock>((DataGrid)monitor.FindName("DgAllergicMatches")).Any(t=>t.Text.Contains("대두밥 → 찰밥")),"Automatic alternative and nutrient comparison are visible in monitoring table");
        var deleteButton=(Button)planner.FindName("DeleteMealButton");var deleteAll=(Button)planner.FindName("DeleteWeekButton");
        var deleteToggle=typeof(Menu_table).GetMethod("DeleteMeal_Click",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!;
        Check(deleteButton.IsEnabled&&!deleteAll.IsEnabled&&deleteButton.Content.ToString()=="삭제하기","Only delete-mode button is enabled initially");
        deleteToggle.Invoke(planner,new object[]{deleteButton,new RoutedEventArgs()});
        Check(deleteAll.IsEnabled&&MealTableEditing.GetIsDeleting(plannerGrid)&&!MealTableEditing.GetIsEditing(plannerGrid)&&deleteButton.Content.ToString()=="삭제종료","Delete mode enables full deletion and the red state without editing");
        deleteToggle.Invoke(planner,new object[]{deleteButton,new RoutedEventArgs()});
        Check(!deleteAll.IsEnabled&&!MealTableEditing.GetIsDeleting(plannerGrid),"Ending delete mode disables full deletion");
        deleteToggle.Invoke(planner,new object[]{deleteButton,new RoutedEventArgs()});
        toggle.Invoke(planner,new object[]{editButton,new RoutedEventArgs()});
        Check(!deleteAll.IsEnabled&&!MealTableEditing.GetIsDeleting(plannerGrid)&&MealTableEditing.GetIsEditing(plannerGrid),"Edit and delete modes are mutually exclusive");
        MealTableEditing.SetIsDeleting(grid,true);grid.UpdateLayout();
        Check(Children<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid).Any(h=>h.Background is SolidColorBrush b&&b.Color==Color.FromRgb(248,218,218)),"Delete state overrides amber with red table headers");
        MealTableEditing.SetIsDeleting(grid,false);grid.UpdateLayout();
        Check(Children<System.Windows.Controls.Primitives.DataGridColumnHeader>(grid).Any(h=>h.Background is SolidColorBrush b&&b.Color==Color.FromRgb(251,230,181)),"Ending deletion restores the previous edit state");
        app.Dispatcher.InvokeShutdown();
    }
}
