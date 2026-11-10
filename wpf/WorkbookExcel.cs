using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace wpf;

public sealed record WorkbookTable(string Title, IList<IList<object>> Rows);

/// <summary>Portable, value-only XLSX for the app's personal tables; no macros, formulas or external links are executed.</summary>
public static class WorkbookExcel
{
    private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Types = "http://schemas.openxmlformats.org/package/2006/content-types";
    private static string Column(int index) { string result = ""; for (int n = index + 1; n > 0; n = (n - 1) / 26) result = (char)('A' + (n - 1) % 26) + result; return result; }
    public static void Write(string path, IReadOnlyList<WorkbookTable> tables)
    {
        if (tables.Count is < 1 or > 100) throw new InvalidOperationException("내보낼 자료 탭 수를 확인해 주세요.");
        if (tables.Any(t => t.Title.Length > 31 || t.Title.IndexOfAny(['[',']',':','*','?','/','\\']) >= 0) || tables.Select(t => t.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tables.Count) throw new InvalidOperationException("엑셀 탭 이름은 중복 없이 31자 이하로 설정해 주세요.");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
            {
                void Add(string name, XDocument xml) { using var output = zip.CreateEntry(name, CompressionLevel.Optimal).Open(); xml.Save(output); }
                var contentTypes = new XElement(Types + "Types",
                    new XElement(Types + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(Types + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(Types + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")));
                contentTypes.Add(tables.Select((_, i) => new XElement(Types + "Override", new XAttribute("PartName", $"/xl/worksheets/sheet{i + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
                Add("[Content_Types].xml", new(contentTypes));
                Add("_rels/.rels", new(new XElement(Pkg + "Relationships", new XElement(Pkg + "Relationship", new XAttribute("Id", "book"), new XAttribute("Type", Rel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml")))));
                Add("xl/workbook.xml", new(new XElement(Ns + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel), new XElement(Ns + "sheets", tables.Select((t, i) => new XElement(Ns + "sheet", new XAttribute("name", t.Title), new XAttribute("sheetId", i + 1), new XAttribute(Rel + "id", "sheet" + (i + 1))))))));
                Add("xl/_rels/workbook.xml.rels", new(new XElement(Pkg + "Relationships", tables.Select((_, i) => new XElement(Pkg + "Relationship", new XAttribute("Id", "sheet" + (i + 1)), new XAttribute("Type", Rel.NamespaceName + "/worksheet"), new XAttribute("Target", $"worksheets/sheet{i + 1}.xml"))))));
                for (int i = 0; i < tables.Count; i++)
                {
                    var table = tables[i];
                    XElement Cell(object value, int row, int col)
                    {
                        var cell = new XElement(Ns + "c", new XAttribute("r", Column(col) + (row + 1)));
                        if (value is bool b) { cell.Add(new XAttribute("t", "b"), new XElement(Ns + "v", b ? "1" : "0")); }
                        else if (value is byte or short or int or long or float or double or decimal) cell.Add(new XElement(Ns + "v", Convert.ToString(value, CultureInfo.InvariantCulture)));
                        else
                        {
                            string text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
                            if (text.Length > 32767) throw new InvalidOperationException($"{table.Title}: 엑셀 셀의 글자 수를 초과합니다. 전체 백업을 이용해 주세요.");
                            cell.Add(new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
                        }
                        return cell;
                    }
                    Add($"xl/worksheets/sheet{i + 1}.xml", new(new XElement(Ns + "worksheet", new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", 0), new XElement(Ns + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))), new XElement(Ns + "sheetData", table.Rows.Select((r, ri) => new XElement(Ns + "row", new XAttribute("r", ri + 1), r.Select((v, ci) => Cell(v, ri, ci))))))));
                }
            }
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static IReadOnlyList<WorkbookTable> Read(string path)
    {
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length > 50L * 1024 * 1024) throw new InvalidOperationException("50MB 이하 .xlsx 파일을 선택해 주세요.");
        using var zip = ZipFile.OpenRead(path);
        if (zip.Entries.Count > 1000 || zip.Entries.Sum(e => e.Length) > 200L * 1024 * 1024) throw new InvalidOperationException("엑셀 파일의 크기와 구성을 확인해 주세요.");
        XDocument Xml(string name)
        {
            using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException("엑셀 구성 파일이 없습니다: " + name)).Open();
            using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 100L * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        var book = Xml("xl/workbook.xml"); var links = Xml("xl/_rels/workbook.xml.rels");
        var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? [] : Xml("xl/sharedStrings.xml").Descendants(Ns + "si").Select(s => string.Concat(s.Descendants(Ns + "t").Select(t => t.Value))).ToArray();
        var dateStyles = new HashSet<int>();
        if (zip.GetEntry("xl/styles.xml") != null)
        {
            var styles = Xml("xl/styles.xml");
            var formats = styles.Descendants(Ns + "numFmt").ToDictionary(x => (int)x.Attribute("numFmtId")!, x => (string?)x.Attribute("formatCode") ?? "");
            int index = 0;
            foreach (var xf in styles.Root?.Element(Ns + "cellXfs")?.Elements(Ns + "xf") ?? [])
            {
                int format = (int?)xf.Attribute("numFmtId") ?? 0;
                string code = Regex.Replace(formats.GetValueOrDefault(format, ""), "\"[^\"]*\"|\\[[^\\]]*\\]|\\\\.", "");
                if (format is >= 14 and <= 17 or 22 || Regex.IsMatch(code, "[yYdD]")) dateStyles.Add(index);
                index++;
            }
        }
        bool date1904 = (string?)book.Root?.Element(Ns + "workbookPr")?.Attribute("date1904") is "1" or "true";
        var tables = new List<WorkbookTable>(); int totalCells = 0;
        foreach (var sheet in book.Descendants(Ns + "sheet"))
        {
            if (tables.Count >= 100) throw new InvalidOperationException("엑셀은 최대 100개 탭까지 가져올 수 있습니다.");
            string title = (string?)sheet.Attribute("name") ?? throw new InvalidDataException("탭 이름이 없습니다.");
            string rel = (string?)sheet.Attribute(Rel + "id") ?? "";
            var link = links.Root!.Elements().SingleOrDefault(x => (string?)x.Attribute("Id") == rel);
            string target = (string?)link?.Attribute("Target") ?? "";
            if ((string?)link?.Attribute("TargetMode") == "External" || target.Contains("..") || target.Contains(':') || target.Contains('\\')) throw new InvalidDataException("외부 연결 자료는 가져올 수 없습니다.");
            target = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
            if (!target.StartsWith("xl/worksheets/", StringComparison.Ordinal) || !target.EndsWith(".xml", StringComparison.Ordinal)) throw new InvalidDataException("엑셀 탭 경로가 올바르지 않습니다.");
            var rows = new List<IList<object>>(); var seen = new HashSet<string>();
            foreach (var cell in Xml(target).Descendants(Ns + "sheetData").Elements(Ns + "row").Elements(Ns + "c"))
            {
                string address = (string?)cell.Attribute("r") ?? "";
                var match = Regex.Match(address, "^([A-Z]+)([1-9][0-9]*)$");
                if (!match.Success || !seen.Add(address)) throw new InvalidDataException("셀 주소가 올바르지 않습니다.");
                int col = 0; foreach (char ch in match.Groups[1].Value) col = checked(col * 26 + ch - 'A' + 1);
                int row = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
                if (row > 200000 || col > 512 || ++totalCells > 2000000) throw new InvalidDataException("가져올 자료 범위가 너무 큽니다.");
                string value = cell.Element(Ns + "v")?.Value ?? "", type = (string?)cell.Attribute("t") ?? "";
                if (cell.Element(Ns + "f") != null && cell.Element(Ns + "v") == null) throw new InvalidDataException($"{title}!{address}: 계산된 값이 없는 수식입니다. 엑셀에서 값으로 붙여넣은 파일을 사용해 주세요.");
                object parsed = type switch
                {
                    "s" => int.TryParse(value, out int si) && si >= 0 && si < strings.Length ? strings[si] : throw new InvalidDataException("엑셀 문자열 참조 오류"),
                    "inlineStr" => string.Concat(cell.Descendants(Ns + "t").Select(t => t.Value)),
                    "b" => value == "1", "e" => throw new InvalidDataException($"{title}!{address}: 오류 셀이 있습니다."),
                    "d" => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd") : value,
                    "str" => value,
                    _ => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) && double.IsFinite(n) ? n : value
                };
                if (parsed is double number && dateStyles.Contains((int?)cell.Attribute("s") ?? 0)) parsed = DateTime.FromOADate(number + (date1904 ? 1462 : 0)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                while (rows.Count < row) rows.Add(new List<object>());
                while (rows[row - 1].Count < col) rows[row - 1].Add("");
                rows[row - 1][col - 1] = parsed;
            }
            tables.Add(new(title, rows));
        }
        if (tables.Count == 0 || tables.Select(t => t.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() != tables.Count) throw new InvalidDataException("엑셀 탭 구성을 확인해 주세요.");
        return tables;
    }
}
