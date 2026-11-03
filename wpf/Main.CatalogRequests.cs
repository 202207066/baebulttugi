using System.Windows;
namespace wpf;
public partial class Main
{
    private void BtnCatalogRequests_Click(object sender,RoutedEventArgs e) => OpenCatalogRequests(false);
    private void BtnCatalogAdmin_Click(object sender,RoutedEventArgs e) => OpenCatalogRequests(true);
    private void OpenCatalogRequests(bool admin)
    {
        _navigationVersion++;
        var service=_sheetsService.FacilityRegistry;
        (admin?BtnCatalogAdmin:BtnCatalogRequests).IsChecked=true;
        SetHeader(admin?"관리자 요청함":"메뉴·식재료 요청",admin?"공식 자료 반영 요청을 검토합니다":"개인 자료는 유지하고 공식 자료 개선을 제안합니다");
        MainFrame.Navigate(new CatalogRequestsPage(admin,new CatalogRequestsClient(service),service.CurrentUser?.Email??""));
    }
}
