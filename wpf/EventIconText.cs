using System.Globalization;
using System.Text;
namespace wpf;
public static class EventIconText
{
    public static string LeadingIcon(string text)
    {
        text=text.TrimStart();
        if(text.Length==0) return "";
        string element=StringInfo.GetNextTextElement(text);
        return element.EnumerateRunes().Any(r => r.Value >= 0x1F000 || r.Value is >= 0x2600 and <= 0x27FF || r.Value == 0x20E3) ? element : "";
    }
    public static string Compose(string icon, string text)
    {
        text=text.Trim();
        if(LeadingIcon(text).Length>0) return text;
        string chosen=LeadingIcon(icon);
        return $"{(chosen.Length>0 ? chosen : "📌")} {text}";
    }
}
public static class DashboardAgeFilter
{
    public static bool Matches(string age, string selected) => selected switch {
        "3-5" => age=="3-5",
        "6-18" => age is "6-18" or "6-11" or "12-18" or "6-8" or "9-11" or "12-14" or "15-18",
        _ => string.IsNullOrWhiteSpace(age)
    };
}
