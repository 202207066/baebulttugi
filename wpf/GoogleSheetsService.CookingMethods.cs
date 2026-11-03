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
    public Task<Dictionary<string,string>> GetCookingMethodsAsync() =>
        Task.FromResult(new Dictionary<string,string>(OfficialCatalog.Current.Cooking));
}
