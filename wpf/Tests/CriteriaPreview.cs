using System.Windows;
using wpf;

internal static class CriteriaPreview
{
    internal static void Run()
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Resources = (ResourceDictionary)Application.LoadComponent(new Uri("/PlannerChecks;component/PreviewResources.xaml", UriKind.Relative));
        new OfficialCatalogWindow(new PublicSheet(1, "공공 메뉴 자료 · 3~5세 · 수치 기준 자동 적용")).ShowDialog();
    }
}
