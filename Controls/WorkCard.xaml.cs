using System.Windows;
using System.Windows.Controls;

namespace AniTechou.Controls
{
    /// <summary>
    /// 作品卡片控件
    /// </summary>
    public partial class WorkCard : UserControl
    {
        // 依赖属性：作品ID
        public static readonly DependencyProperty WorkIdProperty =
            DependencyProperty.Register("WorkId", typeof(int), typeof(WorkCard), new PropertyMetadata(0));
        
        // 依赖属性：标题
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(WorkCard), new PropertyMetadata(""));
        
        // 依赖属性：信息（年份+制作公司）
        public static readonly DependencyProperty InfoProperty =
            DependencyProperty.Register("Info", typeof(string), typeof(WorkCard), new PropertyMetadata(""));
        
        // 依赖属性：进度值（0-100）
        public static readonly DependencyProperty ProgressValueProperty =
            DependencyProperty.Register("ProgressValue", typeof(double), typeof(WorkCard), new PropertyMetadata(0.0));
        
        // 依赖属性：进度文本
        public static readonly DependencyProperty ProgressTextProperty =
            DependencyProperty.Register("ProgressText", typeof(string), typeof(WorkCard), new PropertyMetadata(""));
        
        // 依赖属性：评分显示
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

        // 点击事件
        public event RoutedEventHandler Click;

        public WorkCard()
        {
            InitializeComponent();
            this.MouseLeftButtonUp += (s, e) => Click?.Invoke(this, new RoutedEventArgs());
        }
    }
}