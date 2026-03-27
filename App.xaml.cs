using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AniTechou.Services;

namespace AniTechou
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
            Startup += App_Startup;
        }

        private void App_Startup(object sender, StartupEventArgs e)
        {
            ThemeManager.Initialize(this);
            RegisterInputWheelHandlers();
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"发生未处理的异常：\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", 
                "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private static void RegisterInputWheelHandlers()
        {
            EventManager.RegisterClassHandler(typeof(TextBox), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnInputPreviewMouseWheel), true);
            EventManager.RegisterClassHandler(typeof(PasswordBox), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnInputPreviewMouseWheel), true);
            EventManager.RegisterClassHandler(typeof(ComboBox), UIElement.PreviewMouseWheelEvent, new MouseWheelEventHandler(OnInputPreviewMouseWheel), true);
        }

        private static void OnInputPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.IsDropDownOpen)
            {
                return;
            }

            if (sender is TextBox textBox && (textBox.AcceptsReturn || textBox.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled))
            {
                return;
            }

            if (sender is not DependencyObject dependencyObject)
            {
                return;
            }

            var parent = FindParentScrollViewer(dependencyObject);
            if (parent == null)
            {
                return;
            }

            e.Handled = true;
            var eventArgs = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            parent.RaiseEvent(eventArgs);
        }

        private static ScrollViewer FindParentScrollViewer(DependencyObject child)
        {
            DependencyObject current = child;
            while (current != null)
            {
                current = VisualTreeHelper.GetParent(current);
                if (current is ScrollViewer scrollViewer)
                {
                    return scrollViewer;
                }
            }

            return null;
        }
    }
}
