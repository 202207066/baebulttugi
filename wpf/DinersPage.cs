using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;
using System.Text;
namespace wpf;
public sealed class DinersPage : Page
{
    private readonly GoogleSheetsService service = AppServices.Require();
    private readonly ObservableCollection<Diner> pending = new();
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,12) };
    private readonly DataGrid saved = new() { IsReadOnly = true, AutoGenerateColumns = false, Height = 230 };
    private readonly StackPanel form = new();
    public DinersPage()
    {
        Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/wpf;component/PlannerStyles.xaml", UriKind.Relative) });
        FontSize = 14; Foreground = (Brush)new BrushConverter().ConvertFromString("#263E2F")!;
        Background = (Brush)new BrushConverter().ConvertFromString("#F4F8F4")!;
        var root = new StackPanel { Margin = new Thickness(30) };
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        root.Children.Add(new TextBlock { Text = "식수인원 등록", FontSize = 28, FontWeight = FontWeights.Bold });
        root.Children.Add(new TextBlock { Text = "직접 추가하거나 CSV를 불러온 뒤, 목록을 확인하고 저장해 주세요. 같은 내용의 행은 중복 등록하지 않습니다.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,10,0,20) });
        root.Children.Add(form);
        var inputs = new WrapPanel(); form.Children.Add(inputs);
        TextBox Input(string title) { var panel = new StackPanel { Width = 190, Margin = new Thickness(0,0,12,12) }; panel.Children.Add(new TextBlock { Text=title }); var box = new TextBox(); panel.Children.Add(box); inputs.Children.Add(panel); return box; }
        var name=Input("이름 (필수)"); var age=Input("나이"); var gender=Input("성별"); var notes=Input("특이사항");
        var actions = new WrapPanel(); form.Children.Add(actions);
        Button Button(string text, RoutedEventHandler handler) { var b = new Button { Content=text, Margin=new Thickness(0,0,10,12), Padding=new Thickness(16,10,16,10) }; b.Click+=handler; actions.Children.Add(b); return b; }
        Button("목록에 추가", (_,_) => { try { var d=new Diner(name.Text.Trim(),age.Text.Trim(),gender.Text.Trim(),notes.Text.Trim()); DinerCsv.Validate(d); pending.Add(d); name.Clear(); age.Clear(); gender.Clear(); notes.Clear(); status.Text="아래 목록을 확인하고 저장해 주세요."; } catch(Exception ex) { status.Text=ex.Message; } });
        Button("CSV 가져오기", async (_,_) => {
            var dialog = new OpenFileDialog { Filter="CSV 파일|*.csv" }; if(dialog.ShowDialog()!=true) return;
            form.IsEnabled=false;
            try { using var activity=AppActivity.Begin("CSV 내용을 확인하는 중입니다…"); var list=await Task.Run(()=> { string text; try { text=File.ReadAllText(dialog.FileName,new UTF8Encoding(false,true)); } catch(DecoderFallbackException) { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); text=File.ReadAllText(dialog.FileName,Encoding.GetEncoding(949)); } return DinerCsv.Parse(new StringReader(text)); }); foreach(var d in list) pending.Add(d); status.Text=$"{list.Count}명 불러옴. 아직 저장 전입니다."; }
            catch(Exception ex) { status.Text="CSV를 가져오지 못했습니다: "+ex.Message; } finally {form.IsEnabled=true;}
        });
        Button("CSV 양식 받기", (_,_) => { var dialog=new SaveFileDialog { Filter="CSV 파일|*.csv", FileName="식수인원_입력양식.csv" }; if(dialog.ShowDialog()==true) { try { File.WriteAllText(dialog.FileName,"이름,나이,성별,특이사항\r\n",new UTF8Encoding(true)); status.Text="양식을 저장했습니다. 첫 행 아래에 대상자를 입력해 주세요."; } catch(Exception ex){status.Text=ex.Message;} } });
        Button("입력 목록 비우기", (_,_)=>pending.Clear());
        var grid = new DataGrid { ItemsSource=pending, IsReadOnly=true, AutoGenerateColumns=false, Height=200 }; Columns(grid); form.Children.Add(grid);
        var save=new Button {Style=(Style)FindResource("PrimaryButton"), Content="입력한 명단 저장", HorizontalAlignment=HorizontalAlignment.Left, Margin=new Thickness(0,14,0,0), Padding=new Thickness(20,12,20,12)};
        save.Click+=async(_,_)=> { if(pending.Count==0){status.Text="저장할 명단을 추가해 주세요.";return;} form.IsEnabled=false; try {using var activity=AppActivity.Begin("식수인원을 저장하는 중입니다…"); int added=await service.AddDinersAsync(pending.ToList()); pending.Clear(); await Load(); status.Text=$"{added}명 저장 완료. 동일한 내용은 중복 저장하지 않았습니다.";} catch(Exception ex){status.Text="저장 실패: "+ex.Message;} finally{form.IsEnabled=true;} }; form.Children.Add(save);
        root.Children.Add(status); root.Children.Add(new TextBlock {Text="등록된 식수인원",FontSize=18,FontWeight=FontWeights.Bold,Margin=new Thickness(0,12,0,10)}); Columns(saved);root.Children.Add(saved);
        Loaded+=async(_,_)=>await Load();
    }
    private static void Columns(DataGrid g) { foreach(var (header,path) in new[]{("이름","Name"),("나이","Age"),("성별","Gender"),("특이사항","Notes")}) g.Columns.Add(new DataGridTextColumn {Header=header,Binding=new System.Windows.Data.Binding(path),Width=new DataGridLength(1,DataGridLengthUnitType.Star)}); }
    private async Task Load(){try {using var activity=AppActivity.Begin("등록된 식수인원을 불러오는 중입니다…");var list=await service.GetDinersAsync();saved.ItemsSource=list;status.Text=$"현재 등록 인원 {list.Count}명";}catch(Exception ex){status.Text="명단 조회 실패: "+ex.Message;}}
}
