using System.Globalization;

namespace wpf;

public record CostRow(string Name, string Category, decimal UnitSize, string Unit, decimal UnitPrice, decimal PriceQuantity = 1, string PriceUnit = "", decimal WastePercent = 0, decimal? PackageWeightGrams = null);

public static class CostCalculator
{
    public static (decimal Quantity, string Unit) PurchaseBasis(CostRow row)
    {
        if (row.PriceQuantity <= 0 || row.UnitPrice < 0) throw new InvalidOperationException("구매가격과 기준량을 확인해 주세요.");
        string unit = (string.IsNullOrWhiteSpace(row.PriceUnit) ? row.Unit : row.PriceUnit).Trim().ToLowerInvariant();
        return unit switch {
            "kg" => (row.PriceQuantity * 1000, "g"),
            "g" => (row.PriceQuantity, "g"),
            "l" => (row.PriceQuantity * 1000, "ml"),
            "ml" => (row.PriceQuantity, "ml"),
            "개" or "봉" when row.PackageWeightGrams is > 0 => (row.PriceQuantity * row.PackageWeightGrams.Value, "g"),
            _ when unit.Length > 0 => (row.PriceQuantity, unit),
            _ => throw new InvalidOperationException("구매 기준량의 단위를 입력해 주세요.")
        };
    }
    public static decimal PricePerBaseUnit(CostRow row) => row.UnitPrice / PurchaseBasis(row).Quantity;
    public static decimal Number(string raw, string label, bool positive = false)
    {
        if (!decimal.TryParse(raw.Trim().Replace(",", ""), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            || value < 0 || (positive && value == 0))
            throw new InvalidOperationException($"{label}: 올바른 {(positive ? "양수" : "0 이상의 숫자")}를 입력해 주세요.");
        return value;
    }
    public static decimal GrossSize(CostRow row)
    {
        if(row.WastePercent<0||row.WastePercent>=100)throw new InvalidOperationException($"{row.Name}: 폐기율은 0 이상 100 미만이어야 합니다.");
        if(string.IsNullOrWhiteSpace(row.Unit))throw new InvalidOperationException("정미량 단위를 입력해 주세요.");
        if(row.UnitSize<0)throw new InvalidOperationException("정미량은 0 이상이어야 합니다.");
        return row.UnitSize/(1-row.WastePercent/100);
    }
    public static decimal CostPerPerson(CostRow row)
    {
        if (row.UnitSize < 0 || row.UnitPrice < 0 || row.PriceQuantity <= 0) throw new InvalidOperationException("소요량·단가·단가 기준량을 확인해 주세요.");
        var unit = row.Unit.Trim().ToLowerInvariant();
        var priceUnit = (string.IsNullOrWhiteSpace(row.PriceUnit) ? row.Unit : row.PriceUnit).Trim().ToLowerInvariant();
        if (unit.Length == 0) throw new InvalidOperationException("소요량 단위를 입력해 주세요.");
        if(unit is "g" or "kg" && priceUnit is "개" or "봉" && row.PackageWeightGrams is >0)
            return GrossSize(row)*row.UnitPrice*(unit=="kg"?1000m:1m)/(row.PackageWeightGrams.Value*row.PriceQuantity);
        decimal factor = (unit, priceUnit) switch
        {
            ("g", "kg") or ("ml", "l") => 0.001m,
            ("kg", "g") or ("l", "ml") => 1000m,
            _ when unit == priceUnit => 1m,
            ("g", "개" or "봉") when row.PackageWeightGrams is >0 => 1/row.PackageWeightGrams.Value,
            ("kg", "개" or "봉") when row.PackageWeightGrams is >0 => 1000/row.PackageWeightGrams.Value,
            ("개" or "봉", "g") when row.PackageWeightGrams is >0 => row.PackageWeightGrams.Value,
            ("개" or "봉", "kg") when row.PackageWeightGrams is >0 => row.PackageWeightGrams.Value/1000,
            _ => throw new InvalidOperationException($"{row.Name}: {unit}와 {priceUnit}는 자동 환산할 수 없습니다. 같은 단위를 사용해 주세요.")
        };
        return GrossSize(row) * factor * row.UnitPrice / row.PriceQuantity;
    }
}

public class IngredientItem
{
    private readonly CostRow _row;
    private readonly int _headCount;
    public IngredientItem(CostRow row, int headCount, bool hasPrice=true, string menu="", bool wasteConfirmed=true)
    {
        if (headCount <= 0) throw new ArgumentException("인원은 1명 이상이어야 합니다.");
        _row = row; _headCount = headCount; Menu = menu; WasteConfirmed = wasteConfirmed;
        CostCalculator.GrossSize(row);
        PerPersonCost = hasPrice?CostCalculator.CostPerPerson(row):null;
    }
    public string Name => _row.Name;
    public string Menu { get; }
    public bool WasteConfirmed { get; }
    public CostRow Inputs => _row;
    public string Category => _row.Category;
    public decimal? PerPersonCost { get; }
    public bool HasPrice => PerPersonCost.HasValue;
    public string WasteDisplay => WasteConfirmed ? $"{_row.WastePercent:0.###}%" : "미입력 · 0% 임시 계산";
    public string PurchaseBasisDisplay => !HasPrice ? "미입력" : $"{CostCalculator.PurchaseBasis(_row).Quantity:0.###} {CostCalculator.PurchaseBasis(_row).Unit}";
    public string BaseUnitPriceDisplay => !HasPrice ? "미입력" : $"₩ {CostCalculator.PricePerBaseUnit(_row):0.######} / {CostCalculator.PurchaseBasis(_row).Unit}";
    public string PerPersonCostDisplay => HasPrice ? $"₩ {PerPersonCost:N2}" + (WasteConfirmed ? "" : " (임시)") : "미입력";
    public string GrossWeightDisplay => $"{CostCalculator.GrossSize(_row):0.###} {_row.Unit}";
    public decimal? TotalCost => PerPersonCost * _headCount;
    public decimal TotalQuantity => CostCalculator.GrossSize(_row) * _headCount;
    public string UnitPriceDisplay => !HasPrice?"단가 미입력 · 합계 제외":$"₩ {_row.UnitPrice:N2} / {_row.PriceQuantity:0.###} {(string.IsNullOrWhiteSpace(_row.PriceUnit) ? _row.Unit : _row.PriceUnit)}";
    public string UnitWeightDisplay => $"{_row.UnitSize:0.###} {_row.Unit}";
    public string TotalWeightDisplay => $"{TotalQuantity:0.###} {_row.Unit}";
    public string TotalPriceDisplay => HasPrice?$"₩ {TotalCost:N2}":"미입력 · 합계 제외";
}
