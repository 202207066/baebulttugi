using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace wpf;

public sealed class IngredientStandardEditorWindow : Window
{
    public CostDraft? Edited {get;private set;}
    public IngredientStandardEditorWindow(CostDraft source)
    {
        Title="재료 원가 입력 · "+source.Name;Width=570;Height=630;MinWidth=490;MinHeight=570;
        WindowStartupLocation=WindowStartupLocation.CenterOwner;ResizeMode=ResizeMode.CanResize;
        var root=new DockPanel {Margin=new Thickness(24)};Content=root;
        var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);root.Children.Add(bottom);
        var message=new TextBlock {TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Firebrick,Margin=new Thickness(0,8,0,10)};bottom.Children.Add(message);
        var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};bottom.Children.Add(actions);
        var cancel=new Button {Content="취소",IsCancel=true,MinWidth=85,Margin=new Thickness(0,0,10,0),Padding=new Thickness(12,9,12,9)};
        var apply=new Button {Content="입력값 적용",IsDefault=true,MinWidth=125,Padding=new Thickness(12,9,12,9)};actions.Children.Add(cancel);actions.Children.Add(apply);
        var form=new StackPanel();root.Children.Add(new ScrollViewer {Content=form,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        form.Children.Add(new TextBlock {Text=source.Name,FontSize=23,FontWeight=FontWeights.Bold,TextWrapping=TextWrapping.Wrap});
        form.Children.Add(new TextBlock {Text="같은 이름의 재료에 공통으로 사용할 구매 기준입니다.\n폐기 없음은 0을 입력하세요. 적용 후 ‘공통 기준 저장’을 눌러야 저장됩니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,10,0,12)});
        if(source.Source.Contains("확인 필요"))form.Children.Add(new TextBlock {Text=source.Source,TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkOrange});
        string missing=IngredientCostEntry.MissingFields(source);
        form.Children.Add(new TextBlock {Text=missing.Length>0?"이번에 확인할 항목: "+missing:"입력된 기준값을 수정할 수 있습니다.",TextWrapping=TextWrapping.Wrap,Foreground=Brushes.DarkGreen,Margin=new Thickness(0,0,0,10)});
        var fields=new Dictionary<string,TextBox>();
        TextBox Field(string key,string label,string value,string hint)
        {
            form.Children.Add(new TextBlock {Text=label,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,6,0,4)});
            var input=new TextBox {Text=value,MinHeight=30,ToolTip=hint};fields[key]=input;form.Children.Add(input);return input;
        }
        Field("Price","구매가격 (원) · 예: 2000",source.Price,"그 양을 구입한 전체 가격");
        Field("PriceQuantity","가격 기준량 · 예: 1",source.PriceQuantity,"2000원에 1kg을 샀다면 1");
        form.Children.Add(new TextBlock {Text="가격 단위",FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,6,0,4)});
        var unit=new ComboBox {ItemsSource=new[]{"g","kg","ml","L","개","봉"},MinHeight=32,SelectedItem=source.PriceUnit.Equals("l",StringComparison.OrdinalIgnoreCase)?"L":source.PriceUnit.Trim().ToLowerInvariant()};form.Children.Add(unit);
        Field("WastePercent","폐기율 (%) · 예: 10 / 폐기 없음: 0",source.WastePercent,"0 이상 100 미만. 10은 10%입니다.");
        Field("PackageWeightGrams","개·봉당 중량 (g) · 개수와 무게를 환산할 때 입력",source.PackageWeightGrams,"달걀 1개가 60g이면 60. kg·g로만 구매·사용하면 비워 둡니다.");
        apply.Click+=(_,_)=>
        {
            try
            {
                var draft=MealCostData.Clone(source);
                draft.Price=fields["Price"].Text.Trim();draft.PriceQuantity=fields["PriceQuantity"].Text.Trim();draft.PriceUnit=unit.SelectedItem?.ToString()??"";
                draft.WastePercent=fields["WastePercent"].Text.Trim();draft.PackageWeightGrams=fields["PackageWeightGrams"].Text.Trim();
                string remaining=IngredientCostEntry.MissingFields(draft);
                if(remaining.Length>0)throw new InvalidOperationException("값을 확인해 주세요: "+remaining);
                IngredientStandards.Validate([draft]);Edited=draft;DialogResult=true;
            }
            catch(Exception ex){message.Text=ex.Message;}
        };
        Loaded+=(_,_)=>{var first=fields.Values.FirstOrDefault(f=>string.IsNullOrWhiteSpace(f.Text))??fields["Price"];first.Focus();first.SelectAll();};
    }
}
