namespace wpf;
public record PublicSheet(int Id,string Title)
{
    public static string Url(string spreadsheetId,int sheetId) =>
        "https://docs.google.com/spreadsheets/d/"+Uri.EscapeDataString(spreadsheetId)+"/edit#gid="+sheetId.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
public partial class GoogleSheetsService
{
    public async Task<IReadOnlyList<PublicSheet>> GetPublicSheetsAsync()
    {
        var request=Sheets.Spreadsheets.Get(AppConfig.TemplateSpreadsheetId);
        request.Fields="sheets(properties(sheetId,title,hidden))";
        var book=await request.ExecuteAsync().ConfigureAwait(false);
        return book.Sheets?.Where(sheet=>sheet.Properties?.SheetId is >=0 && sheet.Properties.Hidden!=true)
            .Select(sheet=>new PublicSheet(sheet.Properties.SheetId!.Value,sheet.Properties.Title??"이름 없는 시트")).ToArray() ?? [];
    }
}
