using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace AniTechou.Controls
{
    public partial class WorkCard : UserControl
    {
        // 依赖属性
        public static readonly DependencyProperty WorkIdProperty =
            DependencyProperty.Register("WorkId", typeof(int), typeof(WorkCard), new PropertyMetadata(0));
        
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(WorkCard), new PropertyMetadata(""));
        
        public static readonly DependencyProperty InfoProperty =
            DependencyProperty.Register("Info", typeof(string), typeof(WorkCard), new PropertyMetadata(""));
        
        public static readonly DependencyProperty CoverPathProperty =
            DependencyProperty.Register("CoverPath", typeof(string), typeof(WorkCard), new PropertyMetadata("", OnCoverPathChanged));
        
        public static readonly DependencyProperty ProgressValueProperty =
            DependencyProperty.Register("ProgressValue", typeof(double), typeof(WorkCard), new PropertyMetadata(0.0));
        
        public static readonly DependencyProperty ProgressTextProperty =
            DependencyProperty.Register("ProgressText", typeof(string), typeof(WorkCard), new PropertyMetadata(""));
        
        public static readonly DependencyProperty RatingDisplayProperty =
            DependencyProperty.Register("RatingDisplay", typeof(string), typeof(WorkCard), new PropertyMetadata("未评分"));

        public int WorkId
        {
            get { return (int)GetValue(WorkIdProperty); }
            set { SetValue(WorkIdProperty, value); }
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public string Info
        {
            get { return (string)GetValue(InfoProperty); }
            set { SetValue(InfoProperty, value); }
        }

        public string CoverPath
        {
            get { return (string)GetValue(CoverPathProperty); }
            set { SetValue(CoverPathProperty, value); }
        }

        public double ProgressValue
        {
            get { return (double)GetValue(ProgressValueProperty); }
            set { SetValue(ProgressValueProperty, value); }
        }

        public string ProgressText
        {
            get { return (string)GetValue(ProgressTextProperty); }
            set { SetValue(ProgressTextProperty, value); }
        }

        public string RatingDisplay
        {
            get { return (string)GetValue(RatingDisplayProperty); }
            set { SetValue(RatingDisplayProperty, value); }
        }

        // 封面路径变化时更新图片
        private static void OnCoverPathChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var card = d as WorkCard;
            var path = e.NewValue as string;
            card?.UpdateCover(path);
        }

        private void UpdateCover(string path)
        {
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
            {
                try
                {
                    var bitmap = new BitmapImage(new Uri(path));
                    CoverImage.Source = bitmap;
                    CoverImage.Visibility = Visibility.Visible;
                    CoverPlaceholder.Visibility = Visibility.Collapsed;
                }
                catch
                {
                    CoverImage.Visibility = Visibility.Collapsed;
                    CoverPlaceholder.Visibility = Visibility.Visible;
                }
            }
            else
            {
                CoverImage.Visibility = Visibility.Collapsed;
                CoverPlaceholder.Visibility = Visibility.Visible;
            }
        }

        public event RoutedEventHandler Click;

        public WorkCard()
        {
            InitializeComponent();
            this.MouseLeftButtonUp += (s, e) => Click?.Invoke(this, new RoutedEventArgs());
        }
    }
}