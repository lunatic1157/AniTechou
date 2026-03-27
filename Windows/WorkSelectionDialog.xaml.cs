using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou.Windows
{
    public partial class WorkSelectionDialog : Window
    {
        public List<int> SelectedWorkIds { get; private set; }
        private List<WorkService.WorkListItem> _allWorks;
        private HashSet<int> _selectedIds;
        private ICollectionView _view;
        private bool _suppressSelection;

        public WorkSelectionDialog(List<WorkService.WorkListItem> works, List<int> selectedIds)
        {
            InitializeComponent();
            _allWorks = works ?? new List<WorkService.WorkListItem>();
            _selectedIds = new HashSet<int>(selectedIds ?? new List<int>());
            WorksList.ItemsSource = _allWorks;
            _view = CollectionViewSource.GetDefaultView(WorksList.ItemsSource);
            _view.Filter = FilterWork;

            WorksList.SelectionChanged += WorksList_SelectionChanged;
            ReselectVisibleItems();
        }

        private bool FilterWork(object obj)
        {
            if (obj is not WorkService.WorkListItem w) return false;
            string q = (SearchBox.Text ?? "").Trim();
            if (string.IsNullOrEmpty(q)) return true;
            return (w.Title ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _view?.Refresh();
            ReselectVisibleItems();
        }

        private void WorksList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressSelection) return;
            foreach (var a in e.AddedItems.Cast<WorkService.WorkListItem>()) _selectedIds.Add(a.Id);
            foreach (var r in e.RemovedItems.Cast<WorkService.WorkListItem>()) _selectedIds.Remove(r.Id);
        }

        private void ReselectVisibleItems()
        {
            _suppressSelection = true;
            WorksList.SelectedItems.Clear();
            foreach (var item in WorksList.Items.Cast<WorkService.WorkListItem>())
            {
                if (_selectedIds.Contains(item.Id))
                {
                    WorksList.SelectedItems.Add(item);
                }
            }
            _suppressSelection = false;
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            SelectedWorkIds = _selectedIds.ToList();
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
