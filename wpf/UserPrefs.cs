using System;
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
            Path.Combine(AppConfig.UserDataDirectory, "userprefs.json");

        private static JObject _root = Load();

        private static JObject Load()
        {
            try
            {
                string source = File.Exists(FilePath) ? FilePath : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "userprefs.json");
                if (File.Exists(source))
                {
                    return JObject.Parse(File.ReadAllText(source));
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
                Directory.CreateDirectory(AppConfig.UserDataDirectory);
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
    }
}
