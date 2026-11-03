using Google.Apis.Sheets.v4.Data;
using System.Text.RegularExpressions;

namespace wpf;

public partial class GoogleSheetsService
{
    // Only exact copies of the source's undated example meals qualify.
    // Real dates, edited dishes, extra metadata, and unknown layouts are retained.
    public static List<int> FindCopiedExampleRows(IList<IList<object>> personal, IList<IList<object>> source)
    {
        bool Header(IList<IList<object>> rows) => rows.Count > 0 && Cell(rows[0],0)=="식단 ID" && Cell(rows[0],1)=="날짜";
        if (!Header(personal) || !Header(source)) return [];
        bool Example(IList<object> row) => Regex.IsMatch(Cell(row,0),@"^K\d{3}$") && Regex.IsMatch(Cell(row,1),@"^Day\s+\d+$",RegexOptions.IgnoreCase)
            && row.Count>=9 && row.Skip(9).All(c=>string.IsNullOrWhiteSpace(c?.ToString()));
        var examples=source.Skip(1).Where(Example).ToList();
        return personal.Select((r,i)=>(r,i)).Skip(1).Where(x=>Example(x.r) && examples.Any(sample=>Enumerable.Range(0,9).All(c=>Cell(sample,c)==Cell(x.r,c))))
            .Select(x=>x.i+1).ToList();
    }

}
