using wpf;
using System.Text.Json;
internal static class ReleaseChecks
{
 public static void Run(Action<bool,string> check,Action<Action,string> reject){
  var official=OfficialCatalog.Current;check(official.Menus.Count==3500&&official.Recipes.Count==3494,"Official package contains selected food data only");
  check(official.Menus.Count(m=>!m.Menu.HasNutrition)==15,"Incomplete nutrition remains flagged, never filled with zero");
  var boundary=new TrackMenu("boundary","분류 시험","부찬","2",10,11,10,100,"시험"){ServingGrams=100,ProteinReviewed=true};
  var main=MenuClassification.Official(boundary);check(main.Category=="주찬"&&main.Key==boundary.Key,"Protein classification preserves source key and recipe links");
  check(MenuClassification.Official(boundary with{Category="주찬",Protein=10.999}).Category=="부찬"&&MenuClassification.Official(boundary with{Category="밥류"}).Category=="밥류","Protein classification respects boundary and preserves rice category");
  check(OfficialCatalog.Menus("3-5").Where(MenuClassification.IsSide).All(m=>!m.ProteinReviewed),"Official draft grades remain unapproved pending professor review");
  check(!MenuClassification.AutomaticCandidate(boundary with{ProteinReviewed=false})&&MenuClassification.AutomaticCandidate(main),"Automatic main candidates require reviewed protein density");
  var noData=new OfficialCatalogData{SchemaVersion=99,Version="2026.10.29.1"};try{OfficialCatalog.Validate(noData);throw new Exception("accepted bad catalog");}catch(System.IO.InvalidDataException){check(true,"Unsupported catalog update cannot replace existing data");}
  var rice=new CostRow("쌀","",35,"g",3000,1,"kg");var onion=new CostRow("양파","",20,"g",2000,1,"kg",10);
  check(CostCalculator.CostPerPerson(rice)==105&&decimal.Round((CostCalculator.CostPerPerson(rice)+CostCalculator.CostPerPerson(onion))*30,2)==4483.33m,"Waste formula keeps precision until final display");
  check(CostCalculator.CostPerPerson(new("계란","",30,"g",600,1,"개",0,60))==300,"Per-item price converts using entered package weight");
  reject(()=>CostCalculator.CostPerPerson(onion with{WastePercent=100}),"100 percent waste rejected");
  reject(()=>CostCalculator.CostPerPerson(new("우유","",30,"ml",600,1,"개",0,60)),"Mass and volume are not silently converted");
  var missing=new IngredientItem(rice,30,false);check(missing.PerPersonCost==null&&missing.TotalPriceDisplay.Contains("합계 제외"),"Missing price stays unknown in display and totals");
  var menus=WeeklyMealPlanner.Categories.Select((c,i)=>new TrackMenu(i.ToString(),c+"시험",c,"1",1,2,3,39,"시험")).ToList();menus.Add(menus[3] with{Id="extra1",Name="추가반찬1"});menus.Add(menus[3] with{Id="extra2",Name="추가반찬2"});
  var meal=ManagementChecks.Fixture();var edited=GoogleSheetsService.EditedMealValues(meal,menus);var saved=new StoredMeal{Raw=edited.ToList(),Date=meal.Date};
  check(saved.Menus.Length==8&&saved.Menus[7]=="추가반찬2"&&saved.Calories==312,"Five side dishes persist with full nutrition");
  check(saved.SavedItems![0].Allergens=="1"&&saved.SavedItems[0].Source=="시험","Stored meal retains allergy and nutrition snapshot across catalog updates");
  reject(()=>GoogleSheetsService.EditedMealValues(meal,menus.Append(menus[3]).ToArray()),"A sixth side dish cannot be saved");
  var drafts=MealCostData.Build(saved,[]);check(drafts.Count==8&&drafts.Any(d=>d.Menu=="추가반찬2"),"Added sides reach cost calculation");
  var rows=new List<IList<object>>{new object[]{"ID","성명","알레르기","추가 알레르기 코드","알레르기 상태"},new object[]{1,"","2","5","미확인"},new object[]{2,"번호2","없음","","없음 확인"}};
  var people=PatientSheetCodec.Parse(rows);check(people[0].Name=="대상자-0001"&&people[0].NeedsAllergyReview&&people[0].Allergies=="2,5","Anonymous identifiers and unknown allergy status preserve merged data");
  check(!people[1].NeedsAllergyReview&&people[1].AllergyStatus=="없음 확인","Explicit no-allergy state round trips separately");
  people[0].AllergyState="있음 확인";people[0].AllergyChangeConfirmed=true;var result=PatientSheetCodec.Parse(PatientSheetCodec.Merge(rows,people));check(!result[0].NeedsAllergyReview&&result[0].Allergies=="2,5","User confirmation resolves status without losing extra allergy columns");
  var request=new CatalogRequestDraft{Name="새 식재료",Details="분량 10g",Reason="자료 추가"};request.Validate();request.Name="";reject(request.Validate,"Empty requests cannot be sent");
 }
}
