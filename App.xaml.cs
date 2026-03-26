using System;
using System.Windows;
using System.Windows.Threading;

namespace AniTechou
{
    public partial class App : Application
    {
        public App()
        {
            this.DispatcherUnhandledException += App_DispatcherUnhandledException;
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            MessageBox.Show($"发生未处理的异常：\n{e.Exception.Message}\n\n{e.Exception.StackTrace}", 
                "致命错误", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }
    }
}