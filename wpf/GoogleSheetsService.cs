using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace wpf
{
    /// <summary>
    /// 대시보드 화면에 바인딩할 데이터 모델 클래스
    /// </summary>
    public class DashboardData
    {
        public string TotalPatients { get; set; } = "0 명";
        public string AllergyPatients { get; set; } = "0 명";
        public string DietStatus { get; set; } = "미구성";
        public List<string> TodayMenu { get; set; } = new List<string>();
        // 시트에서 별도 컬럼으로 대치(대안) 메뉴를 제공하는 경우 저장합니다.
        public List<string> TodayAlternativeMenu { get; set; } = new List<string>();
    }

    /// <summary>
    /// 구글 스프레드시트 API 연동을 담당하는 서비스 클래스
    /// </summary>
    public class GoogleSheetsService
    {
        // ⚠️ [필수 변경] 본인의 구글 스프레드시트 URL에 있는 고유 ID를 입력하세요.
        // 예: https://docs.google.com/spreadsheets/d/이부분이_스프레드시트_ID입니다/edit
        private readonly string _spreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";
       

        private readonly SheetsService _service;

        public GoogleSheetsService()
        {
            // 구글 클라우드 콘솔에서 다운로드한 서비스 계정 JSON 키 파일 이름
            string credentialPath = "credentials.json";

            if (!File.Exists(credentialPath))
            {
                throw new FileNotFoundException($"인증 키 파일을 찾을 수 없습니다: {credentialPath}\n프로젝트 출력 디렉토리에 복사되었는지 확인하세요.");
            }

            // 구글 API 인증 자격 증명 생성 (읽기 전용 권한)
            GoogleCredential credential;
            using (var stream = new FileStream(credentialPath, FileMode.Open, FileAccess.Read))
            {
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(SheetsService.Scope.SpreadsheetsReadonly);
            }

            // 구글 시트 서비스 객체 초기화
            _service = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "WPF Diet Management System",
            });
        }

        /// <summary>
        /// 구글 시트에서 대시보드 요약 정보 및 식단 데이터를 비동기로 조회합니다.
        /// </summary>
        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var data = new DashboardData();

            try
            {
                // ⚠️ [시트 구조 정의] 'Dashboard' 시트의 A2부터 E2 범위를 읽어옵니다.
                // A2: 총 피급식자수 (예: 128)
                // B2: 알러지 환자수 (예: 24)
                // C2: 식단 구성 상태 (예: 구성 완료 (대치 3건))
                // D2: 오늘의 메뉴 목록 (예: 밥, 국, 반찬 등을 쉼표(,)나 줄바꿈으로 입력)
                // E2: 오늘의 대치(대안) 메뉴 목록 (알레르기 대치용으로 별도 입력)
                string range = "Dashboard!A2:E2";

                SpreadsheetsResource.ValuesResource.GetRequest request = _service.Spreadsheets.Values.Get(_spreadsheetId, range);
                ValueRange response = await request.ExecuteAsync();
                IList<IList<object>> values = response.Values;

                // 데이터가 비어있지 않은 경우 파싱 진행
                if (values != null && values.Count > 0)
                {
                    var row = values[0];

                    if (row.Count > 0 && row[0] != null)
                        data.TotalPatients = row[0].ToString() + " 명";

                    if (row.Count > 1 && row[1] != null)
                        data.AllergyPatients = row[1].ToString() + " 명";

                    if (row.Count > 2 && row[2] != null)
                        data.DietStatus = row[2].ToString();

                    if (row.Count > 3 && row[3] != null)
                    {
                        // 텍스트에 포함된 줄바꿈(\n)이나 쉼표(,)를 기준으로 메뉴를 쪼개어 리스트에 삽입
                        string rawMenu = row[3].ToString();
                        string[] menuArray = rawMenu.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var menu in menuArray)
                        {
                            data.TodayMenu.Add(menu.Trim());
                        }
                    }

                    // E열(인덱스 4)에 대치 메뉴가 있으면 파싱하여 TodayAlternativeMenu에 추가
                    if (row.Count > 4 && row[4] != null && !string.IsNullOrWhiteSpace(row[4].ToString()))
                    {
                        string rawAlt = row[4].ToString().Trim();
                        string[] altArray = rawAlt.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);

                        foreach (var alt in altArray)
                        {
                            data.TodayAlternativeMenu.Add(alt.Trim());
                        }
                    }
                    else
                    {
                        // 위에서 파싱되지 않았다면 E2 범위를 별도로 요청해 강제 확인합니다.
                        try
                        {
                            var altRequest = _service.Spreadsheets.Values.Get(_spreadsheetId, "Dashboard!E2:E2");
                            ValueRange altResponse = await altRequest.ExecuteAsync();
                            var altValues = altResponse.Values;
                            if (altValues != null && altValues.Count > 0 && altValues[0].Count > 0 && altValues[0][0] != null)
                            {
                                var altRaw = altValues[0][0].ToString().Trim();
                                if (!string.IsNullOrWhiteSpace(altRaw))
                                {
                                    string[] altArray2 = altRaw.Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    foreach (var a in altArray2)
                                        data.TodayAlternativeMenu.Add(a.Trim());
                                }
                            }
                        }
                        catch
                        {
                            // 별도 요청 실패 시 무시하고 빈 리스트 유지
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // 실무 환경에서는 로그 파일이나 디버그 콘솔에 에러를 기록합니다.
                System.Diagnostics.Debug.WriteLine($"[GoogleSheetsService Error] {ex.Message}");

                // 에러 발생 시 프로그램이 튕기지 않도록 기본값 배치 후 리턴
                data.TotalPatients = "오류 명";
                data.AllergyPatients = "오류 명";
                data.DietStatus = "데이터 로드 실패";
                data.TodayMenu = new List<string> { "❌ 데이터를 불러오지 못했습니다." };
                data.TodayAlternativeMenu = new List<string> { "❌ 데이터를 불러오지 못했습니다." };
            }

            return data;
        }
    }
}