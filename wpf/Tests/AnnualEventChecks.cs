using wpf;
internal static class AnnualEventChecks
{
    public static void Run(Action<bool,string> check)
    {
        IList<IList<object>> rows = new List<IList<object>> {
            new List<object>{"날짜","내용"},
            new List<object>{"2026-07-27",AnnualEvent.Encode("🎂 생일",true)},
            new List<object>{"2026-07-27","📌 한 번만"},
            new List<object>(),
            new List<object>{"2024-02-29",AnnualEvent.Encode("윤일 생일",true)},
            new List<object>{"2026-12-31",AnnualEvent.Encode("📌 [휴무] 연말 휴무",true)} };
        var events=AnnualEvent.Parse(rows);var birthday=events[0];
        check(birthday.RowIndex==2 && birthday.Content=="🎂 생일" && birthday.RepeatsYearly,"Yearly event round-trips without exposing storage marker");
        check(new[]{2026,2027,2028}.All(y=>birthday.OccursOn(new DateTime(y,7,27))),"Birthday repeats in 2026, 2027 and 2028");
        check(!birthday.OccursOn(new DateTime(2025,7,27))&&!birthday.OccursOn(new DateTime(2027,7,28)),"Repeat starts on its original date and matches only the same month/day");
        check(events[1].OccursOn(new DateTime(2026,7,27))&&!events[1].OccursOn(new DateTime(2027,7,27)),"Existing events remain one-time events");
        check(events[2].RowIndex==5&&events[2].OccursOn(new DateTime(2028,2,29))&&!events[2].OccursOn(new DateTime(2027,2,28))&&!events[2].OccursOn(new DateTime(2027,3,1)),"Leap-day event repeats only in leap years and keeps original sheet row");
        var days=WeekSchedule.Build(new DateTime(2027,7,27),rows,new Dictionary<DateTime,string>());
        check(days.Single(d=>d.Date==new DateTime(2027,7,27)).Events=="🎂 생일","Weekly schedule shows next year's birthday without old one-time event");
        var end=WeekSchedule.Build(new DateTime(2027,12,31),rows,new Dictionary<DateTime,string>());
        check(end.Single(d=>d.Date==new DateTime(2027,12,31)).IsClosed,"Yearly closure remains excluded from meal generation across year boundary");
        check(AnnualEvent.EditedDate(birthday,new DateTime(2028,7,27),true)==new DateTime(2026,7,27),"Editing a future occurrence retains the series start date");
        check(AnnualEvent.EditedDate(birthday,new DateTime(2028,7,27),false)==new DateTime(2028,7,27),"Turning off recurrence keeps only the selected date");
        check(!AnnualEvent.Parse(new List<IList<object>>{new List<object>{"2028-07-27",AnnualEvent.Encode("🎂 생일",false)}}).Single().RepeatsYearly,"Removing recurrence marker persists a one-time event");
    }
    public static void RunUi(Action<bool,string> check, string? preview = null)
    {
        var page = new EventCalendar();
        var once = (System.Windows.Controls.RadioButton)page.FindName("EventOnce");
        var yearly = (System.Windows.Controls.RadioButton)page.FindName("EventYearly");
        check(once.IsChecked == true && yearly.IsChecked != true,"Event editor defaults to one-time events");
        var birthday = new AnnualEvent(2,new DateTime(2026,7,27),"🎂 생일",true);
        var events = new List<AnnualEvent>{birthday};
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        typeof(EventCalendar).GetField("currentDayEvents",flags)!.SetValue(page,events);
        typeof(EventCalendar).GetField("_allEvents",flags)!.SetValue(page,events);
        var list = (System.Windows.Controls.ListBox)page.FindName("lstEvents");
        list.ItemsSource = events.Select(e=>e.DisplayContent).ToList();list.SelectedIndex=0;
        check(yearly.IsChecked==true && once.IsChecked==false && ((Emoji.Wpf.RichTextBox)page.FindName("txtEventInput")).Text.Trim()=="생일","Selecting yearly event restores radio and clean editable text");
        var calendar = (System.Windows.Controls.Calendar)page.FindName("MainCalendar");
        calendar.SelectedDate=new DateTime(2027,7,27);calendar.DisplayDate=new DateTime(2027,7,27);
        ((System.Windows.Controls.TextBlock)page.FindName("txtBannerYear")).Text="2027년";
        ((System.Windows.Controls.TextBlock)page.FindName("txtSelectedDate")).Text="7월 27일 (화)";
        ((System.Windows.Controls.TextBlock)page.FindName("txtNoEvents")).Visibility=System.Windows.Visibility.Collapsed;
        ((System.Windows.Controls.TextBlock)page.FindName("txtEventCount")).Text="1";
        page.Measure(new System.Windows.Size(1200,900));page.Arrange(new System.Windows.Rect(0,0,1200,900));page.UpdateLayout();
        IEnumerable<System.Windows.DependencyObject> Children(System.Windows.DependencyObject root)
        {
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++) {
                var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);yield return child;
                foreach(var nested in Children(child))yield return nested;
            }
        }
        var day=Children(calendar).OfType<System.Windows.Controls.Primitives.CalendarDayButton>().Single(b=>b.DataContext is DateTime date&&date==new DateTime(2027,7,27));
        check(day.Tag is CalendarDateMark mark && mark.HasEvent && mark.EventIcon=="🎂","Calendar marks birthday icon in the following year");
        if(preview!=null)FeedbackChecks.Render(page,preview,1200,900);
        once.IsChecked=true;
        check(yearly.IsChecked==false,"Repeat radio can be switched off");
    }
}
