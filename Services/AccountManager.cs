using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AniTechou.Services
{
    /// <summary>
    /// 账号模型类
    /// </summary>
    public class Account
    {
        public string UserName { get; set; }
        public string Salt { get; set; }
        public string PasswordHash { get; set; }
        public DateTime CreatedTime { get; set; }
        public DateTime LastLoginTime { get; set; }
        public string AvatarPath { get; set; }
        public string Nickname { get; set; }
    }

    /// <summary>
    /// 账号管理器 - 负责账号的注册、登录、切换
    /// </summary>
    public class AccountManager
    {
        private readonly string _baseDir;
        private readonly string _accountsDir;
        private readonly string _configFile;
        
        private Account _currentAccount;
        public Account CurrentAccount => _currentAccount;

        // 事件：当账号切换时触发
        public event EventHandler<Account> AccountSwitched;
        // 事件：当账号列表更新时触发
        public event EventHandler<List<Account>> AccountListUpdated;

        public AccountManager()
        {
            _baseDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AniTechou"
            );
            
            _accountsDir = Path.Combine(_baseDir, "accounts");
            _configFile = Path.Combine(_baseDir, "config.json");
            
            // 确保目录存在
            Directory.CreateDirectory(_accountsDir);
        }

        /// <summary>
        /// 获取所有账号列表
        /// </summary>
        public List<Account> GetAllAccounts()
        {
            var accounts = new List<Account>();
            var infoFiles = Directory.GetFiles(_accountsDir, "*.info");

            foreach (var infoFile in infoFiles)
            {
                try
                {
                    var json = File.ReadAllText(infoFile);
                    var account = JsonSerializer.Deserialize<Account>(json);
                    if (account != null)
                    {
                        accounts.Add(account);
                    }
                }
                catch
                {
                    // 忽略损坏的文件
                }
            }

            return accounts;
        }

        /// <summary>
        /// 生成随机盐值
        /// </summary>
        private string GenerateSalt()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
                return Convert.ToBase64String(bytes);
            }
        }

        /// <summary>
        /// 哈希密码（SHA256 + 盐）
        /// </summary>
        private string HashPassword(string password, string salt)
        {
            using (var sha256 = SHA256.Create())
            {
                var combined = password + salt;
                var bytes = Encoding.UTF8.GetBytes(combined);
                var hash = sha256.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 保存账号信息到文件
        /// </summary>
        private void SaveAccountInfo(Account account)
        {
            var infoPath = Path.Combine(_accountsDir, $"{account.UserName}.info");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(account, options);
            File.WriteAllText(infoPath, json);
        }

        /// <summary>
        /// 加载账号信息
        /// </summary>
        private Account LoadAccountInfo(string userName)
        {
            var infoPath = Path.Combine(_accountsDir, $"{userName}.info");
            if (!File.Exists(infoPath)) return null;

            try
            {
                var json = File.ReadAllText(infoPath);
                var account = JsonSerializer.Deserialize<Account>(json);
                
                // 如果头像文件不存在，则清除头像路径
                if (account != null && !string.IsNullOrEmpty(account.AvatarPath))
                {
                    if (!File.Exists(account.AvatarPath))
                    {
                        account.AvatarPath = "";
                        SaveAccountInfo(account); // 顺便修复文件
                    }
                }
                
                return account;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 保存最后登录的账号
        /// </summary>
        private void SaveLastAccount(string userName)
        {
            var config = ConfigManager.Load();
            config.LastAccount = userName;
            ConfigManager.Save(config);
        }

        /// <summary>
        /// 加载最后登录的账号
        /// </summary>
        private string LoadLastAccount()
        {
            var config = ConfigManager.Load();
            return config.LastAccount;
        }

        /// <summary>
        /// 创建新账号
        /// </summary>
        public bool CreateAccount(string userName, string password, out string message)
        {
            // 检查用户名是否为空
            if (string.IsNullOrWhiteSpace(userName))
            {
                message = "用户名不能为空";
                return false;
            }

            // 检查密码是否为空
            if (string.IsNullOrWhiteSpace(password))
            {
                message = "密码不能为空";
                return false;
            }

            // 检查用户名是否已存在
            var infoPath = Path.Combine(_accountsDir, $"{userName}.info");
            if (File.Exists(infoPath))
            {
                message = "用户名已存在";
                return false;
            }

            try
            {
                // 生成盐值
                var salt = GenerateSalt();

                // 创建账号对象
                var account = new Account
                {
                    UserName = userName,
                    Salt = salt,
                    PasswordHash = HashPassword(password, salt),
                    CreatedTime = DateTime.Now,
                    LastLoginTime = DateTime.Now
                };

                // 保存账号信息
                SaveAccountInfo(account);

                // 创建对应的数据库
                DatabaseHelper.InitializeForAccount(userName);

                message = "创建成功";
                AccountListUpdated?.Invoke(this, GetAllAccounts());
                return true;
            }
            catch (Exception ex)
            {
                message = $"创建失败：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 登录账号
        /// </summary>
        public bool Login(string userName, string password, out string message)
        {
            // 加载账号信息
            var account = LoadAccountInfo(userName);
            if (account == null)
            {
                message = "账号不存在";
                return false;
            }

            // 验证密码
            var hash = HashPassword(password, account.Salt);
            if (hash != account.PasswordHash)
            {
                message = "密码错误";
                return false;
            }

            // 更新最后登录时间
            account.LastLoginTime = DateTime.Now;
            SaveAccountInfo(account);

            // 设置为当前账号
            _currentAccount = account;
            SaveLastAccount(userName);

            // 初始化数据库（迁移旧数据库）
            DatabaseHelper.InitializeForAccount(userName);

            message = "登录成功";
            AccountSwitched?.Invoke(this, account);
            return true;
        }

        /// <summary>
        /// 尝试自动登录
        /// </summary>
        public bool TryAutoLogin()
        {
        //     return false;
            var lastAccount = LoadLastAccount();
            if (string.IsNullOrEmpty(lastAccount)) return false;

            var account = LoadAccountInfo(lastAccount);
            if (account == null) return false;

            _currentAccount = account;
            AccountSwitched?.Invoke(this, account);

            // 初始化数据库（迁移旧数据库）
            DatabaseHelper.InitializeForAccount(lastAccount);

            return true;
        }

        /// <summary>
        /// 切换账号（不需要密码，用于已登录状态切换）
        /// </summary>
        public bool SwitchToAccount(string userName)
        {
            var account = LoadAccountInfo(userName);
            if (account == null) return false;

            _currentAccount = account;
            SaveLastAccount(userName);
            AccountSwitched?.Invoke(this, account);
            return true;
        }

        public bool UpdateAvatar(string userName, string avatarPath)
        {
            var account = LoadAccountInfo(userName);
            if (account != null)
            {
                account.AvatarPath = avatarPath;
                SaveAccountInfo(account); // 确保写入磁盘

                // 更新内存中的当前账号
                if (_currentAccount != null && _currentAccount.UserName == userName)
                {
                    _currentAccount.AvatarPath = avatarPath;
                }

                return true;
            }
            return false;
        }

        /// <summary>
        /// 退出登录
        /// </summary>
        public void Logout()
        {
            _currentAccount = null;
            // 清除自动登录配置
            var config = ConfigManager.Load();
            config.AutoLogin = false;
            ConfigManager.Save(config);
        }

        /// <summary>
        /// 删除账号
        /// </summary>
        public bool DeleteAccount(string userName, out string message)
        {
            // 不能删除当前登录的账号
            if (_currentAccount != null && _currentAccount.UserName == userName)
            {
                message = "不能删除当前登录的账号";
                return false;
            }

            try
            {
                // 删除账号信息文件
                var infoPath = Path.Combine(_accountsDir, $"{userName}.info");
                if (File.Exists(infoPath))
                    File.Delete(infoPath);

                // 删除对应的数据库文件
                var dbPath = DatabaseHelper.GetDatabasePath(userName);
                if (File.Exists(dbPath))
                    File.Delete(dbPath);

                message = "删除成功";
                AccountListUpdated?.Invoke(this, GetAllAccounts());
                return true;
            }
            catch (Exception ex)
            {
                message = $"删除失败：{ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// 修改密码
        /// </summary>
        public bool ChangePassword(string userName, string oldPassword, string newPassword, out string message)
        {
            var account = LoadAccountInfo(userName);
            if (account == null)
            {
                message = "账号不存在";
                return false;
            }

            // 验证旧密码
            var oldHash = HashPassword(oldPassword, account.Salt);
            if (oldHash != account.PasswordHash)
            {
                message = "原密码错误";
                return false;
            }

            // 更新密码
            account.Salt = GenerateSalt();
            account.PasswordHash = HashPassword(newPassword, account.Salt);
            SaveAccountInfo(account);

            message = "密码修改成功";
            return true;
        }
    }
}