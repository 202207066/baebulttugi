using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace wpf
{
    /// <summary>
    /// 간단한 입력 창. WPF에는 WinForms의 InputBox 같은 기본 대화상자가 없어
    /// 코드로 최소한의 것을 만들어 씁니다.
    ///
    /// 후보 목록을 넘기면 콤보박스(직접 입력 가능)로, 넘기지 않으면
    /// 일반 텍스트 입력으로 표시됩니다.
    /// </summary>
    public static class PromptDialog
    {
        /// <summary>
        /// 입력을 받아 문자열을 돌려줍니다. 취소하면 null.
        /// </summary>
        public static string? Show(Window? owner, string title, string message,
                                   IEnumerable<string>? suggestions = null)
        {
            var window = new Window
            {
                Title = title,
                Width = 420,
                SizeToContent = SizeToContent.Height,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = owner == null
                    ? WindowStartupLocation.CenterScreen
                    : WindowStartupLocation.CenterOwner,
                Owner = owner
            };

            var panel = new StackPanel { Margin = new Thickness(16) };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 10)
            });

            // 후보가 있으면 편집 가능한 콤보박스, 없으면 텍스트 상자
            ComboBox? combo = null;
            TextBox? textBox = null;

            if (suggestions != null)
            {
                combo = new ComboBox { IsEditable = true, Height = 30 };
                foreach (var s in suggestions) combo.Items.Add(s);
                panel.Children.Add(combo);
            }
            else
            {
                textBox = new TextBox { Height = 30, VerticalContentAlignment = VerticalAlignment.Center };
                panel.Children.Add(textBox);
            }

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 14, 0, 0)
            };

            var okButton = new Button { Content = "확인", Width = 80, Height = 30, IsDefault = true };
            var cancelButton = new Button
            {
                Content = "취소",
                Width = 80,
                Height = 30,
                IsCancel = true,
                Margin = new Thickness(8, 0, 0, 0)
            };

            okButton.Click += (_, _) => { window.DialogResult = true; };

            buttons.Children.Add(okButton);
            buttons.Children.Add(cancelButton);
            panel.Children.Add(buttons);

            window.Content = panel;

            window.Loaded += (_, _) =>
            {
                if (combo != null) combo.Focus();
                else textBox?.Focus();
            };

            if (window.ShowDialog() != true) return null;

            string result = combo != null
                ? (combo.Text ?? string.Empty)
                : (textBox?.Text ?? string.Empty);

            result = result.Trim();
            return result.Length == 0 ? null : result;
        }
    }
}
