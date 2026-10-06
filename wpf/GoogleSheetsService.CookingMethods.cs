using System.Text.RegularExpressions;
namespace wpf;
public static class CookingMethods
{
    public static string Key(string name) => Regex.Replace(name.Normalize(), @"\s+", "");
    public static Dictionary<string,string> Parse(IList<IList<object>> rows)
    {
        if(rows.Count==0)throw new InvalidOperationException("조리방법 시트가 비어 있습니다.");
        var header=rows[0].Select(x=>x?.ToString()?.Trim()??"").ToList();
        int name=header.IndexOf("메뉴명"),method=header.IndexOf("조리방법");
        if(name<0||method<0)throw new InvalidOperationException("조리방법 시트의 메뉴명·조리방법 열을 확인해 주세요.");
        string Value(IList<object> row,int i)=>i<row.Count?row[i]?.ToString()?.Trim()??"":"";
        return rows.Skip(1).Where(r=>Value(r,name).Length>0&&Value(r,method).Length>0).GroupBy(r=>Key(Value(r,name)))
            .ToDictionary(g=>g.Key,g=>string.Join("\n\n",g.Select(r=>Value(r,method)).Distinct()));
    }
}
public partial class GoogleSheetsService
{
    public const string CookingSourceId="1r6YjkX57pqOaJV0DMiZYOZY_L1nqdF0qTXJLXu7gDN0";
    public async Task<Dictionary<string,string>> GetCookingMethodsAsync()
    {
        var request=_sheetsService.Spreadsheets.Get(CookingSourceId);request.Fields="sheets(properties(title))";
        var book=await request.ExecuteAsync();
        var titles=book.Sheets.Select(s=>s.Properties.Title).ToList();
        string title=titles.Contains("조리방법")?"조리방법":titles.Contains("cooking_methods_columns")?"cooking_methods_columns":throw new InvalidOperationException("지정한 자료에서 조리방법 시트를 찾을 수 없습니다.");
        var values=await _sheetsService.Spreadsheets.Values.Get(CookingSourceId,QuoteTitle(title)+"!A:B").ExecuteAsync();
        return CookingMethods.Parse(values.Values??new List<IList<object>>());
    }
}
