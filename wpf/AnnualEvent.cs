namespace wpf;

// Store the recurrence rule with the source event; never append a row for every year.
// A reserved prefix keeps existing two-column event sheets compatible.
public record AnnualEvent(int RowIndex, DateTime StartDate, string Content, bool RepeatsYearly)
{
    private const string Prefix = "[매년반복] ";
    public bool OccursOn(DateTime date) => date.Date >= StartDate.Date &&
        (RepeatsYearly ? date.Month == StartDate.Month && date.Day == StartDate.Day : date.Date == StartDate.Date);
    public string DisplayContent => Content + (RepeatsYearly ? " · 매년 반복" : "");
    public static string Encode(string content, bool yearly) => (yearly ? Prefix : "") + content;
    public static List<AnnualEvent> Parse(IList<IList<object>> rows)
    {
        var result = new List<AnnualEvent>();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Count < 2 || !DateTime.TryParse(row[0]?.ToString(), out var date)) continue;
            string content = row[1]?.ToString() ?? "";
            bool yearly = content.StartsWith(Prefix, StringComparison.Ordinal);
            if (yearly) content = content[Prefix.Length..];
            if (!string.IsNullOrWhiteSpace(content)) result.Add(new(i + 1, date.Date, content, yearly));
        }
        return result;
    }
    public static DateTime EditedDate(AnnualEvent original, DateTime selected, bool yearly) =>
        yearly ? original.StartDate : selected.Date;
}
