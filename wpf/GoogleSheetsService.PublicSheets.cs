namespace wpf;
public record PublicSheet(int Id,string Title)
{
    public static string Url(string spreadsheetId,int sheetId) => "https://docs.google.com/spreadsheets/d/"+Uri.EscapeDataString(spreadsheetId)+"/edit#gid="+sheetId;
}
public partial class GoogleSheetsService
{
    public Task<IReadOnlyList<PublicSheet>> GetPublicSheetsAsync()=>Task.FromResult<IReadOnlyList<PublicSheet>>([
        new(1,"트랙 A · 3~5세 메뉴·영양"),new(2,"트랙 B · 6~18세 메뉴·영양"),
        new(3,"트랙 A · 식재료·1인분량"),new(4,"트랙 B · 식재료·1인분량"),new(5,"조리방법")]);
}
