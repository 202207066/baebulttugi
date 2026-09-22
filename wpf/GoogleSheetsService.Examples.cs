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

    private async Task RemoveCopiedExamplesAsync(IProgress<string> progress)
    {
        if (!HasUserDatabase || SpreadsheetId==AppConfig.TemplateSpreadsheetId || SpreadsheetId==AppConfig.SpreadsheetId)
            throw new InvalidOperationException("원본 메뉴 자료는 초기화할 수 없습니다.");
        var get=_driveService.Files.Get(SpreadsheetId); get.Fields="appProperties";
        var metadata=await get.ExecuteAsync();
        var props=metadata.AppProperties ?? new Dictionary<string,string>();
        if (props.TryGetValue("empty-meal-start",out var version) && version=="2") return;
        progress.Report("복사된 예시 식단을 확인하는 중입니다. 직접 작성한 식단은 유지합니다…");
        if ((await GetSheetTitlesAsync()).Contains(AppConfig.WeeklyMenuSheetName))
        {
            string range=QuoteTitle(AppConfig.WeeklyMenuSheetName);
            var original=(await _sheetsService.Spreadsheets.Values.Get(AppConfig.TemplateSpreadsheetId,range).ExecuteAsync()).Values ?? new List<IList<object>>();
            var personal=await GetValuesAsync(range);
            var copied=FindCopiedExampleRows(personal,original);
            if(copied.Count>0)
            {
                await _sheetsService.Spreadsheets.Values.BatchClear(new BatchClearValuesRequest { Ranges=copied.Select(r=>$"{range}!{r}:{r}").ToList() },SpreadsheetId).ExecuteAsync();
            }
        }
        props["empty-meal-start"]="2";
        await _driveService.Files.Update(new Google.Apis.Drive.v3.Data.File { AppProperties=props },SpreadsheetId).ExecuteAsync();
    }
}
