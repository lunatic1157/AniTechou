using System;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou
{
    /// <summary>
    /// 注册窗口
    /// </summary>
    public partial class RegisterWindow : Window
    {
        private AccountManager _accountManager;
        
        // 新注册的用户名，供登录窗口使用
        public string NewUserName { get; private set; }

        public RegisterWindow(AccountManager accountManager)
        {
            InitializeComponent();
            _accountManager = accountManager;
        }

        /// <summary>
        /// 注册按钮点击
        /// </summary>
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            string userName = UserNameBox.Text.Trim();
            string password = PasswordBox.Password;
            string confirmPassword = ConfirmPasswordBox.Password;

            // 验证输入
            if (string.IsNullOrEmpty(userName))
            {
                ShowMessage("请输入用户名");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowMessage("请输入密码");
                return;
            }

            if (password != confirmPassword)
            {
                ShowMessage("两次输入的密码不一致");
                return;
            }

            if (password.Length < 3)
            {
                ShowMessage("密码至少3位");
                return;
            }

            // 创建账号
            if (_accountManager.CreateAccount(userName, password, out string message))
            {
                // 注册成功
                NewUserName = userName;
                DialogResult = true;
                Close();
            }
            else
            {
                ShowMessage(message);
            }
        }

        /// <summary>
        /// 返回登录
        /// </summary>
        private void BackToLogin_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message)
        {
            MessageText.Text = message;
        }
    }
}