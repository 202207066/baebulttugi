using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>로그인한 구글 계정 정보.</summary>
    public class GoogleUserInfo
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhotoUrl { get; set; } = string.Empty;

        /// <summary>화면에 표시할 이름. 이름이 없으면 이메일 아이디 부분을 씁니다.</summary>
        public string FriendlyName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(DisplayName)) return DisplayName;

                int at = Email.IndexOf('@');
                if (at > 0) return Email.Substring(0, at);

                return "사용자";
            }
        }

        /// <summary>아바타 원 안에 넣을 한 글자.</summary>
        public string Initial
        {
            get
            {
                string name = FriendlyName;
                return name.Length > 0 ? name.Substring(0, 1).ToUpperInvariant() : "?";
            }
        }
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
        /// 로그인한 구글 계정 정보. LoadCurrentUserAsync() 호출 전에는 null입니다.
        /// 화면 우측 상단과 사이드바에 항상 표시됩니다.
        /// </summary>
        public GoogleUserInfo? CurrentUser { get; private set; }

        /// <summary>
        /// 로그인한 사용자의 이름과 이메일을 가져옵니다.
        ///
        /// 별도의 profile/email 스코프를 추가하지 않고, 이미 받아 둔 드라이브
        /// 권한으로 about.get을 호출합니다. 스코프가 늘어나면 사용자에게
        /// 동의 화면이 다시 뜨므로 일부러 이 방법을 씁니다.
        /// </summary>
        public async Task<GoogleUserInfo> LoadCurrentUserAsync()
        {
            try
            {
                var request = _driveService.About.Get();
                request.Fields = "user(displayName,emailAddress,photoLink)";
                var about = await request.ExecuteAsync();

                CurrentUser = new GoogleUserInfo
                {
                    DisplayName = about?.User?.DisplayName ?? "",
                    Email = about?.User?.EmailAddress ?? "",
                    PhotoUrl = about?.User?.PhotoLink ?? ""
                };
            }
            catch (Exception ex)
            {
                // 이름을 못 가져와도 프로그램은 정상 동작해야 합니다.
                System.Diagnostics.Debug.WriteLine($"[사용자 정보 조회 실패] {ex.Message}");
                CurrentUser = new GoogleUserInfo();
            }

            return CurrentUser;
        }

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

            // 시트가 바뀌면 이전 시트에서 확인한 탭 이름 캐시는 무효입니다.
            _resolvedSheets.Clear();
            CreatedSheets.Clear();
        }

        /// <summary>현재 연결된 스프레드시트를 구글 시트에서 여는 주소.</summary>
        public string SpreadsheetUrl =>
            string.IsNullOrWhiteSpace(SpreadsheetId)
                ? ""
                : $"https://docs.google.com/spreadsheets/d/{SpreadsheetId}/edit";

        /// <summary>
        /// 붙여넣은 주소에서 스프레드시트 ID만 뽑아냅니다.
        /// 전체 URL도, ID만 붙여넣어도 동작합니다.
        ///
        ///   https://docs.google.com/spreadsheets/d/1AbC.../edit?gid=0#gid=0  →  1AbC...
        ///   1AbC...                                                          →  1AbC...
        /// </summary>
        public static string ParseSpreadsheetId(string urlOrId)
        {
            if (string.IsNullOrWhiteSpace(urlOrId)) return "";

            string text = urlOrId.Trim();

            var match = System.Text.RegularExpressions.Regex.Match(
                text, @"/spreadsheets/d/([a-zA-Z0-9\-_]+)");
            if (match.Success) return match.Groups[1].Value;

            // 주소가 아니면 ID 자체로 봅니다(구글 ID에 쓰이는 문자만 허용).
            if (System.Text.RegularExpressions.Regex.IsMatch(text, @"^[a-zA-Z0-9\-_]{20,}$"))
                return text;

            return "";
        }

        /// <summary>스프레드시트 파일 이름을 가져옵니다.</summary>
        public async Task<string> GetSpreadsheetTitleAsync(string? spreadsheetId = null)
        {
            string id = string.IsNullOrWhiteSpace(spreadsheetId) ? SpreadsheetId : spreadsheetId!;
            if (string.IsNullOrWhiteSpace(id)) return "";

            var request = _sheetsService.Spreadsheets.Get(id);
            request.Fields = "properties.title";
            var spreadsheet = await request.ExecuteAsync();

            return spreadsheet?.Properties?.Title ?? "";
        }

        /// <summary>
        /// 이 계정으로 해당 스프레드시트를 열 수 있는지 확인합니다.
        /// 성공하면 파일 이름을, 실패하면 사유를 돌려줍니다.
        /// </summary>
        public async Task<(bool Ok, string Title, string Error)> CheckSpreadsheetAsync(string spreadsheetId)
        {
            if (string.IsNullOrWhiteSpace(spreadsheetId))
                return (false, "", "스프레드시트 주소 또는 ID를 입력해 주세요.");

            try
            {
                string title = await GetSpreadsheetTitleAsync(spreadsheetId);
                return (true, title, "");
            }
            catch (Google.GoogleApiException ex)
            {
                string reason = ex.HttpStatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound =>
                        "그런 스프레드시트를 찾을 수 없습니다. 주소를 다시 확인해 주세요.",
                    System.Net.HttpStatusCode.Forbidden =>
                        "이 구글 계정에 그 시트를 볼 권한이 없습니다. 시트 소유자에게 편집자로 초대해 달라고 요청하세요.",
                    _ => ex.Message
                };
                return (false, "", reason);
            }
            catch (Exception ex)
            {
                return (false, "", ex.Message);
            }
        }

        /// <summary>
        /// 템플릿을 복사해 새 데이터베이스를 만듭니다. 이름을 직접 정할 수 있습니다.
        /// </summary>
        public async Task<string> CreateDatabaseAsync(string fileName)
        {
            if (string.IsNullOrWhiteSpace(TemplateSpreadsheetId))
            {
                throw new InvalidOperationException(
                    "appsettings.json에 TemplateSpreadsheetId(또는 SpreadsheetId)가 설정되어 있지 않습니다.");
            }

            var metadata = new Google.Apis.Drive.v3.Data.File
            {
                Name = string.IsNullOrWhiteSpace(fileName)
                    ? "스마트 식단 및 알러지 DB"
                    : fileName.Trim()
            };

            var copied = await _driveService.Files.Copy(metadata, TemplateSpreadsheetId).ExecuteAsync();

            SetUserSpreadsheetId(copied.Id);
            return copied.Id;
        }

        /// <summary>
        /// 앱이 사용하는 탭 5개가 모두 있는지 확인하고, 없는 것은 만듭니다.
        /// 각 탭의 (이름, 새로 만들었는지)를 돌려줍니다.
        /// </summary>
        public async Task<List<(string Title, bool Created)>> EnsureAllSheetsAsync()
        {
            var before = new HashSet<string>(CreatedSheets, StringComparer.Ordinal);
            var result = new List<(string, bool)>();

            string menu = await EnsureMenuSheetAsync();
            result.Add((menu, CreatedSheets.Contains(menu) && !before.Contains(menu)));

            string patient = await EnsurePatientSheetAsync();
            result.Add((patient, CreatedSheets.Contains(patient) && !before.Contains(patient)));

            string diet = await EnsureDietSheetAsync();
            result.Add((diet, CreatedSheets.Contains(diet) && !before.Contains(diet)));

            string evt = await EnsureEventSheetAsync();
            result.Add((evt, CreatedSheets.Contains(evt) && !before.Contains(evt)));

            string cost = await EnsureSheetAsync(AppConfig.CostSheetName,
                "식단명", "식재료명", "분류", "1인당 소요량", "단위", "단가");
            result.Add((cost, CreatedSheets.Contains(cost) && !before.Contains(cost)));

            return result;
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

        // ── 시트(탭) 자동 준비 ──────────────────────────────────────────

        private readonly Dictionary<string, string> _resolvedSheets =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>이번 실행에서 새로 만든 시트 이름들.</summary>
        public List<string> CreatedSheets { get; } = new List<string>();

        /// <summary>
        /// 탭 이름 비교용 정규화.
        ///
        /// 공백·언더바를 없애고, 눈으로는 구별되지 않는 여러 종류의 하이픈
        /// (－ ‐ ‑ – — 등)을 보통 하이픈으로 통일한 뒤 소문자로 만듭니다.
        /// "트랙A_3-5세"와 "트랙A 3–5세"가 같은 것으로 취급되도록 하기 위함입니다.
        /// </summary>
        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";

            var sb = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsWhiteSpace(c) || c == '_') continue;

                // 유니코드 대시 계열을 전부 '-' 로
                if (c == '\u2010' || c == '\u2011' || c == '\u2012' || c == '\u2013' ||
                    c == '\u2014' || c == '\u2015' || c == '\uFF0D' || c == '\u2212')
                {
                    sb.Append('-');
                    continue;
                }

                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 필요한 시트(탭)가 있는지 확인하고, 없으면 헤더까지 넣어 새로 만듭니다.
        /// 실제로 사용할 시트 이름을 돌려줍니다.
        ///
        /// 이 처리가 없으면 탭 이름이 조금만 달라도 구글이
        /// "Unable to parse range: '알러지 인원'!A:E" 같은 BadRequest를 던지고,
        /// 사용자는 무엇을 고쳐야 하는지 알 수 없습니다.
        ///
        /// 1) 이름이 정확히 같은 탭이 있으면 그대로 사용
        /// 2) 공백·대소문자만 다른 탭이 있으면(예: "알러지인원") 그 탭을 사용
        /// 3) 둘 다 없으면 새로 만들고 헤더 행을 씁니다
        /// </summary>
        public async Task<string> EnsureSheetAsync(string wantedTitle, params string[] headers)
        {
            if (string.IsNullOrWhiteSpace(wantedTitle)) return wantedTitle;

            if (_resolvedSheets.TryGetValue(wantedTitle, out string? cached)) return cached;

            List<string> titles = await GetSheetTitlesAsync();

            string? found = titles.FirstOrDefault(t => string.Equals(t, wantedTitle, StringComparison.Ordinal));

            if (found == null)
            {
                string wanted = Normalize(wantedTitle);
                found = titles.FirstOrDefault(t => Normalize(t) == wanted);

                if (found != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[시트 이름 보정] 설정값 '{wantedTitle}' → 실제 탭 '{found}'");
                }
            }

            if (found == null)
            {
                await AddSheetAsync(wantedTitle);

                if (headers.Length > 0)
                {
                    await UpdateValuesAsync(
                        $"'{wantedTitle}'!A1",
                        new List<IList<object>> { headers.Cast<object>().ToList() });
                }

                found = wantedTitle;
                CreatedSheets.Add(wantedTitle);
            }

            _resolvedSheets[wantedTitle] = found;
            return found;
        }

        /// <summary>알러지 명단 시트를 준비하고 실제 이름을 돌려줍니다.</summary>
        public Task<string> EnsurePatientSheetAsync() =>
            EnsureSheetAsync(AppConfig.PatientSheetName,
                             "ID", "성명", "구분", "특이 알러지 성분", "비고(메모)");

        /// <summary>메뉴 DB 시트를 준비하고 실제 이름을 돌려줍니다.</summary>
        public Task<string> EnsureMenuSheetAsync() =>
            EnsureSheetAsync(AppConfig.MenuSheetName,
                             "메뉴명", "칼로리", "재료", "유발 알러지",
                             "분류", "탄수화물(g)", "단백질(g)", "지방(g)");

        /// <summary>일정 시트를 준비하고 실제 이름을 돌려줍니다.</summary>
        public Task<string> EnsureEventSheetAsync() =>
            EnsureSheetAsync(AppConfig.EventSheetName, "날짜", "내용");

        /// <summary>저장된 추천 식단 시트를 준비하고 실제 이름을 돌려줍니다.</summary>
        public Task<string> EnsureDietSheetAsync() =>
            EnsureSheetAsync(AppConfig.DietSheetName,
                             "구분", "밥", "국", "주찬", "부찬", "칼로리");

        // ── 메뉴풀 읽기 ─────────────────────────────────────────────────

        /// <summary>어느 연령 트랙의 메뉴를 쓸지.</summary>
        public enum AgeTrack { A3to5, B6to18, All }

        /// <summary>
        /// 시트(탭)가 있으면 실제 이름을, 없으면 null을 돌려줍니다.
        /// EnsureSheetAsync와 달리 새로 만들지 않습니다.
        /// </summary>
        public async Task<string?> ResolveSheetTitleAsync(string wantedTitle)
        {
            if (string.IsNullOrWhiteSpace(wantedTitle)) return null;

            List<string> titles = await GetSheetTitlesAsync();

            string? found = titles.FirstOrDefault(t => string.Equals(t, wantedTitle, StringComparison.Ordinal));
            if (found != null) return found;

            string wanted = Normalize(wantedTitle);
            return titles.FirstOrDefault(t => Normalize(t) == wanted);
        }

        /// <summary>
        /// 자동 조합과 알러지 점검이 사용할 메뉴 목록을 모읍니다.
        ///
        /// 우선순위
        ///   1) 식품영양학과 메뉴풀 시트(트랙A / 트랙B)가 있으면 그것을 읽습니다.
        ///   2) 사용자가 앱에서 추가한 메뉴 시트가 있으면 함께 더합니다.
        ///   3) 트랙 시트가 하나도 없으면 기본 MenuDatabase 형식으로 읽습니다.
        ///      (템플릿으로 새로 만든 빈 DB를 쓰는 급식소를 위한 경로)
        /// </summary>
        /// <summary>
        /// 직전 GetMenuPoolAsync가 실제로 읽은 시트 이름들.
        /// "메뉴가 부족합니다" 같은 안내에서 원인을 짚어 주는 데 씁니다.
        /// </summary>
        public List<string> LastPoolSources { get; } = new List<string>();

        /// <summary>
        /// 트랙 시트를 찾습니다. 설정값과 정확히 같은 이름이 없으면,
        /// "트랙A"/"트랙B"로 시작하는 탭을 찾습니다.
        ///
        /// 실제 시트의 탭 이름은 «트랙A_3-5세» 처럼 조금씩 다르게 적히는 경우가
        /// 많아(하이픈 종류, 언더바, 띄어쓰기) 접두사까지 허용합니다.
        /// </summary>
        public async Task<string?> ResolveTrackSheetAsync(string configuredTitle, string prefix)
        {
            string? exact = await ResolveSheetTitleAsync(configuredTitle);
            if (exact != null) return exact;

            List<string> titles = await GetSheetTitlesAsync();
            string wantedPrefix = Normalize(prefix);

            return titles.FirstOrDefault(t => Normalize(t).StartsWith(wantedPrefix, StringComparison.Ordinal));
        }

        public async Task<List<MenuItem>> GetMenuPoolAsync(AgeTrack track)
        {
            var pool = new List<MenuItem>();
            LastPoolSources.Clear();

            string? trackA = await ResolveTrackSheetAsync(AppConfig.TrackASheetName, "트랙A");
            string? trackB = await ResolveTrackSheetAsync(AppConfig.TrackBSheetName, "트랙B");

            if (trackA != null && (track == AgeTrack.A3to5 || track == AgeTrack.All))
            {
                var rows = await ReadTrackSheetAsync(trackA, "트랙A(3-5세)");
                pool.AddRange(rows);
                LastPoolSources.Add($"{trackA} ({rows.Count}개)");
            }

            if (trackB != null && (track == AgeTrack.B6to18 || track == AgeTrack.All))
            {
                var rows = await ReadTrackSheetAsync(trackB, "트랙B(6-18세)");
                pool.AddRange(rows);
                LastPoolSources.Add($"{trackB} ({rows.Count}개)");
            }

            // 사용자가 앱에서 직접 추가한 메뉴
            string? userSheet = await ResolveSheetTitleAsync(AppConfig.UserMenuSheetName);
            if (userSheet != null)
            {
                var rows = await ReadMenuDatabaseSheetAsync(userSheet, "사용자 추가");
                pool.AddRange(rows);
                if (rows.Count > 0) LastPoolSources.Add($"{userSheet} ({rows.Count}개)");
            }

            // 메뉴풀 시트가 전혀 없는 DB라면 기본 형식으로 읽습니다.
            if (trackA == null && trackB == null)
            {
                string menuSheet = await EnsureMenuSheetAsync();
                var rows = await ReadMenuDatabaseSheetAsync(menuSheet, "");
                pool.AddRange(rows);
                LastPoolSources.Add($"{menuSheet} ({rows.Count}개) — 트랙 시트를 찾지 못해 기본 형식으로 읽음");
            }

            return pool;
        }

        /// <summary>
        /// 메뉴풀 시트 한 장을 읽습니다.
        ///   A 순번 | B 메뉴ID | C 메뉴명 | D 카테고리 | E 대표출처센터
        ///   F 파일유형 | G 전체출처 | H 매칭상태 | I 알레르기코드 | J 검수
        ///
        /// 시트 중간에 «── 밥류 (294개) ──» 같은 구분 행이 끼어 있으므로,
        /// 메뉴ID가 «A-0001» 형태인 행만 데이터로 인정합니다.
        /// </summary>
        private async Task<List<MenuItem>> ReadTrackSheetAsync(string sheetTitle, string trackLabel)
        {
            var list = new List<MenuItem>();

            try
            {
                var values = await GetValuesAsync($"'{sheetTitle}'!A1:J");

                foreach (var row in values)
                {
                    string menuId = Cell(row, 1);
                    if (!System.Text.RegularExpressions.Regex.IsMatch(menuId, @"^[A-Za-z]+(-[A-Za-z]+)?-\d+$"))
                        continue;

                    string name = Cell(row, 2);
                    if (name.Length == 0) continue;

                    list.Add(new MenuItem
                    {
                        MenuId = menuId,
                        Name = name,
                        Category = Cell(row, 3),
                        Source = Cell(row, 4),
                        Allergy = AllergyCodes.Decode(Cell(row, 8)),
                        Track = trackLabel
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[메뉴풀 '{sheetTitle}' 읽기 실패] {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// 기본 MenuDatabase 형식을 읽습니다.
        ///   A 메뉴명 | B 칼로리 | C 재료 | D 유발알러지 | E 분류 | F 탄수 | G 단백 | H 지방
        /// </summary>
        private async Task<List<MenuItem>> ReadMenuDatabaseSheetAsync(string sheetTitle, string trackLabel)
        {
            var list = new List<MenuItem>();

            try
            {
                var values = await GetValuesAsync($"'{sheetTitle}'!A2:H");

                foreach (var row in values)
                {
                    string name = Cell(row, 0);
                    if (name.Length == 0) continue;

                    list.Add(new MenuItem
                    {
                        Name = name,
                        Calories = ParseNumber(Cell(row, 1)),
                        Materials = Cell(row, 2),
                        Allergy = Cell(row, 3),
                        Category = Cell(row, 4),
                        Carb = ParseNumber(Cell(row, 5)),
                        Protein = ParseNumber(Cell(row, 6)),
                        Fat = ParseNumber(Cell(row, 7)),
                        Track = trackLabel
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[메뉴 '{sheetTitle}' 읽기 실패] {ex.Message}");
            }

            return list;
        }

        /// <summary>"320 kcal", "12.5g" 같은 문자열에서 숫자만 뽑습니다.</summary>
        private static double ParseNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;

            string cleaned = System.Text.RegularExpressions.Regex.Replace(raw, @"[^0-9.]", "");
            if (cleaned.Length == 0) return 0;

            int firstDot = cleaned.IndexOf('.');
            if (firstDot >= 0)
            {
                cleaned = cleaned.Substring(0, firstDot + 1) +
                          cleaned.Substring(firstDot + 1).Replace(".", "");
            }

            return double.TryParse(cleaned, out double value) ? value : 0;
        }

        /// <summary>사용자가 앱에서 추가한 메뉴를 저장할 시트를 준비합니다.</summary>
        public Task<string> EnsureUserMenuSheetAsync() =>
            EnsureSheetAsync(AppConfig.UserMenuSheetName,
                             "메뉴명", "칼로리", "재료", "유발 알러지",
                             "분류", "탄수화물(g)", "단백질(g)", "지방(g)");

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
        /// 알러지 명단 시트를 읽어 피급식자 목록을 돌려줍니다.
        ///   A ID | B 성명 | C 구분 | D 특이 알러지 성분 | E 비고
        /// 첫 행의 A열이 숫자가 아니면 헤더로 보고 건너뜁니다.
        /// </summary>
        public async Task<List<PatientModel>> GetPatientsAsync()
        {
            var list = new List<PatientModel>();

            // 탭이 없으면 여기서 헤더까지 갖춰 만들어 둡니다.
            string sheet = await EnsurePatientSheetAsync();

            IList<IList<object>> values = await GetValuesAsync($"'{sheet}'!A:E");
            if (values.Count == 0) return list;

            int startIndex = 0;
            string firstCell = values[0].Count > 0 ? values[0][0]?.ToString() ?? "" : "";
            if (!int.TryParse(firstCell, out _)) startIndex = 1;

            for (int i = startIndex; i < values.Count; i++)
            {
                var row = values[i];
                if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]?.ToString())) continue;

                list.Add(new PatientModel
                {
                    Id = int.TryParse(row[0]?.ToString(), out int parsedId) ? parsedId : i,
                    Name = Cell(row, 1),
                    Category = row.Count > 2 ? Cell(row, 2) : "일반",
                    Allergies = Cell(row, 3),
                    Note = Cell(row, 4)
                });
            }

            return list;
        }

        private static string Cell(IList<object> row, int index) =>
            row.Count > index ? (row[index]?.ToString() ?? "").Trim() : "";

        /// <summary>알러지 성분 문자열을 개별 토큰으로 나눕니다.</summary>
        public static IEnumerable<string> SplitAllergens(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) yield break;

            foreach (var part in raw.Split(new[] { ',', '/', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = part.Trim();
                if (token.Length == 0) continue;
                // "없음"과 헤더 문구는 알러지원이 아닙니다.
                if (token == "없음" || token == "특이 알러지 성분") continue;
                yield return token;
            }
        }

        /// <summary>
        /// 등록된 모든 알러지 성분. 식단 자동 조합에서 제외 대상을 정할 때 씁니다.
        /// </summary>
        public async Task<HashSet<string>> GetRegisteredAllergensAsync()
        {
            var allergens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var patient in await GetPatientsAsync())
                {
                    foreach (var token in SplitAllergens(patient.Allergies)) allergens.Add(token);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[알러지 명단 로드 실패] {ex.Message}");
            }

            return allergens;
        }

        /// <summary>
        /// 알러지 성분별로 대상자 이름을 묶습니다. 대시보드 카드에 사용합니다.
        /// 대상자가 많은 성분부터 정렬합니다.
        /// </summary>
        public async Task<List<(string Allergen, List<string> Names)>> GetAllergyGroupsAsync()
        {
            var groups = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            try
            {
                foreach (var patient in await GetPatientsAsync())
                {
                    foreach (var token in SplitAllergens(patient.Allergies))
                    {
                        if (!groups.TryGetValue(token, out var names))
                        {
                            names = new List<string>();
                            groups[token] = names;
                        }
                        if (!string.IsNullOrWhiteSpace(patient.Name)) names.Add(patient.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[알러지 그룹 집계 실패] {ex.Message}");
            }

            return groups
                .OrderByDescending(kv => kv.Value.Count)
                .ThenBy(kv => kv.Key, StringComparer.Ordinal)
                .Select(kv => (kv.Key, kv.Value))
                .ToList();
        }
    }
}
