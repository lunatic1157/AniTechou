using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AniTechou.Services;

namespace AniTechou.Windows
{
    public partial class DesktopPetWindow : Window
    {
        private readonly DispatcherTimer _animationTimer;
        private readonly DispatcherTimer _bubbleTimer;
        private PetActivity _activity = PetActivity.Idle;
        private readonly Dictionary<PetActivity, PetSpriteAsset> _spriteAssets = new Dictionary<PetActivity, PetSpriteAsset>();
        private PetSpriteAsset _currentSprite;
        private int _spriteFrame;
        private int _tick;

        public event Action<double, double> PetMoved;
        public event Action PetClosedByUser;

        public DesktopPetWindow()
        {
            InitializeComponent();

            _animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _animationTimer.Tick += AnimationTimer_Tick;
            LoadPetSprites();
            SetSpriteActivity(PetActivity.Idle);
            _animationTimer.Start();

            _bubbleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _bubbleTimer.Tick += (s, e) =>
            {
                _bubbleTimer.Stop();
                SpeechBubble.Visibility = Visibility.Collapsed;
            };

            Loaded += (s, e) => ShowBubble("\u5435\u6b7b\u4e86\u2026\u2026\u65e2\u7136\u53eb\u6211\u51fa\u6765\uff0c\u7b14\u8bb0\u5c31\u4e0d\u8981\u5199\u5f97\u4e71\u4e03\u516b\u7cdf\u3002");
        }

        public void ApplyScale(double scale)
        {
            double normalized = Math.Clamp(scale, 0.7, 1.6);
            PetScale.ScaleX = normalized;
            PetScale.ScaleY = normalized;
        }

        public void SetActivity(PetActivity activity, string message = null)
        {
            _activity = activity;
            SetSpriteActivity(activity);
            if (!string.IsNullOrWhiteSpace(message))
            {
                ShowBubble(message);
            }
        }

        public void ShowBubble(string message)
        {
            SpeechText.Text = message ?? "";
            SpeechBubble.Visibility = Visibility.Visible;
            _bubbleTimer.Stop();
            _bubbleTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            _tick++;
            AdvanceSpriteFrame();
            PetBounce.Y = 0;
            PetTilt.Angle = 0;
        }

        private void PetRoot_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount >= 2)
            {
                SetActivity(PetActivity.Pleased, "\u54fc\u3002\u8bf4\u5427\uff0c\u522b\u8ba9\u6211\u7b49\u592a\u4e45\u3002");
                ShowDialogPopup();
                return;
            }

            try
            {
                SetActivity(PetActivity.Moving);
                DragMove();
                PetMoved?.Invoke(Left, Top);
                SetActivity(PetActivity.Idle);
            }
            catch
            {
                SetActivity(PetActivity.Idle);
            }
        }

        private void Greet_Click(object sender, RoutedEventArgs e)
        {
            SetActivity(PetActivity.Pleased, "\u4e0d\u8981\u628a\u6211\u5f53\u6210\u95f2\u804a\u5bf9\u8c61\u3002\u7b14\u8bb0\u62ff\u6765\uff0c\u6211\u770b\u4e00\u773c\u3002");
        }

        private void OpenDialog_Click(object sender, RoutedEventArgs e)
        {
            ShowDialogPopup();
        }

        private void ShowDialogPopup()
        {
            DialogPopup.IsOpen = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                DialogInput.Focus();
                Keyboard.Focus(DialogInput);
            }), DispatcherPriority.Input);
        }

        private void CloseDialog_Click(object sender, RoutedEventArgs e)
        {
            DialogPopup.IsOpen = false;
        }

        private void HideFromDialog_Click(object sender, RoutedEventArgs e)
        {
            DialogPopup.IsOpen = false;
            Hide();
        }

        private void CloseFromDialog_Click(object sender, RoutedEventArgs e)
        {
            DialogPopup.IsOpen = false;
            PetClosedByUser?.Invoke();
        }

        private void DebugState_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element &&
                element.Tag is string stateName &&
                Enum.TryParse(stateName, out PetActivity activity))
            {
                SetActivity(activity, GetDebugStateMessage(activity));
            }
        }

        private void SendDialog_Click(object sender, RoutedEventArgs e)
        {
            SendDialogMessage();
        }

        private void DialogInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                SendDialogMessage();
            }
            else if (e.Key == Key.Escape)
            {
                e.Handled = true;
                DialogPopup.IsOpen = false;
            }
        }

        private void DialogInput_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            DialogPlaceholder.Visibility = string.IsNullOrEmpty(DialogInput.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void SendDialogMessage()
        {
            string input = DialogInput.Text?.Trim() ?? "";
            if (input.Length == 0)
            {
                SetActivity(PetActivity.Annoyed, "\u6ca1\u6709\u8981\u8bf4\u7684\u8bdd\uff0c\u5c31\u4e0d\u8981\u628a\u6211\u53eb\u51fa\u6765\u3002");
                return;
            }

            DialogInput.Clear();
            SetActivity(PickDialogActivity(input), PickDialogReply(input));
        }

        private static PetActivity PickDialogActivity(string input)
        {
            if (ContainsAny(input, "\u9519\u8bef", "\u5931\u8d25", "bug", "\u4e0d\u884c"))
            {
                return PetActivity.Annoyed;
            }

            if (ContainsAny(input, "\u7761", "\u665a\u5b89", "\u4f11\u606f"))
            {
                return PetActivity.Sleeping;
            }

            if (ContainsAny(input, "\u8c22\u8c22", "\u597d\u4e86", "\u5b8c\u6210", "\u4fdd\u5b58"))
            {
                return PetActivity.Pleased;
            }

            return PetActivity.Thinking;
        }

        private static string PickDialogReply(string input)
        {
            if (ContainsAny(input, "\u7b14\u8bb0", "\u4fdd\u5b58", "\u6574\u7406"))
            {
                return "\u7b14\u8bb0\u5c31\u8be5\u5199\u5f97\u80fd\u88ab\u81ea\u5df1\u770b\u61c2\u3002\u8fd9\u4e00\u70b9\uff0c\u4e0d\u8bb8\u5077\u61d2\u3002";
            }

            if (ContainsAny(input, "AI", "ai", "\u52a9\u624b", "\u5bf9\u8bdd"))
            {
                return "\u73b0\u5728\u53ea\u662f\u7b80\u5355\u5bf9\u8bdd\u6846\u3002\u771f\u8981\u8ba9\u6211\u63a5\u624b AI\uff0c\u5f97\u5148\u628a\u4eba\u8bbe\u914d\u7f6e\u5199\u6e05\u695a\u3002";
            }

            if (ContainsAny(input, "\u9519\u8bef", "\u5931\u8d25", "bug", "\u4e0d\u884c"))
            {
                return "\u5435\u6b7b\u4e86\u3002\u5148\u628a\u95ee\u9898\u653e\u5230\u6211\u770b\u5f97\u89c1\u7684\u5730\u65b9\u3002";
            }

            if (ContainsAny(input, "\u7761", "\u665a\u5b89", "\u4f11\u606f"))
            {
                return "\u90a3\u5c31\u5b89\u9759\u4e00\u70b9\u3002\u522b\u628a\u4e66\u9875\u7ffb\u5f97\u90a3\u4e48\u54cd\u3002";
            }

            if (ContainsAny(input, "\u4f60\u597d", "hello", "Hello", "\u5728\u5417"))
            {
                return "\u6211\u5728\u3002\u6240\u4ee5\u5462\uff1f\u522b\u8bf4\u5e9f\u8bdd\u3002";
            }

            return "\u6211\u542c\u5230\u4e86\u3002\u5982\u679c\u662f\u91cd\u8981\u7684\u4e8b\uff0c\u5c31\u8bf4\u5f97\u66f4\u6e05\u695a\u4e00\u70b9\u3002";
        }

        private static string GetDebugStateMessage(PetActivity activity)
        {
            return activity switch
            {
                PetActivity.Thinking => "\u8c03\u8bd5\uff1a\u601d\u8003\u72b6\u6001\u3002",
                PetActivity.Pleased => "\u8c03\u8bd5\uff1a\u6ee1\u610f\u72b6\u6001\u3002",
                PetActivity.Annoyed => "\u8c03\u8bd5\uff1a\u70e6\u8e81\u72b6\u6001\u3002",
                PetActivity.Sleeping => "\u8c03\u8bd5\uff1a\u7761\u7720\u72b6\u6001\u3002",
                PetActivity.Moving => "\u8c03\u8bd5\uff1a\u79fb\u52a8\u72b6\u6001\u3002",
                _ => "\u8c03\u8bd5\uff1a\u9ed8\u8ba4\u72b6\u6001\u3002"
            };
        }

        private static bool ContainsAny(string input, params string[] values)
        {
            foreach (string value in values)
            {
                if (input.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void HidePet_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ClosePet_Click(object sender, RoutedEventArgs e)
        {
            PetClosedByUser?.Invoke();
        }

        private void LoadPetSprites()
        {
            string petDirectory = GetPetDirectory();
            string manifestPath = Path.Combine(petDirectory, "pet.json");
            if (!File.Exists(manifestPath))
            {
                ShowVectorFallback();
                return;
            }

            try
            {
                string json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<PetManifest>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (manifest?.States == null)
                {
                    ShowVectorFallback();
                    return;
                }

                foreach (var pair in manifest.States)
                {
                    if (!Enum.TryParse(pair.Key, true, out PetActivity activity) || pair.Value == null)
                    {
                        continue;
                    }

                    string filePath = Path.Combine(petDirectory, pair.Value.File ?? "");
                    if (!File.Exists(filePath))
                    {
                        continue;
                    }

                    var bitmap = LoadBitmap(filePath);
                    int frameWidth = Math.Max(1, pair.Value.FrameWidth ?? manifest.FrameWidth);
                    int frameHeight = Math.Max(1, pair.Value.FrameHeight ?? manifest.FrameHeight);
                    int frames = Math.Max(1, pair.Value.Frames);
                    int fps = Math.Clamp(pair.Value.Fps, 1, 24);

                    if (bitmap.PixelWidth < frameWidth || bitmap.PixelHeight < frameHeight)
                    {
                        continue;
                    }

                    frames = Math.Min(frames, bitmap.PixelWidth / frameWidth);
                    _spriteAssets[activity] = new PetSpriteAsset(bitmap, frameWidth, frameHeight, frames, fps);
                }
            }
            catch
            {
                _spriteAssets.Clear();
            }

            if (_spriteAssets.Count == 0)
            {
                ShowVectorFallback();
            }
        }

        private static BitmapImage LoadBitmap(string filePath)
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(filePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }

        private static string GetPetDirectory()
        {
            string outputPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Pets", "Dalian");
            if (Directory.Exists(outputPath))
            {
                return outputPath;
            }

            string sourcePath = Path.Combine(Environment.CurrentDirectory, "Assets", "Pets", "Dalian");
            if (Directory.Exists(sourcePath))
            {
                return sourcePath;
            }

            return outputPath;
        }

        private void SetSpriteActivity(PetActivity activity)
        {
            if (_spriteAssets.Count == 0)
            {
                return;
            }

            if (!_spriteAssets.TryGetValue(activity, out _currentSprite) &&
                !_spriteAssets.TryGetValue(PetActivity.Idle, out _currentSprite))
            {
                ShowVectorFallback();
                return;
            }

            _spriteFrame = 0;
            _animationTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / _currentSprite.Fps);
            VectorFallback.Visibility = Visibility.Collapsed;
            PetSprite.Visibility = Visibility.Visible;
            UpdateSpriteFrame();
        }

        private void AdvanceSpriteFrame()
        {
            if (_currentSprite == null)
            {
                return;
            }

            _spriteFrame = (_spriteFrame + 1) % _currentSprite.Frames;
            UpdateSpriteFrame();
        }

        private void UpdateSpriteFrame()
        {
            if (_currentSprite == null)
            {
                return;
            }

            int frameX = _spriteFrame * _currentSprite.FrameWidth;
            var rect = new Int32Rect(frameX, 0, _currentSprite.FrameWidth, _currentSprite.FrameHeight);
            var cropped = new CroppedBitmap(_currentSprite.Sheet, rect);
            cropped.Freeze();
            PetSprite.Source = cropped;
        }

        private void ShowVectorFallback()
        {
            _currentSprite = null;
            PetSprite.Source = null;
            PetSprite.Visibility = Visibility.Collapsed;
            VectorFallback.Visibility = Visibility.Visible;
        }

        private sealed class PetSpriteAsset
        {
            public PetSpriteAsset(BitmapSource sheet, int frameWidth, int frameHeight, int frames, int fps)
            {
                Sheet = sheet;
                FrameWidth = frameWidth;
                FrameHeight = frameHeight;
                Frames = frames;
                Fps = fps;
            }

            public BitmapSource Sheet { get; }
            public int FrameWidth { get; }
            public int FrameHeight { get; }
            public int Frames { get; }
            public int Fps { get; }
        }

        private sealed class PetManifest
        {
            public int FrameWidth { get; set; } = 256;
            public int FrameHeight { get; set; } = 256;
            public Dictionary<string, PetManifestState> States { get; set; }
        }

        private sealed class PetManifestState
        {
            public string File { get; set; }
            public int Frames { get; set; } = 1;
            public int Fps { get; set; } = 6;
            [JsonPropertyName("frameWidth")]
            public int? FrameWidth { get; set; }
            [JsonPropertyName("frameHeight")]
            public int? FrameHeight { get; set; }
        }
    }
}
