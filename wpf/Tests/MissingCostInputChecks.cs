using wpf;

internal static class MissingCostInputChecks
{
    internal static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        var meal=ManagementChecks.Fixture();var input=CostCalculationChecks.Inputs(meal);
        foreach(var d in input){d.Price="";d.WastePercent="";}
        var rows=IngredientCostEntry.Build(input,[]);
        check(rows.Count==1&&rows[0].Name=="양파","공통 기준이 0개여도 선택 식단의 재료명을 모아 입력 목록 생성");
        check(rows[0].Price==""&&rows[0].WastePercent=="","없는 가격·폐기율을 0으로 채우지 않음");
        check(IngredientCostEntry.MissingFields(rows[0]).Contains("구매가격")&&IngredientCostEntry.MissingFields(rows[0]).Contains("폐기율"),"입력할 필드를 구분해 표시");
        check(rows[0].SharedBasisSignature=="","신규 공통 기준에도 동시 수정 확인값 유지");
        var changed=MealCostData.Clone(rows[0]);changed.Price="0";changed.WastePercent="0";
        check(IngredientCostEntry.MissingFields(changed)=="","명시한 0원·0%는 미입력으로 표시하지 않음");
        check(rows[0].Price=="","입력창에서 편집할 복사본은 원본에 즉시 영향을 주지 않음");
        changed.PriceUnit="개";
        check(IngredientCostEntry.MissingFields(changed).Contains("중량"),"개수 구매의 개당 중량 누락 안내");
        changed.PackageWeightGrams="60";changed.WastePercent="100";
        check(IngredientCostEntry.MissingFields(changed).Contains("폐기율"),"100% 폐기율을 완료로 표시하지 않음");
        changed.WastePercent="0";changed.PriceQuantity="0";
        check(IngredientCostEntry.MissingFields(changed).Contains("기준량"),"0 구매 기준량을 완료로 표시하지 않음");
        var conflict=CostCalculationChecks.Inputs(meal);conflict[0].Price="3000";
        var conflicting=IngredientCostEntry.Build(conflict,[]).Single();
        check(conflicting.Price==""&&conflicting.PriceQuantity==""&&conflicting.PriceUnit=="","같은 재료의 구매 조건이 충돌하면 한 값을 임의 선택하지 않음");
        conflict=CostCalculationChecks.Inputs(meal);conflict[0].Price="2,000.0";conflict[0].PriceQuantity="1.0";
        check(IngredientCostEntry.Build(conflict,[]).Single().Price=="2000","같은 수치의 표기 차이를 구매가격 충돌로 오판하지 않음");
        conflict[0].Name="양파 / 다른 제품";
        check(IngredientCostEntry.Build(conflict,[]).Count==2,"다른 제품명은 별도 기준으로 유지");
        var master=new CostDraft{Name="양파",Price="5000",PriceQuantity="1",PriceUnit="kg",WastePercent="5"};
        var existing=IngredientCostEntry.Build(input,[master]).Single();
        check(existing.Price=="5000"&&existing.SharedBasisSignature==IngredientStandards.Signature(master),"기존 공통 기준과 동시 수정 확인값을 보존");

        var http=new CostCalculationSheets(meal);var service=CostCalculationChecks.Service(http);
        foreach(var d in input)http.Tables[AppConfig.CostSheetName].Add(new object[]{d.Menu,d.Name,d.Category,d.Amount,d.Unit,d.Price,d.PriceQuantity,d.PriceUnit,meal.AgeGroup,"",d.WastePercent,""});
        rows[0].Price="2000";rows[0].WastePercent="10";
        service.SaveIngredientStandardsAsync(rows).GetAwaiter().GetResult();
        var saved=service.GetMealCostsAsync(true).GetAwaiter().GetResult();
        check(saved.Count(IngredientStandards.IsStandard)==1,"미입력 여섯 행의 같은 재료를 공통 기준 한 번으로 저장");
        var rebuilt=MealCostData.Build(meal,saved);var calc=MealCostCalculation.Create(meal,rebuilt,30,true);
        check(rebuilt.All(d=>d.Price=="2000"&&d.WastePercent=="10")&&calc.TotalCost==36000,"입력·저장한 가격과 폐기율이 해당 재료의 모든 계산 행에 반영");
        input[0].Amount="45";foreach(var d in input)IngredientStandards.Apply(d,saved);
        check(input[0].Amount=="45"&&MealCostCalculation.Create(meal,input,30,true).TotalCost==33000,"기준 반영 중 저장 전 정미량 수정은 유지");
        var stale=new CostDraft{Name="양파",Price="9000",PriceQuantity="1",PriceUnit="kg",WastePercent="10",SharedBasisSignature=""};
        reject(()=>service.SaveIngredientStandardsAsync([stale]).GetAwaiter().GetResult(),"새 입력창을 연 뒤 다른 곳에 생성된 기준을 오래된 입력으로 덮어쓰지 않음");
        Console.WriteLine("Missing cost input checks use isolated HTTP responses; no live Google calls.");
    }
}
