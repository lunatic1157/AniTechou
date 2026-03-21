using System.Windows;
using System.Windows.Controls;

namespace AniTechou.Views
{
    public partial class AddWorkChoiceView : UserControl
    {
        public event System.Action AIAddRequested;
        public event System.Action ManualAddRequested;

        public AddWorkChoiceView()
        {
            InitializeComponent();
        }

        private void AIAdd_Click(object sender, RoutedEventArgs e)
        {
            AIAddRequested?.Invoke();
        }

        private void ManualAdd_Click(object sender, RoutedEventArgs e)
        {
            ManualAddRequested?.Invoke();
        }
    }
}