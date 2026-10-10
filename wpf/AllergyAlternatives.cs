namespace wpf;
public record AllergyAlternative(string PatientName,string RiskIngredients,string SuggestedAlternative);
public static class AllergyAlternatives
{
    public static bool KnownAllergens(string raw)
    {
        var text=raw.Trim(' ', ',');
        if(text.Length==0 || text.Contains("미확인") || text.Contains("확인 필요"))return false;
        if(text is "없음" or "해당 없음" or "알레르기 없음")return true;
        return System.Text.RegularExpressions.Regex.IsMatch(text,@"(?<!\d)(?:[1-9]|1[0-9])(?!\d)|[①-⑲]") || AllergenPicker.Names.Any(n=>text.Contains(n));
    }
    private static bool Nutrition(TrackMenu m)=>m.HasNutrition && new[]{m.Carb!.Value,m.Protein!.Value,m.Fat!.Value,m.Energy}.All(n=>double.IsFinite(n)&&n>=0);
    public static TrackMenu? Choose(TrackMenu original,IReadOnlyList<TrackMenu> catalog,IReadOnlyCollection<string> allergies)
    {
        if(!Nutrition(original))return null;
        double Difference(double a,double b)=>Math.Abs(a-b)/Math.Max(a,1);
        return catalog.Where(m=>m.Category==original.Category && m.Name!=original.Name && Nutrition(m) &&
            KnownAllergens(m.Allergens) && !GoogleSheetsService.MatchesRegisteredAllergen(m,allergies))
            .OrderBy(m=>Difference(original.Energy,m.Energy)+Difference(original.Carb!.Value,m.Carb!.Value)+Difference(original.Protein!.Value,m.Protein!.Value)+Difference(original.Fat!.Value,m.Fat!.Value))
            .ThenBy(m=>m.Key,StringComparer.Ordinal).FirstOrDefault();
    }
    public static IReadOnlyList<AllergyAlternative> Build(IReadOnlyList<StoredMeal> meals,IReadOnlyList<PatientModel> patients,IReadOnlyDictionary<string,IReadOnlyList<TrackMenu>> catalogs)
    {
        var result=new List<AllergyAlternative>();
        foreach(var patient in patients)
        {
            var allergies=GoogleSheetsService.SplitAllergens(patient.Allergies).ToArray();
            if(allergies.Length==0)continue;
            foreach(var meal in meals)
            {
                string age=meal.AgeGroup.Length==0?"3-5":meal.AgeGroup;
                var catalog=catalogs.GetValueOrDefault(age,Array.Empty<TrackMenu>());
                var keys=meal.Value(25).Split(" / ");
                var originals=meal.Menus.Select((name,i)=>{
                    var candidates=catalog.Where(m=>m.Name==name&&m.Category==WeeklyMealPlanner.Categories[i]).ToArray();
                    return candidates.FirstOrDefault(m=>i<keys.Length&&m.Key==keys[i])??(candidates.Length==1?candidates[0]:null);
                }).ToArray();
                var chosen=originals.ToArray();var changes=new List<string>();var risks=new HashSet<string>();bool incomplete=false;
                for(int i=0;i<6;i++)
                {
                    var original=originals[i];
                    if(original==null || !KnownAllergens(original.Allergens))
                    {incomplete=true;changes.Add(meal.Menus[i]+": 원본·알레르기 정보 확인 필요");continue;}
                    var matches=allergies.Where(a=>GoogleSheetsService.MatchesRegisteredAllergen(original,new[]{a})).ToArray();
                    if(matches.Length==0)continue;
                    risks.UnionWith(matches);
                    var replacement=Choose(original,catalog,allergies);
                    chosen[i]=replacement;
                    if(replacement==null){incomplete=true;changes.Add(original.Name+": 영양·알레르기 정보가 확인된 대체 후보 없음");continue;}
                    changes.Add($"{original.Name} → {replacement.Name} ({original.Energy:0.#} → {replacement.Energy:0.#} kcal)");
                }
                if(changes.Count==0)continue;
                string nutrition="영양 합계 비교 미확인";
                if(originals.All(m=>m!=null&&Nutrition(m))&&chosen.All(m=>m!=null&&Nutrition(m)))
                {
                    string Compare(string label,Func<TrackMenu,double> get,string unit)=>$"{label} {originals.Sum(m=>get(m!)):0.#} → {chosen.Sum(m=>get(m!)):0.#} {unit}";
                    nutrition=string.Join(" · ",new[]{Compare("열량",m=>m.Energy,"kcal"),Compare("탄",m=>m.Carb!.Value,"g"),Compare("단",m=>m.Protein!.Value,"g"),Compare("지",m=>m.Fat!.Value,"g")});
                }
                var menuNames=chosen.Select((m,i)=>m?.Name??meal.Menus[i]+" [확인 필요]");
                result.Add(new(patient.Name,string.Join(", ",risks)+(incomplete?" · 확인 필요":""),
                    $"{meal.Date:M/d} · {meal.Meal} · {age}세 / "+(incomplete?"대체식단 미완성":"대체식단 자동 생성")+"\n"+string.Join("\n",changes)+"\n식단: "+string.Join(" · ",menuNames)+"\n"+nutrition));
            }
        }
        return result;
    }
}
