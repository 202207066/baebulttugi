using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace wpf
{
    public partial class EventCalendar : Page
    {
        // 💡 핵심: 특정 날짜(Key)에 여러 개의 일정(Value 리스트)을 저장하는 딕셔너리
        private Dictionary<DateTime, List<string>> eventDatabase = new Dictionary<DateTime, List<string>>();

        public EventCalendar()
        {
            InitializeComponent();

            // 이벤트 연결 (XAML에서 설정해도 되지만 C#에서 깔끔하게 연결)
            MainCalendar.SelectedDatesChanged += MainCalendar_SelectedDatesChanged;

            // 페이지 로드 시 오늘 날짜를 기본으로 선택
            MainCalendar.SelectedDate = DateTime.Today;
        }

        // 📅 달력에서 날짜를 클릭했을 때
        private void MainCalendar_SelectedDatesChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateUI();
        }

        // 🔄 화면 갱신 통합 메서드 (날짜가 바뀌거나 데이터가 변경될 때마다 호출)
        private void UpdateUI()
        {
            if (MainCalendar.SelectedDate.HasValue)
            {
                DateTime selectedDate = MainCalendar.SelectedDate.Value;
                txtSelectedDate.Text = selectedDate.ToString("yyyy년 MM월 dd일");

                // 해당 날짜에 데이터가 있으면 리스트박스에 뿌려줌
                if (eventDatabase.ContainsKey(selectedDate))
                {
                    lstEvents.ItemsSource = null;
                    lstEvents.ItemsSource = eventDatabase[selectedDate];
                }
                else
                {
                    lstEvents.ItemsSource = null; // 없으면 비움
                }
            }
            else
            {
                txtSelectedDate.Text = "날짜를 선택해주세요";
                lstEvents.ItemsSource = null;
            }

            // 입력창 비우기
            txtEventInput.Clear();
        }

        // 🖱️ 리스트박스에서 특정 일정을 클릭했을 때 -> 입력창으로 내용 가져오기
        private void lstEvents_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstEvents.SelectedItem != null)
            {
                txtEventInput.Text = lstEvents.SelectedItem.ToString();
            }
        }

        // ➕ 새 일정 등록
        private void btnAddEvent_Click(object sender, RoutedEventArgs e)
        {
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

            DateTime date = MainCalendar.SelectedDate.Value;

            // 해당 날짜에 첫 등록이라면 새 리스트 방을 만들어줌
            if (!eventDatabase.ContainsKey(date))
            {
                eventDatabase[date] = new List<string>();
            }

            eventDatabase[date].Add(newEvent);
            UpdateUI(); // 등록 후 화면 싹 새로고침
        }

        // 🔄 선택한 일정 수정
        private void btnEditEvent_Click(object sender, RoutedEventArgs e)
        {
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

            DateTime date = MainCalendar.SelectedDate.Value;
            int selectedIndex = lstEvents.SelectedIndex;

            // 해당 인덱스의 텍스트를 입력창에 적힌 내용으로 덮어쓰기
            eventDatabase[date][selectedIndex] = updatedEvent;
            UpdateUI();
        }

        // 🗑️ 선택한 일정 삭제
        private void btnDeleteEvent_Click(object sender, RoutedEventArgs e)
        {
            if (!MainCalendar.SelectedDate.HasValue || lstEvents.SelectedIndex == -1)
            {
                MessageBox.Show("삭제할 일정을 위의 리스트에서 먼저 클릭해주세요.", "알림");
                return;
            }

            if (MessageBox.Show("선택한 일정을 정말 삭제하시겠습니까?", "삭제 확인", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                DateTime date = MainCalendar.SelectedDate.Value;
                int selectedIndex = lstEvents.SelectedIndex;

                eventDatabase[date].RemoveAt(selectedIndex);

                // 만약 일정을 다 지워서 리스트가 텅 비었다면 날짜(Key) 자체를 삭제
                if (eventDatabase[date].Count == 0)
                {
                    eventDatabase.Remove(date);
                }

                UpdateUI();
            }
        }
    }
}