using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using AniTechou.Controls;
using AniTechou.Services;

namespace AniTechou.Views
{
    public partial class WorksView : UserControl
    {
        private WorkService _workService;
        private string _currentType;
        private string _currentStatus;
        private bool _isGridView = true;

        // 定义颜色
        private SolidColorBrush _selectedBrush = new SolidColorBrush(Color.FromRgb(224, 213, 192));
        private SolidColorBrush _normalBrush = new SolidColorBrush(Color.FromRgb(255, 255, 255));

        public WorksView(string accountName, string type, string status)
        {
            InitializeComponent();
            _workService = new WorkService(accountName);
            _currentType = type;
            _currentStatus = status;
            
            // 设置标题
            TitleText.Text = GetDisplayTitle(type, status);
            
            // 设置初始按钮样式
            UpdateButtonStyles();
            
            // 加载数据
            LoadWorks();
        }

        private string GetDisplayTitle(string type, string status)
        {
            // 类型映射
            var typeMap = new Dictionary<string, string>
            {
                { "all", "全部作品" },
                { "Anime", "动画" },      // 改成大写
                { "Manga", "漫画" },
                { "LightNovel", "轻小说" },
                { "Game", "游戏" }
            };
            
            // 状态映射
            var statusMap = new Dictionary<string, string>
            {
                { "wish", "想看" },
                { "doing", "在看" },
                { "done", "看过" }
            };
            
            string typeName = typeMap.ContainsKey(type) ? typeMap[type] : type;
            string statusName = statusMap.ContainsKey(status) ? statusMap[status] : "";
            
            if (string.IsNullOrEmpty(statusName))
                return typeName;
            else
                return $"{typeName} · {statusName}";
        }

        private void UpdateButtonStyles()
        {
            if (_isGridView)
            {
                GridButton.Background = _selectedBrush;
                ListButton.Background = _normalBrush;
            }
            else
            {
                GridButton.Background = _normalBrush;
                ListButton.Background = _selectedBrush;
            }
        }

        private async void LoadWorks()
        {
            try
            {
                // 显示加载中
                LoadingText.Visibility = Visibility.Visible;
                WorksItemsControl.Visibility = Visibility.Collapsed;
                EmptyText.Visibility = Visibility.Collapsed;

                // 获取数据
                var works = await _workService.GetWorksAsync(_currentType, _currentStatus);
                
                // 更新UI
                CountText.Text = $"({works.Count})";
                
                if (works.Count == 0)
                {
                    WorksItemsControl.Visibility = Visibility.Collapsed;
                    EmptyText.Visibility = Visibility.Visible;
                }
                else
                {
                    WorksItemsControl.Visibility = Visibility.Visible;
                    WorksItemsControl.ItemsSource = works;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"加载失败：{ex.Message}");
            }
            finally
            {
                LoadingText.Visibility = Visibility.Collapsed;
            }
        }

        private void OnWorkCardClick(object sender, RoutedEventArgs e)
        {
            var card = sender as WorkCard;
            if (card != null)
            {
                // TODO: 打开作品详情页
                MessageBox.Show($"打开作品：{card.Title}");
            }
        }

        private void SetGridView_Click(object sender, RoutedEventArgs e)
        {
            if (_isGridView) return;
            _isGridView = true;
            
            // 切换面板模板
            WorksItemsControl.ItemsPanel = (ItemsPanelTemplate)FindResource("GridPanel");
            UpdateButtonStyles();
        }

        private void SetListView_Click(object sender, RoutedEventArgs e)
        {
            if (!_isGridView) return;
            _isGridView = false;
            
            // 切换面板模板
            WorksItemsControl.ItemsPanel = (ItemsPanelTemplate)FindResource("ListPanel");
            UpdateButtonStyles();
        }

        private void AddWork_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowAddWorkOptions();
        }
    }
}