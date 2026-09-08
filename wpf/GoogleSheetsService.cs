using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Drive.v3;

namespace wpf
{
    public class DashboardData
    {
        public string TotalPatients { get; set; } = "0 명";
        public string AllergyPatients { get; set; } = "0 명";
        public string DietStatus { get; set; } = "미구성";
        public List<string> TodayMenu { get; set; } = new List<string>();
        public List<string> TodayAlternativeMenu { get; set; } = new List<string>();
    }

    public class GoogleSheetsService
    {
        // 설정 파일(appsettings.json)에서 읽습니다. 저장소에 ID를 박아 넣지 않습니다.
        private string TemplateSpreadsheetId => AppConfig.TemplateSpreadsheetId;
        private string? _userSpreadsheetId;

        private SheetsService _sheetsService;
        private DriveService _driveService;

        public GoogleSheetsService(UserCredential credential)
        {
            _sheetsService = new SheetsService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "WPF Diet Management System",
            });

            _driveService = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "WPF Diet Management System",
            });
        }

        // 💡 누락되었던 메서드 추가 완료
        public void SetUserSpreadsheetId(string id)
        {
            _userSpreadsheetId = id;
        }

        public async Task<string> SetupUserDatabaseAsync()
        {
            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = "나만의 스마트 식단 및 알러지 DB"
            };

            if (string.IsNullOrWhiteSpace(TemplateSpreadsheetId))
            {
                throw new InvalidOperationException(
                    "appsettings.json에 TemplateSpreadsheetId(또는 SpreadsheetId)가 설정되어 있지 않습니다.");
            }

            var request = _driveService.Files.Copy(fileMetadata, TemplateSpreadsheetId);
            var copiedFile = await request.ExecuteAsync();

            _userSpreadsheetId = copiedFile.Id;
            return _userSpreadsheetId;
        }

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var data = new DashboardData();

            if (string.IsNullOrEmpty(_userSpreadsheetId))
            {
                data.DietStatus = "DB 미배포 상태";
                data.TodayMenu.Add("DB 초기화 및 배포가 필요합니다.");
                return data;
            }

            try
            {
                string range = $"'{AppConfig.DashboardSheetName}'!A2:E2";
                SpreadsheetsResource.ValuesResource.GetRequest request = _sheetsService.Spreadsheets.Values.Get(_userSpreadsheetId, range);

                ValueRange response = await request.ExecuteAsync();
                IList<IList<object>> values = response.Values;

                if (values != null && values.Count > 0)
                {
                    var row = values[0];
                    if (row.Count > 0 && row[0] != null) data.TotalPatients = row[0].ToString() + " 명";
                    if (row.Count > 1 && row[1] != null) data.AllergyPatients = row[1].ToString() + " 명";
                    if (row.Count > 2 && row[2] != null) data.DietStatus = row[2].ToString();

                    if (row.Count > 3 && row[3] != null)
                    {
                        string rawMenu = row[3].ToString() ?? "";
                        string[] menuArray = rawMenu.Split(new char[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (var menu in menuArray) data.TodayMenu.Add(menu.Trim());
                    }

                    if (row.Count > 4 && row[4] != null)
                    {
                        string rawAlt = row[4].ToString() ?? "";
                        if (!string.IsNullOrWhiteSpace(rawAlt))
                        {
                            string[] altArray = rawAlt.Split(new char[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (var alt in altArray) data.TodayAlternativeMenu.Add(alt.Trim());
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GoogleSheetsService Error] {ex.Message}");
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