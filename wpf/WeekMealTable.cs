using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace wpf;

public record WeekMealRow(string Category,string[] Cells,StoredMeal?[] Meals,int MenuIndex):MealDisplayRow(Category,Cells);
public record WeekMealTableState(IReadOnlyList<WeekDayNotice> Days,IReadOnlyList<MealDisplayColumn> Meals);
public static class WeekMealTable
{
    public static void ShowDashboard(DataGrid grid,IReadOnlyList<MealDisplayColumn> meals,IReadOnlyList<WeekDayNotice> days)
    {
        Show(grid,meals,days);
        grid.Width=1120;grid.MaxHeight=double.PositiveInfinity;
        grid.EnableRowVirtualization=false;
        ScrollViewer.SetVerticalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);
        grid.PreviewMouseWheel-=Wheel;
        grid.PreviewMouseWheel-=ScrollDashboard;grid.PreviewMouseWheel+=ScrollDashboard;
    }
    public static void Show(DataGrid grid,IReadOnlyList<MealDisplayColumn> meals,IReadOnlyList<WeekDayNotice> days)
    {
        grid.PreviewMouseWheel-=ScrollDashboard;
        MealPresentation.Show(grid,days.Select(d=>new MealDisplayColumn($"{d.Date:M/d}\n{d.Description}",Enumerable.Repeat("",6).ToArray(),null)).ToArray());
        grid.Tag=new WeekMealTableState(days,meals);
        grid.ColumnHeaderHeight=double.NaN;
        grid.Columns[0].Width=130;
        foreach(var column in grid.Columns.Skip(1)){column.MinWidth=130;column.Width=Math.Max(130,(grid.ActualWidth-150)/7);}
        for(int i=0;i<7;i++)grid.Columns[i+1].Header=new MealTableHeading($"{"일월화수목금토"[i]}요일 · {days[i].Date:M/d}",days[i].Description,days[i].DateColor);
        var rows=new List<WeekMealRow>();
        var kinds=new[]{"조식","중식","석식"}.Where(k=>meals.Any(m=>m.Stored?.Meal==k)).ToArray();
        if(kinds.Length==0)kinds=["중식"];
        foreach(var kind in kinds)
        {
            var columns=days.Select(d=>meals.LastOrDefault(m=>m.Stored?.Date.Date==d.Date && m.Stored.Meal==kind)).ToArray();
            for(int menu=0;menu<=6;menu++)
                rows.Add(new WeekMealRow(kind+"\n"+(menu==6?"총 열량":WeeklyMealPlanner.Categories[menu]),columns.Select((m,i)=>m==null?(days[i].IsClosed?"휴무":"—"):menu==6?(m.Calories.HasValue?$"{m.Calories:0.#} kcal":"열량 미확인"):m.Menus.ElementAtOrDefault(menu)??"—").ToArray(),columns.Select(m=>m?.Stored).ToArray(),menu));
        }
        grid.ItemsSource=rows;
        grid.PreviewMouseWheel-=Wheel;
        grid.PreviewMouseWheel+=Wheel;
    }
    public static (StoredMeal? Meal,int Index) Selected(DataGrid grid)
    {
        if(grid.SelectedCells.Count==0)return(null,-1);
        var cell=grid.SelectedCells[0];int column=cell.Column.DisplayIndex-1;
        return cell.Item is WeekMealRow row && column>=0 && column<7?(row.Meals[column],row.MenuIndex):(null,-1);
    }
    public static T? Ancestor<T>(DependencyObject? item) where T:DependencyObject
    {
        while(item!=null){if(item is T found)return found;item=VisualTreeHelper.GetParent(item);}
        return null;
    }
    private static ScrollViewer? InnerScroll(DependencyObject item)
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(item);i++)
        {
            var child=VisualTreeHelper.GetChild(item,i);
            if(child is ScrollViewer viewer)return viewer;
            if(InnerScroll(child) is ScrollViewer nested)return nested;
        }
        return null;
    }
    public static void ScrollDashboard(object sender,MouseWheelEventArgs e)
    {
        // Bypass the child control's ScrollViewer; the dashboard owns scrolling.
        var parent=Ancestor<ScrollViewer>(VisualTreeHelper.GetParent((DependencyObject)sender));
        if(parent==null)return;
        parent.ScrollToVerticalOffset(parent.VerticalOffset-e.Delta);
        e.Handled=true;
    }
    public static void Wheel(object sender,MouseWheelEventArgs e)
    {
        var grid=(DataGrid)sender;
        var inner=InnerScroll(grid);
        var target=inner!=null && (e.Delta<0?inner.VerticalOffset<inner.ScrollableHeight:inner.VerticalOffset>0)?inner:Ancestor<ScrollViewer>(VisualTreeHelper.GetParent(grid));
        if(target==null)return;
        target.ScrollToVerticalOffset(target.VerticalOffset-e.Delta);e.Handled=true;
    }
}
