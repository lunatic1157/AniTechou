using System;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou.Windows
{
    public partial class EditWorkInfoWindow : Window
    {
        private int _workId;
        private string _accountName;
        private WorkService _workService;

        public EditWorkInfoWindow(int workId, string accountName)
        {
            InitializeComponent();
            _workId = workId;
            _accountName = accountName;
            _workService = new WorkService(accountName);
            LoadData();
        }

        private void LoadData()
        {
            var work = _workService.GetWorkById(_workId);
            if (work != null)
            {
                TitleBox.Text = work.Title;
                OriginalTitleBox.Text = work.OriginalTitle ?? "";

                // 设置类型选中项
                for (int i = 0; i < TypeBox.Items.Count; i++)
                {
                    var item = TypeBox.Items[i] as ComboBoxItem;
                    if (item != null && item.Content.ToString() == work.Type)
                    {
                        TypeBox.SelectedIndex = i;
                        break;
                    }
                }

                CompanyBox.Text = work.Company ?? "";

                // 设置年份
                YearBox.Text = work.Year ?? "";

                // 设置季度
                string season = work.Season ?? "";
                int seasonIndex = season switch
                {
                    "春" => 0,
                    "夏" => 1,
                    "秋" => 2,
                    "冬" => 3,
                    _ => -1
                };
                if (seasonIndex >= 0)
                {
                    SeasonBox.SelectedIndex = seasonIndex;
                }

                EpisodesBox.Text = work.EpisodesVolumes ?? "";
                SynopsisBox.Text = work.Synopsis ?? "";
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var selectedItem = TypeBox.SelectedItem as ComboBoxItem;
                string type = selectedItem?.Content.ToString() ?? "Anime";

                // 分别获取年份和季度
                string year = YearBox.Text.Trim();
                string season = (SeasonBox.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";

                bool success = _workService.UpdateWorkInfo(
                    _workId,
                    TitleBox.Text.Trim(),
                    OriginalTitleBox.Text.Trim(),
                    type,
                    CompanyBox.Text.Trim(),
                    year,
                    season,
                    EpisodesBox.Text.Trim(),
                    SynopsisBox.Text.Trim()
                );

                if (success)
                {
                    MessageBox.Show("保存成功！");
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("保存失败，请重试");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}");
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}