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

    public partial class GoogleSheetsService
    {
        private string TemplateSpreadsheetId => AppConfig.TemplateSpreadsheetId;
        private string? _userSpreadsheetId;

        private readonly UserCredential? _credential;
        private readonly SheetsService _sheetsService;
        private readonly DriveService _driveService;

        public GoogleSheetsService(UserCredential credential)
        {
            _credential = credential;
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
        public SheetsService Sheets => !IsLocal ? _sheetsService : throw new InvalidOperationException("로컬 버전은 Google 연결을 사용하지 않습니다.");

        /// <summary>구글 드라이브 API 원본 핸들.</summary>
        public DriveService Drive => !IsLocal ? _driveService : throw new InvalidOperationException("로컬 버전은 Google 연결을 사용하지 않습니다.");

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
            if (IsLocal) return CurrentUser = new GoogleUserInfo { DisplayName = "이 PC 사용자", Email = "로컬 저장 · Google 로그인 없음" };
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
            _userSpreadsheetId ?? throw new InvalidOperationException("개인 DB를 먼저 준비해 주세요.");

        public bool HasUserDatabase => !string.IsNullOrWhiteSpace(_userSpreadsheetId);

        public void SetUserSpreadsheetId(string id)
        {
            _userSpreadsheetId = string.IsNullOrWhiteSpace(id) ? null : id.Trim();
            _resolvedSheets.Clear();
            InvalidateSheetTitles();
        }

        /// <summary>
        /// 로그인한 계정에 빈 개인 DB를 만듭니다. 공공 원본은 복사하지 않습니다.
        /// </summary>
        public async Task<string> SetupUserDatabaseAsync(CancellationToken cancellationToken = default)
        {
            var book = await CreateBookAsync(new Spreadsheet {
                Properties = new SpreadsheetProperties { Title = "배불뚝이 · 내 식단 자료" },
                Sheets = [new Sheet { Properties = new SheetProperties { Title = "사용 안내" }, Data = [new GridData {
                    RowData = new List<RowData> {
                        new() { Values = new List<CellData> { TextCell("배불뚝이 개인 자료") } },
                        new() { Values = new List<CellData> { TextCell("이 파일은 로그인한 본인 계정에 생성됩니다. 대상자·식단·직접 수정한 레시피는 이 파일에 저장됩니다.") } },
                        new() { Values = new List<CellData> { TextCell("공식 메뉴 자료는 앱에 포함되어 있으며 개인 자료와 별도로 업데이트됩니다.") } }
                    }
                }] }]
            }, cancellationToken);
            SetUserSpreadsheetId(book.SpreadsheetId);
            return book.SpreadsheetId;
        }

        // ── 시트 읽기/쓰기 공통 헬퍼 ────────────────────────────────────
        // 페이지마다 같은 코드를 반복하지 않도록 여기에 모읍니다.

        /// <summary>지정한 범위를 읽어 옵니다. 값이 없으면 빈 목록을 돌려줍니다.</summary>
        public Task<IList<IList<object>>> GetValuesAsync(string range) => ReadValuesAsync(SpreadsheetId, range);

        /// <summary>범위 끝에 행을 덧붙입니다.</summary>
        public async Task AppendRowAsync(string range, IList<object> row)
        {
            RequirePersonalWrite();
            if (_localStore != null) { _localStore.UpdateValues(SpreadsheetId, range, [row], append: true); return; }
            var valueRange = new ValueRange { Values = new List<IList<object>> { row } };
            var request = _sheetsService.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
        }

        /// <summary>범위를 주어진 값으로 덮어씁니다.</summary>
        public async Task UpdateValuesAsync(string range, IList<IList<object>> values)
        {
            RequirePersonalWrite();
            if (_localStore != null) { _localStore.UpdateValues(SpreadsheetId, range, values); return; }
            var valueRange = new ValueRange { Values = values };
            var request = _sheetsService.Spreadsheets.Values.Update(valueRange, SpreadsheetId, range);
            request.ValueInputOption =
                SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
            await request.ExecuteAsync();
        }

        /// <summary>범위의 값을 비웁니다(행 자체는 남습니다).</summary>
        public async Task ClearValuesAsync(string range)
        {
            RequirePersonalWrite();
            if (_localStore != null) { _localStore.ClearValues(SpreadsheetId, [range]); return; }
            var request = _sheetsService.Spreadsheets.Values.Clear(
                new ClearValuesRequest(), SpreadsheetId, range);
            await request.ExecuteAsync();
        }

        /// <summary>시트(탭) 제목으로 내부 sheetId를 찾습니다. 없으면 null.</summary>
        public async Task<int?> GetSheetIdByTitleAsync(string title)
        {
            var spreadsheet = await ReadBookAsync(SpreadsheetId);
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
            RequirePersonalWrite();
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

            await ApplyBookChangesAsync(request, SpreadsheetId);
            return true;
        }

        /// <summary>새 시트(탭)를 추가합니다.</summary>
        public async Task AddSheetAsync(string title)
        {
            RequirePersonalWrite();
            string spreadsheetId = SpreadsheetId;
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
            await ApplyBookChangesAsync(request, spreadsheetId).ConfigureAwait(false);
            InvalidateSheetTitles(spreadsheetId);
        }

        // ── 시트(탭) 자동 준비 ──────────────────────────────────────────

        private readonly Dictionary<string, string> _resolvedSheets =
            new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>이번 실행에서 새로 만든 시트 이름들.</summary>
        public List<string> CreatedSheets { get; } = new List<string>();

        /// <summary>비교용으로 공백을 없애고 소문자로 만듭니다.</summary>
        private static string Normalize(string s) =>
            new string(s.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToLowerInvariant();

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
            EnsureSheetAsync(AppConfig.RecipeSheetName,
                             "메뉴명", "칼로리", "재료", "유발 알러지",
                             "분류", "탄수화물(g)", "단백질(g)", "지방(g)");

        /// <summary>일정 시트를 준비하고 실제 이름을 돌려줍니다.</summary>
        public Task<string> EnsureEventSheetAsync() =>
            EnsureSheetAsync(AppConfig.EventSheetName, "날짜", "내용");

        /// <summary>저장된 추천 식단 시트를 준비하고 실제 이름을 돌려줍니다.</summary>
        public Task<string> EnsureDietSheetAsync() =>
            EnsureSheetAsync(AppConfig.DietSheetName,
                             "구분", "밥", "국", "주찬", "부찬", "칼로리");

        private readonly System.Threading.SemaphoreSlim _sheetTitlesGate = new(1, 1);
        private readonly object _sheetTitlesSync = new();
        private readonly Dictionary<string, (DateTime Time, string[] Titles)> _sheetTitlesCache = new(StringComparer.Ordinal);
        private long _sheetTitlesVersion;

        private void InvalidateSheetTitles(string? id = null)
        {
            var owner = FacilityRegistry;
            lock (owner._sheetTitlesSync)
            {
                owner._sheetTitlesVersion++;
                if (id == null) owner._sheetTitlesCache.Clear();
                else owner._sheetTitlesCache.Remove(id);
            }
        }

        /// <summary>급식소별 탭 목록을 45초간 공유하고 동시 요청을 합칩니다.</summary>
        public async Task<List<string>> GetSheetTitlesAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            string id = SpreadsheetId;
            var owner = FacilityRegistry;
            await owner._sheetTitlesGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                long version;
                lock (owner._sheetTitlesSync)
                {
                    if (!forceRefresh && owner._sheetTitlesCache.TryGetValue(id, out var cached) && DateTime.UtcNow - cached.Time < TimeSpan.FromSeconds(45))
                        return cached.Titles.ToList();
                    version = owner._sheetTitlesVersion;
                }
                Spreadsheet spreadsheet;
                try { spreadsheet = await ReadBookAsync(id, cancellationToken).ConfigureAwait(false); }
                catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1.5), cancellationToken).ConfigureAwait(false);
                    spreadsheet = await ReadBookAsync(id, cancellationToken).ConfigureAwait(false);
                }
                var titles = spreadsheet.Sheets?.Select(sheet => sheet.Properties?.Title)
                    .Where(title => !string.IsNullOrEmpty(title)).Select(title => title!).ToArray() ?? [];
                lock (owner._sheetTitlesSync)
                {
                    // 탭 생성·책 전환 중 시작했던 응답으로 무효화된 캐시를 되살리지 않습니다.
                    if (version == owner._sheetTitlesVersion) owner._sheetTitlesCache[id] = (DateTime.UtcNow, titles);
                }
                return titles.ToList();
            }
            finally { owner._sheetTitlesGate.Release(); }
        }

        // ── 대시보드 ────────────────────────────────────────────────────

        public async Task<DashboardData> GetDashboardDataAsync()
        {
            var stored = (await GetStoredMealsAsync(DateTime.Today)).Where(m => m.Date == DateTime.Today).ToList();
            var meals = await DisplayMealsAsync(stored);
            return new DashboardData
            {
                DietStatus = meals.Count == 0 ? "아직 작성되지 않음" : $"{meals.Count}끼 작성됨",
                TodayMenu = meals.Select(m => m.Header + "\n" + string.Join(", ", m.Menus)).ToList()
            };
        }

        // ── 피급식자(알러지) 명단 ───────────────────────────────────────

        /// <summary>
        /// 알러지 명단 시트를 읽어 피급식자 목록을 돌려줍니다.
        ///   A ID | B 성명 | C 구분 | D 특이 알러지 성분 | E 비고
        /// 첫 행의 A열이 숫자가 아니면 헤더로 보고 건너뜁니다.
        /// </summary>
        public async Task<List<PatientModel>> GetPatientsAsync()
        {
            string sheet = await EnsurePatientSheetAsync();
            var rows = await ReadPatientRowsAsync(sheet);
            var result = PatientSheetCodec.Parse(rows);
            _patientSnapshot = System.Text.Json.JsonSerializer.Serialize(rows);
            return result;
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
                if (PatientAllergyInfo.IsNone(token) || token == "특이 알러지 성분") continue;
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
