using System.Windows;
using System.Windows.Controls;
using Google.Apis.Sheets.v4.Data;

namespace wpf;

public sealed class DayMealEditWindow:Window
{
    private readonly List<(StoredMeal Meal,MealEditWindow Editor,string[] Original)> editors=[];
    public IReadOnlyList<(StoredMeal Meal,IReadOnlyList<TrackMenu> Items)> Changes => editors
        .Where(x=>!x.Original.SequenceEqual(x.Editor.Selection.Select(m=>m.Key)))
        .Select(x=>(x.Meal,(IReadOnlyList<TrackMenu>)x.Editor.Selection)).ToArray();
    public DayMealEditWindow(DateTime date,IReadOnlyList<StoredMeal> meals,IReadOnlyList<TrackMenu> catalog)
    {
        Title=$"{date:M월 d일} ({"일월화수목금토"[(int)date.DayOfWeek]}) 식단 수정";
        Width=680;Height=760;MaxHeight=SystemParameters.WorkArea.Height;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var dock=new DockPanel{Margin=new Thickness(16)};Content=dock;
        var save=new Button{Content="이 날짜의 변경사항 저장",Padding=new Thickness(16,12,16,12),Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(save,Dock.Bottom);dock.Children.Add(save);save.Click+=(_,_)=>DialogResult=true;
        var tabs=new TabControl();dock.Children.Add(tabs);
        foreach(var meal in meals)
        {
            var editor=new MealEditWindow(meal,catalog,showSave:false);
            var content=editor.Content;editor.Content=null;
            editors.Add((meal,editor,editor.Selection.Select(m=>m.Key).ToArray()));
            tabs.Items.Add(new TabItem{Header=meal.Meal,Content=content});
        }
    }
}

public partial class GoogleSheetsService
{
    public async Task ChangeStoredDayAsync(IReadOnlyList<(StoredMeal Meal,IReadOnlyList<TrackMenu> Items)> changes)
    {
        if(changes.Count==0)return;
        if(changes.Select(x=>x.Meal.Date.Date).Distinct().Count()!=1)throw new InvalidOperationException("하루의 식단만 수정할 수 있습니다.");
        RequirePersonalWrite();await _mealSaveGate.WaitAsync().ConfigureAwait(false);
        try
        {
            var sheet=AppConfig.WeeklyMenuSheetName;
            var current=ParseStoredMeals(await GetValuesAsync(QuoteTitle(sheet)).ConfigureAwait(false));
            int id=await GetSheetIdByTitleAsync(sheet).ConfigureAwait(false)??throw new InvalidOperationException("식단 탭이 없습니다.");
            var requests=new List<Request>();
            var metadata=_sheetsService.Spreadsheets.Get(SpreadsheetId);metadata.Fields="sheets(properties)";
            var book=await metadata.ExecuteAsync().ConfigureAwait(false);
            int columns=book.Sheets.First(s=>s.Properties.SheetId==id).Properties.GridProperties.ColumnCount??0;
            if(columns<WeeklyHeaders.Length)requests.Add(new Request{AppendDimension=new AppendDimensionRequest{SheetId=id,Dimension="COLUMNS",Length=WeeklyHeaders.Length-columns}});
            foreach(var change in changes)
            {
                var latest=current.LastOrDefault(m=>m.Identity==change.Meal.Identity);
                if(latest==null || latest.Snapshot!=change.Meal.Snapshot)throw new InvalidOperationException("식단이 다른 곳에서 변경되었습니다. 새로고침 후 다시 수정해 주세요.");
                requests.Add(new Request{UpdateCells=new UpdateCellsRequest{Start=new GridCoordinate{SheetId=id,RowIndex=latest.RowIndex,ColumnIndex=0},Rows=[new RowData{Values=EditedMealValues(change.Meal,change.Items).Select(DataCell).ToList()}],Fields="userEnteredValue"}});
            }
            await _sheetsService.Spreadsheets.BatchUpdate(new BatchUpdateSpreadsheetRequest{Requests=requests},SpreadsheetId).ExecuteAsync().ConfigureAwait(false);
        }
        finally{_mealSaveGate.Release();}
    }
}
