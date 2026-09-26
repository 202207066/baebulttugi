using Microsoft.VisualBasic.FileIO;
using System.IO;

namespace wpf;

public record IngredientCatalogItem(string Name, string Product, string Unit, string Price, string Source);

public static class IngredientCsv
{
    private static readonly string[] NameHeaders = ["원재료명", "식재료명", "식품명", "품명", "상품명", "재료명"];
    private static readonly string[] ProductHeaders = ["규격", "상품명", "품목", "비고"];
    private static readonly string[] UnitHeaders = ["단위", "규격단위"];
    private static readonly string[] PriceHeaders = ["단가", "가격", "구매가격", "금액"];

    public static List<IngredientCatalogItem> Parse(TextReader reader, string source)
    {
        using var parser = new TextFieldParser(reader) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        parser.SetDelimiters(",");
        var header = (parser.ReadFields() ?? throw new InvalidOperationException("CSV가 비어 있습니다.")).Select(x => x.Trim().TrimStart('\uFEFF')).ToArray();
        int Find(string[] options) => Array.FindIndex(header, h => options.Contains(h));
        int name = Find(NameHeaders), product = Find(ProductHeaders), unit = Find(UnitHeaders), price = Find(PriceHeaders);
        if (name < 0) throw new InvalidOperationException("CSV 첫 행에 원재료명·식재료명·식품명·품명 중 하나가 필요합니다.");
        string At(string[] row, int index) => index >= 0 && index < row.Length ? row[index].Trim() : "";
        var result = new List<IngredientCatalogItem>();
        while (!parser.EndOfData)
        {
            var row = parser.ReadFields()!;
            string ingredient = At(row, name);
            if (ingredient.Length == 0) continue;
            result.Add(new IngredientCatalogItem(ingredient, At(row, product), At(row, unit), At(row, price), source));
        }
        return result.GroupBy(i => (i.Name, i.Product, i.Unit, i.Price)).Select(g => g.First()).ToList();
    }
}
