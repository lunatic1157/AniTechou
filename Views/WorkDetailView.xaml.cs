using System;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;
using AniTechou.Windows;

namespace AniTechou.Views
{
    public partial class WorkDetailView : UserControl
    {
        private int _workId;
        private int _userListId;
        private string _accountName;
        private WorkService _workService;

        public WorkDetailView(int workId, int userListId, string accountName)
        {
            InitializeComponent();
            _workId = workId;
            _userListId = userListId;
            _accountName = accountName;
            _workService = new WorkService(accountName);

            LoadData();
        }

        private void LoadData()
        {
            var work = _workService.GetWorkById(_workId);
            var userWork = _workService.GetUserWorkByWorkId(_workId);

            if (work != null)
            {
                TitleText.Text = work.Title;
                OriginalTitleText.Text = work.OriginalTitle ?? "";
                TypeText.Text = $"类型：{GetTypeDisplayName(work.Type)}";
                CompanyText.Text = $"制作：{work.Company ?? "未知"}";

                // 显示年份和季度
                string year = work.Year ?? "";
                string season = work.Season ?? "";
                if (!string.IsNullOrEmpty(year))
                {
                    string displaySeason = season switch
                    {
                        "春" => "春",
                        "夏" => "夏",
                        "秋" => "秋",
                        "冬" => "冬",
                        _ => ""
                    };
                    YearText.Text = $"年份：{year} {(string.IsNullOrEmpty(displaySeason) ? "" : displaySeason)}";
                }
                else
                {
                    YearText.Text = "年份：未知";
                }

                EpisodesText.Text = $"集数/卷数：{work.EpisodesVolumes ?? "未知"}";

                string synopsis = work.Synopsis ?? "";
                SynopsisTextBlock.Text = string.IsNullOrEmpty(synopsis) ? "暂无简介" : synopsis;

                // 加载封面
                LoadCoverImage(work.CoverPath);
            }

            // 加载个人状态
            if (userWork != null)
            {
                // 设置状态
                string statusText = userWork.Status switch
                {
                    "wish" => "想看",
                    "doing" => "在看",
                    "done" => "看过",
                    _ => "想看"
                };
                
                for (int i = 0; i < StatusBox.Items.Count; i++)
                {
                    var item = StatusBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == statusText)
                    {
                        StatusBox.SelectedIndex = i;
                        break;
                    }
                }
                
                // 设置进度
                ProgressBox.Text = userWork.Progress ?? "";
                
                // 设置评分
                int ratingIndex = userWork.Rating switch
                {
                    2 => 1,
                    4 => 2,
                    6 => 3,
                    8 => 4,
                    10 => 5,
                    _ => 0
                };
                RatingBox.SelectedIndex = ratingIndex;
            }
        }

        private void LoadCoverImage(string coverPath)
        {
            if (string.IsNullOrEmpty(coverPath))
            {
                CoverImage.Source = null;
                return;
            }

            try
            {
                // 尝试直接路径
                if (System.IO.File.Exists(coverPath))
                {
                    SetImageSource(coverPath);
                    return;
                }
                
                // 尝试相对路径
                string fullPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, coverPath);
                if (System.IO.File.Exists(fullPath))
                {
                    SetImageSource(fullPath);
                    return;
                }
                
                // 尝试应用程序数据目录
                string appDataPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AniTechou",
                    "covers",
                    System.IO.Path.GetFileName(coverPath));
                if (System.IO.File.Exists(appDataPath))
                {
                    SetImageSource(appDataPath);
                    return;
                }
                
                CoverImage.Source = null;
            }
            catch
            {
                CoverImage.Source = null;
            }
        }

        private void SetImageSource(string path)
        {
            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(path);
            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            CoverImage.Source = bitmap;
        }

        private string GetTypeDisplayName(string type)
        {
            return type switch
            {
                "Anime" => "动画",
                "Manga" => "漫画",
                "LightNovel" => "轻小说",
                "Game" => "游戏",
                _ => type
            };
        }

        private void Update_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取状态
                var statusItem = StatusBox.SelectedItem as ComboBoxItem;
                string statusText = statusItem?.Content.ToString() ?? "在看";
                string statusEn = statusText switch
                {
                    "想看" => "wish",
                    "在看" => "doing",
                    "看过" => "done",
                    _ => "wish"
                };
                
                // 获取评分
                var ratingItem = RatingBox.SelectedItem as ComboBoxItem;
                string ratingText = ratingItem?.Content.ToString() ?? "未评分";
                int rating = ratingText switch
                {
                    "★☆☆☆☆ (1-2分)" => 2,
                    "★★☆☆☆ (3-4分)" => 4,
                    "★★★☆☆ (5-6分)" => 6,
                    "★★★★☆ (7-8分)" => 8,
                    "★★★★★ (9-10分)" => 10,
                    _ => 0
                };
                
                // 进度
                string progress = ProgressBox.Text.Trim();
                
                // 更新数据库
                _workService.UpdateUserWork(_userListId, statusEn, progress, rating);
                
                MessageBox.Show("更新成功！");
                
                // 重新加载数据以确认更新
                LoadData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败：{ex.Message}");
            }
        }

        private void EditInfo_Click(object sender, RoutedEventArgs e)
        {
            var editDialog = new EditWorkInfoWindow(_workId, _accountName);
            editDialog.Owner = Application.Current.MainWindow;
            if (editDialog.ShowDialog() == true)
            {
                LoadData();
            }
        }

        private void WriteNote_Click(object sender, RoutedEventArgs e)
        {
            var editor = new NoteEditor(_accountName, null, EditorSource.WorkDetail, _workId);
            // 自动关联当前作品
            editor.PreSelectWork(_workId);
            editor.NoteSaved += () => LoadData();
            editor.NoteCancelled += () => LoadData();
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.ShowDetailView(editor);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow?.RefreshCurrentView();
        }
    }
}