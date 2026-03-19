using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AniTechou.Services;

namespace AniTechou
{
    /// <summary>
    /// 登录窗口
    /// </summary>
    public partial class LoginWindow : Window
    {
        private AccountManager _accountManager;
        private List<Account> _accounts;

        public LoginWindow(AccountManager accountManager)
        {
            InitializeComponent();
            
            _accountManager = accountManager;
            
            // 订阅账号列表更新事件
            _accountManager.AccountListUpdated += OnAccountListUpdated;
            
            // 加载账号列表
            LoadAccounts();
        }

        /// <summary>
        /// 加载账号列表到下拉框
        /// </summary>
        private void LoadAccounts()
        {
            _accounts = _accountManager.GetAllAccounts();
            AccountCombo.ItemsSource = _accounts;
            
            // 如果有上次登录的账号，默认选中
            if (_accountManager.CurrentAccount != null)
            {
                var lastAccount = _accounts.FirstOrDefault(a => 
                    a.UserName == _accountManager.CurrentAccount.UserName);
                if (lastAccount != null)
                {
                    AccountCombo.SelectedItem = lastAccount;
                }
            }
            
            // 如果只有一个账号，直接选中
            if (_accounts.Count == 1)
            {
                AccountCombo.SelectedIndex = 0;
                PasswordBox.Focus();
            }
        }

        /// <summary>
        /// 账号列表更新时的处理
        /// </summary>
        private void OnAccountListUpdated(object sender, List<Account> accounts)
        {
            Dispatcher.Invoke(() =>
            {
                _accounts = accounts;
                AccountCombo.ItemsSource = _accounts;
            });
        }

        /// <summary>
        /// 登录按钮点击
        /// </summary>
        private void Login_Click(object sender, RoutedEventArgs e)
        {
            // 获取输入的账号
            string userName = AccountCombo.Text.Trim();
            string password = PasswordBox.Password;

            // 验证输入
            if (string.IsNullOrEmpty(userName))
            {
                ShowMessage("请输入账号");
                return;
            }

            if (string.IsNullOrEmpty(password))
            {
                ShowMessage("请输入密码");
                return;
            }

            // 尝试登录
            if (_accountManager.Login(userName, password, out string message))
            {
                // 登录成功，关闭窗口
                DialogResult = true;
                Close();
            }
            else
            {
                ShowMessage(message);
                PasswordBox.Password = ""; // 清空密码
                PasswordBox.Focus();
            }
        }

        /// <summary>
        /// 注册链接点击
        /// </summary>
        private void Register_Click(object sender, RoutedEventArgs e)
        {
            // 打开注册窗口
            var registerWindow = new RegisterWindow(_accountManager);
            registerWindow.Owner = this;
            
            if (registerWindow.ShowDialog() == true)
            {
                // 注册成功，自动填充账号
                AccountCombo.Text = registerWindow.NewUserName;
                PasswordBox.Password = "";
                PasswordBox.Focus();
            }
        }

        /// <summary>
        /// 显示消息
        /// </summary>
        private void ShowMessage(string message)
        {
            MessageText.Text = message;
        }

        /// <summary>
        /// 窗口关闭时的清理
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _accountManager.AccountListUpdated -= OnAccountListUpdated;
        }
    }
}