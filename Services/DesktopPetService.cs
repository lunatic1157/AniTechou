using System;
using System.Windows;
using AniTechou.Windows;

namespace AniTechou.Services
{
    public enum PetActivity
    {
        Idle,
        Thinking,
        Pleased,
        Annoyed,
        Sleeping,
        Moving
    }

    public sealed class DesktopPetService
    {
        public static DesktopPetService Instance { get; } = new DesktopPetService();

        private DesktopPetWindow _window;

        private DesktopPetService()
        {
        }

        public void Initialize()
        {
            RefreshFromConfig();
        }

        public void RefreshFromConfig()
        {
            var config = ConfigManager.Load();
            if (config.EnableDesktopPet)
            {
                EnsureWindow(config);
                _window.Show();
            }
            else
            {
                _window?.Hide();
            }
        }

        public void Notify(PetActivity activity, string message = null)
        {
            if (_window == null || !_window.IsVisible)
            {
                return;
            }

            _window.SetActivity(activity, message ?? GetDefaultMessage(activity));
        }

        public void Shutdown()
        {
            if (_window == null)
            {
                return;
            }

            _window.Close();
            _window = null;
        }

        private void EnsureWindow(AppConfig config)
        {
            if (_window != null)
            {
                _window.ApplyScale(config.DesktopPetScale);
                return;
            }

            _window = new DesktopPetWindow();
            _window.ApplyScale(config.DesktopPetScale);
            ApplyInitialPosition(_window, config);

            _window.PetMoved += SavePosition;
            _window.PetClosedByUser += DisablePetFromWindow;
            _window.Closed += (s, e) =>
            {
                if (ReferenceEquals(_window, s))
                {
                    _window = null;
                }
            };
        }

        private static void ApplyInitialPosition(Window window, AppConfig config)
        {
            Rect area = SystemParameters.WorkArea;
            double defaultLeft = area.Right - window.Width - 520;
            double defaultTop = area.Bottom - window.Height - 90;

            window.Left = IsUsableCoordinate(config.DesktopPetX)
                ? Math.Clamp(config.DesktopPetX, area.Left, Math.Max(area.Left, area.Right - window.Width))
                : defaultLeft;
            window.Top = IsUsableCoordinate(config.DesktopPetY)
                ? Math.Clamp(config.DesktopPetY, area.Top, Math.Max(area.Top, area.Bottom - window.Height))
                : defaultTop;
        }

        private static bool IsUsableCoordinate(double value)
        {
            return value >= 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private static void SavePosition(double left, double top)
        {
            var config = ConfigManager.Load();
            config.DesktopPetX = left;
            config.DesktopPetY = top;
            ConfigManager.Save(config);
        }

        private void DisablePetFromWindow()
        {
            var config = ConfigManager.Load();
            config.EnableDesktopPet = false;
            ConfigManager.Save(config);
            _window?.Close();
        }

        private static string GetDefaultMessage(PetActivity activity)
        {
            return activity switch
            {
                PetActivity.Thinking => "\u522b\u50ac\u3002\u6211\u8fd8\u5728\u60f3\uff0c\u4e0d\u8981\u6253\u65ad\u6211\u3002",
                PetActivity.Pleased => "\u8fd8\u4e0d\u9519\u3002\u81f3\u5c11\u6ca1\u6709\u7b28\u5230\u8ba9\u6211\u53f9\u6c14\u3002",
                PetActivity.Annoyed => "\u5435\u6b7b\u4e86\u3002\u8fd9\u79cd\u9519\u8bef\u4e5f\u8981\u6211\u6307\u51fa\u6765\u5417\uff1f",
                PetActivity.Sleeping => "\u5b89\u9759\u4e00\u70b9\u3002\u8ba9\u6211\u7761\u4f1a\u513f\u3002",
                PetActivity.Moving => "\u522b\u4e71\u62d6\u3002\u5934\u53d1\u4f1a\u88ab\u4f60\u5f04\u4e71\u3002",
                _ => null
            };
        }
    }
}
