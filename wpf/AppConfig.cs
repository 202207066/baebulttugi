using System;
using System.IO;
using Newtonsoft.Json.Linq;

namespace wpf
{
    /// <summary>
    /// 애플리케이션 설정을 한 곳에서 관리합니다.
    ///
    /// 설정 값은 실행 파일과 같은 폴더의 appsettings.json에서 읽습니다.
    /// 이 파일에는 스프레드시트 ID와 API 키가 들어가므로 절대 git에 커밋하지 마세요.
    /// (.gitignore에 등록되어 있습니다. appsettings.sample.json을 복사해서 만드세요.)
    /// </summary>
    public static class AppConfig
    {
        private const string ConfigFileName = "appsettings.json";

        private static readonly Lazy<JObject> _root = new Lazy<JObject>(Load);

        /// <summary>설정 파일을 찾지 못했거나 파싱에 실패한 경우의 사유. 정상이면 null.</summary>
        public static string? LoadError { get; private set; }

        /// <summary>설정 파일의 전체 경로(존재 여부와 무관하게 기대 위치).</summary>
        public static string ConfigFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        private static JObject Load()
        {
            try
            {
                string path = ConfigFilePath;
                if (!File.Exists(path))
                {
                    LoadError =
                        $"설정 파일을 찾을 수 없습니다: {path}\n" +
                        "appsettings.sample.json을 appsettings.json으로 복사한 뒤 값을 채워 주세요.";
                    return new JObject();
                }

                return JObject.Parse(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                LoadError = $"설정 파일을 읽는 중 오류가 발생했습니다: {ex.Message}";
                return new JObject();
            }
        }

        /// <summary>
        /// 설정 값을 읽습니다. "Sheets:Menu"처럼 콜론으로 중첩 경로를 표현할 수 있습니다.
        /// </summary>
        private static string GetString(string key, string fallback = "")
        {
            try
            {
                // JSON 경로는 점(.)을 쓰므로 콜론을 변환합니다.
                JToken? token = _root.Value.SelectToken(key.Replace(':', '.'));
                if (token == null || token.Type == JTokenType.Null) return fallback;

                string value = token.ToString();
                return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            }
            catch
            {
                return fallback;
            }
        }

        // ── 구글 스프레드시트 ────────────────────────────────────────────

        /// <summary>공용(템플릿) 스프레드시트 ID. 비어 있으면 기능이 비활성화됩니다.</summary>
        public static string SpreadsheetId => GetString("SpreadsheetId");

        /// <summary>사용자 개인 DB를 만들 때 복사해 갈 원본 템플릿 ID.</summary>
        public static string TemplateSpreadsheetId =>
            GetString("TemplateSpreadsheetId", SpreadsheetId);

        /// <summary>OAuth 클라이언트 비밀 파일 경로(실행 폴더 기준).</summary>
        public static string CredentialsPath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                         GetString("CredentialsFileName", "credentials.json"));

        /// <summary>OAuth 토큰 저장 폴더.</summary>
        public static string TokenStorePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                         GetString("TokenStoreFolder", "token.json"));

        /// <summary>사용자 전용 스프레드시트 ID를 기억해 두는 파일 경로.</summary>
        public static string UserSheetIdFilePath =>
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "user_sheet_id.txt");

        // ── 시트(탭) 이름 ────────────────────────────────────────────────

        public static string MenuSheetName => GetString("Sheets:Menu", "MenuDatabase");
        public static string DietSheetName => GetString("Sheets:Diet", "식단");
        public static string EventSheetName => GetString("Sheets:Event", "Event");
        public static string PatientSheetName => GetString("Sheets:Patient", "알러지 인원");
        public static string DashboardSheetName => GetString("Sheets:Dashboard", "Dashboard");

        /// <summary>원가 계산용 식재료 단가 시트. A=식단명 B=식재료명 C=분류 D=1인소요량 E=단위 F=단가</summary>
        public static string CostSheetName => GetString("Sheets:Cost", "원가");

        // ── 연령 트랙별 메뉴풀 ──────────────────────────────────────────
        //
        // 식품영양학과에서 정리한 메뉴풀 시트입니다. 열 구성은
        //   A 순번 | B 메뉴ID | C 메뉴명 | D 카테고리 | E 대표출처센터
        //   F 레시피파일유형 | G 전체출처센터 | H 레시피매칭상태 | I 알레르기코드 | J 검수
        // 이 시트가 있으면 앱은 MenuDatabase 대신 이쪽을 읽습니다.

        /// <summary>만 3~5세 메뉴풀 시트.</summary>
        public static string TrackASheetName => GetString("Sheets:TrackA", "트랙A_3-5세");

        /// <summary>만 6~18세 메뉴풀 시트.</summary>
        public static string TrackBSheetName => GetString("Sheets:TrackB", "트랙B_6-18세");

        /// <summary>사용자가 앱에서 직접 추가한 메뉴를 쌓는 시트.</summary>
        public static string UserMenuSheetName => GetString("Sheets:UserMenu", "사용자추가메뉴");

        // ── 공공데이터포털 특일 정보 API ─────────────────────────────────

        /// <summary>공공데이터포털 서비스 키. 비어 있으면 공휴일 조회를 건너뜁니다.</summary>
        public static string HolidayApiKey => GetString("HolidayApiKey");

        public static bool HasHolidayApiKey => !string.IsNullOrWhiteSpace(HolidayApiKey);

        public static bool HasSpreadsheetId => !string.IsNullOrWhiteSpace(SpreadsheetId);
    }
}
