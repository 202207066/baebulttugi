using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
namespace wpf;

public record MealDisplayColumn(string Header, string[] Menus, double? Calories, StoredMeal? Stored = null);
public record MealTableHeading(string Title, string Detail, string Color="#28523A");
public record MealCellText(string Name, string Calories);
public record MealDisplayRow(string Category, string[] Cells)
{
    public MealCellText[] Values => Cells.Select(text => {
        int separator = text.LastIndexOf(" · ", StringComparison.Ordinal);
        return separator < 0 ? new MealCellText(text, "") : new MealCellText(text[..separator], text[(separator + 3)..]);
    }).ToArray();
}
public static class MealPresentation
{
    public static string Label(string name, double? calories) => $"{name} · {(calories.HasValue ? $"{calories:0.#} kcal" : "열량 미확인")}";
    public static bool Matches(string name, string query) => string.Concat(name.Where(c => !char.IsWhiteSpace(c))).Contains(string.Concat(query.Where(c => !char.IsWhiteSpace(c))), StringComparison.OrdinalIgnoreCase);
    public static void Show(DataGrid grid, IReadOnlyList<MealDisplayColumn> meals)
    {
        grid.ItemsSource = null; grid.Columns.Clear(); grid.Tag = meals;
        grid.SelectionUnit = DataGridSelectionUnit.Cell; grid.SelectionMode = DataGridSelectionMode.Single;
        var resources = new ResourceDictionary { Source = new Uri("/wpf;component/MealTableStyles.xaml", UriKind.Relative) };
        grid.ColumnHeaderStyle = (Style)resources["MealTableHeader"];
        grid.RowStyle = (Style)resources["MealTableRow"];
        grid.CellStyle = (Style)resources["MealTableCell"];
        grid.Background = System.Windows.Media.Brushes.White;
        grid.BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(217,231,221));
        grid.BorderThickness = new Thickness(1);
        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        grid.HorizontalGridLinesBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(232,239,233));
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.ColumnHeaderHeight = 76;
        grid.SizeChanged -= ResizeColumns;
        grid.SizeChanged += ResizeColumns;
        grid.AlternationCount = 2; grid.FrozenColumnCount = 1; grid.MinRowHeight = 72; grid.RowHeight = double.NaN;
        grid.CanUserSortColumns = false; grid.FontSize = 14;
        grid.Columns.Add(new DataGridTextColumn { Header = new MealTableHeading("메뉴 구성", "1인분 기준"), Binding = new Binding("Category"), Width = 110, CanUserSort = false });
        for (int i = 0; i < meals.Count; i++) {
            var panel = new FrameworkElementFactory(typeof(StackPanel));
            var name = new FrameworkElementFactory(typeof(TextBlock));
            name.SetBinding(TextBlock.TextProperty, new Binding($"Values[{i}].Name"));
            name.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            name.SetValue(TextBlock.FontWeightProperty, FontWeights.Medium);
            panel.AppendChild(name);
            var energy = new FrameworkElementFactory(typeof(TextBlock));
            energy.SetBinding(TextBlock.TextProperty, new Binding($"Values[{i}].Calories"));
            energy.SetValue(TextBlock.FontSizeProperty, 12d);
            energy.SetValue(TextBlock.ForegroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(108,135,115)));
            energy.SetValue(FrameworkElement.MarginProperty, new Thickness(0,5,0,0));
            panel.AppendChild(energy);
            var lines = meals[i].Header.Split('\n', 2);
            string heading = meals[i].Stored is StoredMeal saved ? $"{saved.Day}요일 · {saved.Date:M/d}" : lines[0];
            grid.Columns.Add(new DataGridTemplateColumn { Header = new MealTableHeading(heading, lines.Length > 1 ? lines[1] : ""), CellTemplate = new DataTemplate { VisualTree = panel }, Width = new DataGridLength(1, DataGridLengthUnitType.Star), MinWidth = 165, CanUserReorder = false, CanUserSort = false });
        }
        grid.ItemsSource = meals.Count == 0 ? Array.Empty<MealDisplayRow>() : WeeklyMealPlanner.Categories.Select((c,i) => new MealDisplayRow(c, meals.Select(m => m.Menus[i]).ToArray())).Append(new MealDisplayRow("총 열량", meals.Select(m => m.Calories.HasValue ? $"{m.Calories:0.#} kcal" : "열량 미확인").ToArray())).ToArray();
        grid.CanUserReorderColumns = false;
        var textStyle = new Style(typeof(TextBlock));
        textStyle.Setters.Add(new Setter(TextBlock.TextWrappingProperty, TextWrapping.Wrap));
        textStyle.Setters.Add(new Setter(TextBlock.PaddingProperty, new Thickness(6)));
        foreach(var column in grid.Columns.OfType<DataGridTextColumn>()) column.ElementStyle = textStyle;
        ScrollViewer.SetHorizontalScrollBarVisibility(grid, ScrollBarVisibility.Auto);
        FitColumns(grid);
    }
    private static void ResizeColumns(object sender, SizeChangedEventArgs e) => FitColumns((DataGrid)sender);
    private static void FitColumns(DataGrid grid)
    {
        if(grid.Columns.Count < 2 || grid.ActualWidth <= 0) return;
        double width = grid.Tag is WeekMealTableState ? Math.Max(130,(grid.ActualWidth-150)/7) : Math.Max(165, (grid.ActualWidth - 130) / (grid.Columns.Count - 1));
        foreach(var column in grid.Columns.Skip(1)) column.Width = new DataGridLength(width);
    }
    public static StoredMeal? Selected(DataGrid grid) => grid.Tag is WeekMealTableState ? WeekMealTable.Selected(grid).Meal : grid.SelectedCells.Count > 0 && grid.Tag is IReadOnlyList<MealDisplayColumn> meals && grid.SelectedCells[0].Column?.DisplayIndex is int i && i > 0 && i <= meals.Count ? meals[i - 1].Stored : null;
}
public partial class GoogleSheetsService
{
    public async Task<List<MealDisplayColumn>> DisplayMealsAsync(IReadOnlyList<StoredMeal> meals)
    {
        var catalogs = new Dictionary<string, IReadOnlyList<TrackMenu>>();
        foreach (var age in meals.Select(m => string.IsNullOrEmpty(m.AgeGroup) ? "3-5" : m.AgeGroup).Distinct()) {
            try { catalogs[age] = (await GetTrackCatalogAsync(age)).Menus; }
            catch { catalogs[age] = Array.Empty<TrackMenu>(); }
        }
        return meals.Select(m => {
            var catalog = catalogs[string.IsNullOrEmpty(m.AgeGroup) ? "3-5" : m.AgeGroup];
            var keys = m.Value(25).Split(" / ");
            var labels = m.Menus.Select((name,i) => {
                var exact = catalog.FirstOrDefault(x => i < keys.Length && x.Key == keys[i]);
                var candidates = catalog.Where(x => x.Name == name && x.Category == WeeklyMealPlanner.Categories[i]).ToList();
                var item = exact ?? (candidates.Count == 1 ? candidates[0] : null);
                return MealPresentation.Label(name, item?.DisplayCalories);
            }).ToArray();
            return new MealDisplayColumn($"{m.Date:M/d} ({m.Day})\n{m.Meal} · {m.AgeGroup}세", labels, m.Calories, m);
        }).ToList();
    }
}
