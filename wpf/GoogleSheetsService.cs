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

    /// <summary>
    /// 앱 전체가 공유하는 구글 서비스 보관소.
    ///
    /// 예전에는 페이지마다 credentials.json을 직접 열어 SheetsService를 새로 만들었는데,
    /// 어떤 곳은 OAuth(GoogleWebAuthorizationBroker), 어떤 곳은 서비스 계정
    /// (GoogleCredential.FromStream) 방식이라 같은 파일을 서로 다른 포맷으로 읽었고
    /// 둘 중 하나는 반드시 런타임 예외가 났습니다.
    ///
    /// 이제 로그인 시 만든 GoogleSheetsService 인스턴스 하나를 여기 담아 두고
    /// 모든 페이지가 그것만 사용합니다.
    /// </summary>
    public static class AppServices
    {
        public static GoogleSheetsService? Sheets { get; set; }

        /// <summary>로그인 전에 접근한 경우를 알아채기 쉽도록 예외 메시지를 명확히 합니다.</summary>
        public static GoogleSheetsService Require() =>
            Sheets ?? throw new InvalidOperationException(
                "구글 로그인이 완료되기 전에 시트 서비스에 접근했습니다. " +
                "App 시작 흐름(App.xaml.cs)을 확인하세요.");
    }

    public class GoogleSheetsService
    {
        private string TemplateSpreadsheetId => AppConfig.TemplateSpreadsheetId;
        private string? _userSpreadsheetId;

        private readonly SheetsService _sheetsService;
        private readonly DriveService _driveService;

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

        /// <summary>구글 시트 API 원본 핸들. 각 페이지가 이것을 공유합니다.</summary>
        public SheetsService Sheets => _sheetsService;

        /// <summary>구글 드라이브 API 원본 핸들.</summary>
        public DriveService Drive => _driveService;

        /// <summary>
        /// 실제로 읽고 쓸 스프레드시트 ID.
        /// 사용자 전용 사본이 준비되어 있으면 그것을, 없으면 설정 파일의 공용 ID를 씁니다.
        /// </summary>
        public string SpreadsheetId =>
            string.IsNullOrWhiteSpace(_userSpreadsheetId)
                ? AppConfig.SpreadsheetId
                : _userSpreadsheetId!;

        public bool HasUserDatabase => !string.IsNullOrWhiteSpace(_userSpreadsheetId);

        public void SetUserSpreadsheetId(string id)
        {
            _userSpreadsheetId = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
        }

        /// <summary>
        /// 템플릿 스프레드시트를 사용자 드라이브로 복사해 개인 DB를 만듭니다.
        /// </summary>
        public async Task<string> SetupUserDatabaseAsync()
        {
            if (string.IsNullOrWhiteSpace(TemplateSpreadsheetId))
            {
                throw new InvalidOperationException(
                    "appsettings.json에 TemplateSpreadsheetId(또는 SpreadsheetId)가 설정되어 있지 않습니다.");
            }

            var fileMetadata = new Google.Apis.Drive.v3.Data.File()
            {
                Name = "나만의 스마트 식단 및 알러지 DB"
            };

            var request = _driveService.Files.Copy(fileMetadata, TemplateSpreadsheetId);
            var copiedFile = await request.ExecuteAsync();

            _userSpreadsheetId = copiedFile.Id;
            return _userSpreadsheetId;
        }

        // ── 시트 읽기/쓰기 공통 헬퍼 ────────────────────────────────────
        // 페이지마다 같은 코드를 반복하지 않도록 여기에 모읍니다.

        /// <summary>지정한 범위를 읽어 옵니다. 값이 없으면 빈 목록을 돌려줍니다.</summary>
        public async Task<IList<IList<object>>> GetValuesAsync(string range)
        {
            var request = _sheetsService.Spreadsheets.Values.Get(SpreadsheetId, range);
            ValueRange response = await request.ExecuteAsync();
            return response.Values ?? new List<IList<object>>();
        }

        /// <summary>범위 끝에 행을 덧붙입니다.</summary>
        public async Task AppendRowAsync(string range, IList<object> row)
        {
            var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
            var request = _sheetsService.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
        }

        /// <summary>범위를 주어진 값으로 덮어씁니다.</summary>
        public async Task UpdateValuesAsync(string range, IList<IList<object>> values)
        {
            var valueRange = new ValueRange { Values = values };
            var request = _sheetsService.Spreadsheets.Values.Update(valueRange, SpreadsheetId, range);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
        }

        /// <summary>범위의 값을 비웁니다(행 자체는 남습니다).</summary>
        public async Task ClearValuesAsync(string range)
        {
            var request = _sheetsService.Spreadsheets.Values.Clear(
                new ClearValuesRequest(), SpreadsheetId, range);
            await request.ExecuteAsync();
        }

        /// <summary>시트(탭) 제목으로 내부 sheetId를 찾습니다. 없으면 null.</summary>
        public async Task<int?> GetSheetIdByTitleAsync(string title)
        {
            var spreadsheet = await _sheetsService.Spreadsheets.Get(SpreadsheetId).ExecuteAsync();
            if (spreadsheet.Sheets == null) return null;

            foreach (var sheet in spreadsheet.Sheets)
            {
                if (string.Equals(sheet.Properties?.Title, title, StringComparison.Ordinal))
                {
                    return sheet.Properties?.SheetId;
                }
            }
            return null;
        }

        /// <summary>
        /// 행을 실제로 삭제합니다(내용만 비우는 Clear와 달리 아래 행이 위로 당겨집니다).
        /// </summary>
        /// <param name="sheetTitle">탭 이름</param>
        /// <param name="rowIndexOneBased">시트 화면에 보이는 행 번호(1부터)</param>
        public async Task<bool> DeleteRowAsync(string sheetTitle, int rowIndexOneBased)
        {
            int? sheetId = await GetSheetIdByTitleAsync(sheetTitle);
            if (sheetId == null) return false;

            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        DeleteDimension = new DeleteDimensionRequest
                        {
                            Range = new DimensionRange
                            {
                                SheetId = sheetId,
                                Dimension = "ROWS",
                                // API는 0부터 세고 끝 인덱스는 제외됩니다.
                                StartIndex = rowIndexOneBased - 1,
                                EndIndex = rowIndexOneBased
                            }
                        }
                    }
                }
            };

            await _sheetsService.Spreadsheets.BatchUpdate(request, SpreadsheetId).ExecuteAsync();
            return true;
        }

        /// <summary>새 시트(탭)를 추가합니다.</summary>
        public async Task AddSheetAsync(string title)
        {
            var request = new BatchUpdateSpreadsheetRequest
            {
                Requests = new List<Request>
                {
                    new Request
                    {
                        AddSheet = new AddSheetRequest
                        {
                            Properties = new SheetProperties { Title = title }
                        }
                    }
                }
            };
            await _sheetsService.Spreadsheets.BatchUpdate(request, SpreadsheetId).ExecuteAsync();
        }

        /// <summary>스프레드시트에 있는 모든 탭 이름.</summary>
        public async Task<List<string>> GetSheetTitlesAsync()
        {
            var titles = new List<string>();
            var spreadsheet = await _sheetsService.Spreadsheets.Get(SpreadsheetId).ExecuteAsync();
            if (spreadsheet.Sheets != null)
            {
                foreach (var sheet in spreadsheet.Sheets)
                {
                    string? title = sheet.Properties?.Title;
                    if (!string.IsNullOrEmpty(title)) titles.Add(title!);
                }
            }
            return titles;
        }

        // ── 대시보드 ────────────────────────────────────────────────────

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var data = new DashboardData();

            if (string.IsNullOrEmpty(SpreadsheetId))
            {
                data.DietStatus = "DB 미배포 상태";
                data.TodayMenu.Add("DB 초기화 및 배포가 필요합니다.");
                return data;
            }

            try
            {
                string range = $"'{AppConfig.DashboardSheetName}'!A2:E2";
                IList<IList<object>> values = await GetValuesAsync(range);

                if (values.Count > 0)
                {
                    var row = values[0];
                    if (row.Count > 0 && row[0] != null) data.TotalPatients = row[0].ToString() + " 명";
                    if (row.Count > 1 && row[1] != null) data.AllergyPatients = row[1].ToString() + " 명";
                    if (row.Count > 2 && row[2] != null) data.DietStatus = row[2].ToString() ?? "미구성";

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

        // ── 피급식자(알러지) 명단 ───────────────────────────────────────

        /// <summary>
        /// 알러지 명단 시트에서 등록된 모든 알러지 성분을 모읍니다.
        /// 식단 자동 조합에서 제외 대상을 정할 때 사용합니다.
        /// </summary>
        public async Task<HashSet<string>> GetRegisteredAllergensAsync()
        {
            var allergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string range = $"'{AppConfig.PatientSheetName}'!A:E";
                IList<IList<object>> values = await GetValuesAsync(range);

                foreach (var row in values)
                {
                    if (row.Count <= 3) continue;

                    string cell = row[3]?.ToString() ?? "";
                    foreach (var part in cell.Split(new[] { ',', '/', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        string token = part.Trim();
                        // 헤더 행과 "없음" 같은 값은 알러지원이 아닙니다.
                        if (token.Length == 0) continue;
                        if (token == "없음" || token == "특이 알러지 성분") continue;
                        allergens.Add(token);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[알러지 명단 로드 실패] {ex.Message}");
            }

            return allergens;
        }
    }
}
