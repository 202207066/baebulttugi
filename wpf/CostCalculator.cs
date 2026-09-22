using System.Globalization;

namespace wpf;

public record CostRow(string Name, string Category, decimal UnitSize, string Unit, decimal UnitPrice, decimal PriceQuantity = 1, string PriceUnit = "");

public static class CostCalculator
{
    public static decimal Number(string raw, string label, bool positive = false)
    {
        if (!decimal.TryParse(raw.Trim().Replace(",", ""), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < 0 || (positive && value == 0))
            throw new InvalidOperationException($"{label}: 올바른 {(positive ? "양수" : "0 이상의 숫자")}를 입력해 주세요.");
        return value;
    }
    public static decimal CostPerPerson(CostRow row)
    {
        if (row.UnitSize < 0 || row.UnitPrice < 0 || row.PriceQuantity <= 0) throw new InvalidOperationException("소요량·단가·단가 기준량을 확인해 주세요.");
        var unit = row.Unit.Trim().ToLowerInvariant();
        var priceUnit = (string.IsNullOrWhiteSpace(row.PriceUnit) ? row.Unit : row.PriceUnit).Trim().ToLowerInvariant();
        if (unit.Length == 0) throw new InvalidOperationException("소요량 단위를 입력해 주세요.");
        decimal factor = (unit, priceUnit) switch
        {
            ("g", "kg") or ("ml", "l") => 0.001m,
            ("kg", "g") or ("l", "ml") => 1000m,
            _ when unit == priceUnit => 1m,
            _ => throw new InvalidOperationException($"{row.Name}: {unit}와 {priceUnit}는 자동 환산할 수 없습니다. 같은 단위를 사용해 주세요.")
        };
        return row.UnitSize * factor / row.PriceQuantity * row.UnitPrice;
    }
}

public class IngredientItem
{
    private readonly CostRow _row;
    private readonly int _headCount;
    public IngredientItem(CostRow row, int headCount)
    {
        if (headCount <= 0) throw new ArgumentException("인원은 1명 이상이어야 합니다.");
        _row = row; _headCount = headCount;
        PerPersonCost = CostCalculator.CostPerPerson(row);
    }
    public string Name => _row.Name;
    public string Category => _row.Category;
    public decimal PerPersonCost { get; }
    public decimal TotalCost => PerPersonCost * _headCount;
    public decimal TotalQuantity => _row.UnitSize * _headCount;
    public string UnitPriceDisplay => $"₩ {_row.UnitPrice:N2} / {_row.PriceQuantity:0.###} {(string.IsNullOrWhiteSpace(_row.PriceUnit) ? _row.Unit : _row.PriceUnit)}";
    public string UnitWeightDisplay => $"{_row.UnitSize:0.###} {_row.Unit}";
    public string TotalWeightDisplay => $"{TotalQuantity:0.###} {_row.Unit}";
    public string TotalPriceDisplay => $"₩ {TotalCost:N2}";
}
