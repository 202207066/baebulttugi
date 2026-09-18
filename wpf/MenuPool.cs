using System;
using System.Collections.Generic;
using System.Linq;

namespace wpf
{
    /// <summary>
    /// 식약처가 지정한 알레르기 유발물질 19종.
    ///
    /// 메뉴풀 시트의 I열에는 이 번호가 원문자(①②③…)로 적혀 있습니다.
    /// 앱은 «대두», «밀» 같은 이름으로 대조하므로 여기서 풀어 씁니다.
    ///
    /// ⚠️ 이 표는 안전에 직결됩니다. 센터 원본 파일의 범례와 다르면
    ///    반드시 이 배열을 먼저 고치세요.
    /// </summary>
    public static class AllergyCodes
    {
        /// <summary>원문자 → 성분명. 순서가 곧 번호입니다(①=난류 … ⑲=잣).</summary>
        private static readonly (char Symbol, string Name)[] Table =
        {
            ('①', "난류"),   ('②', "우유"),   ('③', "메밀"),
            ('④', "땅콩"),   ('⑤', "대두"),   ('⑥', "밀"),
            ('⑦', "고등어"), ('⑧', "게"),     ('⑨', "새우"),
            ('⑩', "돼지고기"), ('⑪', "복숭아"), ('⑫', "토마토"),
            ('⑬', "아황산류"), ('⑭', "호두"),  ('⑮', "닭고기"),
            ('⑯', "쇠고기"), ('⑰', "오징어"), ('⑱', "조개류"),
            ('⑲', "잣"),
        };

        /// <summary>참고용 전체 목록 (예: "①난류").</summary>
        public static IEnumerable<string> Legend =>
            Table.Select(t => $"{t.Symbol}{t.Name}");

        /// <summary>
        /// "①⑤⑥" → "난류, 대두, 밀"
        ///
        /// 원문자가 아니라 "1,5,6" 처럼 숫자로 적혀 있어도 읽습니다.
        /// 알아볼 수 없는 표기는 원문 그대로 남겨 두어, 조용히 사라지지 않게 합니다.
        /// </summary>
        public static string Decode(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";

            var names = new List<string>();
            var leftover = new List<string>();

            // 1) 원문자 먼저
            foreach (char c in raw)
            {
                foreach (var (symbol, name) in Table)
                {
                    if (c == symbol && !names.Contains(name)) names.Add(name);
                }
            }

            // 2) 원문자가 하나도 없으면 숫자 표기로 간주 ("1,5,6" / "1 5 6")
            if (names.Count == 0)
            {
                foreach (string part in raw.Split(new[] { ',', '/', ';', ' ', '·' },
                                                  StringSplitOptions.RemoveEmptyEntries))
                {
                    string token = part.Trim();
                    if (int.TryParse(token, out int number) &&
                        number >= 1 && number <= Table.Length)
                    {
                        string name = Table[number - 1].Name;
                        if (!names.Contains(name)) names.Add(name);
                    }
                    else if (token.Length > 0)
                    {
                        leftover.Add(token);
                    }
                }
            }

            names.AddRange(leftover.Where(x => !names.Contains(x)));
            return string.Join(", ", names);
        }
    }

    /// <summary>식단을 구성하는 네 자리 + 그 외.</summary>
    public enum MenuSlot
    {
        Rice,       // 밥류
        Soup,       // 국·찌개류
        Main,       // 주찬류
        Side,       // 부찬류 (김치류 포함)
        Snack,      // 간식류 - 자동 조합에서는 쓰지 않습니다
        Unknown
    }

    /// <summary>메뉴풀의 한 행. 시트 구조가 무엇이든 앱은 이 형태로만 다룹니다.</summary>
    public class MenuItem
    {
        public string MenuId { get; set; } = "";
        public string Name { get; set; } = "";

        /// <summary>시트에 적힌 카테고리 원문 (예: "국·찌개류").</summary>
        public string Category { get; set; } = "";

        /// <summary>알레르기 성분 이름 목록 (예: "대두, 밀").</summary>
        public string Allergy { get; set; } = "";

        /// <summary>재료. 레시피 매칭이 끝나기 전에는 비어 있습니다.</summary>
        public string Materials { get; set; } = "";

        public double Calories { get; set; }
        public double Carb { get; set; }
        public double Protein { get; set; }
        public double Fat { get; set; }

        /// <summary>대표출처 센터 (예: "센터B_부천시").</summary>
        public string Source { get; set; } = "";

        /// <summary>어느 트랙에서 왔는지.</summary>
        public string Track { get; set; } = "";

        public bool HasNutrients => Carb > 0 || Protein > 0 || Fat > 0;

        /// <summary>
        /// 카테고리를 식단의 자리로 바꿉니다.
        /// 시트에 카테고리가 없으면 메뉴 이름으로 추정합니다(정확도가 낮습니다).
        /// </summary>
        public MenuSlot Slot
        {
            get
            {
                string c = Category.Replace(" ", "");

                if (c.Length > 0)
                {
                    if (c.Contains("밥") || c.Contains("주식") || c.Contains("면")) return MenuSlot.Rice;
                    if (c.Contains("국") || c.Contains("찌개") || c.Contains("탕")) return MenuSlot.Soup;
                    if (c.Contains("주찬")) return MenuSlot.Main;
                    if (c.Contains("부찬") || c.Contains("김치")) return MenuSlot.Side;
                    if (c.Contains("간식") || c.Contains("후식")) return MenuSlot.Snack;
                    return MenuSlot.Unknown;
                }

                return GuessSlotFromName(Name);
            }
        }

        private static MenuSlot GuessSlotFromName(string name)
        {
            if (name.Contains("나물") || name.Contains("무침") ||
                name.Contains("샐러드") || name.Contains("김치")) return MenuSlot.Side;

            if ((name.Contains("국") || name.Contains("탕") || name.Contains("찌개")) &&
                !name.Contains("국수")) return MenuSlot.Soup;

            if (name.Contains("밥") || name.Contains("죽") ||
                name.Contains("국수") || name.Contains("면")) return MenuSlot.Rice;

            if (name.Contains("고기") || name.Contains("닭") || name.Contains("돈") ||
                name.Contains("생선") || name.Contains("가스") || name.Contains("조림") ||
                name.Contains("볶음")) return MenuSlot.Main;

            return MenuSlot.Unknown;
        }

        // ── 맛 밸런스 판정 (자동 조합에서 사용) ────────────────────────

        public bool IsSpicy => ContainsAny("제육", "김치", "청양", "매운", "떡볶이", "카레",
                                           "짬뽕", "고추장", "불닭", "낙지볶음", "오징어볶음");

        public bool IsSalty => ContainsAny("장조림", "젓갈", "조림", "찌개", "자반",
                                           "굴비", "스팸", "소세지", "소시지", "피클");

        public bool IsStrongTaste => IsSpicy || IsSalty ||
                                     ContainsAny("튀김", "탕수육", "돈가스", "까스", "강정");

        public bool IsMild => !IsStrongTaste;

        private bool ContainsAny(params string[] keywords) =>
            keywords.Any(k => Name.Contains(k, StringComparison.Ordinal));
    }
}
