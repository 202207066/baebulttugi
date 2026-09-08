using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Newtonsoft.Json.Linq; // 💡 공공데이터 API JSON 파싱용 (NuGet에서 Newtonsoft.Json 설치 필수)

namespace wpf
{
    public partial class EventCalendar : Page
    {
        // 💡 구글 시트 서비스 객체
        private SheetsService? sheetsService;
        private static string SpreadsheetId => AppConfig.SpreadsheetId;
        private static string SheetName => AppConfig.EventSheetName;

        private List<GoogleSheetRow> currentDayEvents = new List<GoogleSheetRow>();

        // 💡 공공데이터포털 특일 정보 API 설정 (키는 appsettings.json에서 읽습니다)
        private static string OpenApiKey => AppConfig.HolidayApiKey;
        private static readonly HttpClient httpClient = new HttpClient();

        // API 호출 낭비를 막기 위한 캐시 (키: "20260626", 값: "이벤트명")
        private Dictionary<string, string> holidayCache = new Dictionary<string, string>();
        private int currentLoadedYear = -1;
        private int currentLoadedMonth = -1;

        public EventCalendar()
        {
            InitializeComponent();
            InitializeGoogleSheets();

            MainCalendar.SelectedDatesChanged += MainCalendar_SelectedDatesChanged;
            MainCalendar.SelectedDate = DateTime.Today; // 오늘 날짜 기본 선택
        }

        // 🔑 구글 API 인증 및 서비스 초기화 (기존 동일)
        private void InitializeGoogleSheets()
        {
            try
            {
                string[] scopes = { SheetsService.Scope.Spreadsheets };
                string keyFilePath = AppConfig.CredentialsPath;

                GoogleCredential credential;
                using (var stream = new FileStream(keyFilePath, FileMode.Open, FileAccess.Read))
                {
                    credential = GoogleCredential.FromStream(stream).CreateScoped(scopes);
                }

                sheetsService = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "WPF Event Calendar"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"구글 시트 연결 실패: {ex.Message}", "오류");
            }
        }

        // 📅 달력에서 날짜를 클릭했을 때 (async 비동기로 변경)
        private async void MainCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateUI();
        }

        // 🌐 [추가] 공공데이터 API에서 해당 월의 특일(기념일/공휴일) 가져오기
        private async Task FetchHolidaysForMonthAsync(int year, int month)
        {
            // 이미 이번 달 데이터를 불러왔다면 API 재요청 안 함 (최적화)
            if (currentLoadedYear == year && currentLoadedMonth == month) return;

            // 키가 설정되지 않았으면 공휴일 조회를 건너뜁니다(구글 시트 일정은 그대로 동작).
            if (!AppConfig.HasHolidayApiKey)
            {
                holidayCache.Clear();
                currentLoadedYear = year;
                currentLoadedMonth = month;
                return;
            }

            // API 요청 주소 조립 (평문 http → https, 키는 URL 인코딩)
            string url =
                "https://apis.data.go.kr/B090041/openapi/service/SpcdeInfoService/getRestDeInfo" +
                $"?serviceKey={Uri.EscapeDataString(OpenApiKey)}" +
                $"&solYear={year}&solMonth={month:D2}&_type=json&numOfRows=50";

            try
            {
                HttpResponseMessage response = await httpClient.GetAsync(url);
                string json = await response.Content.ReadAsStringAsync();

                // 키가 잘못되면 공공데이터포털은 200 응답에 XML 오류문서를 실어 보냅니다.
                // 그대로 JObject.Parse하면 예외가 나므로 미리 걸러냅니다.
                if (!response.IsSuccessStatusCode || !json.TrimStart().StartsWith("{"))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[특일정보 API] 예상과 다른 응답입니다. status={(int)response.StatusCode}, body={json.Substring(0, Math.Min(200, json.Length))}");
                    holidayCache.Clear();
                    currentLoadedYear = year;
                    currentLoadedMonth = month;
                    return;
                }

                JObject jsonObj = JObject.Parse(json);
                var itemsToken = jsonObj["response"]?["body"]?["items"]?["item"];

                holidayCache.Clear(); // 달이 바뀌었으니 이전 캐시 초기화

                if (itemsToken != null)
                {
                    // 공공데이터 API는 항목이 여러 개면 Array, 1개면 Object로 주는 골때리는 특징이 있어서 분기 처리함
                    if (itemsToken is JArray itemsArray)
                    {
                        foreach (var item in itemsArray)
                        {
                            string date = item["locdate"]?.ToString() ?? "";
                            string name = item["dateName"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(date)) holidayCache[date] = name;
                        }
                    }
                    else if (itemsToken is JObject singleItem)
                    {
                        string date = singleItem["locdate"]?.ToString() ?? "";
                        string name = singleItem["dateName"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(date)) holidayCache[date] = name;
                    }
                }

                currentLoadedYear = year;
                currentLoadedMonth = month;
            }
            catch (Exception ex)
            {
                // 공휴일 조회 실패는 치명적이지 않으므로 화면을 막지 않고 로그만 남깁니다.
                System.Diagnostics.Debug.WriteLine($"[특일정보 API] 연동 실패(무시됨): {ex.Message}");
            }
        }

        // 🔄 화면 갱신 (API 통신을 위해 async Task로 변경됨)
        private async Task UpdateUI()
        {
            txtEventInput.Clear();
            currentDayEvents.Clear();

            if (!MainCalendar.SelectedDate.HasValue)
            {
                txtSelectedDate.Text = "날짜를 선택해주세요";
                lstEvents.ItemsSource = null;
                txtHolidayEvent.Text = "등록된 특별한 이벤트가 없습니다.";
                return;
            }

            DateTime selectedDate = MainCalendar.SelectedDate.Value;
            txtSelectedDate.Text = selectedDate.ToString("yyyy년 MM월 dd일 (ddd)");

            // 1️⃣ 공공데이터 API 이벤트 처리
            await FetchHolidaysForMonthAsync(selectedDate.Year, selectedDate.Month);

            string dateKey = selectedDate.ToString("yyyyMMdd"); // "20260626" 포맷
            if (holidayCache.TryGetValue(dateKey, out string holidayName))
            {
                txtHolidayEvent.Text = $"🎉 {holidayName}";
            }
            else
            {
                txtHolidayEvent.Text = "등록된 특별한 이벤트가 없습니다.";
            }

            // 2️⃣ 구글 시트 개인 일정 처리
            if (sheetsService == null) return;

            try
            {
                string range = $"{SheetName}!A:B";
                var request = sheetsService.Spreadsheets.Values.Get(SpreadsheetId, range);
                var response = await request.ExecuteAsync(); // 동기식에서 비동기식으로 변경
                IList<IList<object>> values = response.Values;

                if (values != null && values.Count > 0)
                {
                    string targetDateStr = selectedDate.ToString("yyyy-MM-dd");

                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i].Count > 0 && values[i][0]?.ToString() == targetDateStr)
                        {
                            string content = values[i].Count > 1 ? values[i][1]?.ToString() ?? "" : "";

                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                currentDayEvents.Add(new GoogleSheetRow
                                {
                                    RowIndex = i + 1,
                                    Content = content
                                });
                            }
                        }
                    }
                }

                lstEvents.ItemsSource = null;
                lstEvents.ItemsSource = currentDayEvents.Select(e => e.Content).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"데이터를 불러오지 못했습니다: {ex.Message}", "오류");
            }
        }

        // 🖱️ 리스트박스 선택 이벤트
        private void lstEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstEvents.SelectedItem != null)
            {
                txtEventInput.Text = lstEvents.SelectedItem.ToString();
            }
        }

        // ➕ 일정 추가
        private async void btnAddEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sheetsService == null || !MainCalendar.SelectedDate.HasValue) return;

            string newEvent = txtEventInput.Text.Trim();
            if (string.IsNullOrEmpty(newEvent))
            {
                MessageBox.Show("일정 내용을 입력해주세요.", "알림");
                return;
            }

            try
            {
                DateTime date = MainCalendar.SelectedDate.Value;
                string range = $"{SheetName}!A:B";

                var valueRange = new ValueRange();
                var objectList = new List<object>() { date.ToString("yyyy-MM-dd"), newEvent };
                valueRange.Values = new List<IList<object>> { objectList };

                var appendRequest = sheetsService.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                await appendRequest.ExecuteAsync();

                await UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"일정 등록 실패: {ex.Message}", "오류");
            }
        }

        // 🔄 일정 수정
        private async void btnEditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sheetsService == null || !MainCalendar.SelectedDate.HasValue || lstEvents.SelectedIndex == -1) return;

            string updatedEvent = txtEventInput.Text.Trim();
            if (string.IsNullOrEmpty(updatedEvent))
            {
                MessageBox.Show("수정할 내용을 입력해주세요.", "알림");
                return;
            }

            try
            {
                int selectedIndex = lstEvents.SelectedIndex;
                int targetRowIndex = currentDayEvents[selectedIndex].RowIndex;

                string range = $"{SheetName}!B{targetRowIndex}";
                var valueRange = new ValueRange();
                valueRange.Values = new List<IList<object>> { new List<object> { updatedEvent } };

                var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, SpreadsheetId, range);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                await updateRequest.ExecuteAsync();

                await UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"일정 수정 실패: {ex.Message}", "오류");
            }
        }

        // 🗑️ 일정 삭제
        private async void btnDeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sheetsService == null || !MainCalendar.SelectedDate.HasValue || lstEvents.SelectedIndex == -1) return;

            if (MessageBox.Show("선택한 일정을 정말 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    int selectedIndex = lstEvents.SelectedIndex;
                    int targetRowIndex = currentDayEvents[selectedIndex].RowIndex;

                    string range = $"{SheetName}!A{targetRowIndex}:B{targetRowIndex}";
                    var clearRequest = sheetsService.Spreadsheets.Values.Clear(new ClearValuesRequest(), SpreadsheetId, range);
                    await clearRequest.ExecuteAsync();

                    await UpdateUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"일정 삭제 실패: {ex.Message}", "오류");
                }
            }
        }
    }

    public class GoogleSheetRow
    {
        public int RowIndex { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
