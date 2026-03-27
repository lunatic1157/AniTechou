using System.Windows;

namespace AniTechou.Windows
{
    public partial class LinkInsertDialog : Window
    {
        public string LinkTextValue => LinkTextBox.Text;
        public string LinkUrlValue => LinkUrlBox.Text;

        public LinkInsertDialog()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}

