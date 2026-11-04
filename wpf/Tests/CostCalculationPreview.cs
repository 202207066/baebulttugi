using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using wpf;

internal static class CostCalculationPreview
{
    internal static void Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
        app.Resources=(ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml",UriKind.Relative));
        var meal=ManagementChecks.Fixture();var http=new CostCalculationSheets(meal);
        foreach(var d in CostCalculationChecks.Inputs(meal))http.Tables[AppConfig.CostSheetName].Add(new object[] {d.Menu,d.Name,d.Category,d.Amount,d.Unit,d.Price,d.PriceQuantity,d.PriceUnit,meal.AgeGroup,"",d.WastePercent,""});
        http.Tables[AppConfig.CostSheetName].Add(new object[] {IngredientStandards.Marker,"양파","재료 공통 기준","","",2000,1,"kg","","MASTER:양파",10,""});
        for(int i=1;i<=30;i++)http.Tables["식수인원"].Add(new object[]{"검증대상-"+i,5,"",""});
        AppServices.Sheets=CostCalculationChecks.Service(http);
        var window=new Window {Title="배불뚝이 · 원가 입력·저장 검증 · 가상 재료 / Google 전송 없음",Content=new CalculatePage(),Width=1260,Height=850,Left=0,Top=0};
        window.ShowDialog();
        File.WriteAllText(Path.Combine(folder,"ui-saved-costs.json"),JsonSerializer.Serialize(new{ActualGoogleCalls=0,http.AtomicCalculationBatches,Tables=http.Tables},new JsonSerializerOptions{WriteIndented=true}));
    }
}
