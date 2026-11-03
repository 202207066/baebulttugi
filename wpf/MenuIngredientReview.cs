using System.Text.RegularExpressions;
namespace wpf;

public static class MenuIngredientReview
{
    public static TrackMenu Apply(TrackMenu menu, IEnumerable<PortionIngredient> source)
    {
        var ingredients = source.ToList();
        string[] names = ingredients.Select(i => Regex.Replace(i.Name, @"\s+", "")).ToArray();
        bool Has(params string[] words) => names.Any(n => words.Any(n.Contains));
        bool composite = Has("소스", "드레싱", "양념장", "간장", "고추장", "된장", "마요네즈", "케첩", "카레", "햄", "어묵", "맛살", "만두", "가공", "믹스", "분말");
        string Flag(bool present, bool uncertain = false) => present ? "포함" : ingredients.Count == 0 || composite || uncertain ? "검토 필요" : "미검출(1차)";
        bool sesame = Has("참깨", "통깨", "깨소금", "참기름", "흑임자");
        var almond = Flag(Has("아몬드"));
        var sesameFlag = Flag(sesame, Has("들깨", "들기름"));
        var beans = Flag(Has("완두", "강낭", "병아리콩", "녹두", "팥", "렌틸", "렌즈콩", "리마콩"));
        bool massKnown = ingredients.Count > 0 && ingredients.All(i => i.Amount >= 0 && i.Unit is "g" or "kg");
        double? grams = massKnown ? (double)ingredients.Sum(i => i.Amount * (i.Unit == "kg" ? 1000 : 1)) : null;
        return menu with {
            ServingGrams = grams is > 0 ? grams : null,
            ServingBasis = grams is > 0 ? "완성 제공량 미확인 · 재료 투입량 합계(g) 대용" : "제공량 확인 필요 · 무게 단위 또는 재료 누락",
            ProteinReviewed = false, Almond = almond, Sesame = sesameFlag, OtherBeans = beans,
            AllergyReview = ingredients.Count == 0 ? "식재료 누락" : composite ? "복합재료의 제품 원재료 표시 확인 필요" : Has("들깨", "들기름") ? "들깨·들기름 확인 필요" : ""
        };
    }
    public static string EffectiveAllergens(TrackMenu menu) => string.Join(",", new[] {
        menu.Allergens, menu.Almond == "포함" ? "아몬드" : "", menu.Sesame == "포함" ? "참깨" : "", menu.OtherBeans == "포함" ? "콩류(기타)" : ""
    }.Where(s => s.Length > 0));
    public static bool HasUnresolvedExtras(TrackMenu menu) => new[] { menu.Almond, menu.Sesame, menu.OtherBeans }.Any(s => s is not ("포함" or "미검출(1차)" or "없음 확인"));
}
