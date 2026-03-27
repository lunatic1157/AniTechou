using System.Windows;
using System.Windows.Controls;

namespace AniTechou.Windows
{
    public partial class AppMessageDialog : Window
    {
        public AppMessageDialog(string title, string message, bool showCancel = false, string confirmText = "确定", string cancelText = "取消")
        {
            InitializeComponent();

            DialogTitleText.Text = title;
            DialogMessageText.Text = message;

            var confirmButton = new Button
            {
                Content = confirmText,
                Width = 88,
                Height = 36,
                Margin = new Thickness(0, 0, showCancel ? 10 : 0, 0),
                Style = (Style)FindResource("AppPrimaryButtonStyle")
            };
            confirmButton.Click += (_, _) =>
            {
                DialogResult = true;
                Close();
            };
            ButtonPanel.Children.Add(confirmButton);

            if (showCancel)
            {
                var cancelButton = new Button
                {
                    Content = cancelText,
                    Width = 88,
                    Height = 36,
                    Style = (Style)FindResource("AppSecondaryButtonStyle")
                };
                cancelButton.Click += (_, _) =>
                {
                    DialogResult = false;
                    Close();
                };
                ButtonPanel.Children.Add(cancelButton);
            }
        }

        public static bool Show(Window owner, string title, string message, bool showCancel = false, string confirmText = "确定", string cancelText = "取消")
        {
            var dialog = new AppMessageDialog(title, message, showCancel, confirmText, cancelText)
            {
                Owner = owner ?? Application.Current.MainWindow
            };
            return dialog.ShowDialog() == true;
        }
    }
}
