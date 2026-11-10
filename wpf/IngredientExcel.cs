using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;
namespace wpf;

public static class IngredientExcel
{
    public static List<CostDraft> Read(string path)
    {
        if (!path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || new FileInfo(path).Length > 20 * 1024 * 1024) throw new InvalidOperationException("20MB 이하의 .xlsx 재료표를 선택해 주세요.");
        using var archive = ZipFile.OpenRead(path);
        if (archive.Entries.Count > 200 || archive.Entries.Sum(e => e.Length) > 60 * 1024 * 1024) throw new InvalidOperationException("재료표 파일의 크기·구조를 확인해 주세요.");
        XDocument Xml(string entry)
        {
            using var stream = (archive.GetEntry(entry) ?? throw new InvalidOperationException("엑셀 구성 파일이 없습니다: " + entry)).Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 40 * 1024 * 1024 });
            return XDocument.Load(reader);
        }
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var workbook = Xml("xl/workbook.xml");
        var sheet = workbook.Descendants(ns + "sheet").SingleOrDefault(s => (string?)s.Attribute("name") == "재료기준표") ?? throw new InvalidOperationException("'재료기준표' 시트가 필요합니다. 앱에서 양식을 저장해 사용해 주세요.");
        string relation = (string?)sheet.Attribute(rel + "id") ?? "";
        var link = Xml("xl/_rels/workbook.xml.rels").Root!.Elements().SingleOrDefault(e => (string?)e.Attribute("Id") == relation);
        string target = (string?)link?.Attribute("Target") ?? "";
        if ((string?)link?.Attribute("TargetMode") == "External" || target.Contains("..") || target.Contains(':')) throw new InvalidOperationException("외부 연결 엑셀은 가져올 수 없습니다.");
        target = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        if (!target.StartsWith("xl/worksheets/") || !target.EndsWith(".xml")) throw new InvalidOperationException("재료표 시트 경로를 확인할 수 없습니다.");
        var shared = archive.GetEntry("xl/sharedStrings.xml") == null ? [] : Xml("xl/sharedStrings.xml").Descendants(ns + "si").Select(x => string.Concat(x.Descendants(ns + "t").Select(t => t.Value))).ToArray();
        string Value(XElement cell)
        {
            if (cell.Element(ns + "f") != null) throw new InvalidOperationException("재료 기준값에 수식이 있습니다. 값으로 붙여 넣은 뒤 다시 불러와 주세요.");
            var value = cell.Element(ns + "v")?.Value ?? "";
            return (string?)cell.Attribute("t") switch { "s" => int.TryParse(value, out int i) && i >= 0 && i < shared.Length ? shared[i] : throw new InvalidOperationException("엑셀 문자열 참조 오류"), "inlineStr" => string.Concat(cell.Descendants(ns + "t").Select(t => t.Value)), "e" => throw new InvalidOperationException("엑셀에 오류 셀이 있습니다."), _ => value };
        }
        var table = new List<string[]>();
        foreach (var row in Xml(target).Descendants(ns + "row"))
        {
            if (table.Count >= 20001) throw new InvalidOperationException("재료표는 최대 20,000행까지 가져올 수 있습니다.");
            var values = new string[7]; Array.Fill(values, "");
            foreach (var cell in row.Elements(ns + "c"))
            {
                string reference = (string?)cell.Attribute("r") ?? ""; int column = 0;
                foreach (char ch in reference.TakeWhile(char.IsLetter)) column = checked(column * 26 + char.ToUpperInvariant(ch) - 'A' + 1);
                if (column is >= 1 and <= 7) values[column - 1] = Value(cell).Trim();
            }
            if (values.Any(v => v.Length > 0)) table.Add(values);
        }
        if (table.Count == 0 || !table[0].SequenceEqual(IngredientStandards.Headers)) throw new InvalidOperationException("재료표 첫 행의 열 이름·순서가 양식과 다릅니다.");
        return IngredientStandards.Validate(table.Skip(1).Where(r => r[1].Length > 0 || r[4].Length > 0 || r[5].Length > 0).Select(r => new CostDraft { Name = r[0], Price = r[1], PriceQuantity = r[2], PriceUnit = r[3], WastePercent = r[4], PackageWeightGrams = r[5], Source = r[6] }));
    }
}
