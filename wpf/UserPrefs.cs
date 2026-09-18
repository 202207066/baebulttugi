using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace wpf
{
    /// <summary>
    /// 사용자별 UI 상태를 기억합니다(실행 폴더의 userprefs.json).
    ///
    /// 지금은 "튜토리얼을 이미 봤는가" 하나만 쓰지만,
    /// 앞으로 화면 설정 같은 걸 추가하기 쉽도록 분리해 둡니다.
    /// 읽기/쓰기에 실패해도 프로그램이 멈추지 않도록 전부 삼킵니다.
    /// </summary>
    public static class UserPrefs
    {
        private static string FilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userprefs.json");

        private static JObject _root = Load();

        private static JObject Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    return JObject.Parse(File.ReadAllText(FilePath));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserPrefs 로드 실패] {ex.Message}");
            }
            return new JObject();
        }

        private static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, _root.ToString());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[UserPrefs 저장 실패] {ex.Message}");
            }
        }

        private static bool GetBool(string key, bool fallback = false)
        {
            var token = _root[key];
            if (token == null) return fallback;
            return token.Type == JTokenType.Boolean ? token.Value<bool>() : fallback;
        }

        private static void SetBool(string key, bool value)
        {
            _root[key] = value;
            Save();
        }

        /// <summary>시작 튜토리얼을 이미 본 적이 있는가.</summary>
        public static bool HasSeenTutorial
        {
            get => GetBool("hasSeenTutorial");
            set => SetBool("hasSeenTutorial", value);
        }

        /// <summary>메인 화면 안내 오버레이를 이미 본 적이 있는가.</summary>
        public static bool HasSeenTour
        {
            get => GetBool("hasSeenTour");
            set => SetBool("hasSeenTour", value);
        }

        /// <summary>이 PC에서 처음 실행하는 것인지.</summary>
        public static bool IsFirstRun => !HasSeenTutorial;

        // ── 최근 사용한 데이터베이스 ──────────────────────────────────
        //
        // 급식소마다 자기 스프레드시트를 쓰게 되므로, 한 번 연결한 시트는
        // 목록에 남겨 두고 클릭 한 번으로 다시 전환할 수 있게 합니다.

        /// <summary>최근에 연결했던 스프레드시트 (최신 항목이 앞).</summary>
        public static List<RecentDatabase> RecentDatabases
        {
            get
            {
                var list = new List<RecentDatabase>();
                try
                {
                    if (_root["recentDatabases"] is JArray array)
                    {
                        foreach (var item in array)
                        {
                            string id = item?["id"]?.ToString() ?? "";
                            if (string.IsNullOrWhiteSpace(id)) continue;

                            list.Add(new RecentDatabase
                            {
                                Id = id,
                                Title = item?["title"]?.ToString() ?? "(이름 없음)"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[최근 DB 목록 읽기 실패] {ex.Message}");
                }
                return list;
            }
        }

        /// <summary>목록 맨 앞에 올립니다. 이미 있으면 위로 끌어올리고 이름을 갱신합니다.</summary>
        public static void RememberDatabase(string id, string title)
        {
            if (string.IsNullOrWhiteSpace(id)) return;

            try
            {
                var list = RecentDatabases;
                list.RemoveAll(d => string.Equals(d.Id, id, StringComparison.Ordinal));
                list.Insert(0, new RecentDatabase { Id = id, Title = title });

                // 너무 길어지지 않게 8개까지만 둡니다.
                if (list.Count > 8) list = list.GetRange(0, 8);

                var array = new JArray();
                foreach (var d in list)
                {
                    array.Add(new JObject { ["id"] = d.Id, ["title"] = d.Title });
                }

                _root["recentDatabases"] = array;
                Save();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[최근 DB 목록 저장 실패] {ex.Message}");
            }
        }
    }

    /// <summary>최근에 연결했던 스프레드시트 한 건.</summary>
    public class RecentDatabase
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        /// <summary>목록에 보여 줄 짧은 ID(앞 12자).</summary>
        public string ShortId => Id.Length > 12 ? Id.Substring(0, 12) + "…" : Id;
    }
}
