using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou
{
    public partial class MainWindow : Window
    {
        private AccountManager _accountManager;
        private bool _isAIPanelVisible = true;

        public MainWindow()
        {
            InitializeComponent();
            _accountManager = new AccountManager();
            
            _accountManager.AccountSwitched += OnAccountSwitched;
            
            LoadRecentItems();
        }

        private void OnAccountSwitched(object sender, Account account)
        {
            this.Title = $"追番手账 · {account.UserName}";
        }

        private void LoadRecentItems()
        {
            var recent = new List<dynamic>
            {
                new { Title = "葬送的芙莉莲" },
                new { Title = "孤独摇滚" },
                new { Title = "我独自升级" }
            };
            RecentList.ItemsSource = recent;
        }

        private void QuickNote_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("打开快速笔记窗口");
        }

        private void SidebarMenu_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null)
            {
                ContentPlaceholder.Text = $"当前选择：{button.Content}";
            }
        }

        private void RecentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = RecentList.SelectedItem;
            if (selected != null)
            {
                MessageBox.Show($"打开：{selected.GetType().GetProperty("Title")?.GetValue(selected)}");
                RecentList.SelectedItem = null;
            }
        }

        private void Profile_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("打开个人主页");
        }

        private void ToggleAIPanel_Click(object sender, RoutedEventArgs e)
        {
            _isAIPanelVisible = !_isAIPanelVisible;
            
            if (_isAIPanelVisible)
            {
                MainGrid.ColumnDefinitions[4].Width = new GridLength(300);
                (sender as Button).Content = "▶ 收起";
            }
            else
            {
                MainGrid.ColumnDefinitions[4].Width = new GridLength(0);
                (sender as Button).Content = "◀ 展开";
            }
        }

        private void AISearch_Click(object sender, RoutedEventArgs e)
        {
            string query = AISearchBox.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                MessageBox.Show("请输入搜索内容");
                return;
            }

            MessageBox.Show($"搜索：{query}");
            
            var results = new List<dynamic>
            {
                new { DisplayText = $"搜索结果1：{query}" },
                new { DisplayText = $"搜索结果2：{query}" },
                new { DisplayText = $"搜索结果3：{query}" }
            };
            AIResultList.ItemsSource = results;
        }
    }
}