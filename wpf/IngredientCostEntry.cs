using System.Globalization;

namespace wpf;

/// <summary>Build editable purchase standards from the ingredients already in the meal.</summary>
public static class IngredientCostEntry
{
    public static string MissingFields(CostDraft row)
    {
        var missing=new List<string>();
        bool Number(string value,bool positive=false)
        {
            try { CostCalculator.Number(value,"입력값",positive);return true; }
            catch(InvalidOperationException){return false;}
        }
        if(!Number(row.Price))missing.Add("구매가격");
        if(!Number(row.PriceQuantity,true))missing.Add("가격 기준량");
        string unit=row.PriceUnit.Trim().ToLowerInvariant();
        if(unit is not ("g" or "kg" or "ml" or "l" or "개" or "봉"))missing.Add("가격 단위");
        if(!Number(row.WastePercent)||CostCalculator.Number(row.WastePercent,"폐기율")>=100)missing.Add("폐기율");
        if((unit is "개" or "봉" || row.Unit.Trim() is "개" or "봉" && unit is "g" or "kg")&&!Number(row.PackageWeightGrams,true))missing.Add("개·봉당 중량");
        return string.Join(" · ",missing);
    }

    public static List<CostDraft> Build(IEnumerable<CostDraft> ingredients,IReadOnlyList<CostDraft> standards)
    {
        var result=standards.Select(MealCostData.Clone).ToList();
        foreach(var row in result)row.SharedBasisSignature=IngredientStandards.Signature(row);
        foreach(var group in ingredients.Where(d=>!string.IsNullOrWhiteSpace(d.Name)).GroupBy(d=>IngredientStandards.Key(d.Name)))
        {
            var existing=result.SingleOrDefault(r=>IngredientStandards.Key(r.Name)==group.Key);
            if(existing!=null)
            {
                if(group.Any(d=>d.Unit.Trim() is "개" or "봉"))existing.Unit="개";
                continue;
            }
            // Do not choose one recipe's price when the same ingredient has conflicting values.
            string Unique(IEnumerable<string> values)=>values.Select(v=>v.Trim()).Where(v=>v.Length>0).Distinct().ToArray() is [var value]?value:"";
            string Numeric(string value)=>decimal.TryParse(value.Trim().Replace(",",""),NumberStyles.Number,CultureInfo.InvariantCulture,out var n)?n.ToString("G29",CultureInfo.InvariantCulture):value.Trim();
            var priced=group.Where(d=>!string.IsNullOrWhiteSpace(d.Price)).Select(d=>(Price:Numeric(d.Price),Quantity:Numeric(d.PriceQuantity),Unit:d.PriceUnit.Trim().ToLowerInvariant())).Distinct().ToArray();
            var draft=new CostDraft {Name=group.Key,SharedBasisSignature="",Source="선택한 식단의 재료",Unit=group.Any(d=>d.Unit.Trim() is "개" or "봉")?"개":"g",
                Price=priced.Length==1?priced[0].Price:"",
                PriceQuantity=priced.Length==1?priced[0].Quantity:priced.Length>1?"":Unique(group.Select(d=>Numeric(d.PriceQuantity))),
                PriceUnit=priced.Length==1?priced[0].Unit:priced.Length>1?"":Unique(group.Select(d=>d.PriceUnit.ToLowerInvariant())),
                WastePercent=Unique(group.Select(d=>Numeric(d.WastePercent))),
                PackageWeightGrams=Unique(group.Select(d=>Numeric(d.PackageWeightGrams)))};
            if(priced.Length>1)draft.Source+=" · 서로 다른 구매가격·기준량이 있어 직접 확인 필요";
            result.Add(draft);
        }
        return result;
    }
}
