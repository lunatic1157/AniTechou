using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AniTechou.Services;

namespace AniTechou.Windows
{
    public partial class WorkSelectionDialog : Window
    {
        public List<int> SelectedWorkIds { get; private set; }

        public WorkSelectionDialog(List<WorkService.WorkListItem> works, List<int> selectedIds)
        {
            InitializeComponent();
            WorksList.ItemsSource = works;
            
            // 预选
            foreach (var item in works)
            {
                if (selectedIds.Contains(item.Id))
                {
                    WorksList.SelectedItems.Add(item);
                }
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedWorkIds = WorksList.SelectedItems.Cast<WorkService.WorkListItem>().Select(w => w.Id).ToList();
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