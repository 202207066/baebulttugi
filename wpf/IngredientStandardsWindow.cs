using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;
namespace wpf;

public sealed class IngredientStandardsWindow : Window
{
    private readonly ObservableCollection<CostDraft> rows = [];
    private readonly DataGrid grid = new() { AutoGenerateColumns = false, CanUserAddRows = true, CanUserDeleteRows = false, EnableRowVirtualization = true, EnableColumnVirtualization = true };
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) };
    private readonly List<CostDraft> ingredients;
    private bool missingOnly;
    private bool busy;
    public bool HasSavedChanges {get;private set;}
    public HashSet<string> ChangedNames {get;}=[];
    public IngredientStandardsWindow(GoogleSheetsService service,IReadOnlyList<CostDraft>? mealIngredients=null,bool openMissing=false)
    {
        ingredients=mealIngredients?.Select(MealCostData.Clone).ToList()??[];
        Title = "원가 기준 입력 · 재료 공통 기준표 · 엑셀"; Width = 1180; Height = 680; MinWidth = 900; MinHeight = 500; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var root = new DockPanel { Margin = new Thickness(20) }; Content = root;
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
        top.Children.Add(new TextBlock { Text = "식재료별로 한 번 입력하면 모든 메뉴에 적용됩니다", FontSize = 22, FontWeight = FontWeights.Bold, TextWrapping = TextWrapping.Wrap });
        top.Children.Add(new TextBlock { Text = "동일한 식재료명에만 적용합니다. 제품·부위가 다르면 이름을 구분하세요. 빈 값은 기존 값을 유지합니다. 폐기율 0%는 0으로 입력하세요.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 10) });
        top.Children.Add(new TextBlock { Text = "입력: 구매가격 / 가격 기준량·단위 / 폐기율(%) / 개·봉 구매 시 1개 중량(g). 예: 양파 2,000원, 1kg, 폐기율 10 → g당 2원.\n정미량은 메뉴마다 다르므로 뒤의 원가 화면에서 레시피 분량과 식수를 확인하고 ‘한 끼 원가 계산·저장’을 누르세요. 액체의 ml·L는 무게(g)로 임의 환산하지 않습니다.", TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,14) });
        var buttons = new WrapPanel(); top.Children.Add(buttons);
        void Button(string title, RoutedEventHandler handler) { var button = new Button { Content = title, Padding = new Thickness(12, 8, 12, 8), Margin = new Thickness(0, 0, 12, 0) }; button.Click += handler; buttons.Children.Add(button); }
        Button("미입력 값 입력",(_,_)=>EditMissing());
        Button("선택 재료 입력·수정",(_,_)=>EditSelected());
        Button("전체 기준 보기",(_,_)=>{if(busy)return;try{Commit();missingOnly=false;RefreshRows();status.Text=$"재료 {rows.Count:N0}개 · 수정할 재료를 선택한 뒤 ‘선택 재료 입력·수정’을 누르세요.";}catch(Exception ex){status.Text=ex.Message;}});
        Button("엑셀 양식 저장", (_, _) => {
            var dialog = new SaveFileDialog { Filter = "Excel 파일|*.xlsx", FileName = "배불뚝이_재료기준표.xlsx" }; if (dialog.ShowDialog(this) != true) return;
            try { using var source = typeof(IngredientStandardsWindow).Assembly.GetManifestResourceStream("wpf.IngredientTemplate") ?? throw new InvalidOperationException("양식 파일이 포함되지 않았습니다."); using var output = File.Create(dialog.FileName); source.CopyTo(output); status.Text = "양식을 저장했습니다. 재료기준표 시트에 기준값을 채운 뒤 불러오세요."; } catch (Exception ex) { status.Text = ex.Message; }
        });
        Button("엑셀 불러오기 · 미리보기", (_, _) => {
            if (busy) return; var dialog = new OpenFileDialog { Filter = "Excel 파일|*.xlsx" }; if (dialog.ShowDialog(this) != true) return;
            try {
                Commit(); var imported = IngredientExcel.Read(dialog.FileName);
                var next = rows.Select(MealCostData.Clone).ToList();
                foreach (var entry in imported) { int index = next.FindIndex(r => IngredientStandards.Key(r.Name) == IngredientStandards.Key(entry.Name)); var merged = IngredientStandards.Merge(index < 0 ? null : next[index], entry); if (index < 0) next.Add(merged); else next[index] = merged; }
                IngredientStandards.Validate(next); rows.Clear(); foreach (var entry in next) rows.Add(entry);RefreshRows();
                status.Text = $"입력한 재료 {imported.Count:N0}개를 미리 불러왔습니다. 아직 저장 전입니다. 값을 확인하고 공통 기준 저장을 누르세요.";
            } catch (Exception ex) { status.Text = "불러오기 실패 · 저장하지 않았습니다: " + ex.Message; }
        });
        Button("공통 기준 저장", async (_, _) => {
            if (busy) return;
            bool saved = false;
            try {
                Commit(); var validated = IngredientStandards.Validate(rows);
                var changed=validated.Where(r=>(r.Price.Length>0||r.WastePercent.Length>0||r.PackageWeightGrams.Length>0)&&IngredientStandards.Signature(r)!=(r.SharedBasisSignature??"")).Select(r=>IngredientStandards.Key(r.Name)).ToArray();
                if(changed.Length==0){status.Text="새로 입력하거나 수정한 값이 없습니다. ‘미입력 값 입력’에서 재료를 입력하세요.";return;}
                busy = true; grid.IsEnabled = false; buttons.IsEnabled = false;
                await service.SaveIngredientStandardsAsync(validated); saved = true;
                HasSavedChanges=true;ChangedNames.UnionWith(changed);
                var latest = (await service.GetMealCostsAsync(true)).Where(IngredientStandards.IsStandard).ToArray();
                rows.Clear(); foreach (var row in IngredientCostEntry.Build(ingredients,latest.Select(r=>r.Draft).ToArray()))rows.Add(row);
                RefreshRows();status.Text = $"변경한 재료 {changed.Length:N0}개를 내 원가 시트에 저장했습니다. 창을 닫으면 원가 계산에 반영됩니다. 미입력 {rows.Count(r=>IngredientCostEntry.MissingFields(r).Length>0):N0}개.";
            }
            catch (Exception ex) { status.Text = (saved ? "저장은 완료했지만 재조회에 실패했습니다. 창을 다시 열어 최신 값을 확인해 주세요: " : "저장 실패: ") + ex.Message; }
            finally { busy = false; grid.IsEnabled = true; buttons.IsEnabled = true; }
        });
        top.Children.Add(status);
        var properties = new[] { "Name", "Price", "PriceQuantity", "PriceUnit", "WastePercent", "PackageWeightGrams" };
        for (int i = 0; i < properties.Length; i++) grid.Columns.Add(new DataGridTextColumn { Header = IngredientStandards.Headers[i], Binding = new Binding(properties[i]), Width = i == 0 ? 220 : i == 5 ? 132 : 100 });
        grid.Columns.Add(new DataGridTextColumn {Header="환산 기준량",Binding=new Binding(nameof(CostDraft.PurchaseBasisDisplay)),IsReadOnly=true,Width=110});
        grid.Columns.Add(new DataGridTextColumn {Header="g·ml당 단가 (자동)",Binding=new Binding(nameof(CostDraft.BaseUnitPriceDisplay)),IsReadOnly=true,Width=160});
        grid.Columns.Insert(1,new DataGridTextColumn {Header="입력 상태",Binding=new Binding(nameof(CostDraft.PurchaseInputStatus)),IsReadOnly=true,Width=200});
        grid.CellEditEnding+=(_,_)=>Dispatcher.BeginInvoke(new Action(()=>{if(!busy){try{Commit();RefreshRows();}catch(Exception ex){status.Text=ex.Message;}}}));
        grid.MouseDoubleClick+=(_,_)=>EditSelected();
        grid.ItemsSource = rows; root.Children.Add(grid);
        Loaded += async (_, _) =>
        {
            busy=true;buttons.IsEnabled=false;grid.IsEnabled=false;bool loaded=false;
            try
            {
                var saved=await service.GetMealCostsAsync(true);
                if(mealIngredients==null)ingredients.AddRange(saved.Where(r=>!IngredientStandards.IsStandard(r)).Select(r=>r.Draft));
                foreach(var row in IngredientCostEntry.Build(ingredients,saved.Where(IngredientStandards.IsStandard).Select(r=>r.Draft).ToArray()))rows.Add(row);
                status.Text=$"재료 {rows.Count:N0}개 · 미입력 {rows.Count(r=>IngredientCostEntry.MissingFields(r).Length>0):N0}개. ‘미입력 값 입력’을 누르면 입력창이 열립니다.";
                loaded=true;
            }
            catch(Exception ex){status.Text="조회 실패: "+ex.Message;}
            finally {busy=false;buttons.IsEnabled=true;grid.IsEnabled=true;}
            if(loaded&&openMissing)EditMissing();
        };
    }
    private void Commit() { if (!grid.CommitEdit(DataGridEditingUnit.Cell, true) || !grid.CommitEdit(DataGridEditingUnit.Row, true)) throw new InvalidOperationException("편집 중인 셀을 확인해 주세요."); }
    private void RefreshRows()
    {
        var view=CollectionViewSource.GetDefaultView(rows);
        view.Filter=missingOnly?item=>item is CostDraft row&&NeedsInput(row):null;
        view.Refresh();grid.Items.Refresh();
    }
    private bool NeedsInput(CostDraft row)=>IngredientCostEntry.MissingFields(row).Length>0&&(ingredients.Count==0||ingredients.Any(d=>IngredientStandards.Key(d.Name)==IngredientStandards.Key(row.Name)));
    private void EditMissing()
    {
        if(busy)return;
        try
        {
            Commit();
            var first=rows.FirstOrDefault(NeedsInput);
            if(first==null){missingOnly=false;RefreshRows();status.Text=rows.Count==0?"불러올 재료가 없습니다. 원가 계산 화면에서 저장된 식단을 선택한 뒤 다시 열어 주세요.":"선택한 식단의 미입력 값이 없습니다. 재료를 선택하고 ‘선택 재료 입력·수정’에서 값을 수정할 수 있습니다.";return;}
            missingOnly=true;RefreshRows();
            grid.SelectedItem=first;grid.ScrollIntoView(first);EditSelected();
        }
        catch(Exception ex){status.Text=ex.Message;}
    }
    private void EditSelected()
    {
        if(busy)return;
        try
        {
            Commit();
            if(grid.SelectedItem is not CostDraft selected){status.Text="입력하거나 수정할 재료를 먼저 선택해 주세요.";return;}
            var editor=new IngredientStandardEditorWindow(selected){Owner=this};
            if(editor.ShowDialog()!=true||editor.Edited==null)return;
            rows[rows.IndexOf(selected)]=editor.Edited;
            if(!rows.Any(NeedsInput))missingOnly=false;
            RefreshRows();if(!missingOnly)grid.SelectedItem=editor.Edited;
            status.Text=$"‘{selected.Name}’ 입력값을 적용했습니다. 아직 저장 전입니다. ‘공통 기준 저장’을 눌러 주세요. 남은 미입력 {rows.Count(NeedsInput):N0}개.";
        }
        catch(Exception ex){status.Text=ex.Message;}
    }
}
