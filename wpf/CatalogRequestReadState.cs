using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace wpf;

public static class CatalogRequestReadState
{
    public static event EventHandler? Changed;
    internal static string DirectoryPath = Path.Combine(AppConfig.UserDataDirectory, "RequestReadState");
    private static string FilePath(string account, bool admin) => Path.Combine(DirectoryPath,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(account.Trim().ToLowerInvariant()))) + (admin ? "-admin.json" : "-mine.json"));
    private static string Version(CatalogRequestItem row) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes($"{row.Revision}|{row.UpdatedAt}|{row.Status}|{row.AdminNote}")));
    private static Dictionary<string,string> Read(string account, bool admin)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(FilePath(account,admin))) ?? new(); }
        catch { return new(); }
    }
    public static int CountUnread(string account, bool admin, IEnumerable<CatalogRequestItem> rows)
    {
        var seen = Read(account,admin);
        return rows.Where(row => admin || row.Revision > 1 || !string.IsNullOrWhiteSpace(row.AdminNote) || row.Status != "접수")
            .DistinctBy(row => row.Id).Count(row => !seen.TryGetValue(row.Id,out var value) || value != Version(row));
    }
    public static void MarkRead(string account, bool admin, CatalogRequestItem row)
    {
        if (string.IsNullOrWhiteSpace(account) || string.IsNullOrWhiteSpace(row.Id)) return;
        var seen = Read(account,admin);var version=Version(row);
        if(seen.TryGetValue(row.Id,out var prior) && prior==version)return;
        seen[row.Id]=version;
        try {
            Directory.CreateDirectory(DirectoryPath);var path=FilePath(account,admin);
            File.WriteAllText(path+".tmp",JsonSerializer.Serialize(seen));File.Move(path+".tmp",path,true);
            Changed?.Invoke(null,EventArgs.Empty);
        } catch(IOException) { } catch(UnauthorizedAccessException) { }
    }
}
