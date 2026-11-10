using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using wpf;

internal static class LayoutChecks
{
    static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static IEnumerable<T> Children<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {var c=VisualTreeHelper.GetChild(root,i);if(c is T t)yield return t;foreach(var d in Children<T>(c))yield return d;}
    }
    static void Pump(Task task)
    {
        var dispatcher=Dispatcher.CurrentDispatcher;
        if(!task.IsCompleted)
        {
            var frame=new DispatcherFrame();
            _=task.ContinueWith(_=>dispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.Default);
            Dispatcher.PushFrame(frame);
        }
        task.GetAwaiter().GetResult();
    }
    internal static void Run(string folder,bool preview=false)
    {
        Directory.CreateDirectory(folder);
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources=(ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml",UriKind.Relative));
        var root=GoogleSheetsService.OpenLocal(Path.Combine(Path.GetFullPath(folder),Guid.NewGuid()+".json"));
        var facility=root.GetFacilitiesAsync().GetAwaiter().GetResult().Single();
        facility=root.SaveFacilityAsync(facility,"화면 검증용 급식소",30).GetAwaiter().GetResult()!;
        var service=root.ForFacility(facility);AppServices.Sheets=service;
        var week=WeeklyMealPlanner.Monday(DateTime.Today);
        var items=WeeklyMealPlanner.Categories.Select((c,i)=>new TrackMenu("LAYOUT"+i,new[]{"현미밥","맑은무국","닭살구이","시금치무침","배추김치","사과"}[i],c,"없음",10,c=="주찬"?12:2,2,80,"화면 검증용 가상자료"){ServingGrams=100,Almond="없음 확인",Sesame="없음 확인",OtherBeans="없음 확인"}).ToArray();
        var meals=Enumerable.Range(0,7).SelectMany(i=>new[]{"조식","중식","석식"}.Select(k=>new WeeklyMeal {Date=week.AddDays(i),Meal=k,AgeGroup="3-5",Track="A",Items=i==6?items.Select(m=>m with{Name="일요일전용"+m.Name}).ToArray():items,Targets=new(60,20,12,15)})).ToArray();
        service.SaveWeeklyMealsAsync(meals).GetAwaiter().GetResult();
        service.GetPatientsAsync().GetAwaiter().GetResult();
        service.SavePatientsAsync([new PatientModel{Id=1,Name="검증-우유참깨",Category="일반",Allergies="우유,참깨",AllergyState="있음 확인",AllergyChangeConfirmed=true}]).GetAwaiter().GetResult();
        var shown=service.GetStoredMealsAsync(week,excludeSunday:true).GetAwaiter().GetResult();
        Check(shown.Count==18&&shown.All(m=>m.Date.DayOfWeek!=DayOfWeek.Sunday),"주간 조회는 월~토 18끼, 기존 일요일 자료는 보존");
        var sunday=service.GetStoredMealsAsync().GetAwaiter().GetResult().Where(m=>m.Date.DayOfWeek==DayOfWeek.Sunday).Select(m=>m.Snapshot).ToArray();
        var days=WeekSchedule.Build(week,new List<IList<object>>(),new Dictionary<DateTime,string>());
        Check(days.Count==6&&days[0].Date.DayOfWeek==DayOfWeek.Monday&&days[^1].Date.DayOfWeek==DayOfWeek.Saturday,"주간 일정은 월~토 여섯 날짜");
        var crossing=WeekSchedule.Build(new DateTime(2026,12,31),new List<IList<object>>(),new Dictionary<DateTime,string>());
        Check(crossing[0].Date==new DateTime(2026,12,28)&&crossing[^1].Date==new DateTime(2027,1,2),"연말 주간 범위도 날짜 이동 없이 월~토");
        var display=service.DisplayMealsAsync(service.GetStoredMealsAsync().GetAwaiter().GetResult()).GetAwaiter().GetResult();
        var table=new DataGrid {AutoGenerateColumns=false,IsReadOnly=true,CanUserAddRows=false};WeekMealTable.Show(table,display,days);
        table.Measure(new Size(1100,550));table.Arrange(new Rect(0,0,1100,550));table.UpdateLayout();
        Check(table.Columns.Count==7&&table.Items.Cast<WeekMealRow>().All(x=>x.Cells.Length==6),"주간 표는 구분 열과 월~토만 렌더링");
        table.SelectedCells.Add(new DataGridCellInfo(table.Items[2],table.Columns[6]));
        Check(WeekMealTable.Selected(table).Meal?.Date==week.AddDays(5),"토요일 선택이 토요일 저장 식단으로 연결");
        var pdf=MealPlanPdf.CreatePages("검증",week,display);
        var texts=new List<string>();
        foreach(var p in pdf){p.Measure(new Size(MealPlanPdf.Width,MealPlanPdf.Height));p.Arrange(new Rect(0,0,MealPlanPdf.Width,MealPlanPdf.Height));p.UpdateLayout();texts.AddRange(Children<TextBlock>(p).Select(t=>t.Text));}
        Check(!texts.Any(t=>t.Contains("일요일전용")||t.Contains("일 (일)"))&&texts.Any(t=>t.Contains("일 (토)")),"PDF는 일요일 메뉴·열을 빼고 토요일까지 출력");
        MealPlanPdf.Save(Path.Combine(folder,"월토_검증.pdf"),"검증",week,display);
        if(!preview)
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
            var planner=new Menu_table("3-5",week);
            Pump((Task)typeof(Menu_table).GetMethod("LoadWeekAsync",Private)!.Invoke(planner,null)!);
            FeedbackChecks.Render(planner,Path.Combine(folder,"planner-compact.png"),1100,620);
            var result=(DataGrid)planner.FindName("GridDietResult");
            Check(result.Columns.Count==7&&result.Items.Count==7,"식단 만들기는 월~토를 끼니별로 표시");
            ((ComboBox)planner.FindName("MealView")).SelectedItem="석식";
            Check(result.Items.Cast<WeekMealRow>().All(r=>r.Category.StartsWith("석식")),"표시 끼니를 바꿔도 해당 끼니의 여섯 날짜를 유지");
            var dashboardMain=new wpf.Main(service);
            var load=(Task<Page>)typeof(wpf.Main).GetMethod("LoadDefaultDashboardAsync",Private)!.Invoke(dashboardMain,null)!;
            Pump(load);
            FeedbackChecks.Render(load.Result,Path.Combine(folder,"dashboard-compact.png"),1100,620);
            var allergyButton=Children<Button>(load.Result).Single(b=>Equals(b.Content,"대상자 · 대체 식단 보기"));
            var at=allergyButton.TransformToAncestor(load.Result).Transform(new Point());
            Check(at.Y>=0&&at.Y+allergyButton.ActualHeight<300,"대시보드 알레르기 상세 버튼이 스크롤 없이 상단에 표시");
            dashboardMain.Close();
            Check(service.DeleteStoredMealsForWeekAsync(week,"3-5",excludeSunday:true).GetAwaiter().GetResult()==18,"이번 주 삭제는 화면에 표시한 월~토만 대상");
            Check(service.GetStoredMealsAsync().GetAwaiter().GetResult().Select(m=>m.Snapshot).SequenceEqual(sunday),"주간 삭제 후 기존 일요일 3끼 원문 유지");
            root.CloseLocal();
            app.Shutdown();Dispatcher.CurrentDispatcher.InvokeShutdown();
            Console.WriteLine("Layout data checks complete. No live Google requests.");return;
        }
        var main=new wpf.Main(service){Title="배불뚝이 · 화면 검증용 가상자료",WindowState=WindowState.Normal,Width=1366,Height=768,Left=0,Top=0};
        main.Loaded-=(RoutedEventHandler)Delegate.CreateDelegate(typeof(RoutedEventHandler),main,typeof(wpf.Main).GetMethod("Main_Loaded",Private)!);
        ((TextBlock)main.FindName("SideAccountName")).Text="화면 검증용";
        ((TextBlock)main.FindName("SideAccountEmail")).Text="실제 계정 연결 없음";
        ((TextBlock)main.FindName("FacilityCount")).Text="가상 급식소 · 30명";
        main.Loaded+=async(_,_)=>{await (Task<Page>)typeof(wpf.Main).GetMethod("LoadDefaultDashboardAsync",Private)!.Invoke(main,null)!;};
        main.Closed+=(_,_)=>{root.CloseLocal();app.Shutdown();};
        main.Show();Dispatcher.Run();
    }
}
