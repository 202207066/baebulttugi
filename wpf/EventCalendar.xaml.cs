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
        // 💡 구글 시트 서비스 객체 (로그인 때 만든 공용 서비스를 공유)
        private readonly GoogleSheetsService _service;
        private SheetsService? sheetsService;

        private string SpreadsheetId => _service.SpreadsheetId;
        /// <summary>실제 일정 탭 이름(없으면 처음 접근할 때 자동 생성).</summary>
        private string SheetName = AppConfig.EventSheetName;

        private List<GoogleSheetRow> currentDayEvents = new List<GoogleSheetRow>();

        // 💡 공공데이터포털 특일 정보 API 설정 (키는 appsettings.json에서 읽습니다)
        private static string OpenApiKey => AppConfig.HolidayApiKey;
        private static readonly HttpClient httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };

        // API 호출 낭비를 막기 위한 캐시 (키: "20260626", 값: "이벤트명")
        private Dictionary<string, string> holidayCache = new Dictionary<string, string>();
        private int currentLoadedYear = -1;
        private int currentLoadedMonth = -1;
        private int _viewVersion;
        private DateTime? _requestedDate;
        private bool _calendarReady;
        private readonly HashSet<DateTime> _eventDates = new();
        private readonly HashSet<DateTime> _mealDates = new();
        private readonly Dictionary<DateTime,string> _eventIcons = new();
        private static readonly string[] EventIcons = ["📌", "🎉", "🎂", "🍁", "🎄", "⭐"];

        public EventCalendar()
        {
            InitializeComponent();

            EventIconPicker.ItemsSource = EventIcons; EventIconPicker.Text = "📌";
            // 페이지에서 따로 인증하지 않고 로그인 때 만든 서비스를 씁니다.
            _service = AppServices.Require();
            sheetsService = _service.Sheets;

            MainCalendar.LayoutUpdated += (_,_) => PaintDates();
            MainCalendar.DisplayDateChanged += async (_,_) => { if(_calendarReady){ await FetchHolidaysForMonthAsync(MainCalendar.DisplayDate.Year,MainCalendar.DisplayDate.Month); PaintDates(); } };
            MainCalendar.SelectedDatesChanged += MainCalendar_SelectedDatesChanged;
            MainCalendar.SelectedDate = DateTime.Today; // 오늘 날짜 기본 선택
            Loaded += async (_, _) => { _calendarReady = true; await UpdateUI(); };
        }

        // 📅 달력에서 날짜를 클릭했을 때 (async 비동기로 변경)
        // EventHandler<SelectionChangedEventArgs>의 sender는 nullable이라 맞춰 줍니다.
        private async void MainCalendar_SelectedDatesChanged(object? sender, SelectionChangedEventArgs e)
        {
            var selected = e.AddedItems.OfType<DateTime>().LastOrDefault();
            if (selected == default) return;
            _requestedDate = selected.Date;
            if (_calendarReady) await UpdateUI(_requestedDate);
        }

        // 🌐 [추가] 공공데이터 API에서 해당 월의 특일(기념일/공휴일) 가져오기
        private async Task FetchHolidaysForMonthAsync(int year, int month)
        {
            foreach(var item in CalendarDates.Holidays(year)) holidayCache[item.Key.ToString("yyyyMMdd")]=item.Value;
            // 이미 이번 달 데이터를 불러왔다면 API 재요청 안 함 (최적화)
            if (currentLoadedYear == year && currentLoadedMonth == month) return;

            // 키가 설정되지 않았으면 공휴일 조회를 건너뜁니다(구글 시트 일정은 그대로 동작).
            if (!AppConfig.HasHolidayApiKey)
            {
                // Keep verified built-in dates when the API is unavailable.
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
                    // Keep verified built-in dates when the API is unavailable.
                    currentLoadedYear = year;
                    currentLoadedMonth = month;
                    return;
                }

                JObject jsonObj = JObject.Parse(json);
                var itemsToken = jsonObj["response"]?["body"]?["items"]?["item"];

                // Keep verified built-in dates when the API is unavailable. // 달이 바뀌었으니 이전 캐시 초기화

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
        private async Task UpdateUI(DateTime? selectedOverride = null)
        {
            int version = ++_viewVersion;
            DateTime? requestedDate = selectedOverride?.Date ?? MainCalendar.SelectedDate?.Date;
            using var activity = AppActivity.Begin("선택한 날짜의 식단과 일정을 확인하는 중입니다…");
            txtEventInput.Clear();

            if (!MainCalendar.SelectedDate.HasValue)
            {
                txtBannerYear.Text = "";
                txtSelectedDate.Text = "날짜를 선택해주세요";
                BadgeHoliday.Visibility = Visibility.Collapsed;
                currentDayEvents.Clear();
                lstEvents.ItemsSource = null;
                RefreshEventListState();
                txtHolidayEvent.Text = "등록된 특별한 이벤트가 없습니다.";
                return;
            }

            DateTime selectedDate = requestedDate!.Value;
            txtBannerYear.Text = selectedDate.ToString("yyyy년");
            txtSelectedDate.Text = selectedDate.ToString("M월 d일 (ddd)");

            // 1️⃣ 공공데이터 API 이벤트 처리
            await FetchHolidaysForMonthAsync(selectedDate.Year, selectedDate.Month);
            if (version != _viewVersion || (_requestedDate.HasValue && _requestedDate.Value != selectedDate)) return;

            string dateKey = selectedDate.ToString("yyyyMMdd"); // "20260626" 포맷
            if (holidayCache.TryGetValue(dateKey, out string? holidayName))
            {
                txtHolidayEvent.Text = holidayName;
                txtBadgeHoliday.Text = holidayName;
                BadgeHoliday.Visibility = Visibility.Visible;
            }
            else
            {
                txtHolidayEvent.Text = "이 날은 지정된 공휴일·절기가 없습니다.";
                BadgeHoliday.Visibility = Visibility.Collapsed;
            }

            string special = CalendarDates.Special(selectedDate);
            if(special.Length>0)txtHolidayEvent.Text=(holidayCache.ContainsKey(dateKey)?txtHolidayEvent.Text+"\n":"")+special;
            PaintDates();
            // 2️⃣ 구글 시트 개인 일정 처리
            if (sheetsService == null) return;

            try
            {
                var allMeals = await _service.GetStoredMealsAsync();
                if (version != _viewVersion) return;
                _mealDates.Clear(); _mealDates.UnionWith(allMeals.Select(m => m.Date.Date));
                var meals = await _service.DisplayMealsAsync(allMeals.Where(m => m.Date.Date == selectedDate.Date).ToList());
                if (version != _viewVersion || (_requestedDate.HasValue && _requestedDate.Value != selectedDate)) return;
                lstTodayMeals.ItemsSource = meals.Count == 0 ? new[] { "아직 식단이 작성되지 않았습니다." } : meals.Select(m => m.Header + "\n" + string.Join("\n", m.Menus) + "\n합계: " + (m.Calories.HasValue ? $"{m.Calories:0.#} kcal" : "열량 미확인")).ToList();
                BtnCreateMeal.Content = meals.Count == 0 ? "식단 만들기" : "이 주의 식단 다시 만들기";
                // 탭이 없으면 헤더까지 갖춰 자동으로 만듭니다.
                SheetName = await _service.EnsureEventSheetAsync();
                if (version != _viewVersion || (_requestedDate.HasValue && _requestedDate.Value != selectedDate)) return;

                string range = $"'{SheetName}'!A:B";
                var request = sheetsService.Spreadsheets.Values.Get(SpreadsheetId, range);
                var response = await request.ExecuteAsync(); // 동기식에서 비동기식으로 변경
                if (version != _viewVersion || (_requestedDate.HasValue && _requestedDate.Value != selectedDate)) return;
                IList<IList<object>> values = response.Values;
                var eventDates = new HashSet<DateTime>();
                if(values!=null)foreach(var eventRow in values)if(eventRow.Count>1 && !string.IsNullOrWhiteSpace(eventRow[1]?.ToString()) && DateTime.TryParse(eventRow[0]?.ToString(),out var eventDate))eventDates.Add(eventDate.Date);
                _eventDates.Clear();_eventDates.UnionWith(eventDates);
                _eventIcons.Clear();
                if(values != null) foreach(var row in values) if(row.Count > 1 && DateTime.TryParse(row[0]?.ToString(), out var day) && !string.IsNullOrWhiteSpace(row[1]?.ToString())) {
                    var content = row[1]?.ToString() ?? "";
                    var icon = EventIconText.LeadingIcon(content) is string parsed && parsed.Length > 0 ? parsed : "📌";
                    if(!_eventIcons.ContainsKey(day.Date)) _eventIcons[day.Date] = icon;
                    else if(!_eventIcons[day.Date].Contains(icon)) _eventIcons[day.Date] += icon;
                }
                PaintDates();
                var foundEvents = new List<GoogleSheetRow>();

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
                                foundEvents.Add(new GoogleSheetRow
                                {
                                    RowIndex = i + 1,
                                    Content = content
                                });
                            }
                        }
                    }
                }

                currentDayEvents = foundEvents;
                lstEvents.SelectedIndex = -1;
                lstEvents.ItemsSource = null;
                lstEvents.ItemsSource = currentDayEvents.Select(e => e.Content).ToList();

                RefreshEventListState();
            }
            catch (Exception ex)
            {
                if (version != _viewVersion || (_requestedDate.HasValue && _requestedDate.Value != selectedDate)) return;
                lstTodayMeals.ItemsSource = new[] { "식단 또는 일정을 불러오지 못했습니다. 날짜를 다시 선택해 주세요." };
                MessageBox.Show($"자료를 불러오지 못했습니다: {ex.Message}", "연결 확인");
            }
        }

        private void PaintDates()
        {
            foreach(var day in Descendants<System.Windows.Controls.Primitives.CalendarDayButton>(MainCalendar)) {
                if(day.DataContext is not DateTime date)continue;
                string? holiday=holidayCache.GetValueOrDefault(date.ToString("yyyyMMdd")) ?? CalendarDates.Holidays(date.Year).GetValueOrDefault(date.Date);
                var mark=CalendarDates.Mark(date,holiday,_eventDates.Contains(date.Date)) with { HasMeal = _mealDates.Contains(date.Date), ConnectPrevious = date.DayOfWeek != DayOfWeek.Sunday && _mealDates.Contains(date.Date.AddDays(-1)), ConnectNext = date.DayOfWeek != DayOfWeek.Saturday && _mealDates.Contains(date.Date.AddDays(1)), EventIcon = _eventIcons.GetValueOrDefault(date.Date, "") };
                if(mark.HasMeal) mark = mark with { Label = mark.Label + " · 식단 작성됨" };
                if(Equals(day.Tag,mark))continue;
                day.Tag=mark;day.ToolTip=mark.Label;
                System.Windows.Automation.AutomationProperties.SetName(day,mark.Label);
            }
        }
        private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
        {
            for(int i=0;i<System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);i++){
                var child=System.Windows.Media.VisualTreeHelper.GetChild(root,i);if(child is T typed)yield return typed;
                foreach(var nested in Descendants<T>(child))yield return nested;
            }
        }

        private void CreateMeal_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is Main main) main.OpenMealBuilder(MainCalendar.SelectedDate);
        }

        /// <summary>일정 개수 배지와 "등록된 일정이 없습니다" 안내를 갱신합니다.</summary>
        private void RefreshEventListState()
        {
            int count = currentDayEvents.Count;

            txtEventCount.Text = count.ToString();
            txtNoEvents.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        // 🖱️ 리스트박스 선택 이벤트
        private void lstEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstEvents.SelectedItem != null)
            {
                var text = lstEvents.SelectedItem.ToString() ?? "";
                var icon = EventIconText.LeadingIcon(text);
                EventIconPicker.Text = icon.Length > 0 ? icon : "📌";
                txtEventInput.Text = icon.Length == 0 ? text : text[icon.Length..].TrimStart();
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
                string range = $"'{SheetName}'!A:B";

                var valueRange = new ValueRange();
                var objectList = new List<object>() { date.ToString("yyyy-MM-dd"), EventIconText.Compose(EventIconPicker.Text, newEvent) };
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

                string range = $"'{SheetName}'!B{targetRowIndex}";
                var valueRange = new ValueRange();
                valueRange.Values = new List<IList<object>> { new List<object> { EventIconText.Compose(EventIconPicker.Text, updatedEvent) } };

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

                    // 예전에는 Values.Clear로 셀 내용만 비웠습니다. 행 자체는 남아
                    // 시트에 빈 줄이 쌓이고, 이후 행 번호가 어긋나 엉뚱한 일정이
                    // 수정·삭제되는 원인이 됐습니다. 행을 실제로 삭제합니다.
                    bool deleted = await _service.DeleteRowAsync(SheetName, targetRowIndex);

                    if (!deleted)
                    {
                        MessageBox.Show($"'{SheetName}' 시트를 찾지 못해 삭제하지 못했습니다.",
                                        "삭제 실패", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

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
