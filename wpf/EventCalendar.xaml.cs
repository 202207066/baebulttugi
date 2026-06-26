using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;

namespace wpf
{
    public partial class EventCalendar : Page
    {
        // 💡 구글 시트 서비스 객체 (Null 허용 경고 해결을 위해 ? 추가)
        private SheetsService? sheetsService;

        // ⚠️ [필수 확인] 본인의 구글 스프레드시트 ID로 변경하셔야 합니다!
        private readonly string SpreadsheetId = "1Z-h4zeyDL3IbjbJj4KsSH7tU1AioWabI2iI0Momo2P8";
        private readonly string SheetName = "Event"; // 구글 시트 하단 탭 이름

        // 화면 표시에 사용할 현재 선택된 날짜의 일정 리스트 (인덱스 추적용)
        private List<GoogleSheetRow> currentDayEvents = new List<GoogleSheetRow>();

        public EventCalendar()
        {
            InitializeComponent();
            InitializeGoogleSheets();

            // 이벤트 연결
            MainCalendar.SelectedDatesChanged += MainCalendar_SelectedDatesChanged;

            // 페이지 로드 시 오늘 날짜를 기본으로 선택
            MainCalendar.SelectedDate = DateTime.Today;
        }

        // 🔑 구글 API 인증 및 서비스 초기화
        private void InitializeGoogleSheets()
        {
            try
            {
                string[] scopes = { SheetsService.Scope.Spreadsheets };
                string keyFilePath = "credentials.json"; // 실행 파일(bin\Debug 등)과 같은 경로에 위치해야 함

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

        // 📅 달력에서 날짜를 클릭했을 때
        private void MainCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        // 🔄 화면 갱신: 구글 시트에서 데이터를 읽어와 표시
        private void UpdateUI()
        {
            txtEventInput.Clear();
            currentDayEvents.Clear();

            if (sheetsService == null)
            {
                MessageBox.Show("구글 서비스가 초기화되지 않았습니다.", "알림");
                return;
            }

            if (!MainCalendar.SelectedDate.HasValue)
            {
                txtSelectedDate.Text = "날짜를 선택해주세요";
                lstEvents.ItemsSource = null;
                return;
            }

            DateTime selectedDate = MainCalendar.SelectedDate.Value;
            txtSelectedDate.Text = selectedDate.ToString("yyyy년 MM월 dd일");

            try
            {
                // 시트의 모든 데이터 가져오기 (A열: 날짜, B열: 내용)
                string range = $"{SheetName}!A:B";
                SpreadsheetsResource.ValuesResource.GetRequest request = sheetsService.Spreadsheets.Values.Get(SpreadsheetId, range);
                ValueRange response = request.Execute();
                IList<IList<object>> values = response.Values;

                if (values != null && values.Count > 0)
                {
                    string targetDateStr = selectedDate.ToString("yyyy-MM-dd");

                    // 행 번호(Row Index)를 기억하면서 선택한 날짜와 일치하는 일정 필터링
                    for (int i = 0; i < values.Count; i++)
                    {
                        if (values[i].Count > 0 && values[i][0]?.ToString() == targetDateStr)
                        {
                            string content = values[i].Count > 1 ? values[i][1]?.ToString() : "";

                            // 빈 데이터가 아니라면 리스트에 추가 (삭제된 행 걸러내기)
                            if (!string.IsNullOrWhiteSpace(content))
                            {
                                currentDayEvents.Add(new GoogleSheetRow
                                {
                                    RowIndex = i + 1, // 구글 시트는 인덱스가 1부터 시작함
                                    Content = content
                                });
                            }
                        }
                    }
                }

                // 리스트박스 바인딩 (내용 텍스트만 추출하여 바인딩)
                lstEvents.ItemsSource = null;
                lstEvents.ItemsSource = currentDayEvents.Select(e => e.Content).ToList();
            }
            catch (Exception ex)
            {
                // ⚠️ 오타가 있던 주석 및 코드 라인 완벽 제거 완료
                MessageBox.Show($"데이터를 불러오지 못했습니다: {ex.Message}", "오류");
            }
        }

        // 🖱️ 리스트박스에서 특정 일정을 클릭했을 때 -> 입력창으로 내용 가져오기
        private void lstEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstEvents.SelectedItem != null)
            {
                txtEventInput.Text = lstEvents.SelectedItem.ToString();
            }
        }

        // ➕ 구글 시트에 새 일정 추가 (맨 아래 행에 추가)
        private void btnAddEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sheetsService == null) return;

            if (!MainCalendar.SelectedDate.HasValue)
            {
                MessageBox.Show("일정을 등록할 날짜를 달력에서 먼저 선택하세요.", "알림");
                return;
            }

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

                // Append 요청 생성 및 실행
                var appendRequest = sheetsService.Spreadsheets.Values.Append(valueRange, SpreadsheetId, range);
                appendRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.AppendRequest.ValueInputOptionEnum.USERENTERED;
                appendRequest.Execute();

                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"일정 등록 실패: {ex.Message}", "오류");
            }
        }

        // 🔄 구글 시트의 특정 행 수정
        private void btnEditEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sheetsService == null) return;

            if (!MainCalendar.SelectedDate.HasValue || lstEvents.SelectedIndex == -1)
            {
                MessageBox.Show("수정할 일정을 위의 리스트에서 먼저 클릭해주세요.", "알림");
                return;
            }

            string updatedEvent = txtEventInput.Text.Trim();
            if (string.IsNullOrEmpty(updatedEvent))
            {
                MessageBox.Show("수정할 내용을 입력해주세요.", "알림");
                return;
            }

            try
            {
                // 현재 선택된 항목의 실제 구글 시트 행 번호 가져오기
                int selectedIndex = lstEvents.SelectedIndex;
                int targetRowIndex = currentDayEvents[selectedIndex].RowIndex;

                string range = $"{SheetName}!B{targetRowIndex}"; // B열(내용)만 수정
                var valueRange = new ValueRange();
                valueRange.Values = new List<IList<object>> { new List<object> { updatedEvent } };

                var updateRequest = sheetsService.Spreadsheets.Values.Update(valueRange, SpreadsheetId, range);
                updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.USERENTERED;
                updateRequest.Execute();

                UpdateUI();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"일정 수정 실패: {ex.Message}", "오류");
            }
        }

        // 🗑️ 구글 시트의 특정 행 삭제 (데이터 지우기)
        private void btnDeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (sheetsService == null) return;

            if (!MainCalendar.SelectedDate.HasValue || lstEvents.SelectedIndex == -1)
            {
                MessageBox.Show("삭제할 일정을 위의 리스트에서 먼저 클릭해주세요.", "알림");
                return;
            }

            if (MessageBox.Show("선택한 일정을 정말 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                try
                {
                    int selectedIndex = lstEvents.SelectedIndex;
                    int targetRowIndex = currentDayEvents[selectedIndex].RowIndex;

                    // A열과 B열의 데이터를 빈칸(Clear)으로 처리합니다.
                    string range = $"{SheetName}!A{targetRowIndex}:B{targetRowIndex}";
                    var clearRequest = sheetsService.Spreadsheets.Values.Clear(new ClearValuesRequest(), SpreadsheetId, range);
                    clearRequest.Execute();

                    UpdateUI();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"일정 삭제 실패: {ex.Message}", "오류");
                }
            }
        }
    }

    // 📋 구글 시트의 몇 번째 줄(Row)에 저장되어 있는지 추적하기 위한 데이터 모델 클래스
    public class GoogleSheetRow
    {
        public int RowIndex { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
