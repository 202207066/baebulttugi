using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Data;
using System.Windows.Media;

namespace wpf;

public record WeekMealRow(string Category,string[] Cells,StoredMeal?[] Meals,int MenuIndex):MealDisplayRow(Category,Cells);
public record WeekMealTableState(IReadOnlyList<WeekDayNotice> Days,IReadOnlyList<MealDisplayColumn> Meals);
public static class WeekMealTable
{
    public static void ShowDashboard(DataGrid grid,IReadOnlyList<MealDisplayColumn> meals,IReadOnlyList<WeekDayNotice> days)
    {
        Show(grid,meals,days);
        grid.HorizontalAlignment=HorizontalAlignment.Stretch;
        grid.EnableRowVirtualization=true;
        ScrollViewer.SetVerticalScrollBarVisibility(grid,ScrollBarVisibility.Auto);
    }

    public static void Show(DataGrid grid,IReadOnlyList<MealDisplayColumn> meals,IReadOnlyList<WeekDayNotice> days)
    {
        days=days.Where(d=>d.Date.DayOfWeek!=DayOfWeek.Sunday).OrderBy(d=>d.Date).ToArray();
        meals=meals.Where(m=>m.Stored?.Date.DayOfWeek!=DayOfWeek.Sunday).ToArray();
        grid.PreviewMouseWheel-=ScrollDashboard;
        MealPresentation.Show(grid,days.Select(d=>new MealDisplayColumn($"{d.Date:M/d}\n{d.Description}",Enumerable.Repeat("",6).ToArray(),null)).ToArray());
        grid.Tag=new WeekMealTableState(days,meals);
        grid.ColumnHeaderHeight=double.NaN;
        grid.MinRowHeight=36;
        grid.Columns[0].Width=110;
        grid.ColumnHeaderStyle=new Style(typeof(DataGridColumnHeader),grid.ColumnHeaderStyle){Setters={new Setter(Control.PaddingProperty,new Thickness(8,6,8,6))}};
        grid.CellStyle=new Style(typeof(DataGridCell),grid.CellStyle){Setters={new Setter(Control.PaddingProperty,new Thickness(6,2,6,2))}};
        ((DataGridTextColumn)grid.Columns[0]).ElementStyle=new Style(typeof(TextBlock)){Setters={new Setter(TextBlock.TextWrappingProperty,TextWrapping.Wrap),new Setter(TextBlock.FontSizeProperty,13d)}};
        for(int i=0;i<days.Count;i++)
        {
            var column=(DataGridTemplateColumn)grid.Columns[i+1];
            var content=new FrameworkElementFactory(typeof(TextBlock));
            content.SetBinding(TextBlock.TextProperty,new Binding($"Cells[{i}]"));
            content.SetBinding(FrameworkElement.ToolTipProperty,new Binding($"Cells[{i}]"));
            content.SetValue(TextBlock.TextWrappingProperty,TextWrapping.Wrap);
            content.SetValue(TextBlock.FontSizeProperty,13d);
            content.SetValue(TextBlock.FontWeightProperty,FontWeights.SemiBold);
            column.CellTemplate=new DataTemplate {VisualTree=content};
            column.MinWidth=80;column.Width=new DataGridLength(1,DataGridLengthUnitType.Star);
        }
        ScrollViewer.SetHorizontalScrollBarVisibility(grid,ScrollBarVisibility.Disabled);
        for(int i=0;i<days.Count;i++)grid.Columns[i+1].Header=new MealTableHeading($"{"일월화수목금토"[(int)days[i].Date.DayOfWeek]}요일 · {days[i].Date:M/d}",days[i].Description,days[i].DateColor);
        var rows=new List<WeekMealRow>();
        var kinds=new[]{"조식","중식","석식"}.Where(k=>meals.Any(m=>m.Stored?.Meal==k)).ToArray();
        if(kinds.Length==0)kinds=["중식"];
        foreach(var kind in kinds)
        {
            var columns=days.Select(d=>meals.LastOrDefault(m=>m.Stored?.Date.Date==d.Date && m.Stored.Meal==kind)).ToArray();
            int count=Math.Max(6,columns.Where(m=>m!=null).Select(m=>m!.Menus.Length).DefaultIfEmpty(6).Max());
            for(int menu=0;menu<=count;menu++)
                rows.Add(new WeekMealRow(kind+" · "+(menu==count?"총 열량":MealSnapshot.LabelAt(menu)),columns.Select((m,i)=>m==null?(days[i].IsClosed?"휴무":"—"):menu==count?(m.Calories.HasValue?$"{m.Calories:0.#} kcal":"열량 미확인"):m.Menus.ElementAtOrDefault(menu)??"—").ToArray(),columns.Select(m=>m?.Stored).ToArray(),menu==count?-1:menu));
        }
        grid.ItemsSource=rows;
        grid.PreviewMouseWheel-=Wheel;
        grid.PreviewMouseWheel+=Wheel;
    }
    public static (StoredMeal? Meal,int Index) Selected(DataGrid grid)
    {
        if(grid.SelectedCells.Count==0)return(null,-1);
        var cell=grid.SelectedCells[0];
        if(cell.Column==null)return(null,-1);
        int column=cell.Column.DisplayIndex-1;
        return cell.Item is WeekMealRow row && column>=0 && column<row.Meals.Length?(row.Meals[column],row.MenuIndex):(null,-1);
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
