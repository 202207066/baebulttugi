using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json;

namespace wpf;

/// <summary>Local edition persistence. All changes in a batch commit together, with a previous-file backup.</summary>
public sealed class LocalWorkbookStore : IDisposable
{
    public const string RegistryId = "local-registry";
    private const int MaxRows = 200000, MaxColumns = 512;
    private readonly object _gate = new();
    private readonly FileStream _lease;
    private Document _document;
    private bool _disposed;
    public string FilePath { get; }
    public sealed class Document
    {
        public int Version { get; set; } = 1;
        public Dictionary<string, Book> Books { get; set; } = new(StringComparer.Ordinal);
    }
    public sealed class Book
    {
        public string Title { get; set; } = "";
        public List<Tab> Tabs { get; set; } = [];
    }
    public sealed class Tab
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int RowCount { get; set; } = 1000;
        public int ColumnCount { get; set; } = 26;
        public List<List<ExtendedValue>> Rows { get; set; } = [];
    }

    public LocalWorkbookStore(string path)
    {
        FilePath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        try { _lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        catch (IOException ex) { throw new InvalidOperationException("로컬 자료를 다른 배불뚝이 창에서 사용 중입니다. 먼저 열린 로컬 버전을 닫고 다시 실행해 주세요.", ex); }
        try
        {
            if (File.Exists(FilePath))
            {
                if (new FileInfo(FilePath).Length > 256L * 1024 * 1024) throw new InvalidDataException("자료 파일이 너무 큽니다.");
                _document = JsonConvert.DeserializeObject<Document>(File.ReadAllText(FilePath, Encoding.UTF8)) ?? throw new InvalidDataException("자료 파일이 비어 있습니다.");
                Validate(_document);
            }
            else
            {
                if (File.Exists(FilePath + ".bak")) throw new InvalidDataException("원본 파일은 없지만 이전 저장본이 남아 있습니다. 백업을 확인해 주세요.");
                _document = new Document();
                _document.Books.Add(RegistryId, new Book { Title = "배불뚝이 · 이 PC 자료", Tabs = [new Tab { Id = 0, Title = "사용 안내", Rows = [[Text("배불뚝이 로컬 자료")], [Text("대상자·식단·레시피·원가는 이 PC에 저장됩니다. 내 자료에서 엑셀 내보내기 또는 전체 백업을 이용하세요.")]] }] });
                Save(_document);
            }
        }
        catch (Exception ex)
        {
            _lease.Dispose();
            throw new InvalidOperationException("로컬 자료를 열지 못했습니다. 기존 파일은 초기화하지 않았습니다.\n" + FilePath + "\n" + ex.Message, ex);
        }
    }

    private static ExtendedValue Text(string s) => new() { StringValue = s };
    private static T Clone<T>(T value) => JsonConvert.DeserializeObject<T>(JsonConvert.SerializeObject(value))!;
    private static bool Empty(ExtendedValue? v) => v == null || (v.NumberValue == null && v.BoolValue == null && string.IsNullOrEmpty(v.StringValue) && string.IsNullOrEmpty(v.FormulaValue));
    private static object Value(ExtendedValue v) => v.NumberValue is double n ? n : v.BoolValue is bool b ? b : (object?)v.FormulaValue ?? v.StringValue ?? "";
    private static ExtendedValue FromValue(object? v) => v switch
    {
        null => Text(""), bool b => new() { BoolValue = b },
        byte or short or int or long or float or double or decimal => new() { NumberValue = Convert.ToDouble(v, CultureInfo.InvariantCulture) },
        _ => Text(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "")
    };
    private static void Validate(Document doc)
    {
        if (doc.Version != 1 || doc.Books == null || !doc.Books.ContainsKey(RegistryId)) throw new InvalidDataException("지원하지 않는 로컬 자료 형식입니다.");
        foreach (var book in doc.Books.Values)
        {
            if (book == null || book.Tabs == null || book.Tabs.Count == 0 || book.Tabs.Count > 200 || book.Tabs.Select(t => t.Id).Distinct().Count() != book.Tabs.Count || book.Tabs.Select(t => t.Title).Distinct(StringComparer.OrdinalIgnoreCase).Count() != book.Tabs.Count) throw new InvalidDataException("자료 탭 구성이 올바르지 않습니다.");
            foreach (var tab in book.Tabs)
            {
                if (string.IsNullOrWhiteSpace(tab.Title) || tab.RowCount < 1 || tab.RowCount > MaxRows || tab.ColumnCount < 1 || tab.ColumnCount > MaxColumns || tab.Rows == null || tab.Rows.Count > tab.RowCount || tab.Rows.Any(r => r == null || r.Count > tab.ColumnCount || r.Any(v => v == null || v.NumberValue is double d && !double.IsFinite(d)))) throw new InvalidDataException("자료의 행·열 또는 숫자를 확인해 주세요.");
            }
        }
    }
    private void Save(Document next)
    {
        Validate(next);
        string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(fs, new UTF8Encoding(false), 65536, leaveOpen: true)) { writer.Write(JsonConvert.SerializeObject(next)); writer.Flush(); }
                fs.Flush(true);
            }
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private T Write<T>(Func<Document, T> edit)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var next = Clone(_document);
            T result = edit(next);
            Save(next);
            _document = next;
            return result;
        }
    }
    private static Book Find(Document doc, string id) => doc.Books.TryGetValue(id, out var b) ? b : throw new InvalidOperationException("급식소 자료를 찾을 수 없습니다. 급식소를 다시 선택해 주세요.");
    private static Tab Find(Book book, int? id) => book.Tabs.SingleOrDefault(t => t.Id == id) ?? throw new InvalidOperationException("자료 탭을 찾을 수 없습니다.");
    private static Sheet Metadata(Tab t) => new() { Properties = new() { SheetId = t.Id, Title = t.Title, GridProperties = new() { RowCount = t.RowCount, ColumnCount = t.ColumnCount } } };
    public Spreadsheet ReadBook(string id)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var book = Find(_document, id);
            return new() { SpreadsheetId = id, Properties = new() { Title = book.Title }, Sheets = book.Tabs.Select(Metadata).ToList() };
        }
    }
    public Spreadsheet CreateBook(Spreadsheet input) => Write(doc =>
    {
        string id = "local-" + Guid.NewGuid().ToString("N");
        var book = new Book { Title = input.Properties?.Title ?? "내 자료" };
        foreach (var sheet in input.Sheets ?? [])
        {
            var tab = AddTab(book, sheet.Properties);
            foreach (var grid in sheet.Data ?? []) Put(tab, grid.StartRow ?? 0, grid.StartColumn ?? 0, grid.RowData ?? []);
        }
        if (book.Tabs.Count == 0) AddTab(book, new() { Title = "자료" });
        doc.Books.Add(id, book);
        return new Spreadsheet { SpreadsheetId = id, Properties = new() { Title = book.Title }, Sheets = book.Tabs.Select(Metadata).ToList() };
    });
    private static Tab AddTab(Book b, SheetProperties? p)
    {
        string title = p?.Title ?? "자료";
        if (string.IsNullOrWhiteSpace(title) || b.Tabs.Any(t => string.Equals(t.Title, title, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("자료 탭 이름이 비어 있거나 중복됩니다.");
        int id = p?.SheetId ?? (b.Tabs.Count == 0 ? 0 : b.Tabs.Max(t => t.Id) + 1);
        if (b.Tabs.Any(t => t.Id == id)) throw new InvalidOperationException("자료 탭 ID가 중복됩니다.");
        var tab = new Tab { Id = id, Title = title, RowCount = p?.GridProperties?.RowCount ?? 1000, ColumnCount = p?.GridProperties?.ColumnCount ?? 26 };
        b.Tabs.Add(tab);
        return tab;
    }
    private static void Bounds(int row, int col, int height, int width)
    {
        if (row < 0 || col < 0 || height < 0 || width < 0 || (long)row + height > MaxRows || (long)col + width > MaxColumns) throw new InvalidOperationException("로컬 자료의 행·열 범위를 벗어났습니다.");
    }
    private static void Put(Tab tab, int row, int col, IList<RowData> rows)
    {
        int width = rows.Select(r => r.Values?.Count ?? 0).DefaultIfEmpty().Max();
        Bounds(row, col, rows.Count, width);
        tab.RowCount = Math.Max(tab.RowCount, row + rows.Count);
        tab.ColumnCount = Math.Max(tab.ColumnCount, col + width);
        while (tab.Rows.Count < row + rows.Count) tab.Rows.Add([]);
        for (int r = 0; r < rows.Count; r++)
        {
            var target = tab.Rows[row + r]; var cells = rows[r].Values ?? [];
            while (target.Count < col + cells.Count) target.Add(Text(""));
            for (int c = 0; c < cells.Count; c++) target[col + c] = Clone(cells[c].UserEnteredValue ?? Text(""));
        }
        Trim(tab);
    }
    private static void Trim(Tab tab)
    {
        foreach (var row in tab.Rows) while (row.Count > 0 && Empty(row[^1])) row.RemoveAt(row.Count - 1);
        while (tab.Rows.Count > 0 && tab.Rows[^1].Count == 0) tab.Rows.RemoveAt(tab.Rows.Count - 1);
    }
    private sealed record Area(Tab Tab, int Row, int Col, int EndRow, int EndCol);
    private static Area Parse(Book book, string range)
    {
        int split = range.LastIndexOf('!');
        string title = split < 0 ? range : range[..split];
        if (title.StartsWith("'") && title.EndsWith("'")) title = title[1..^1].Replace("''", "'");
        var tab = book.Tabs.SingleOrDefault(t => t.Title == title) ?? throw new InvalidOperationException("자료 탭을 찾을 수 없습니다: " + title);
        if (split < 0) return new(tab, 0, 0, tab.RowCount, tab.ColumnCount);
        var parts = range[(split + 1)..].Replace("$", "").Split(':');
        if (parts.Length > 2) throw new InvalidOperationException("잘못된 자료 범위입니다.");
        (int? Row, int? Col) Address(string value)
        {
            var m = Regex.Match(value, "^([A-Za-z]*)([1-9][0-9]*)?$");
            if (!m.Success || value.Length == 0) throw new InvalidOperationException("잘못된 셀 주소입니다.");
            int? col = null;
            if (m.Groups[1].Length > 0) { int c = 0; foreach (char letter in m.Groups[1].Value.ToUpperInvariant()) c = checked(c * 26 + letter - 'A' + 1); col = c - 1; }
            return (m.Groups[2].Success ? int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) - 1 : null, col);
        }
        var first = Address(parts[0]); var last = Address(parts[^1]);
        int r = first.Row ?? 0, c = first.Col ?? 0, er = last.Row + 1 ?? tab.RowCount, ec = last.Col + 1 ?? tab.ColumnCount;
        Bounds(r, c, er - r, ec - c);
        return new(tab, r, c, er, ec);
    }
    public IList<IList<object>> ReadValues(string id, string range)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var a = Parse(Find(_document, id), range);
            var result = new List<IList<object>>();
            for (int r = a.Row; r < Math.Min(a.EndRow, a.Tab.Rows.Count); r++)
            {
                var cells = a.Tab.Rows[r]; int end = Math.Min(a.EndCol, cells.Count);
                while (end > a.Col && Empty(cells[end - 1])) end--;
                result.Add(cells.Skip(a.Col).Take(Math.Max(0, end - a.Col)).Select(Value).ToList());
            }
            while (result.Count > 0 && result[^1].Count == 0) result.RemoveAt(result.Count - 1);
            return result;
        }
    }
    public void UpdateValues(string id, string range, IList<IList<object>> rows, bool append = false) => Write(doc =>
    {
        var a = Parse(Find(doc, id), range);
        int row = append ? Math.Max(a.Row, a.Tab.Rows.Count) : a.Row;
        Put(a.Tab, row, a.Col, rows.Select(r => new RowData { Values = r.Select(v => new CellData { UserEnteredValue = FromValue(v) }).ToList() }).ToList());
        return true;
    });
    private static void Clear(Area a)
    {
        for (int r = a.Row; r < Math.Min(a.EndRow, a.Tab.Rows.Count); r++)
            for (int c = a.Col; c < Math.Min(a.EndCol, a.Tab.Rows[r].Count); c++) a.Tab.Rows[r][c] = Text("");
        Trim(a.Tab);
    }
    public void ClearValues(string id, IEnumerable<string> ranges) => Write(doc =>
    {
        var b = Find(doc, id); foreach (var range in ranges) Clear(Parse(b, range)); return true;
    });
    public void Apply(string id, BatchUpdateSpreadsheetRequest batch) => Write(doc =>
    {
        var book = Find(doc, id);
        foreach (var request in batch.Requests ?? [])
        {
            if (request.AddSheet is { } add) AddTab(book, add.Properties);
            else if (request.AppendDimension is { } dim)
            {
                var tab = Find(book, dim.SheetId); int length = dim.Length ?? 0;
                if (length < 0) throw new InvalidOperationException("잘못된 추가 행·열 수입니다.");
                if (dim.Dimension == "ROWS") tab.RowCount = checked(tab.RowCount + length);
                else if (dim.Dimension == "COLUMNS") tab.ColumnCount = checked(tab.ColumnCount + length);
                else throw new InvalidOperationException("지원하지 않는 자료 방향입니다.");
            }
            else if (request.UpdateCells is { } update)
            {
                if (Convert.ToString(update.Fields, CultureInfo.InvariantCulture) != "userEnteredValue") throw new InvalidOperationException("지원하지 않는 셀 수정입니다.");
                var tab = Find(book, update.Start?.SheetId ?? update.Range?.SheetId);
                int r = update.Start?.RowIndex ?? update.Range?.StartRowIndex ?? 0, c = update.Start?.ColumnIndex ?? update.Range?.StartColumnIndex ?? 0;
                if (update.Range is { } area)
                {
                    int er = area.EndRowIndex ?? tab.RowCount, ec = area.EndColumnIndex ?? tab.ColumnCount;
                    Bounds(r, c, er - r, ec - c);
                    if ((update.Rows?.Count ?? 0) > er - r || (update.Rows ?? []).Any(row => (row.Values?.Count ?? 0) > ec - c)) throw new InvalidOperationException("셀 값이 지정 범위를 벗어났습니다.");
                    Clear(new(tab, r, c, er, ec));
                }
                Put(tab, r, c, update.Rows ?? []);
            }
            else if (request.AppendCells is { } append)
            {
                if (Convert.ToString(append.Fields, CultureInfo.InvariantCulture) != "userEnteredValue") throw new InvalidOperationException("지원하지 않는 셀 추가입니다.");
                var tab = Find(book, append.SheetId); Put(tab, tab.Rows.Count, 0, append.Rows ?? []);
            }
            else if (request.DeleteDimension?.Range is { } remove)
            {
                var tab = Find(book, remove.SheetId);
                int start = remove.StartIndex ?? 0, end = remove.EndIndex ?? tab.RowCount;
                if (remove.Dimension != "ROWS" || start < 0 || end <= start || end > tab.RowCount) throw new InvalidOperationException("잘못된 삭제 범위입니다.");
                if (start < tab.Rows.Count) tab.Rows.RemoveRange(start, Math.Min(end, tab.Rows.Count) - start);
                tab.RowCount = Math.Max(tab.Rows.Count, Math.Max(1, tab.RowCount - end + start));
                Trim(tab);
            }
            else throw new InvalidOperationException("로컬 버전이 지원하지 않는 자료 변경입니다. 저장하지 않았습니다.");
        }
        return true;
    });
    public Facility ImportFacility(string registryTitle, string name, int diners, IReadOnlyList<WorkbookTable> tables) => Write(doc =>
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || diners < 0 || tables.Count == 0) throw new InvalidOperationException("급식소 이름·식수와 가져올 자료를 확인해 주세요.");
        var registry = Find(doc, RegistryId).Tabs.Single(t => t.Title == registryTitle);
        string At(List<ExtendedValue> row, int c) => c < row.Count ? Convert.ToString(Value(row[c]), CultureInfo.InvariantCulture) ?? "" : "";
        if (registry.Rows.Skip(1).Any(r => At(r, 1) == name.Trim() && At(r, 5) != "1")) throw new InvalidOperationException("이미 등록된 급식소 이름입니다. 새로운 이름을 입력해 주세요.");
        var book = new Book { Title = "배불뚝이 · " + name.Trim() };
        foreach (var table in tables)
        {
            var tab = AddTab(book, new() { Title = table.Title });
            Put(tab, 0, 0, table.Rows.Select(r => new RowData { Values = r.Select(v => new CellData { UserEnteredValue = FromValue(v) }).ToList() }).ToList());
        }
        string id = Guid.NewGuid().ToString("N"), bookId = "local-" + Guid.NewGuid().ToString("N");
        doc.Books.Add(bookId, book);
        foreach (var row in registry.Rows.Skip(1)) { while (row.Count < 5) row.Add(Text("")); row[4] = Text("0"); }
        registry.Rows.Add(new object[] { id, name.Trim(), bookId, diners, "1", "0" }.Select(FromValue).ToList());
        registry.RowCount = Math.Max(registry.RowCount, registry.Rows.Count);
        return new Facility(id, name.Trim(), bookId, diners, true);
    });
    public void ExportBackup(string destination)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            string full = Path.GetFullPath(destination);
            if (string.Equals(full, FilePath, StringComparison.OrdinalIgnoreCase) || full.StartsWith(FilePath + ".", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("백업은 현재 자료 파일과 다른 위치에 저장해 주세요.");
            File.WriteAllText(full, JsonConvert.SerializeObject(_document), new UTF8Encoding(false));
        }
    }
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; _lease.Dispose(); } }
}
