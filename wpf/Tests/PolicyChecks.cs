using wpf;

internal static class PolicyChecks
{
    public static void Run(Action<bool,string> check,Action<Action,string> reject)
    {
        IList<IList<object>> rows=new List<IList<object>>{
            new List<object>{"ID","성명","알레르기 코드","특이사항","추가 알레르기 코드","비고"},
            new List<object>{1,"대상","1,2","달걀,우유","⑤,대두","원본 메모"},
            new List<object>{2,"충돌","없음","우유","",""},
            new List<object>{3,"누락","","","",""},
            new List<object>{4,"미확인","125","키위 의심","",""}
        };
        var people=PatientSheetCodec.Parse(rows);
        check(people[0].Allergies=="1,2,5"&&!people[0].NeedsAllergyReview,"코드·이름·원문자 중복을 제거하며 모든 열을 통합합니다");
        check(people[1].NeedsAllergyReview&&people[1].Allergies.Contains("우유")&&people[1].Allergies.Contains("없음"),"모순된 정보는 양쪽 원문과 확인 상태를 유지합니다");
        check(people[2].NeedsAllergyReview&&people[3].NeedsAllergyReview,"빈 정보·잘못된 코드·모호한 표현은 확인 대상입니다");
        people[0].Note="메모만 수정";
        var preserved=PatientSheetCodec.Merge(rows,people);
        check(Enumerable.Range(2,3).All(c=>preserved[1][c].Equals(rows[1][c]))&&preserved[1][5].Equals("메모만 수정"),"일반 정보 저장은 여러 열의 원본을 보존합니다");
        people[0].Allergies="1,2,5,6";people[0].AllergyChangeConfirmed=true;
        var edited=PatientSheetCodec.Merge(rows,people);
        check(PatientSheetCodec.Parse(edited)[0].Allergies=="1,2,5,6"&&edited[1][3].Equals("")&&edited[1][4].Equals(""),"승인한 수정만 관련 열의 통합 정보로 저장합니다");
        people[1].Allergies="없음";
        reject(()=>PatientSheetCodec.Merge(rows,people),"확인되지 않은 충돌 정보를 정상으로 덮어쓰지 않습니다");
        var warnings=AllergyAlternatives.Build([],PatientSheetCodec.Parse(rows),new Dictionary<string,IReadOnlyList<TrackMenu>>());
        check(warnings.Count==3&&warnings.All(w=>w.RiskIngredients.Contains("확인 필요")),"식단이 없어도 대체식 화면에 누락·충돌 확인 대상이 표시됩니다");
        var merged=PatientSheetCodec.Parse(rows)[0];
        var menu=new TrackMenu("x","메뉴","밥류","5",1,1,1,20,"");
        check(GoogleSheetsService.MatchesRegisteredAllergen(menu,GoogleSheetsService.SplitAllergens(merged.Allergies)),"추가 열의 알레르기도 기존 판별 로직에 반영됩니다");
    }
}
