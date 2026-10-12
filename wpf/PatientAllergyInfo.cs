using System.Text;
using System.Text.RegularExpressions;

namespace wpf;

public record PatientAllergyInfo(string Value, string Review)
{
    public static string Key(string token)
    {
        token=token.Trim().Normalize(NormalizationForm.FormKC);
        token=token switch { "계란" or "달걀" => "난류", "소고기" => "쇠고기", "대두콩" => "대두", _ => token };
        int index=Array.IndexOf(AllergenPicker.Names,token);
        if(index>=0)return (index+1).ToString();
        return int.TryParse(token,out int number)&&number is >=1 and <=19?number.ToString():token;
    }

    public static bool IsNone(string value) => Regex.Replace(value,@"\s+","") is "없음" or "해당없음" or "알레르기없음" or "알러지없음";

    public static PatientAllergyInfo Merge(IEnumerable<string> cells, bool hasColumns=true)
    {
        var tokens=new List<string>();var keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var issues=new List<string>();bool none=false;
        foreach(string raw in cells.Where(s=>!string.IsNullOrWhiteSpace(s)))
        {
            string expanded=Regex.Replace(raw,"[①-⑳]",m=>((int)m.Value[0]-'①'+1).ToString()+",").Normalize(NormalizationForm.FormKC);
            foreach(string part in Regex.Split(expanded,@"[,;/\r\n]+"))
            {
                string value=part.Trim();if(value.Length==0)continue;
                if(IsNone(value)){none=true;continue;}
                var parts=Regex.IsMatch(value,@"^[\d\s]+$")?Regex.Split(value,@"\s+"):new[]{value};
                foreach(var token in parts)
                {
                    string key=Key(token);
                    if(keys.Add(key))tokens.Add(token);
                    bool known=int.TryParse(key,out int n)&&n is >=1 and <=19 || key is "아몬드" or "참깨" or "콩류(기타)" or "굴" or "전복" or "홍합";
                    if(!known)issues.Add("불명확한 항목: "+token);
                }
            }
        }
        if(!hasColumns)issues.Add("알레르기 열 누락");
        else if(tokens.Count==0&&!none)issues.Add("알레르기 정보 미입력");
        if(none&&tokens.Count>0)issues.Add("'없음'과 알레르기 정보가 함께 기록됨");
        // 모순된 '없음'도 원문 정보로 남겨 다시 읽을 때 확인 상태가 사라지지 않게 합니다.
        if(none)tokens.Add("없음");
        return new(string.Join(",",tokens),string.Join(" · ",issues.Distinct()));
    }
}
