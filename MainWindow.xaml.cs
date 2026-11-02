using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using OxiDock.Services;
using Forms = System.Windows.Forms;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfContextMenu = System.Windows.Controls.ContextMenu;
using WpfMenuItem = System.Windows.Controls.MenuItem;
using WpfSeparator = System.Windows.Controls.Separator;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfBrush = System.Windows.Media.Brush;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfCursors = System.Windows.Input.Cursors;
using WpfOrientation = System.Windows.Controls.Orientation;
using DragEventArgs = System.Windows.DragEventArgs;
using DragDropEffects = System.Windows.DragDropEffects;
using Windows.Media.Control;
using TextBox = System.Windows.Controls.TextBox;

namespace OxiDock
{
    public partial class MainWindow : Window
    {
        private AppSettings _settings;
        private readonly DispatcherTimer _clockTimer = new();
        private readonly DispatcherTimer _telemetryTimer = new();
        private readonly DispatcherTimer _equalizerTimer = new();
        private readonly DispatcherTimer _pomodoroTimer = new();
        private readonly DispatcherTimer _networkTimer = new();
        private readonly HttpClient _httpClient = new();
        private Forms.NotifyIcon? _notifyIcon;
        private LocalAiAssistantServer? _aiServer;

        // Windows GSMTC (Global Media Transport) & Multimedia Keys
        private GlobalSystemMediaTransportControlsSessionManager? _mediaManager;
        private GlobalSystemMediaTransportControlsSession? _currentMediaSession;

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_MEDIA_NEXT_TRACK = 0xB0;
        private const byte VK_MEDIA_PREV_TRACK = 0xB1;
        private const byte VK_MEDIA_PLAY_PAUSE = 0xB3;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        private bool _isPlaying = false;
        private readonly Random _rand = new();

        // Saat Durumu (OxiDock Mac Replica: 0=Dijital, 1=Flip Clock, 2=Analog Clock, 3=Dünya Saati)
        private int _clockMode = 0;
        private bool _clockShowSeconds = false;

        // Pomodoro Değişkenleri (OxiDock Mac Replica: 25-5-25-5 Döngüsü, Progress Bar & Wellness)
        private bool _pomoIsRunning = false;
        private int _pomoState = 0; // 0: Odak, 1: Kısa Mola, 2: Uzun Mola
        private bool _pomoIsFocus => _pomoState == 0;
        private int _pomoSecondsRemaining = 25 * 60;
        private int _pomoRound = 1;

        // Hava Durumu Son Çekilen Şehir
        private string _lastWeatherCity = "";

        // Widget Kütüphanesi Açık Referansı
        private WidgetBrowserWindow? _widgetBrowserWindow;

        // CPU İzleme Değişkenleri (Win32 GetSystemTimes)
        private long _prevIdleTime = 0;
        private long _prevKernelTime = 0;
        private long _prevUserTime = 0;

        public MainWindow()
        {
            InitializeComponent();
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("OxiDock/1.0 (Windows)");
            _settings = AppSettings.Load();
            ApplySettings(_settings);
            InitializeSystemTray();
            InitializeTimers();
            InitializeNotes();
            InitializeAiServer();
            InitializeMediaController();
            InitializeNetworkMonitor();
        }

        #region Ayarları Uygulama & Konumlandırma

        public void ApplySettings(AppSettings s)
        {
            _settings = s;
            var activeProfile = _settings.GetActiveProfile();

            // 1. Her zaman üstte
            Topmost = _settings.AlwaysOnTop;

            // 2. Ölçekleme
            DockScaleTransform.ScaleX = _settings.DockScale;
            DockScaleTransform.ScaleY = _settings.DockScale;

            // 3. Profil Sekmelerini Güncelle
            UpdateProfileTabsUI();

            // 4. Widget Görünürlükleri (Aktif profile göre)
            WidgetClock.Visibility = activeProfile.ShowClock ? Visibility.Visible : Visibility.Collapsed;
            WidgetPomodoro.Visibility = activeProfile.ShowPomodoro ? Visibility.Visible : Visibility.Collapsed;
            WidgetWeather.Visibility = activeProfile.ShowWeather ? Visibility.Visible : Visibility.Collapsed;
            WidgetSystem.Visibility = activeProfile.ShowSystemStats ? Visibility.Visible : Visibility.Collapsed;
            SpBattery.Visibility = activeProfile.ShowBattery ? Visibility.Visible : Visibility.Collapsed;
            WidgetApps.Visibility = activeProfile.ShowApps ? Visibility.Visible : Visibility.Collapsed;
            WidgetCustomAction.Visibility = activeProfile.ShowCustomAction ? Visibility.Visible : Visibility.Collapsed;
            WidgetNetwork.Visibility = activeProfile.ShowNetwork ? Visibility.Visible : Visibility.Collapsed;
            WidgetMusic.Visibility = activeProfile.ShowMusic ? Visibility.Visible : Visibility.Collapsed;
            WidgetNotes.Visibility = activeProfile.ShowNotes ? Visibility.Visible : Visibility.Collapsed;
            WidgetCrypto.Visibility = activeProfile.ShowCrypto ? Visibility.Visible : Visibility.Collapsed;
            WidgetCalendar.Visibility = activeProfile.ShowCalendar ? Visibility.Visible : Visibility.Collapsed;
            WidgetQuickSearch.Visibility = activeProfile.ShowQuickSearch ? Visibility.Visible : Visibility.Collapsed;
            WidgetVolume.Visibility = activeProfile.ShowVolume ? Visibility.Visible : Visibility.Collapsed;
            WidgetClipboard.Visibility = activeProfile.ShowClipboard ? Visibility.Visible : Visibility.Collapsed;

            // Android Tarzı Widget Boyut Varyantlarını Uygula (1x1, 1x2, 1x3)
            ApplyWidgetSizes(activeProfile);

            // Dinamik Kullanıcı Uygulama Widget'larını Yansıt
            RenderCustomWidgets(activeProfile);

            // 5. Canlı ve Belirgin Dock Stilleri (Glass & Temalar)
            bool isLight = _settings.DockStyle == "LightGlass";
            byte baseAlpha = _settings.ReduceTransparency ? (byte)248 : (byte)195;

            // Tema Uyumlu Metin & Arka Plan Fırçaları
            var fgColor = isLight ? MediaColor.FromRgb(15, 23, 42) : MediaColor.FromRgb(255, 255, 255);
            var secColor = isLight ? MediaColor.FromRgb(51, 65, 85) : MediaColor.FromArgb(170, 255, 255, 255);
            var mutedColor = isLight ? MediaColor.FromRgb(100, 116, 139) : MediaColor.FromArgb(136, 255, 255, 255);
            var widgetBgColor = isLight ? MediaColor.FromArgb(30, 0, 0, 0) : MediaColor.FromArgb(20, 255, 255, 255);
            var widgetBorderColor = isLight ? MediaColor.FromArgb(45, 0, 0, 0) : MediaColor.FromArgb(26, 255, 255, 255);
            var btnBgColor = isLight ? MediaColor.FromArgb(24, 0, 0, 0) : MediaColor.FromArgb(26, 255, 255, 255);
            var btnBorderColor = isLight ? MediaColor.FromArgb(38, 0, 0, 0) : MediaColor.FromArgb(34, 255, 255, 255);

            var primaryBrush = new SolidColorBrush(fgColor);
            var secondaryBrush = new SolidColorBrush(secColor);
            var mutedBrush = new SolidColorBrush(mutedColor);

            Resources["DockForegroundBrush"] = primaryBrush;
            Resources["DockSecondaryForegroundBrush"] = secondaryBrush;
            Resources["DockMutedForegroundBrush"] = mutedBrush;
            Resources["DockWidgetBackgroundBrush"] = new SolidColorBrush(widgetBgColor);
            Resources["DockWidgetBorderBrush"] = new SolidColorBrush(widgetBorderColor);
            Resources["DockButtonBackgroundBrush"] = new SolidColorBrush(btnBgColor);
            Resources["DockButtonForegroundBrush"] = primaryBrush;
            Resources["DockButtonBorderBrush"] = new SolidColorBrush(btnBorderColor);

            // Doğrudan Temel Metin Renklerini Güncelle
            TxtClock.Foreground = primaryBrush;
            TxtDate.Foreground = secondaryBrush;
            TxtWeatherTemp.Foreground = primaryBrush;
            TxtWeatherCity.Foreground = secondaryBrush;
            TxtForecastTomorrow.Foreground = secondaryBrush;
            TxtPomoTimer.Foreground = primaryBrush;
            TxtCustomActionTitle.Foreground = primaryBrush;
            TxtSongTitle.Foreground = primaryBrush;
            TxtSongArtist.Foreground = secondaryBrush;
            TxtPingLabel.Foreground = mutedBrush;

            if (isLight)
            {
                ProfileBarBorder.Background = new SolidColorBrush(MediaColor.FromArgb(238, 241, 245, 249));
                ProfileBarBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(140, 148, 163, 184));
            }
            else
            {
                ProfileBarBorder.Background = new SolidColorBrush(MediaColor.FromArgb(217, 18, 18, 23));
                ProfileBarBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(37, 255, 255, 255));
            }

            switch (_settings.DockStyle)
            {
                case "LightGlass":
                    MainDockBorder.Background = new SolidColorBrush(MediaColor.FromArgb(baseAlpha, 240, 243, 248));
                    MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(130, 100, 116, 139));
                    DockShadow.Color = MediaColor.FromArgb(70, 0, 0, 0);
                    break;
                case "LiquidGlass":
                    MainDockBorder.Background = new SolidColorBrush(MediaColor.FromArgb(_settings.ReduceTransparency ? (byte)245 : (byte)165, 12, 20, 38));
                    MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(180, 56, 189, 248));
                    DockShadow.Color = MediaColor.FromArgb(140, 56, 189, 248);
                    break;
                case "Cyberpunk":
                    MainDockBorder.Background = new SolidColorBrush(MediaColor.FromArgb(_settings.ReduceTransparency ? (byte)248 : (byte)180, 14, 8, 28));
                    MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(200, 168, 85, 247));
                    DockShadow.Color = MediaColor.FromArgb(160, 168, 85, 247);
                    break;
                case "OledBlack":
                case "Black":
                    MainDockBorder.Background = new SolidColorBrush(MediaColor.FromRgb(4, 4, 6));
                    MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255));
                    DockShadow.Color = MediaColor.FromRgb(0, 0, 0);
                    break;
                case "Transparent":
                    MainDockBorder.Background = new SolidColorBrush(MediaColor.FromArgb(_settings.ReduceTransparency ? (byte)140 : (byte)35, 0, 0, 0));
                    MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(45, 255, 255, 255));
                    DockShadow.Color = MediaColor.FromArgb(90, 0, 0, 0);
                    break;
                default: // DarkGlass
                    MainDockBorder.Background = new SolidColorBrush(MediaColor.FromArgb(baseAlpha, 16, 16, 22));
                    MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(51, 255, 255, 255));
                    DockShadow.Color = MediaColor.FromRgb(0, 0, 0);
                    break;
            }

            // 6. Konumlandırma
            RepositionDock();

            // 6.5 Açık olan Widget Kütüphanesi varsa senkronize et
            if (_widgetBrowserWindow != null && _widgetBrowserWindow.IsLoaded)
            {
                _widgetBrowserWindow.RefreshFromDock();
            }

            // 7. Hava Durumu Şehir Kontrolü
            if (_settings.WeatherCity != _lastWeatherCity)
            {
                _ = FetchWeatherAsync();
            }
        }

        private void UpdateProfileTabsUI()
        {
            SpProfileTabs.Children.Clear();
            var active = _settings.GetActiveProfile();
            bool isLight = _settings.DockStyle == "LightGlass";

            foreach (var profile in _settings.Profiles)
            {
                var btn = new WpfButton
                {
                    Style = (Style)FindResource("ProfileTabStyle"),
                    Content = profile.Name,
                    Tag = profile.Id,
                    AllowDrop = true,
                    ToolTip = $"Sol Tık: '{profile.Name}' Alanına Geç | Sağ Tık: Alanı Sil, Yeniden Adlandır veya Klonla"
                };
                if (profile.Id == active.Id)
                {
                    btn.Background = new SolidColorBrush(MediaColor.FromArgb(60, 56, 189, 248));
                    btn.Foreground = isLight ? new SolidColorBrush(MediaColor.FromRgb(2, 132, 199)) : new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
                    btn.FontWeight = FontWeights.Bold;
                }
                else
                {
                    btn.Foreground = isLight ? new SolidColorBrush(MediaColor.FromRgb(30, 41, 59)) : new SolidColorBrush(MediaColor.FromArgb(176, 255, 255, 255));
                }

                // Sağ Tık Çalışma Alanı Menüsü (Silme, Yeniden Adlandırma, Klonlama, Yeni Ekleme)
                var tabMenu = new WpfContextMenu
                {
                    Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 30)),
                    Foreground = WpfBrushes.White,
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(50, 255, 255, 255)),
                    BorderThickness = new Thickness(1)
                };

                var itemRename = new WpfMenuItem { Header = $"✏️  '{profile.Name}' Alanını Yeniden Adlandır..." };
                itemRename.Click += (s, ev) => ShowProfileRenameDialog(profile);
                tabMenu.Items.Add(itemRename);

                var itemClone = new WpfMenuItem { Header = $"📋  '{profile.Name}' Alanını Klonla" };
                itemClone.Click += (s, ev) => CloneProfile(profile);
                tabMenu.Items.Add(itemClone);

                var itemAdd = new WpfMenuItem { Header = "➕  Yeni Çalışma Alanı Ekle..." };
                itemAdd.Click += (s, ev) => ShowAddNewProfileDialog();
                tabMenu.Items.Add(itemAdd);

                tabMenu.Items.Add(new WpfSeparator());

                var itemDelete = new WpfMenuItem
                {
                    Header = $"🗑️  '{profile.Name}' Alanını Sil",
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)),
                    IsEnabled = _settings.Profiles.Count > 1
                };
                if (_settings.Profiles.Count <= 1)
                {
                    itemDelete.ToolTip = "En az 1 çalışma alanı kalmalıdır; tek profil silinemez.";
                }
                itemDelete.Click += (s, ev) => DeleteProfile(profile);
                tabMenu.Items.Add(itemDelete);

                btn.ContextMenu = tabMenu;

                btn.Click += BtnProfileTab_Click;
                btn.DragOver += ProfileTab_DragOver;
                btn.DragLeave += ProfileTab_DragLeave;
                btn.Drop += ProfileTab_Drop;
                SpProfileTabs.Children.Add(btn);
            }

            // Hızlı Yeni Çalışma Alanı Ekle Butonu
            var btnAddTab = new WpfButton
            {
                Style = (Style)FindResource("ProfileTabStyle"),
                Content = "➕ Alan",
                ToolTip = "Hızlı Yeni Çalışma Alanı (Profil) Ekle",
                FontWeight = FontWeights.SemiBold,
                Foreground = isLight ? new SolidColorBrush(MediaColor.FromRgb(2, 132, 199)) : new SolidColorBrush(MediaColor.FromRgb(56, 189, 248))
            };
            btnAddTab.Click += (s, ev) => ShowAddNewProfileDialog();
            SpProfileTabs.Children.Add(btnAddTab);

            // Widget Kütüphanesi Kısayol Butonu
            var btnLib = new WpfButton
            {
                Style = (Style)FindResource("ProfileTabStyle"),
                Content = "🧩 + Widget",
                ToolTip = "12 Widget Kütüphanesini Aç (Sürükle & Bırak)",
                Foreground = isLight ? new SolidColorBrush(MediaColor.FromRgb(2, 132, 199)) : new SolidColorBrush(MediaColor.FromRgb(56, 189, 248))
            };
            btnLib.Click += BtnOpenWidgetBrowser_Click;
            SpProfileTabs.Children.Add(btnLib);
        }

        public void DeleteProfile(DockProfile profile)
        {
            if (_settings.Profiles.Count <= 1)
            {
                System.Windows.MessageBox.Show("En az bir çalışma alanı bulunmalıdır. Son profil silinemez.", "OxiDock Çalışma Alanı", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show($"'{profile.Name}' çalışma alanını silmek istediğinize emin misiniz?", "Çalışma Alanını Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _settings.Profiles.Remove(profile);
                if (_settings.ActiveProfileId == profile.Id)
                {
                    _settings.ActiveProfileId = _settings.Profiles[0].Id;
                }
                _settings.Save();
                ApplySettings(_settings);
            }
        }

        public void ShowProfileRenameDialog(DockProfile profile)
        {
            var dlg = new Window
            {
                Title = "OxiDock • Çalışma Alanını Yeniden Adlandır",
                Width = 360,
                Height = 170,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(MediaColor.FromRgb(20, 20, 28)),
                Foreground = WpfBrushes.White,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize
            };
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock { Text = "Çalışma Alanı Adı:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var txt = new TextBox { Text = profile.Name, Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 14) };
            txt.SelectAll();
            sp.Children.Add(txt);

            var dp = new DockPanel();
            var btnSave = new WpfButton { Content = "Kaydet", Padding = new Thickness(14, 6, 14, 6), Background = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)), Foreground = WpfBrushes.Black, FontWeight = FontWeights.Bold };
            btnSave.Click += (s, e) =>
            {
                if (!string.IsNullOrWhiteSpace(txt.Text))
                {
                    profile.Name = txt.Text.Trim();
                    _settings.Save();
                    ApplySettings(_settings);
                }
                dlg.Close();
            };
            dp.Children.Add(btnSave);
            sp.Children.Add(dp);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        public void ShowAddNewProfileDialog()
        {
            var dlg = new Window
            {
                Title = "OxiDock • Yeni Çalışma Alanı Ekle",
                Width = 380,
                Height = 175,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(MediaColor.FromRgb(20, 20, 28)),
                Foreground = WpfBrushes.White,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize
            };
            var sp = new StackPanel { Margin = new Thickness(16) };
            sp.Children.Add(new TextBlock { Text = "Yeni Çalışma Alanı Adı (Örn: Kodlama, Oyun, Medya):", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 8) });
            var txt = new TextBox { Text = $"{_settings.Profiles.Count + 1}. Alan", Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(8, 6, 8, 6), Margin = new Thickness(0, 0, 0, 14) };
            txt.SelectAll();
            sp.Children.Add(txt);

            var dp = new DockPanel();
            var btnCreate = new WpfButton { Content = "Oluştur ve Geçiş Yap", Padding = new Thickness(14, 6, 14, 6), Background = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)), Foreground = WpfBrushes.Black, FontWeight = FontWeights.Bold };
            btnCreate.Click += (s, e) =>
            {
                string name = string.IsNullOrWhiteSpace(txt.Text) ? $"Alan {_settings.Profiles.Count + 1}" : txt.Text.Trim();
                var newProfile = new DockProfile
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    ShowClock = true,
                    ShowWeather = true,
                    ShowSystemStats = true,
                    ShowApps = true,
                    ShowMusic = true,
                    ShowNotes = true,
                    ShowPomodoro = true,
                    ShowBattery = true
                };
                _settings.Profiles.Add(newProfile);
                _settings.ActiveProfileId = newProfile.Id;
                _settings.Save();
                ApplySettings(_settings);
                dlg.Close();
            };
            dp.Children.Add(btnCreate);
            sp.Children.Add(dp);
            dlg.Content = sp;
            dlg.ShowDialog();
        }

        public void CloneProfile(DockProfile source)
        {
            var cloned = new DockProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = source.Name + " (Kopya)",
                ShowClock = source.ShowClock,
                ShowWeather = source.ShowWeather,
                ShowSystemStats = source.ShowSystemStats,
                ShowApps = source.ShowApps,
                ShowMusic = source.ShowMusic,
                ShowNotes = source.ShowNotes,
                ShowPomodoro = source.ShowPomodoro,
                ShowBattery = source.ShowBattery,
                ShowCustomAction = source.ShowCustomAction,
                ShowNetwork = source.ShowNetwork
            };
            _settings.Profiles.Add(cloned);
            _settings.ActiveProfileId = cloned.Id;
            _settings.Save();
            ApplySettings(_settings);
        }

        private void BtnProfileTab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string profileId)
            {
                SwitchProfile(profileId);
            }
        }

        public void SwitchProfile(string profileId)
        {
            _settings.ActiveProfileId = profileId;
            _settings.Save();
            ApplySettings(_settings);
        }

        private void RepositionDock()
        {
            var workArea = SystemParameters.WorkArea;
            double currentWidth = ActualWidth > 0 ? ActualWidth : (double.IsNaN(Width) ? 600 : Width);
            double currentHeight = ActualHeight > 0 ? ActualHeight : (double.IsNaN(Height) ? 64 : Height);
            double scaledWidth = currentWidth * _settings.DockScale;
            double scaledHeight = currentHeight * _settings.DockScale;

            // Yatay Konum
            switch (_settings.DockAlignment)
            {
                case "Left":
                    Left = workArea.Left + 20;
                    break;
                case "Right":
                    Left = workArea.Right - scaledWidth - 20;
                    break;
                default: // Center
                    Left = (workArea.Width - scaledWidth) / 2 + workArea.Left;
                    break;
            }

            // Dikey Konum
            Top = _settings.DockPosition == "Top" 
                ? workArea.Top + 6 
                : workArea.Height - scaledHeight - 6 + workArea.Top;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                if (e.OriginalSource is DependencyObject dep)
                {
                    DependencyObject? current = dep;
                    while (current != null)
                    {
                        if (current is TextBox || current is Slider || current is WpfButton) return;
                        current = VisualTreeHelper.GetParent(current);
                    }
                }
                DragMove();
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RepositionDock();
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void ApplyWidgetSizes(DockProfile p)
        {
            // Clock
            PnlClock1x1.Visibility = p.ClockSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlClock1x2.Visibility = (p.ClockSize == "1x2" || string.IsNullOrEmpty(p.ClockSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlClock1x3.Visibility = p.ClockSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // Pomodoro
            PnlPomo1x1.Visibility = p.PomodoroSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlPomo1x2.Visibility = (p.PomodoroSize == "1x2" || string.IsNullOrEmpty(p.PomodoroSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlPomo1x3.Visibility = p.PomodoroSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // Weather
            PnlWeather1x1.Visibility = p.WeatherSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlWeather1x2.Visibility = (p.WeatherSize == "1x2" || string.IsNullOrEmpty(p.WeatherSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlWeather1x3.Visibility = p.WeatherSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // System
            PnlSystem1x1.Visibility = p.SystemSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlSystem1x2.Visibility = (p.SystemSize == "1x2" || string.IsNullOrEmpty(p.SystemSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlSystem1x3.Visibility = p.SystemSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // Apps
            if (p.AppsSize == "1x1")
            {
                BtnAppCalc.Visibility = Visibility.Collapsed;
                BtnAppNote.Visibility = Visibility.Collapsed;
                BtnAppTask.Visibility = Visibility.Collapsed;
                BtnAppBrowser.Visibility = Visibility.Collapsed;
                BtnAppPaint.Visibility = Visibility.Collapsed;
                BtnAppCmd.Visibility = Visibility.Collapsed;
            }
            else if (p.AppsSize == "1x3")
            {
                BtnAppCalc.Visibility = Visibility.Visible;
                BtnAppNote.Visibility = Visibility.Visible;
                BtnAppTask.Visibility = Visibility.Visible;
                BtnAppBrowser.Visibility = Visibility.Visible;
                BtnAppPaint.Visibility = Visibility.Visible;
                BtnAppCmd.Visibility = Visibility.Visible;
            }
            else // 1x2 Default
            {
                BtnAppCalc.Visibility = Visibility.Visible;
                BtnAppNote.Visibility = Visibility.Visible;
                BtnAppTask.Visibility = Visibility.Visible;
                BtnAppBrowser.Visibility = Visibility.Collapsed;
                BtnAppPaint.Visibility = Visibility.Collapsed;
                BtnAppCmd.Visibility = Visibility.Collapsed;
            }

            // Music
            PnlMusic1x1.Visibility = p.MusicSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlMusic1x2.Visibility = (p.MusicSize == "1x2" || string.IsNullOrEmpty(p.MusicSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlMusic1x3.Visibility = p.MusicSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // Crypto
            PnlCrypto1x1.Visibility = p.CryptoSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlCrypto1x2.Visibility = (p.CryptoSize == "1x2" || string.IsNullOrEmpty(p.CryptoSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlCrypto1x3.Visibility = p.CryptoSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // Calendar
            PnlCalendar1x1.Visibility = p.CalendarSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlCalendar1x2.Visibility = (p.CalendarSize == "1x2" || string.IsNullOrEmpty(p.CalendarSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlCalendar1x3.Visibility = p.CalendarSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // QuickSearch
            PnlSearch1x1.Visibility = p.QuickSearchSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlSearch1x2.Visibility = (p.QuickSearchSize == "1x2" || string.IsNullOrEmpty(p.QuickSearchSize)) ? Visibility.Visible : Visibility.Collapsed;
            PnlSearch1x3.Visibility = p.QuickSearchSize == "1x3" ? Visibility.Visible : Visibility.Collapsed;

            // Volume
            PnlVolume1x1.Visibility = p.VolumeSize == "1x1" ? Visibility.Visible : Visibility.Collapsed;
            PnlVolume1x2.Visibility = (p.VolumeSize == "1x2" || string.IsNullOrEmpty(p.VolumeSize)) ? Visibility.Visible : Visibility.Collapsed;

            UpdateCalendarVisuals();
            UpdateClipboardPreview();
        }

        private void Window_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? cur = dep;
                while (cur != null && cur != this)
                {
                    if (cur is FrameworkElement fe && fe.ContextMenu != null)
                    {
                        fe.ContextMenu.PlacementTarget = fe;
                        fe.ContextMenu.IsOpen = true;
                        e.Handled = true;
                        return;
                    }
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }
            ShowDockContextMenu();
        }

        private void ShowDockContextMenu()
        {
            var menu = new WpfContextMenu
            {
                Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 30)),
                Foreground = WpfBrushes.White,
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(40, 255, 255, 255)),
                BorderThickness = new Thickness(1)
            };

            // 1. Widget Kütüphanesi
            var itemWidgets = new WpfMenuItem { Header = "🧩  Widget Kütüphanesini Aç (160+)..." };
            itemWidgets.Click += (s, e) => OpenWidgetBrowserWindow();
            menu.Items.Add(itemWidgets);

            menu.Items.Add(new WpfSeparator());

            // 2. Profil Değiştir (Alt Menü)
            var itemProfiles = new WpfMenuItem { Header = "📁  Profil Değiştir" };
            var active = _settings.GetActiveProfile();
            foreach (var p in _settings.Profiles)
            {
                var pItem = new WpfMenuItem 
                { 
                    Header = p.Name, 
                    IsChecked = (p.Id == active.Id),
                    Tag = p.Id 
                };
                pItem.Click += (s, e) => SwitchProfile((string)((WpfMenuItem)s).Tag);
                itemProfiles.Items.Add(pItem);
            }
            menu.Items.Add(itemProfiles);

            // 2.5 Hızlı Tema Değiştir (Alt Menü)
            var itemThemes = new WpfMenuItem { Header = "🎨  Hızlı Tema Seç..." };
            var themeList = new[]
            {
                ("DarkGlass", "🌙 Dark Glass (Koyu Cam)"),
                ("LiquidGlass", "🌊 Liquid Glass (Akıcı Mavi Cam)"),
                ("LightGlass", "❄️ Light Glass (Aydınlık Kristal)"),
                ("Cyberpunk", "⚡ Cyberpunk Neon (Mor/Neon)"),
                ("OledBlack", "⬛ OLED Deep Black (Saf Siyah)"),
                ("Transparent", "🪟 Saydam (Floating)")
            };
            foreach (var (tKey, tLabel) in themeList)
            {
                var tItem = new WpfMenuItem
                {
                    Header = tLabel,
                    IsChecked = (_settings.DockStyle == tKey),
                    Tag = tKey
                };
                tItem.Click += (s, e) =>
                {
                    _settings.DockStyle = (string)((WpfMenuItem)s).Tag;
                    _settings.Save();
                    ApplySettings(_settings);
                };
                itemThemes.Items.Add(tItem);
            }
            menu.Items.Add(itemThemes);

            menu.Items.Add(new WpfSeparator());

            // 3. Pomodoro Hızlı Kontrol
            var itemPomo = new WpfMenuItem { Header = _pomoIsRunning ? "⏸  Pomodoro'yu Duraklat" : "▶  Pomodoro Odak Başlat" };
            itemPomo.Click += (s, e) => TogglePomodoro();
            menu.Items.Add(itemPomo);

            var itemPomoReset = new WpfMenuItem { Header = "↺  Pomodoro'yu Sıfırla (25 dk)" };
            itemPomoReset.Click += (s, e) => ResetPomodoro();
            menu.Items.Add(itemPomoReset);

            menu.Items.Add(new WpfSeparator());

            // 4. Ayarlar & Çıkış
            var itemSettings = new WpfMenuItem { Header = "⚙️  OxiDock Ayarlar..." };
            itemSettings.Click += (s, e) => OpenSettingsWindow();
            menu.Items.Add(itemSettings);

            var itemExit = new WpfMenuItem { Header = "✕  Çıkış" };
            itemExit.Click += (s, e) => CloseApplication();
            menu.Items.Add(itemExit);

            menu.IsOpen = true;
        }

        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_settings.AutoHide)
            {
                Opacity = 1.0;
            }
            ProfileBarBorder.Visibility = Visibility.Visible;
        }

        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (_settings.AutoHide)
            {
                Opacity = 0.25;
            }
        }

        #endregion

        #region Widget Kütüphanesi & Ayarlar Panelleri

        private void BtnOpenWidgetBrowser_Click(object sender, RoutedEventArgs e)
        {
            OpenWidgetBrowserWindow();
        }

        private void OpenWidgetBrowserWindow()
        {
            if (_widgetBrowserWindow != null && _widgetBrowserWindow.IsLoaded)
            {
                _widgetBrowserWindow.Close();
                _widgetBrowserWindow = null;
                return;
            }

            _widgetBrowserWindow = new WidgetBrowserWindow(_settings);
            _widgetBrowserWindow.Owner = this;
            _widgetBrowserWindow.PositionAboveDock(Left, Top, Width * _settings.DockScale, Height * _settings.DockScale);
            _widgetBrowserWindow.OnSettingsUpdated = (newSettings) => ApplySettings(newSettings);
            _widgetBrowserWindow.Closed += (s, e) => _widgetBrowserWindow = null;
            _widgetBrowserWindow.Show();
        }

        #region Sürükle ve Bırak (Drag & Drop) Olayları

        private void MainDockBorder_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("OxiDockWidget"))
            {
                MainDockBorder.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
            }
        }

        private void MainDockBorder_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("OxiDockWidget"))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainDockBorder_DragLeave(object sender, DragEventArgs e)
        {
            ApplySettings(_settings);
        }

        private void MainDockBorder_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("OxiDockWidget") && e.Data.GetData("OxiDockWidget") is string widgetKey)
            {
                var activeProfile = _settings.GetActiveProfile();
                EnableWidgetInProfile(activeProfile, widgetKey);
                _settings.Save();
                ApplySettings(_settings);
            }
        }

        private void ProfileTab_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("OxiDockWidget") && sender is WpfButton btn)
            {
                e.Effects = DragDropEffects.Copy;
                btn.Background = new SolidColorBrush(MediaColor.FromArgb(90, 56, 189, 248));
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void ProfileTab_DragLeave(object sender, DragEventArgs e)
        {
            UpdateProfileTabsUI();
        }

        private void ProfileTab_Drop(object sender, DragEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string profileId && 
                e.Data.GetDataPresent("OxiDockWidget") && e.Data.GetData("OxiDockWidget") is string widgetKey)
            {
                var targetProfile = _settings.Profiles.Find(p => p.Id == profileId);
                if (targetProfile != null)
                {
                    EnableWidgetInProfile(targetProfile, widgetKey);
                    _settings.Save();
                    // Hedef profile anında geçiş yap ve güncelle
                    SwitchProfile(targetProfile.Id);
                }
            }
        }

        private void EnableWidgetInProfile(DockProfile profile, string widgetKey)
        {
            if (widgetKey.StartsWith("customwidget:", StringComparison.OrdinalIgnoreCase))
            {
                string id = widgetKey.Substring("customwidget:".Length);
                var cw = profile.CustomWidgets?.Find(x => x.Id == id);
                if (cw != null) cw.IsEnabled = true;
                return;
            }

            switch (widgetKey.ToLowerInvariant())
            {
                case "clock": profile.ShowClock = true; break;
                case "pomodoro": profile.ShowPomodoro = true; break;
                case "weather": profile.ShowWeather = true; break;
                case "system": profile.ShowSystemStats = true; break;
                case "battery": profile.ShowBattery = true; break;
                case "apps": profile.ShowApps = true; break;
                case "music": profile.ShowMusic = true; break;
                case "notes": profile.ShowNotes = true; break;
                case "customaction":
                case "custom": profile.ShowCustomAction = true; break;
                case "network":
                case "ping": profile.ShowNetwork = true; break;
                case "crypto": profile.ShowCrypto = true; break;
                case "calendar": profile.ShowCalendar = true; break;
                case "quicksearch":
                case "search": profile.ShowQuickSearch = true; break;
                case "volume": profile.ShowVolume = true; break;
                case "clipboard": profile.ShowClipboard = true; break;
            }
        }

        #endregion

        #region Kişiselleştirilmiş Özel Araç & Canlı Ağ Olayları

        private void WidgetCustomAction_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                try
                {
                    string path = string.IsNullOrWhiteSpace(_settings.CustomActionPath) ? "wt.exe" : _settings.CustomActionPath;
                    var psi = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = _settings.CustomActionArgs,
                        UseShellExecute = true
                    };
                    Process.Start(psi);
                }
                catch (Exception ex)
                {
                    Forms.MessageBox.Show($"Özel araç başlatılamadı:\n{ex.Message}\n\nLütfen sağ tıklayıp aracı özelleştirin.", "OxiDock Özel Araç", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
                }
            }
        }

        private void MenuCustomizeAction_Click(object sender, RoutedEventArgs e)
        {
            OpenCustomActionDialog();
        }

        private void MenuHideCustomAction_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowCustomAction = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        public void OpenCustomActionDialog()
        {
            var dlg = new Window
            {
                Title = "OxiDock • Özel Araç / İşlem Yapılandırması",
                Width = 430,
                Height = 330,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(MediaColor.FromRgb(20, 20, 28)),
                Foreground = WpfBrushes.White,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize
            };

            var sp = new StackPanel { Margin = new Thickness(16) };

            sp.Children.Add(new TextBlock { Text = "⚡ Sık Kullanılan Özel Araç / İşlem", FontWeight = FontWeights.Bold, FontSize = 14, Margin = new Thickness(0, 0, 0, 12) });

            sp.Children.Add(new TextBlock { Text = "Görünen Ad:", FontSize = 12, Foreground = new SolidColorBrush(MediaColor.FromArgb(180, 255, 255, 255)) });
            var txtTitle = new TextBox { Text = _settings.CustomActionTitle, Margin = new Thickness(0, 4, 0, 10), Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            sp.Children.Add(txtTitle);

            sp.Children.Add(new TextBlock { Text = "Simge / Emoji (⚡, 🚀, 💻, 🌐, 🛠️, 📝, 🎨, 📊):", FontSize = 12, Foreground = new SolidColorBrush(MediaColor.FromArgb(180, 255, 255, 255)) });
            var txtIcon = new TextBox { Text = _settings.CustomActionIcon, Margin = new Thickness(0, 4, 0, 10), Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            sp.Children.Add(txtIcon);

            sp.Children.Add(new TextBlock { Text = "Uygulama (.exe, .bat, .lnk), Klasör veya Web URL'si:", FontSize = 12, Foreground = new SolidColorBrush(MediaColor.FromArgb(180, 255, 255, 255)) });
            var dpPath = new DockPanel { Margin = new Thickness(0, 4, 0, 14) };
            var btnBrowse = new WpfButton { Content = "Gözat...", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            var txtPath = new TextBox { Text = _settings.CustomActionPath, Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            btnBrowse.Click += (s, e) =>
            {
                var ofd = new Forms.OpenFileDialog
                {
                    Title = "Başlatılacak Uygulamayı Seçin",
                    Filter = "Uygulamalar ve Kısayollar (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Tüm Dosyalar (*.*)|*.*"
                };
                if (ofd.ShowDialog() == Forms.DialogResult.OK)
                {
                    txtPath.Text = ofd.FileName;
                    if (string.IsNullOrWhiteSpace(txtTitle.Text) || txtTitle.Text == "Terminal")
                    {
                        txtTitle.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };
            DockPanel.SetDock(btnBrowse, Dock.Right);
            dpPath.Children.Add(btnBrowse);
            dpPath.Children.Add(txtPath);
            sp.Children.Add(dpPath);

            var dpBtns = new DockPanel();
            var btnSave = new WpfButton { Content = "Kaydet ve Dock'a Ekle", Padding = new Thickness(16, 6, 16, 6), Background = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)), Foreground = WpfBrushes.Black, FontWeight = FontWeights.Bold };
            btnSave.Click += (s, e) =>
            {
                _settings.CustomActionTitle = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Özel Araç" : txtTitle.Text.Trim();
                _settings.CustomActionIcon = string.IsNullOrWhiteSpace(txtIcon.Text) ? "⚡" : txtIcon.Text.Trim();
                _settings.CustomActionPath = string.IsNullOrWhiteSpace(txtPath.Text) ? "wt.exe" : txtPath.Text.Trim();
                var p = _settings.GetActiveProfile();
                p.ShowCustomAction = true;
                _settings.Save();
                ApplySettings(_settings);
                dlg.Close();
            };
            dpBtns.Children.Add(btnSave);
            sp.Children.Add(dpBtns);

            dlg.Content = sp;
            dlg.ShowDialog();
        }

        private static string? ResolveExecutablePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath)) return null;
            string cleaned = Environment.ExpandEnvironmentVariables(rawPath.Trim().Trim('"'));
            if (cleaned.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                cleaned.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (File.Exists(cleaned)) return cleaned;

            string fileName = cleaned;
            if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) &&
                !fileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                fileName += ".exe";
            }

            // 1. Registry App Paths (winword.exe, excel.exe, chrome.exe vb.)
            try
            {
                using var keyLM = Microsoft.Win32.Registry.LocalMachine.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{Path.GetFileName(fileName)}");
                if (keyLM?.GetValue(null) is string regPathLM && File.Exists(regPathLM.Trim('"')))
                    return regPathLM.Trim('"');

                using var keyCU = Microsoft.Win32.Registry.CurrentUser.OpenSubKey($@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{Path.GetFileName(fileName)}");
                if (keyCU?.GetValue(null) is string regPathCU && File.Exists(regPathCU.Trim('"')))
                    return regPathCU.Trim('"');
            }
            catch { }

            // 2. PATH ve Sistem Klasörleri
            var searchDirs = new List<string>
            {
                Environment.SystemDirectory,
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps")
            };
            var envPath = Environment.GetEnvironmentVariable("PATH");
            if (!string.IsNullOrEmpty(envPath))
            {
                searchDirs.AddRange(envPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries));
            }

            foreach (var dir in searchDirs)
            {
                try
                {
                    string candidate = Path.Combine(dir, fileName);
                    if (File.Exists(candidate)) return candidate;
                    string candidateRaw = Path.Combine(dir, cleaned);
                    if (File.Exists(candidateRaw)) return candidateRaw;
                }
                catch { }
            }

            return null;
        }

        public static ImageSource? TryExtractAppIconImage(string targetPath)
        {
            try
            {
                string? resolved = ResolveExecutablePath(targetPath);
                if (string.IsNullOrEmpty(resolved) || !File.Exists(resolved)) return null;

                using var sysIcon = System.Drawing.Icon.ExtractAssociatedIcon(resolved);
                if (sysIcon == null) return null;

                var bmp = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
                    sysIcon.Handle,
                    Int32Rect.Empty,
                    System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());
                bmp.Freeze();
                return bmp;
            }
            catch
            {
                return null;
            }
        }

        public static MediaColor ParseAccentColor(string? hex)
        {
            var fallback = MediaColor.FromRgb(56, 189, 248);
            try
            {
                if (!string.IsNullOrWhiteSpace(hex))
                {
                    var converted = System.Windows.Media.ColorConverter.ConvertFromString(hex.Trim());
                    if (converted is MediaColor mc) return mc;
                }
            }
            catch { }
            return fallback;
        }

        public void ApplyCustomWidgetSurface(Border container, CustomAppWidget w)
        {
            var accent = ParseAccentColor(w.AccentColor);
            double radius = w.CornerStyle switch
            {
                "Capsule" => 18,
                "Sharp" => 7,
                _ => 13 // Squircle (HyperOS / HarmonyOS)
            };
            container.CornerRadius = new CornerRadius(radius);

            switch (w.BackgroundMode)
            {
                case "Gradient":
                    container.Background = new LinearGradientBrush(
                        MediaColor.FromArgb(145, accent.R, accent.G, accent.B),
                        MediaColor.FromArgb(210, 15, 23, 42),
                        45.0);
                    container.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(170, accent.R, accent.G, accent.B));
                    container.BorderThickness = new Thickness(1.2);
                    break;
                case "Solid":
                    container.Background = new LinearGradientBrush(
                        MediaColor.FromArgb(235, accent.R, accent.G, accent.B),
                        MediaColor.FromArgb(210, (byte)(accent.R * 0.7), (byte)(accent.G * 0.7), (byte)(accent.B * 0.7)),
                        35.0);
                    container.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(220, 255, 255, 255));
                    container.BorderThickness = new Thickness(1);
                    break;
                case "Outline":
                    container.Background = new SolidColorBrush(MediaColor.FromArgb(120, 12, 16, 26));
                    container.BorderBrush = new SolidColorBrush(accent);
                    container.BorderThickness = new Thickness(1.5);
                    break;
                default: // Glass (HarmonyOS / HyperOS Frosted Glass)
                    container.Background = new LinearGradientBrush(
                        MediaColor.FromArgb(55, accent.R, accent.G, accent.B),
                        MediaColor.FromArgb(35, 255, 255, 255),
                        45.0);
                    container.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(95, accent.R, accent.G, accent.B));
                    container.BorderThickness = new Thickness(1);
                    break;
            }
        }

        public void RenderCustomWidgets(DockProfile profile)
        {
            SpCustomWidgets.Children.Clear();
            if (profile.CustomWidgets == null) return;

            for (int idx = 0; idx < profile.CustomWidgets.Count; idx++)
            {
                var widget = profile.CustomWidgets[idx];
                if (!widget.IsEnabled) continue;

                var border = new Border
                {
                    Style = (Style)FindResource("WidgetContainerStyle"),
                    Cursor = WpfCursors.Hand,
                    Tag = widget,
                    Padding = new Thickness(4, 2, 4, 2)
                };

                ApplyCustomWidgetSurface(border, widget);

                border.ToolTip = $"✨ {widget.Title} ({widget.WidgetSize})\n{widget.Subtitle}\n• Sol Tık: Başlat | Üzerine Gel: ✎ Düzenle / ✕ Sil | Sağ Tık: Tüm Seçenekler";

                // Ana kapsayıcı: İçerik + Android/HyperOS Hover Aksiyon Barı (✎ Düzenle | 📏 Boyut | ✕ Kaldır)
                var rootGrid = new Grid();
                var contentEl = BuildCustomWidgetContent(widget);
                rootGrid.Children.Add(contentEl);

                var hoverPill = new Border
                {
                    Background = new SolidColorBrush(MediaColor.FromArgb(235, 15, 23, 42)),
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(140, 56, 189, 248)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(2, 1, 2, 1),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, -2, -2, 0),
                    Visibility = Visibility.Collapsed
                };

                var spHoverActions = new StackPanel { Orientation = WpfOrientation.Horizontal };

                var btnQuickEdit = new WpfButton
                {
                    Style = (Style)FindResource("DockButtonStyle"),
                    Width = 18,
                    Height = 18,
                    Content = new TextBlock { Text = "✎", FontSize = 9, Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)), FontWeight = FontWeights.Bold },
                    ToolTip = "Widget Stüdyosunda Düzenle / Özelleştir"
                };
                btnQuickEdit.Click += (s, ev) =>
                {
                    ev.Handled = true;
                    ShowCustomWidgetEditorDialog(widget);
                };

                var btnQuickSize = new WpfButton
                {
                    Style = (Style)FindResource("DockButtonStyle"),
                    Width = 18,
                    Height = 18,
                    Margin = new Thickness(2, 0, 2, 0),
                    Content = new TextBlock { Text = "📏", FontSize = 8 },
                    ToolTip = "Boyutu Değiştir (1x1 → 1x2 → 1x3)"
                };
                btnQuickSize.Click += (s, ev) =>
                {
                    ev.Handled = true;
                    widget.WidgetSize = widget.WidgetSize switch
                    {
                        "1x1" => "1x2",
                        "1x2" => "1x3",
                        _ => "1x1"
                    };
                    _settings.Save();
                    ApplySettings(_settings);
                };

                var btnQuickDelete = new WpfButton
                {
                    Style = (Style)FindResource("DockButtonStyle"),
                    Width = 18,
                    Height = 18,
                    Content = new TextBlock { Text = "✕", FontSize = 9, Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)), FontWeight = FontWeights.Bold },
                    ToolTip = "Widget'ı Dock'tan Kaldır"
                };
                btnQuickDelete.Click += (s, ev) =>
                {
                    ev.Handled = true;
                    profile.CustomWidgets.Remove(widget);
                    _settings.Save();
                    ApplySettings(_settings);
                };

                spHoverActions.Children.Add(btnQuickEdit);
                spHoverActions.Children.Add(btnQuickSize);
                spHoverActions.Children.Add(btnQuickDelete);
                hoverPill.Child = spHoverActions;
                rootGrid.Children.Add(hoverPill);

                border.MouseEnter += (s, ev) => hoverPill.Visibility = Visibility.Visible;
                border.MouseLeave += (s, ev) => hoverPill.Visibility = Visibility.Collapsed;

                // Tıklama ile çalıştırma (hover butonlarına tıklanmadıysa)
                border.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        if (e.OriginalSource is DependencyObject dep)
                        {
                            DependencyObject? cur = dep;
                            while (cur != null && cur != border)
                            {
                                if (cur is WpfButton) return;
                                cur = VisualTreeHelper.GetParent(cur);
                            }
                        }
                        e.Handled = true;
                        LaunchCustomWidget(widget);
                    }
                };

                // Zengin HarmonyOS 3 / HyperOS 3 Sağ Tık Menüsü
                var cm = new WpfContextMenu
                {
                    Background = new SolidColorBrush(MediaColor.FromRgb(20, 22, 32)),
                    Foreground = WpfBrushes.White,
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(80, 56, 189, 248)),
                    BorderThickness = new Thickness(1)
                };

                var miLaunch = new WpfMenuItem { Header = $"🚀  '{widget.Title}' Uygulamasını Başlat", FontWeight = FontWeights.Bold };
                miLaunch.Click += (s, e) => LaunchCustomWidget(widget);
                cm.Items.Add(miLaunch);

                var miSettings = new WpfMenuItem { Header = $"🎨  Widget Stüdyosunda Özelleştir / Düzenle...", Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)) };
                miSettings.Click += (s, e) => ShowCustomWidgetEditorDialog(widget);
                cm.Items.Add(miSettings);

                cm.Items.Add(new WpfSeparator());

                // Kart Şablonu Alt Menüsü (HarmonyOS 3 / HyperOS 3 / EMUI 13)
                var miTemplate = new WpfMenuItem { Header = "✨  OS Kart Tasarımı (Harmony / HyperOS / EMUI)" };
                void AddTemplateItem(string label, string key)
                {
                    var item = new WpfMenuItem { Header = label, IsChecked = widget.CardTemplate == key };
                    item.Click += (s, e) => { widget.CardTemplate = key; _settings.Save(); ApplySettings(_settings); };
                    miTemplate.Items.Add(item);
                }
                AddTemplateItem("🟢 HarmonyOS 3 Servis Kartı (Kapsül & Durum Noktası)", "HarmonyServiceCard");
                AddTemplateItem("🔵 Xiaomi HyperOS 3 Süper İkon (3B Parlama & Rozet)", "HyperSuperIcon");
                AddTemplateItem("🟠 Huawei EMUI 13 Akıllı Kombo (Çift Kısayol Grubu)", "EmuiComboCard");
                AddTemplateItem("🌊 OxiDock Liquid Glass (Neon Gradyan Kart)", "LiquidGlassCard");
                cm.Items.Add(miTemplate);

                // Yüzey Stili Alt Menüsü
                var miSurface = new WpfMenuItem { Header = "🪄  Arka Plan Yüzey Stili" };
                void AddSurfaceItem(string label, string key)
                {
                    var item = new WpfMenuItem { Header = label, IsChecked = widget.BackgroundMode == key };
                    item.Click += (s, e) => { widget.BackgroundMode = key; _settings.Save(); ApplySettings(_settings); };
                    miSurface.Items.Add(item);
                }
                AddSurfaceItem("🪟 Buzlu Cam (Frosted Glass)", "Glass");
                AddSurfaceItem("🌈 Çift Tonlu Gradyan (HyperOS Gradient)", "Gradient");
                AddSurfaceItem("🎨 Tam Renkli Dolgu (Android 14 Solid)", "Solid");
                AddSurfaceItem("⚡ Neon Çerçeve (Minimal Outline)", "Outline");
                cm.Items.Add(miSurface);

                // Boyut Değiştir Alt Menüsü
                var miSize = new WpfMenuItem { Header = "📏  Android Boyutu Değiştir" };
                var mi1x1 = new WpfMenuItem { Header = "1x1 Kare Kompakt", IsChecked = widget.WidgetSize == "1x1" };
                mi1x1.Click += (s, e) => { widget.WidgetSize = "1x1"; _settings.Save(); ApplySettings(_settings); };
                var mi1x2 = new WpfMenuItem { Header = "1x2 Yatay Standart", IsChecked = widget.WidgetSize == "1x2" };
                mi1x2.Click += (s, e) => { widget.WidgetSize = "1x2"; _settings.Save(); ApplySettings(_settings); };
                var mi1x3 = new WpfMenuItem { Header = "1x3 Genişletilmiş Servis Kartı", IsChecked = widget.WidgetSize == "1x3" };
                mi1x3.Click += (s, e) => { widget.WidgetSize = "1x3"; _settings.Save(); ApplySettings(_settings); };
                miSize.Items.Add(mi1x1);
                miSize.Items.Add(mi1x2);
                miSize.Items.Add(mi1x3);
                cm.Items.Add(miSize);

                // Sıralama & Çoğaltma
                int capturedIdx = idx;
                var miMoveLeft = new WpfMenuItem { Header = "⬅️  Sola Kaydır", IsEnabled = capturedIdx > 0 };
                miMoveLeft.Click += (s, e) =>
                {
                    if (capturedIdx > 0)
                    {
                        profile.CustomWidgets.RemoveAt(capturedIdx);
                        profile.CustomWidgets.Insert(capturedIdx - 1, widget);
                        _settings.Save();
                        ApplySettings(_settings);
                    }
                };
                cm.Items.Add(miMoveLeft);

                var miMoveRight = new WpfMenuItem { Header = "➡️  Sağa Kaydır", IsEnabled = capturedIdx < profile.CustomWidgets.Count - 1 };
                miMoveRight.Click += (s, e) =>
                {
                    if (capturedIdx < profile.CustomWidgets.Count - 1)
                    {
                        profile.CustomWidgets.RemoveAt(capturedIdx);
                        profile.CustomWidgets.Insert(capturedIdx + 1, widget);
                        _settings.Save();
                        ApplySettings(_settings);
                    }
                };
                cm.Items.Add(miMoveRight);

                var miDuplicate = new WpfMenuItem { Header = "📋  Widget'ı Çoğalt (Kopya Oluştur)" };
                miDuplicate.Click += (s, e) =>
                {
                    var clone = JsonSerializer.Deserialize<CustomAppWidget>(JsonSerializer.Serialize(widget));
                    if (clone != null)
                    {
                        clone.Id = Guid.NewGuid().ToString();
                        clone.Title = widget.Title + " +";
                        profile.CustomWidgets.Add(clone);
                        _settings.Save();
                        ApplySettings(_settings);
                    }
                };
                cm.Items.Add(miDuplicate);

                cm.Items.Add(new WpfSeparator());

                var miHide = new WpfMenuItem { Header = "👁️  Dock'ta Gizle (Kütüphanede Tut)" };
                miHide.Click += (s, e) =>
                {
                    widget.IsEnabled = false;
                    _settings.Save();
                    ApplySettings(_settings);
                };
                cm.Items.Add(miHide);

                var miDelete = new WpfMenuItem { Header = $"🗑️  '{widget.Title}' Widget'ını Kalıcı Kaldır", Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)), FontWeight = FontWeights.Bold };
                miDelete.Click += (s, e) =>
                {
                    profile.CustomWidgets.Remove(widget);
                    _settings.Save();
                    ApplySettings(_settings);
                };
                cm.Items.Add(miDelete);

                border.ContextMenu = cm;
                border.Child = rootGrid;

                SpCustomWidgets.Children.Add(border);
            }
        }

        public Border BuildCustomWidgetPreviewCard(CustomAppWidget w)
        {
            var previewBorder = new Border
            {
                Padding = new Thickness(6, 4, 6, 4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            ApplyCustomWidgetSurface(previewBorder, w);
            previewBorder.Child = BuildCustomWidgetContent(w);
            return previewBorder;
        }

        public static Border BuildStyledIconElement(CustomAppWidget w, double boxSize = 30, double fontSize = 14)
        {
            var accentColor = ParseAccentColor(w.AccentColor);
            double radius = w.CardTemplate == "HyperSuperIcon" ? boxSize * 0.34 : boxSize * 0.28;

            var iconBorder = new Border
            {
                Width = boxSize,
                Height = boxSize,
                CornerRadius = new CornerRadius(radius),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var iconTxt = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(w.IconValue) ? "📄" : w.IconValue,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            switch (w.IconType)
            {
                case "Monochrome":
                    iconBorder.Background = new SolidColorBrush(MediaColor.FromArgb(35, 255, 255, 255));
                    iconBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(110, 255, 255, 255));
                    iconBorder.BorderThickness = new Thickness(1);
                    iconTxt.Foreground = WpfBrushes.White;
                    break;
                case "Material":
                    iconBorder.Background = new SolidColorBrush(MediaColor.FromArgb(45, accentColor.R, accentColor.G, accentColor.B));
                    iconBorder.BorderBrush = new SolidColorBrush(accentColor);
                    iconBorder.BorderThickness = new Thickness(1.4);
                    iconTxt.Foreground = new SolidColorBrush(accentColor);
                    break;
                case "Color":
                    iconBorder.Background = new LinearGradientBrush(
                        accentColor,
                        MediaColor.FromRgb((byte)(accentColor.R * 0.65), (byte)(accentColor.G * 0.65), (byte)(accentColor.B * 0.65)),
                        45.0);
                    iconBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(140, 255, 255, 255));
                    iconBorder.BorderThickness = new Thickness(1);
                    iconTxt.Foreground = WpfBrushes.White;
                    break;
                case "SuperIcon":
                    iconBorder.Background = new LinearGradientBrush(
                        MediaColor.FromArgb(230, accentColor.R, accentColor.G, accentColor.B),
                        MediaColor.FromArgb(180, 15, 23, 42),
                        45.0);
                    iconBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(200, 255, 255, 255));
                    iconBorder.BorderThickness = new Thickness(1.3);
                    iconBorder.Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        Color = accentColor,
                        BlurRadius = 10,
                        ShadowDepth = 0,
                        Opacity = 0.65
                    };
                    iconTxt.Foreground = WpfBrushes.White;
                    break;
                default: // Fluent & NativeExe
                    iconBorder.Background = new SolidColorBrush(MediaColor.FromArgb(45, accentColor.R, accentColor.G, accentColor.B));
                    iconBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(120, accentColor.R, accentColor.G, accentColor.B));
                    iconBorder.BorderThickness = new Thickness(1);
                    iconTxt.Foreground = new SolidColorBrush(accentColor);
                    break;
            }

            // Gerçek .exe / .lnk Windows Simgesi Varsa Göster
            if (w.UseNativeExeIcon || w.IconType == "NativeExe")
            {
                var nativeImg = TryExtractAppIconImage(w.TargetPath);
                if (nativeImg != null)
                {
                    var img = new System.Windows.Controls.Image
                    {
                        Source = nativeImg,
                        Width = boxSize * 0.68,
                        Height = boxSize * 0.68,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
                    iconBorder.Child = img;
                    return iconBorder;
                }
            }

            iconBorder.Child = iconTxt;
            return iconBorder;
        }

        public FrameworkElement BuildCustomWidgetContent(CustomAppWidget w)
        {
            var accentColor = ParseAccentColor(w.AccentColor);
            bool isSolid = w.BackgroundMode == "Solid";
            WpfBrush primaryFg = isSolid ? WpfBrushes.White : (WpfBrush)FindResource("DockForegroundBrush");
            WpfBrush secondaryFg = isSolid
                ? new SolidColorBrush(MediaColor.FromArgb(220, 255, 255, 255))
                : new SolidColorBrush(accentColor);

            string template = string.IsNullOrWhiteSpace(w.CardTemplate) ? "HarmonyServiceCard" : w.CardTemplate;

            // 1. XIAOMI HYPEROS 3 "SUPER ICON" KARTI
            if (template == "HyperSuperIcon")
            {
                var iconEl = BuildStyledIconElement(w, w.WidgetSize == "1x1" ? 34 : 32, w.WidgetSize == "1x1" ? 16 : 15);
                iconEl.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = accentColor,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.7
                };

                if (w.WidgetSize == "1x1")
                {
                    var g = new Grid { MinWidth = 54, Margin = new Thickness(6, 2, 6, 2) };
                    var sp = new StackPanel
                    {
                        Orientation = WpfOrientation.Vertical,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    sp.Children.Add(iconEl);
                    sp.Children.Add(new TextBlock
                    {
                        Text = w.Title,
                        FontSize = 9,
                        FontWeight = FontWeights.Bold,
                        Foreground = primaryFg,
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 56,
                        Margin = new Thickness(0, 3, 0, 0)
                    });
                    g.Children.Add(sp);

                    if (!string.IsNullOrWhiteSpace(w.BadgeText))
                    {
                        var pill = new Border
                        {
                            Background = new SolidColorBrush(accentColor),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(3, 0, 3, 0),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                            VerticalAlignment = VerticalAlignment.Top,
                            Child = new TextBlock { Text = w.BadgeText, FontSize = 6.5, FontWeight = FontWeights.Bold, Foreground = WpfBrushes.Black }
                        };
                        g.Children.Add(pill);
                    }
                    return g;
                }
                else
                {
                    var dp = new DockPanel
                    {
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(7, 2, 7, 2),
                        MinWidth = w.WidgetSize == "1x3" ? 142 : 95
                    };
                    DockPanel.SetDock(iconEl, Dock.Left);
                    dp.Children.Add(iconEl);

                    var sp = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    var spTop = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    spTop.Children.Add(new TextBlock
                    {
                        Text = w.Title,
                        FontSize = 11,
                        FontWeight = FontWeights.Bold,
                        Foreground = primaryFg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = w.WidgetSize == "1x3" ? 85 : 58
                    });
                    if (!string.IsNullOrWhiteSpace(w.BadgeText))
                    {
                        spTop.Children.Add(new Border
                        {
                            Background = new SolidColorBrush(MediaColor.FromArgb(55, accentColor.R, accentColor.G, accentColor.B)),
                            BorderBrush = new SolidColorBrush(accentColor),
                            BorderThickness = new Thickness(0.8),
                            CornerRadius = new CornerRadius(4),
                            Padding = new Thickness(3, 0, 3, 0),
                            Margin = new Thickness(4, 0, 0, 0),
                            Child = new TextBlock { Text = w.BadgeText, FontSize = 7, FontWeight = FontWeights.Bold, Foreground = secondaryFg }
                        });
                    }
                    sp.Children.Add(spTop);
                    sp.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(w.Subtitle) ? "HyperOS Süper İkon" : w.Subtitle,
                        FontSize = 8.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = secondaryFg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = w.WidgetSize == "1x3" ? 100 : 68,
                        Margin = new Thickness(0, 1, 0, 0)
                    });
                    dp.Children.Add(sp);
                    return dp;
                }
            }

            // 2. HUAWEI EMUI 13 "SMART WIDGET COMBO" (ÇİFT KISAYOL GRUP KARTI)
            if (template == "EmuiComboCard")
            {
                var iconEl = BuildStyledIconElement(w, 28, 13);
                var dp = new DockPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 2, 6, 2),
                    MinWidth = w.WidgetSize == "1x1" ? 62 : (w.WidgetSize == "1x3" ? 150 : 108)
                };

                // Sağdaki 2. Hızlı Eylem Kapsülü (EMUI 13 Combo)
                var btnSub = new WpfButton
                {
                    Style = (Style)FindResource("DockButtonStyle"),
                    Height = 28,
                    Padding = new Thickness(6, 0, 6, 0),
                    Margin = new Thickness(6, 0, 0, 0),
                    Background = new SolidColorBrush(MediaColor.FromArgb(45, accentColor.R, accentColor.G, accentColor.B)),
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(110, accentColor.R, accentColor.G, accentColor.B)),
                    BorderThickness = new Thickness(1),
                    ToolTip = $"Kombo İkincil Eylem: {w.SecondaryTitle} ({w.SecondaryPath})"
                };
                var spSub = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                spSub.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(w.SecondaryIcon) ? "⚡" : w.SecondaryIcon,
                    FontSize = 11,
                    VerticalAlignment = VerticalAlignment.Center
                });
                if (w.WidgetSize == "1x3")
                {
                    spSub.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(w.SecondaryTitle) ? "Ek" : w.SecondaryTitle,
                        FontSize = 8.5,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = WpfBrushes.White,
                        Margin = new Thickness(4, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center
                    });
                }
                btnSub.Content = spSub;
                btnSub.Click += (s, ev) =>
                {
                    ev.Handled = true;
                    try
                    {
                        string secPath = string.IsNullOrWhiteSpace(w.SecondaryPath) ? "wt.exe" : w.SecondaryPath;
                        Process.Start(new ProcessStartInfo { FileName = secPath, UseShellExecute = true });
                    }
                    catch { }
                };

                DockPanel.SetDock(btnSub, Dock.Right);
                dp.Children.Add(btnSub);

                DockPanel.SetDock(iconEl, Dock.Left);
                dp.Children.Add(iconEl);

                if (w.WidgetSize != "1x1")
                {
                    var spText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
                    spText.Children.Add(new TextBlock
                    {
                        Text = w.Title,
                        FontSize = 10.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = primaryFg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 68
                    });
                    spText.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(w.Subtitle) ? "EMUI Kombo" : w.Subtitle,
                        FontSize = 8,
                        Foreground = secondaryFg,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        MaxWidth = 68
                    });
                    dp.Children.Add(spText);
                }

                return dp;
            }

            // 3. HUAWEI HARMONYOS 3 "SERVICE CARD" (SERVİS KARTI & DURUM ÇUBUĞU) / LIQUID GLASS
            var mainIcon = BuildStyledIconElement(w, w.WidgetSize == "1x1" ? 30 : 28, w.WidgetSize == "1x1" ? 14 : 13);

            if (w.WidgetSize == "1x1")
            {
                var pnl = new StackPanel
                {
                    Orientation = WpfOrientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(7, 2, 7, 2),
                    MinWidth = 52
                };
                pnl.Children.Add(mainIcon);
                pnl.Children.Add(new TextBlock
                {
                    Text = w.Title,
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = primaryFg,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 54,
                    Margin = new Thickness(0, 2, 0, 1)
                });
                // HarmonyOS 3 Alt Servis Çizgisi (Service Snippet Indicator Bar)
                pnl.Children.Add(new Border
                {
                    Width = 18,
                    Height = 2.5,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(accentColor),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center
                });
                return pnl;
            }
            else
            {
                var outerStack = new StackPanel
                {
                    Orientation = WpfOrientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(7, 2, 7, 2),
                    MinWidth = w.WidgetSize == "1x3" ? 140 : 92
                };

                var dock = new DockPanel { VerticalAlignment = VerticalAlignment.Center };
                DockPanel.SetDock(mainIcon, Dock.Left);
                dock.Children.Add(mainIcon);

                if (w.WidgetSize == "1x3")
                {
                    var pillLaunch = new Border
                    {
                        Width = 22,
                        Height = 22,
                        CornerRadius = new CornerRadius(11),
                        Background = new SolidColorBrush(MediaColor.FromArgb(50, accentColor.R, accentColor.G, accentColor.B)),
                        BorderBrush = new SolidColorBrush(accentColor),
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(6, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = "▶",
                            FontSize = 8,
                            Foreground = isSolid ? WpfBrushes.White : new SolidColorBrush(accentColor),
                            HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    DockPanel.SetDock(pillLaunch, Dock.Right);
                    dock.Children.Add(pillLaunch);
                }

                var spText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) };

                // Üst Durum Noktası + Başlık
                var spHeader = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                spHeader.Children.Add(new Border
                {
                    Width = 5,
                    Height = 5,
                    CornerRadius = new CornerRadius(2.5),
                    Background = new SolidColorBrush(isSolid ? MediaColor.FromRgb(52, 211, 153) : accentColor),
                    Margin = new Thickness(0, 0, 4, 0),
                    VerticalAlignment = VerticalAlignment.Center
                });
                spHeader.Children.Add(new TextBlock
                {
                    Text = w.Title,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = primaryFg,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = w.WidgetSize == "1x3" ? 82 : 62
                });
                spText.Children.Add(spHeader);

                spText.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(w.Subtitle) ? "Servis Kartı" : w.Subtitle,
                    FontSize = 8.5,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = secondaryFg,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = w.WidgetSize == "1x3" ? 90 : 66,
                    Margin = new Thickness(0, 1, 0, 0)
                });

                dock.Children.Add(spText);
                outerStack.Children.Add(dock);

                // HarmonyOS 3 Alt Servis Kapsül Çizgisi
                outerStack.Children.Add(new Border
                {
                    Width = 24,
                    Height = 2.5,
                    CornerRadius = new CornerRadius(1.5),
                    Background = new SolidColorBrush(MediaColor.FromArgb(170, accentColor.R, accentColor.G, accentColor.B)),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(0, 3, 0, 0)
                });

                return outerStack;
            }
        }

        private void LaunchCustomWidget(CustomAppWidget widget)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(widget.TargetPath))
                {
                    ShowCustomWidgetEditorDialog(widget);
                    return;
                }

                string target = Environment.ExpandEnvironmentVariables(widget.TargetPath.Trim());
                string? resolved = ResolveExecutablePath(target);

                var psi = new ProcessStartInfo
                {
                    FileName = resolved ?? target,
                    Arguments = widget.Arguments,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                var res = Forms.MessageBox.Show(
                    $"'{widget.Title}' başlatılamadı:\n{ex.Message}\n\nŞimdi Widget Stüdyosunu açıp dosya yolunu düzenlemek ister misiniz?",
                    "OxiDock Widget Başlatıcı",
                    Forms.MessageBoxButtons.YesNo,
                    Forms.MessageBoxIcon.Warning);
                if (res == Forms.DialogResult.Yes)
                {
                    ShowCustomWidgetEditorDialog(widget);
                }
            }
        }

        public void ShowCustomWidgetEditorDialog(CustomAppWidget? widgetToEdit = null)
        {
            var isNew = (widgetToEdit == null);
            var widget = widgetToEdit ?? new CustomAppWidget
            {
                Title = "VS Code",
                Subtitle = "Kod & Geliştirme",
                IconType = "Fluent",
                IconValue = "💻",
                AccentColor = "#38BDF8",
                TargetPath = "code.cmd",
                WidgetSize = "1x2",
                CardTemplate = "HarmonyServiceCard",
                BackgroundMode = "Glass",
                BadgeText = "SERVİS",
                UseNativeExeIcon = true,
                CornerStyle = "Squircle",
                SecondaryTitle = "Terminal",
                SecondaryIcon = "⚡",
                SecondaryPath = "wt.exe"
            };

            // Önizleme için geçici çalışma kopyası
            var draft = new CustomAppWidget
            {
                Id = widget.Id,
                Title = widget.Title,
                Subtitle = widget.Subtitle,
                TargetPath = widget.TargetPath,
                Arguments = widget.Arguments,
                IconType = widget.IconType,
                IconValue = widget.IconValue,
                AccentColor = widget.AccentColor,
                WidgetSize = widget.WidgetSize,
                IsEnabled = widget.IsEnabled,
                CardTemplate = string.IsNullOrWhiteSpace(widget.CardTemplate) ? "HarmonyServiceCard" : widget.CardTemplate,
                BackgroundMode = string.IsNullOrWhiteSpace(widget.BackgroundMode) ? "Glass" : widget.BackgroundMode,
                BadgeText = widget.BadgeText ?? "SERVİS",
                UseNativeExeIcon = widget.UseNativeExeIcon,
                CornerStyle = string.IsNullOrWhiteSpace(widget.CornerStyle) ? "Squircle" : widget.CornerStyle,
                SecondaryTitle = widget.SecondaryTitle ?? "Terminal",
                SecondaryIcon = widget.SecondaryIcon ?? "⚡",
                SecondaryPath = widget.SecondaryPath ?? "wt.exe"
            };

            var dlg = new Window
            {
                Title = isNew ? "OxiDock • HarmonyOS 3 / HyperOS 3 / EMUI 13 Widget Stüdyosu" : $"OxiDock • '{widget.Title}' Widget Stüdyosu",
                Width = 680,
                Height = 760,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(MediaColor.FromRgb(13, 17, 26)),
                Foreground = WpfBrushes.White,
                Topmost = true,
                ResizeMode = ResizeMode.CanResize
            };

            var rootScroll = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };
            var sp = new StackPanel { Margin = new Thickness(20, 16, 20, 20) };

            // Başlık & Alt Bilgi
            sp.Children.Add(new TextBlock
            {
                Text = isNew ? "✨ OxiDock Görsel Widget Stüdyosu (HarmonyOS 3 • HyperOS 3 • EMUI 13)" : $"🎨 '{widget.Title}' Widget'ını Özelleştir veya Kaldır",
                FontWeight = FontWeights.Bold,
                FontSize = 16,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248))
            });
            sp.Children.Add(new TextBlock
            {
                Text = "Kart şablonunu, renk paletini, ikon tasarımını ve Android boyutunu seçin; üstteki canlı sahnede anında önizleyin.",
                FontSize = 11,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 12)
            });

            // 1. CANLI GÖRSEL DOCK ÖNİZLEME SAHNESİ (LIVE PREVIEW STAGE)
            var previewStageBorder = new Border
            {
                Background = new LinearGradientBrush(
                    MediaColor.FromRgb(18, 24, 38),
                    MediaColor.FromRgb(10, 14, 22),
                    90.0),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(90, 56, 189, 248)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(14),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 12)
            };
            var dpStage = new DockPanel();
            var spStageInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            spStageInfo.Children.Add(new TextBlock
            {
                Text = "👁️ CANLI DOCK ÖNİZLEMESİ",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248))
            });
            var txtStageMeta = new TextBlock
            {
                Text = "HarmonyOS 3 Servis Kartı • 1x2 • Buzlu Cam",
                FontSize = 10,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)),
                Margin = new Thickness(0, 2, 0, 0)
            };
            spStageInfo.Children.Add(txtStageMeta);
            DockPanel.SetDock(spStageInfo, Dock.Left);
            dpStage.Children.Add(spStageInfo);

            var previewHolder = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(160, 8, 11, 18)),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(45, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 8, 14, 8),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center
            };
            dpStage.Children.Add(previewHolder);
            previewStageBorder.Child = dpStage;
            sp.Children.Add(previewStageBorder);

            // Kontrollerin İleri Tanımları
            var txtTitle = new TextBox { Text = draft.Title, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(7, 5, 7, 5), BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255)) };
            var txtSubtitle = new TextBox { Text = draft.Subtitle, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(7, 5, 7, 5), BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255)) };
            var txtBadge = new TextBox { Text = draft.BadgeText, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(7, 5, 7, 5), BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255)) };
            var txtPath = new TextBox { Text = draft.TargetPath, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(7, 5, 7, 5), BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255)) };
            var txtIcon = new TextBox { Text = draft.IconValue, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(7, 5, 7, 5), BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255)) };
            var txtColor = new TextBox { Text = draft.AccentColor, Width = 86, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(7, 5, 7, 5), BorderBrush = new SolidColorBrush(MediaColor.FromArgb(60, 255, 255, 255)) };
            var chkNativeIcon = new System.Windows.Controls.CheckBox
            {
                Content = "Gerçek Windows Uygulama Simgesini (.exe/.lnk) Otomatik Çek",
                IsChecked = draft.UseNativeExeIcon,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)),
                FontSize = 11,
                Margin = new Thickness(0, 4, 0, 8)
            };
            var txtSecTitle = new TextBox { Text = draft.SecondaryTitle, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            var txtSecIcon = new TextBox { Text = draft.SecondaryIcon, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            var txtSecPath = new TextBox { Text = draft.SecondaryPath, Background = new SolidColorBrush(MediaColor.FromRgb(24, 30, 44)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            var bdComboSection = new Border
            {
                Background = new SolidColorBrush(MediaColor.FromArgb(35, 249, 115, 22)),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(100, 249, 115, 22)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(10, 8, 10, 8),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var iconPreviewGallery = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };

            void RefreshLivePreview()
            {
                draft.Title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Uygulama" : txtTitle.Text.Trim();
                draft.Subtitle = txtSubtitle.Text.Trim();
                draft.BadgeText = txtBadge.Text.Trim();
                draft.TargetPath = txtPath.Text.Trim();
                draft.IconValue = string.IsNullOrWhiteSpace(txtIcon.Text) ? "📄" : txtIcon.Text.Trim();
                draft.AccentColor = string.IsNullOrWhiteSpace(txtColor.Text) ? "#38BDF8" : txtColor.Text.Trim();
                draft.UseNativeExeIcon = chkNativeIcon.IsChecked == true;
                draft.SecondaryTitle = txtSecTitle.Text.Trim();
                draft.SecondaryIcon = txtSecIcon.Text.Trim();
                draft.SecondaryPath = txtSecPath.Text.Trim();

                bdComboSection.Visibility = draft.CardTemplate == "EmuiComboCard" ? Visibility.Visible : Visibility.Collapsed;
                previewHolder.Child = BuildCustomWidgetPreviewCard(draft);
                txtStageMeta.Text = $"{draft.CardTemplate} • Boyut: {draft.WidgetSize} • Yüzey: {draft.BackgroundMode} • İkon: {draft.IconType}";

                // İkon Tasarımları Canlı Önizleme Kartlarını Güncelle
                iconPreviewGallery.Children.Clear();
                var iconStyles = new (string Key, string Label, bool UseNative)[]
                {
                    ("NativeExe", "Gerçek (.exe)", true),
                    ("SuperIcon", "HyperOS 3B", false),
                    ("Fluent", "Fluent Cam", false),
                    ("Material", "Material", false),
                    ("Color", "Tam Dolgu", false),
                    ("Monochrome", "Monokrom", false),
                    ("Emoji", "Emoji/Harf", false)
                };
                foreach (var st in iconStyles)
                {
                    bool isSelected = (st.Key == "NativeExe" && draft.UseNativeExeIcon) || (!draft.UseNativeExeIcon && draft.IconType == st.Key);
                    var sample = new CustomAppWidget
                    {
                        TargetPath = draft.TargetPath,
                        IconValue = draft.IconValue,
                        AccentColor = draft.AccentColor,
                        IconType = st.Key == "NativeExe" ? "Fluent" : st.Key,
                        UseNativeExeIcon = st.UseNative,
                        CardTemplate = st.Key == "SuperIcon" ? "HyperSuperIcon" : draft.CardTemplate
                    };

                    var card = new Border
                    {
                        Width = 78,
                        Height = 62,
                        CornerRadius = new CornerRadius(10),
                        Margin = new Thickness(0, 0, 6, 6),
                        Cursor = WpfCursors.Hand,
                        Background = isSelected
                            ? new SolidColorBrush(MediaColor.FromArgb(55, 56, 189, 248))
                            : new SolidColorBrush(MediaColor.FromArgb(22, 255, 255, 255)),
                        BorderBrush = isSelected
                            ? new SolidColorBrush(MediaColor.FromRgb(56, 189, 248))
                            : new SolidColorBrush(MediaColor.FromArgb(40, 255, 255, 255)),
                        BorderThickness = new Thickness(isSelected ? 1.6 : 1)
                    };
                    var spCard = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
                    spCard.Children.Add(BuildStyledIconElement(sample, 28, 13));
                    spCard.Children.Add(new TextBlock
                    {
                        Text = st.Label,
                        FontSize = 8.5,
                        FontWeight = isSelected ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = isSelected ? new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)) : new SolidColorBrush(MediaColor.FromRgb(203, 213, 225)),
                        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0)
                    });
                    card.Child = spCard;
                    string capturedKey = st.Key;
                    bool capturedNative = st.UseNative;
                    card.MouseLeftButtonDown += (s, ev) =>
                    {
                        chkNativeIcon.IsChecked = capturedNative;
                        if (capturedKey != "NativeExe") draft.IconType = capturedKey;
                        RefreshLivePreview();
                    };
                    iconPreviewGallery.Children.Add(card);
                }
            }

            // 2. HARMONYOS 3 / HYPEROS 3 / EMUI 13 KART ŞABLONU SEÇİCİ
            sp.Children.Add(new TextBlock { Text = "1. Ekosistem Kart Tasarımı (HarmonyOS 3 • HyperOS 3 • EMUI 13):", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)) });
            var wpTemplates = new WrapPanel { Margin = new Thickness(0, 4, 0, 10) };
            var templateOptions = new (string Key, string Title, string Sub, string ColorHex)[]
            {
                ("HarmonyServiceCard", "🟢 HarmonyOS 3 Servis", "Kapsül & Durum Çubuğu", "#10B981"),
                ("HyperSuperIcon", "🔵 HyperOS 3 Süper İkon", "3B Parlama & Rozet", "#38BDF8"),
                ("EmuiComboCard", "🟠 EMUI 13 Akıllı Kombo", "Çift Uygulama Grubu", "#F97316"),
                ("LiquidGlassCard", "🌊 OxiDock Liquid Glass", "Akıcı Neon Cam", "#8B5CF6")
            };
            foreach (var tpl in templateOptions)
            {
                var btnTpl = new WpfButton
                {
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 6, 6),
                    Background = new SolidColorBrush(MediaColor.FromArgb(30, 255, 255, 255)),
                    Foreground = WpfBrushes.White,
                    BorderBrush = new SolidColorBrush(ParseAccentColor(tpl.ColorHex)),
                    BorderThickness = new Thickness(1),
                    Cursor = WpfCursors.Hand
                };
                var spTpl = new StackPanel();
                spTpl.Children.Add(new TextBlock { Text = tpl.Title, FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = WpfBrushes.White });
                spTpl.Children.Add(new TextBlock { Text = tpl.Sub, FontSize = 8.5, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)) });
                btnTpl.Content = spTpl;
                string capturedTpl = tpl.Key;
                btnTpl.Click += (s, ev) =>
                {
                    draft.CardTemplate = capturedTpl;
                    RefreshLivePreview();
                };
                wpTemplates.Children.Add(btnTpl);
            }
            sp.Children.Add(wpTemplates);

            // 3. BOYUT (1x1 / 1x2 / 1x3) & YÜZEY MODU (GLASS / GRADIENT / SOLID / OUTLINE)
            var gridSizeSurface = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            gridSizeSurface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridSizeSurface.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });

            var spSizes = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
            spSizes.Children.Add(new TextBlock { Text = "2. Android Widget Boyutu:", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 4) });
            var wpSizes = new WrapPanel();
            foreach (var sz in new[] { ("1x1", "1x1 Kare"), ("1x2", "1x2 Standart"), ("1x3", "1x3 Geniş") })
            {
                var bSz = new WpfButton
                {
                    Content = sz.Item2,
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 5, 4),
                    Background = new SolidColorBrush(MediaColor.FromArgb(35, 56, 189, 248)),
                    Foreground = WpfBrushes.White,
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Cursor = WpfCursors.Hand
                };
                string capturedSz = sz.Item1;
                bSz.Click += (s, ev) => { draft.WidgetSize = capturedSz; RefreshLivePreview(); };
                wpSizes.Children.Add(bSz);
            }
            spSizes.Children.Add(wpSizes);
            Grid.SetColumn(spSizes, 0);
            gridSizeSurface.Children.Add(spSizes);

            var spSurfaces = new StackPanel { Margin = new Thickness(8, 0, 0, 0) };
            spSurfaces.Children.Add(new TextBlock { Text = "3. Kart Yüzey Görünümü:", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 4) });
            var wpSurfaces = new WrapPanel();
            foreach (var sf in new[] { ("Glass", "🪟 Buzlu Cam"), ("Gradient", "🌈 Gradyan"), ("Solid", "🎨 Tam Dolgu"), ("Outline", "⚡ Neon Çizgi") })
            {
                var bSf = new WpfButton
                {
                    Content = sf.Item2,
                    Padding = new Thickness(9, 5, 9, 5),
                    Margin = new Thickness(0, 0, 5, 4),
                    Background = new SolidColorBrush(MediaColor.FromArgb(30, 255, 255, 255)),
                    Foreground = WpfBrushes.White,
                    FontSize = 10,
                    Cursor = WpfCursors.Hand
                };
                string capturedSf = sf.Item1;
                bSf.Click += (s, ev) => { draft.BackgroundMode = capturedSf; RefreshLivePreview(); };
                wpSurfaces.Children.Add(bSf);
            }
            spSurfaces.Children.Add(wpSurfaces);
            Grid.SetColumn(spSurfaces, 1);
            gridSizeSurface.Children.Add(spSurfaces);
            sp.Children.Add(gridSizeSurface);

            // 4. CANLI RENK PALETİ (COLOR SWATCHES)
            sp.Children.Add(new TextBlock { Text = "4. Vurgu Renk Paleti (Tek Tıkla Seçin veya Özel Hex Girin):", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)), Margin = new Thickness(0, 0, 0, 4) });
            var dpPalette = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            DockPanel.SetDock(txtColor, Dock.Right);
            dpPalette.Children.Add(txtColor);

            var wpSwatches = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
            var paletteColors = new (string Hex, string Name)[]
            {
                ("#38BDF8", "HyperOS Mavi"),
                ("#2563EB", "Kraliyet Mavisi"),
                ("#10B981", "Harmony Zümrüt"),
                ("#22C55E", "Spotify Yeşil"),
                ("#F97316", "EMUI Turuncu"),
                ("#EAB308", "Altın Sarısı"),
                ("#EF4444", "Mercan Kırmızı"),
                ("#EC4899", "Neon Pembe"),
                ("#8B5CF6", "Siber Mor"),
                ("#06B6D4", "Turkuaz Cam"),
                ("#6366F1", "İndigo"),
                ("#F43F5E", "Yakut")
            };
            foreach (var sw in paletteColors)
            {
                var swBorder = new Border
                {
                    Width = 26,
                    Height = 26,
                    CornerRadius = new CornerRadius(13),
                    Background = new SolidColorBrush(ParseAccentColor(sw.Hex)),
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(180, 255, 255, 255)),
                    BorderThickness = new Thickness(1.5),
                    Margin = new Thickness(0, 0, 7, 4),
                    Cursor = WpfCursors.Hand,
                    ToolTip = $"{sw.Name} ({sw.Hex})"
                };
                string capturedHex = sw.Hex;
                swBorder.MouseLeftButtonDown += (s, ev) =>
                {
                    txtColor.Text = capturedHex;
                    RefreshLivePreview();
                };
                wpSwatches.Children.Add(swBorder);
            }
            dpPalette.Children.Add(wpSwatches);
            sp.Children.Add(dpPalette);

            // 5. İKON TASARIMI CANLI ÖNİZLEME GALERİSİ
            sp.Children.Add(new TextBlock { Text = "5. İkon Tasarım Stili (Canlı Önizlemeli Kartlar):", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(226, 232, 240)) });
            sp.Children.Add(iconPreviewGallery);

            // 6. BAŞLIK, ALT BAŞLIK, ROZET VE İKON KARAKTERİ
            var gridTexts = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            gridTexts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            gridTexts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.2, GridUnitType.Star) });
            gridTexts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.8, GridUnitType.Star) });
            gridTexts.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.7, GridUnitType.Star) });

            var spT1 = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            spT1.Children.Add(new TextBlock { Text = "Başlık:", FontSize = 10.5, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 2) });
            spT1.Children.Add(txtTitle);
            Grid.SetColumn(spT1, 0);
            gridTexts.Children.Add(spT1);

            var spT2 = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            spT2.Children.Add(new TextBlock { Text = "Alt Başlık / Durum:", FontSize = 10.5, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 2) });
            spT2.Children.Add(txtSubtitle);
            Grid.SetColumn(spT2, 1);
            gridTexts.Children.Add(spT2);

            var spT3 = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            spT3.Children.Add(new TextBlock { Text = "Üst Etiket (Rozet):", FontSize = 10.5, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 2) });
            spT3.Children.Add(txtBadge);
            Grid.SetColumn(spT3, 2);
            gridTexts.Children.Add(spT3);

            var spT4 = new StackPanel();
            spT4.Children.Add(new TextBlock { Text = "Simge / Harf:", FontSize = 10.5, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 2) });
            spT4.Children.Add(txtIcon);
            Grid.SetColumn(spT4, 3);
            gridTexts.Children.Add(spT4);

            sp.Children.Add(gridTexts);

            // 7. HEDEF UYGULAMA YOLU (.EXE / .LNK / URL)
            sp.Children.Add(new TextBlock { Text = "Çalıştırılacak Uygulama (.exe, .lnk, .bat) veya Web Adresi:", FontSize = 10.5, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 2) });
            var dpPath = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
            var btnBrowse = new WpfButton
            {
                Content = "📂 Gözat...",
                Width = 85,
                Margin = new Thickness(6, 0, 0, 0),
                Background = new SolidColorBrush(MediaColor.FromArgb(40, 56, 189, 248)),
                Foreground = WpfBrushes.White,
                Cursor = WpfCursors.Hand
            };
            btnBrowse.Click += (s, e) =>
            {
                var ofd = new Forms.OpenFileDialog
                {
                    Title = "Uygulama veya Kısayol Seçin",
                    Filter = "Uygulama ve Kısayollar (*.exe;*.lnk;*.bat;*.cmd)|*.exe;*.lnk;*.bat;*.cmd|Tüm Dosyalar (*.*)|*.*"
                };
                if (ofd.ShowDialog() == Forms.DialogResult.OK)
                {
                    txtPath.Text = ofd.FileName;
                    txtTitle.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    chkNativeIcon.IsChecked = true;
                    RefreshLivePreview();
                }
            };
            DockPanel.SetDock(btnBrowse, Dock.Right);
            dpPath.Children.Add(btnBrowse);
            dpPath.Children.Add(txtPath);
            sp.Children.Add(dpPath);
            sp.Children.Add(chkNativeIcon);

            // EMUI 13 Kombo Kart 2. Eylem Bölümü
            var spComboInner = new StackPanel();
            spComboInner.Children.Add(new TextBlock { Text = "🟠 EMUI 13 Akıllı Kombo — İkincil Hızlı Eylem:", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(251, 146, 60)), Margin = new Thickness(0, 0, 0, 4) });
            var gridCombo = new Grid();
            gridCombo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridCombo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.6, GridUnitType.Star) });
            gridCombo.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.6, GridUnitType.Star) });
            Grid.SetColumn(txtSecTitle, 0);
            txtSecIcon.Margin = new Thickness(6, 0, 6, 0);
            Grid.SetColumn(txtSecIcon, 1);
            Grid.SetColumn(txtSecPath, 2);
            gridCombo.Children.Add(txtSecTitle);
            gridCombo.Children.Add(txtSecIcon);
            gridCombo.Children.Add(txtSecPath);
            spComboInner.Children.Add(gridCombo);
            bdComboSection.Child = spComboInner;
            sp.Children.Add(bdComboSection);

            // 8. HAZIR HARMONYOS 3 / HYPEROS 3 / EMUI 13 ŞABLON KÜTÜPHANESİ
            var spPresets = new StackPanel { Margin = new Thickness(0, 2, 0, 12) };
            spPresets.Children.Add(new TextBlock { Text = "⚡ Hazır HarmonyOS 3 / HyperOS 3 / EMUI 13 Widget Şablonları (Tek Tıkla Doldur):", FontSize = 10.5, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            var wpPresets = new WrapPanel();
            void AddPresetBtn(string label, string title, string sub, string icon, string color, string path, string tpl, string surface, string badge)
            {
                var b = new WpfButton
                {
                    Content = label,
                    Padding = new Thickness(9, 4, 9, 4),
                    Margin = new Thickness(0, 0, 5, 5),
                    Background = new SolidColorBrush(MediaColor.FromArgb(28, 255, 255, 255)),
                    Foreground = WpfBrushes.White,
                    BorderBrush = new SolidColorBrush(ParseAccentColor(color)),
                    BorderThickness = new Thickness(1),
                    FontSize = 10,
                    Cursor = WpfCursors.Hand
                };
                b.Click += (s, e) =>
                {
                    txtTitle.Text = title;
                    txtSubtitle.Text = sub;
                    txtIcon.Text = icon;
                    txtColor.Text = color;
                    txtPath.Text = path;
                    txtBadge.Text = badge;
                    draft.CardTemplate = tpl;
                    draft.BackgroundMode = surface;
                    RefreshLivePreview();
                };
                wpPresets.Children.Add(b);
            }
            AddPresetBtn("🟦 Word Servis", "Word", "Belge Servisi", "W", "#2563EB", "winword.exe", "HarmonyServiceCard", "Glass", "OFİS");
            AddPresetBtn("🟩 Excel Servis", "Excel", "Tablo & Veri", "X", "#16A34A", "excel.exe", "HarmonyServiceCard", "Glass", "OFİS");
            AddPresetBtn("🟧 PowerPoint", "PowerPoint", "Sunum Stüdyosu", "P", "#EA580C", "powerpnt.exe", "HarmonyServiceCard", "Gradient", "SUNUM");
            AddPresetBtn("🔵 HyperOS VS Code", "VS Code", "Süper İkon IDE", "💻", "#38BDF8", "code.cmd", "HyperSuperIcon", "Gradient", "HYPER");
            AddPresetBtn("🟠 EMUI Dev Kombo", "VS Code", "Kod + Terminal", "⚡", "#F97316", "code.cmd", "EmuiComboCard", "Glass", "KOMBO");
            AddPresetBtn("🎵 Spotify Canlı", "Spotify", "Müzik & Medya", "🎵", "#22C55E", "spotify.exe", "HarmonyServiceCard", "Gradient", "CANLI");
            AddPresetBtn("🌐 Chrome Süper", "Chrome", "Web Tarayıcı", "🌐", "#EAB308", "chrome.exe", "HyperSuperIcon", "Glass", "WEB");
            AddPresetBtn("🤖 ChatGPT AI", "ChatGPT", "Yapay Zeka", "✨", "#8B5CF6", "https://chatgpt.com", "LiquidGlassCard", "Gradient", "AI");
            spPresets.Children.Add(wpPresets);
            sp.Children.Add(spPresets);

            // Canlı Önizleme Event Bağlantıları
            txtTitle.TextChanged += (s, e) => RefreshLivePreview();
            txtSubtitle.TextChanged += (s, e) => RefreshLivePreview();
            txtBadge.TextChanged += (s, e) => RefreshLivePreview();
            txtPath.TextChanged += (s, e) => RefreshLivePreview();
            txtIcon.TextChanged += (s, e) => RefreshLivePreview();
            txtColor.TextChanged += (s, e) => RefreshLivePreview();
            txtSecTitle.TextChanged += (s, e) => RefreshLivePreview();
            txtSecIcon.TextChanged += (s, e) => RefreshLivePreview();
            txtSecPath.TextChanged += (s, e) => RefreshLivePreview();
            chkNativeIcon.Checked += (s, e) => RefreshLivePreview();
            chkNativeIcon.Unchecked += (s, e) => RefreshLivePreview();

            RefreshLivePreview();

            // 9. ALT AKSİYON BAR (KAYDET / ÇOĞALT / KALDIR)
            var dpAction = new DockPanel { Margin = new Thickness(0, 6, 0, 0) };

            if (!isNew)
            {
                var btnDelete = new WpfButton
                {
                    Content = "🗑️ Widget'ı Kaldır / Sil",
                    Padding = new Thickness(14, 8, 14, 8),
                    Margin = new Thickness(0, 0, 8, 0),
                    Background = new SolidColorBrush(MediaColor.FromRgb(220, 38, 38)),
                    Foreground = WpfBrushes.White,
                    FontWeight = FontWeights.Bold,
                    Cursor = WpfCursors.Hand
                };
                btnDelete.Click += (s, e) =>
                {
                    var prof = _settings.GetActiveProfile();
                    prof.CustomWidgets?.Remove(widget);
                    _settings.Save();
                    ApplySettings(_settings);
                    dlg.Close();
                };
                DockPanel.SetDock(btnDelete, Dock.Left);
                dpAction.Children.Add(btnDelete);
            }

            var btnSave = new WpfButton
            {
                Content = isNew ? "✨ Widget'ı Oluştur ve Dock'a Ekle" : "💾 Değişiklikleri Kaydet ve Uygula",
                Padding = new Thickness(18, 8, 18, 8),
                Background = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)),
                Foreground = WpfBrushes.Black,
                FontWeight = FontWeights.Bold,
                Cursor = WpfCursors.Hand,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right
            };
            btnSave.Click += (s, e) =>
            {
                RefreshLivePreview();
                widget.Title = draft.Title;
                widget.Subtitle = draft.Subtitle;
                widget.BadgeText = draft.BadgeText;
                widget.TargetPath = draft.TargetPath;
                widget.IconValue = draft.IconValue;
                widget.AccentColor = draft.AccentColor;
                widget.IconType = draft.IconType;
                widget.WidgetSize = draft.WidgetSize;
                widget.CardTemplate = draft.CardTemplate;
                widget.BackgroundMode = draft.BackgroundMode;
                widget.UseNativeExeIcon = draft.UseNativeExeIcon;
                widget.CornerStyle = draft.CornerStyle;
                widget.SecondaryTitle = draft.SecondaryTitle;
                widget.SecondaryIcon = draft.SecondaryIcon;
                widget.SecondaryPath = draft.SecondaryPath;
                widget.IsEnabled = true;

                var prof = _settings.GetActiveProfile();
                prof.CustomWidgets ??= new List<CustomAppWidget>();
                if (isNew)
                {
                    prof.CustomWidgets.Add(widget);
                }

                _settings.Save();
                ApplySettings(_settings);
                dlg.Close();
            };
            DockPanel.SetDock(btnSave, Dock.Right);
            dpAction.Children.Add(btnSave);

            sp.Children.Add(dpAction);
            rootScroll.Content = sp;
            dlg.Content = rootScroll;
            dlg.ShowDialog();
        }

        #endregion

        private void BtnOpenSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenSettingsWindow();
        }

        private void OpenSettingsWindow()
        {
            var win = new SettingsWindow(_settings);
            win.Owner = this;
            win.OnPreviewSettings = (previewSettings) => ApplySettings(previewSettings);
            win.OnSettingsChanged = (newSettings) =>
            {
                _settings = newSettings;
                _settings.Save();
                ApplySettings(_settings);
            };
            bool? result = win.ShowDialog();
            if (result != true)
            {
                // İptal edildiyse orijinal ayarlara dön
                ApplySettings(_settings);
            }
        }

        #endregion

        #region Pomodoro & Wellness Mekanizması (OxiDock Mac Replica: 25-5-25-5, Rounds, Progress)

        private int GetCurrentPomoTotalSeconds()
        {
            return _pomoState switch
            {
                1 => _settings.PomodoroBreakMinutes * 60,
                2 => _settings.PomodoroLongBreakMinutes * 60,
                _ => _settings.PomodoroFocusMinutes * 60
            };
        }

        private string GetRoundDots(int round)
        {
            return round switch
            {
                1 => "● ○ ○ ○",
                2 => "● ● ○ ○",
                3 => "● ● ● ○",
                _ => "● ● ● ●"
            };
        }

        private void WidgetPomodoro_Click(object sender, MouseButtonEventArgs? e = null)
        {
            TogglePomodoro();
        }

        private void TogglePomodoro()
        {
            _pomoIsRunning = !_pomoIsRunning;
            if (_pomoIsRunning)
            {
                _pomodoroTimer.Start();
                UpdatePomoVisuals();
            }
            else
            {
                _pomodoroTimer.Stop();
                TxtPomoStatus.Text = "DURAKLATILDI";
                TxtPomoStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(251, 191, 36));
            }
        }

        private void ResetPomodoro()
        {
            _pomodoroTimer.Stop();
            _pomoIsRunning = false;
            _pomoSecondsRemaining = GetCurrentPomoTotalSeconds();
            int m = _pomoSecondsRemaining / 60;
            int s = _pomoSecondsRemaining % 60;
            TxtPomoTimer.Text = $"{m:00}:{s:00}";
            PbPomoProgress.Value = 100;
            UpdatePomoVisuals();
        }

        private void SkipPomodoro()
        {
            if (_pomoState == 0) // Odaktan Molaya
            {
                if (_pomoRound >= 4)
                {
                    _pomoState = 2; // Uzun Mola (15 dk)
                    _pomoRound = 1;
                }
                else
                {
                    _pomoState = 1; // Kısa Mola (5 dk)
                }
            }
            else // Moladan Odağa
            {
                if (_pomoState == 1) _pomoRound++;
                _pomoState = 0;
            }

            _pomoSecondsRemaining = GetCurrentPomoTotalSeconds();
            int m = _pomoSecondsRemaining / 60;
            int s = _pomoSecondsRemaining % 60;
            TxtPomoTimer.Text = $"{m:00}:{s:00}";
            PbPomoProgress.Value = 100;
            UpdatePomoVisuals();

            // Mac OxiDock Wellness: Mola başlangıcında postür & su hatırlatması
            if (_pomoState != 0 && (_settings.PostureReminderEnabled || _settings.WaterReminderEnabled))
            {
                _notifyIcon?.ShowBalloonTip(3500, "OxiDock Mola Zamanı", "Harika odaklandınız! Şimdi bir bardak su için ve dik durun 💧🚶", Forms.ToolTipIcon.Info);
            }
        }

        private void SetPomodoroMode(string mode)
        {
            _pomodoroTimer.Stop();
            _pomoIsRunning = false;
            switch (mode.ToLowerInvariant())
            {
                case "shortbreak":
                    _pomoState = 1;
                    break;
                case "longbreak":
                    _pomoState = 2;
                    break;
                default: // focus
                    _pomoState = 0;
                    break;
            }
            _pomoSecondsRemaining = GetCurrentPomoTotalSeconds();
            int m = _pomoSecondsRemaining / 60;
            int s = _pomoSecondsRemaining % 60;
            TxtPomoTimer.Text = $"{m:00}:{s:00}";
            PbPomoProgress.Value = 100;
            UpdatePomoVisuals();
        }

        private void UpdatePomoVisuals()
        {
            TxtPomoRounds.Text = GetRoundDots(_pomoRound);
            TxtPomo1x3Rounds.Text = TxtPomoRounds.Text;

            if (_pomoState == 0)
            {
                TxtPomoStatus.Text = $"ODAK (Tur {_pomoRound}/4)";
                TxtPomoStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68)); // #EF4444 Coral Red
                TxtPomo1x3Status.Text = TxtPomoStatus.Text;
                TxtPomo1x3Status.Foreground = TxtPomoStatus.Foreground;
                TxtPomoEmoji.Text = "🍅";
                BdPomoEmoji.Background = new SolidColorBrush(MediaColor.FromArgb(40, 239, 68, 68));
                PbPomoProgress.Foreground = new SolidColorBrush(MediaColor.FromRgb(239, 68, 68));
                PbPomo1x3Progress.Foreground = PbPomoProgress.Foreground;
            }
            else if (_pomoState == 1)
            {
                TxtPomoStatus.Text = "KISA MOLA";
                TxtPomoStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129)); // #10B981 Emerald
                TxtPomo1x3Status.Text = TxtPomoStatus.Text;
                TxtPomo1x3Status.Foreground = TxtPomoStatus.Foreground;
                TxtPomoEmoji.Text = "☕";
                BdPomoEmoji.Background = new SolidColorBrush(MediaColor.FromArgb(40, 16, 185, 129));
                PbPomoProgress.Foreground = new SolidColorBrush(MediaColor.FromRgb(16, 185, 129));
                PbPomo1x3Progress.Foreground = PbPomoProgress.Foreground;
            }
            else
            {
                TxtPomoStatus.Text = "UZUN MOLA";
                TxtPomoStatus.Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)); // #38BDF8 Sky Blue
                TxtPomo1x3Status.Text = TxtPomoStatus.Text;
                TxtPomo1x3Status.Foreground = TxtPomoStatus.Foreground;
                TxtPomoEmoji.Text = "🏖️";
                BdPomoEmoji.Background = new SolidColorBrush(MediaColor.FromArgb(40, 56, 189, 248));
                PbPomoProgress.Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
                PbPomo1x3Progress.Foreground = PbPomoProgress.Foreground;
            }
        }

        private void UpdatePomodoroTick()
        {
            if (!_pomoIsRunning) return;

            if (_pomoSecondsRemaining > 0)
            {
                _pomoSecondsRemaining--;
                int m = _pomoSecondsRemaining / 60;
                int s = _pomoSecondsRemaining % 60;
                string timeStr = $"{m:00}:{s:00}";
                TxtPomoTimer.Text = timeStr;
                TxtPomo1x1Timer.Text = $"{m}m";
                TxtPomo1x3Timer.Text = timeStr;

                int totalSec = GetCurrentPomoTotalSeconds();
                if (totalSec > 0)
                {
                    double pct = Math.Clamp((double)_pomoSecondsRemaining / totalSec * 100.0, 0, 100);
                    PbPomoProgress.Value = pct;
                    PbPomo1x3Progress.Value = pct;
                }
            }
            else
            {
                try { System.Media.SystemSounds.Exclamation.Play(); } catch { }
                SkipPomodoro();
            }
        }

        private void MenuPomoToggle_Click(object sender, RoutedEventArgs e) => TogglePomodoro();
        private void MenuPomoSkip_Click(object sender, RoutedEventArgs e) => SkipPomodoro();
        private void MenuPomoReset_Click(object sender, RoutedEventArgs e) => ResetPomodoro();
        private void MenuPomoMode_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfMenuItem item && item.Tag is string tag)
            {
                SetPomodoroMode(tag);
            }
        }
        private void MenuHidePomo_Click(object sender, RoutedEventArgs e)
        {
            var prof = _settings.GetActiveProfile();
            prof.ShowPomodoro = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        #endregion

        #region Saat Mekanizması (OxiDock Mac Replica: Dijital, Retro Flip, Analog & Dünya Saatleri)

        public void WidgetClock_Click(object sender, MouseButtonEventArgs? e = null)
        {
            _clockMode = (_clockMode + 1) % 4;
            UpdateClock();
        }

        private void MenuClockStyle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfMenuItem item && item.Tag is string tag)
            {
                switch (tag.ToLowerInvariant())
                {
                    case "digital": _clockMode = 0; break;
                    case "flip": _clockMode = 1; break;
                    case "analog": _clockMode = 2; break;
                    case "world": _clockMode = 3; break;
                }
                UpdateClock();
            }
        }

        private void MenuClockToggleSeconds_Click(object sender, RoutedEventArgs e)
        {
            _clockShowSeconds = !_clockShowSeconds;
            UpdateClock();
        }

        private void MenuHideClock_Click(object sender, RoutedEventArgs e)
        {
            var prof = _settings.GetActiveProfile();
            prof.ShowClock = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        private void UpdateClock()
        {
            var local = DateTime.Now;
            var nowUtc = DateTime.UtcNow;

            // 1x1 Kompakt Saat
            TxtClock1x1.Text = local.ToString("HH:mm");

            // 1x3 Genişletilmiş Saat ve Küresel Şehirler
            TxtClock1x3Big.Text = local.ToString(_clockShowSeconds ? "HH:mm:ss" : "HH:mm:ss");
            TxtClock1x3Date.Text = $"{_settings.WeatherCity} (UTC+3) • {local.ToString("dd MMMM dddd", new CultureInfo("tr-TR"))}";
            TxtWorldTimeMulti.Text = $"NYC {nowUtc.AddHours(-4):HH:mm} • LON {nowUtc:HH:mm} • TYO {nowUtc.AddHours(9):HH:mm}";

            // 1. Panel Görünürlükleri
            PnlClockDigital.Visibility = _clockMode == 0 ? Visibility.Visible : Visibility.Collapsed;
            PnlClockFlip.Visibility = _clockMode == 1 ? Visibility.Visible : Visibility.Collapsed;
            PnlClockAnalog.Visibility = _clockMode == 2 ? Visibility.Visible : Visibility.Collapsed;
            PnlClockWorld.Visibility = _clockMode == 3 ? Visibility.Visible : Visibility.Collapsed;

            switch (_clockMode)
            {
                case 1: // Retro Flip Clock (Bölünmüş Kartlar)
                    TxtFlipHours.Text = local.ToString("HH");
                    TxtFlipMinutes.Text = local.ToString("mm");
                    TxtFlipDate.Text = local.ToString("dd MMM • ddd", new CultureInfo("tr-TR")).ToUpperInvariant();
                    break;

                case 2: // Analog Saat (360° Dönen Akrep, Yelkovan ve Kırmızı Saniye İbresi)
                    AnalogHourTransform.Angle = ((local.Hour % 12) + local.Minute / 60.0) * 30.0;
                    AnalogMinuteTransform.Angle = (local.Minute + local.Second / 60.0) * 6.0;
                    AnalogSecondTransform.Angle = local.Second * 6.0;
                    TxtAnalogDigital.Text = _clockShowSeconds ? local.ToString("HH:mm:ss") : local.ToString("HH:mm");
                    TxtAnalogDate.Text = local.ToString("dd MMM", new CultureInfo("tr-TR"));
                    break;

                case 3: // Dünya Saatleri (Küresel Zaman Dilimleri)
                    TxtWorldTime1.Text = "IST " + local.ToString("HH:mm");
                    TxtWorldTime2.Text = "NYC " + nowUtc.AddHours(-4).ToString("HH:mm");
                    TxtWorldTime3.Text = "LON " + nowUtc.ToString("HH:mm") + " • TYO " + nowUtc.AddHours(9).ToString("HH:mm");
                    break;

                default: // Dijital Saat (Minimalist)
                    TxtClock.Text = local.ToString(_clockShowSeconds ? "HH:mm:ss" : "HH:mm");
                    TxtDate.Text = local.ToString("dd MMM, ddd", new CultureInfo("tr-TR"));
                    break;
            }
        }

        #endregion

        #region Sistem Tepsisi (Tray Icon)

        private void InitializeSystemTray()
        {
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = System.Drawing.SystemIcons.Application,
                Text = "OxiDock — Akıllı İkinci Dock",
                Visible = true
            };

            var contextMenu = new Forms.ContextMenuStrip();
            
            var itemShowHide = new Forms.ToolStripMenuItem("OxiDock'u Göster / Gizle", null, (s, e) => ToggleVisibility());
            var itemWidgets = new Forms.ToolStripMenuItem("🧩  Widget Kütüphanesi...", null, (s, e) => OpenWidgetBrowserWindow());
            var itemSettings = new Forms.ToolStripMenuItem("⚙️  Ayarlar Paneli...", null, (s, e) => OpenSettingsWindow());
            
            var itemExit = new Forms.ToolStripMenuItem("Çıkış", null, (s, e) => CloseApplication());

            contextMenu.Items.Add(itemShowHide);
            contextMenu.Items.Add(itemWidgets);
            contextMenu.Items.Add(itemSettings);
            contextMenu.Items.Add(new Forms.ToolStripSeparator());
            contextMenu.Items.Add(itemExit);

            _notifyIcon.ContextMenuStrip = contextMenu;
            _notifyIcon.DoubleClick += (s, e) => ToggleVisibility();
        }

        private void ToggleVisibility()
        {
            if (Visibility == Visibility.Visible) Hide();
            else { Show(); Activate(); }
        }

        private void CloseApplication()
        {
            _aiServer?.Stop();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            System.Windows.Application.Current.Shutdown();
        }

        #endregion

        #region Yerel AI Asistan Entegrasyonu (OxiDock MCP Server)

        private void InitializeAiServer()
        {
            if (!_settings.EnableAiAssistant) return;

            _aiServer = new LocalAiAssistantServer(
                getStatus: () => new
                {
                    appName = "OxiDock",
                    activeProfile = _settings.GetActiveProfile().Name,
                    profiles = _settings.Profiles,
                    dockPosition = _settings.DockPosition,
                    dockStyle = _settings.DockStyle,
                    pomodoro = new { running = _pomoIsRunning, focus = _pomoIsFocus, remainingSeconds = _pomoSecondsRemaining, round = _pomoRound },
                    telemetry = new { cpu = TxtCpuVal.Text, ram = TxtRamVal.Text }
                },
                executeAction: (path, payload) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        if (path == "/api/profile" && payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("profileId", out var pId))
                        {
                            SwitchProfile(pId.GetString() ?? "");
                        }
                        else if (path == "/api/pomodoro")
                        {
                            TogglePomodoro();
                        }
                        else if (path == "/api/widget/toggle" && payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("widget", out var wKey))
                        {
                            string key = wKey.GetString() ?? "";
                            var prof = _settings.GetActiveProfile();
                            if (key == "clock") prof.ShowClock = !prof.ShowClock;
                            else if (key == "pomodoro") prof.ShowPomodoro = !prof.ShowPomodoro;
                            else if (key == "weather") prof.ShowWeather = !prof.ShowWeather;
                            else if (key == "system") prof.ShowSystemStats = !prof.ShowSystemStats;
                            else if (key == "music") prof.ShowMusic = !prof.ShowMusic;
                            else if (key == "notes") prof.ShowNotes = !prof.ShowNotes;
                            else if (key == "crypto") prof.ShowCrypto = !prof.ShowCrypto;
                            else if (key == "calendar") prof.ShowCalendar = !prof.ShowCalendar;
                            else if (key == "quicksearch" || key == "search") prof.ShowQuickSearch = !prof.ShowQuickSearch;
                            else if (key == "volume") prof.ShowVolume = !prof.ShowVolume;
                            else if (key == "clipboard") prof.ShowClipboard = !prof.ShowClipboard;
                            _settings.Save();
                            ApplySettings(_settings);
                        }
                    });
                }
            );

            _aiServer.Start(_settings.AiAssistantPort);
        }

        #endregion

        #region Zamanlayıcılar (Saat, Donanım, Ekolayzır, Pomodoro)

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, int dwMinimumWorkingSetSize, int dwMaximumWorkingSetSize);

        private void InitializeTimers()
        {
            // RAM Minimizasyon Zamanlayıcısı
            var ramTrimTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            ramTrimTimer.Tick += (s, e) =>
            {
                ramTrimTimer.Stop();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                GC.WaitForPendingFinalizers();
                SetProcessWorkingSetSize(Process.GetCurrentProcess().Handle, -1, -1);
            };
            ramTrimTimer.Start();

            // 1. Saat Zamanlayıcısı
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();
            UpdateClock();

            // 2. Pomodoro Zamanlayıcısı
            _pomodoroTimer.Interval = TimeSpan.FromSeconds(1);
            _pomodoroTimer.Tick += (s, e) => UpdatePomodoroTick();

            // 3. Donanım İstatistik Zamanlayıcısı
            _telemetryTimer.Interval = TimeSpan.FromSeconds(2);
            _telemetryTimer.Tick += (s, e) => UpdateTelemetry();
            _telemetryTimer.Start();
            UpdateTelemetry();

            // 4. Müzik Ekolayzır Animasyon Zamanlayıcısı
            _equalizerTimer.Interval = TimeSpan.FromMilliseconds(120);
            _equalizerTimer.Tick += (s, e) => AnimateEqualizer();
            _equalizerTimer.Start();
        }

        private void AnimateEqualizer()
        {
            if (!_isPlaying)
            {
                Bar1.Height = 3; Bar2.Height = 3; Bar3.Height = 3; Bar4.Height = 3;
                return;
            }

            Bar1.Height = _rand.Next(4, 14);
            Bar2.Height = _rand.Next(6, 16);
            Bar3.Height = _rand.Next(4, 15);
            Bar4.Height = _rand.Next(5, 13);
        }

        #endregion

        #region Canlı Donanım İstatistikleri & Batarya

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetSystemTimes(out System.Runtime.InteropServices.ComTypes.FILETIME idleTime,
                                                   out System.Runtime.InteropServices.ComTypes.FILETIME kernelTime,
                                                   out System.Runtime.InteropServices.ComTypes.FILETIME userTime);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private void UpdateTelemetry()
        {
            // RAM Kullanımı
            var memStatus = new MEMORYSTATUSEX();
            int ramPercent = 45;
            if (GlobalMemoryStatusEx(memStatus))
            {
                ramPercent = (int)memStatus.dwMemoryLoad;
                TxtRamVal.Text = $"{ramPercent}%";
                PbRam.Value = ramPercent;
                double usedGb = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);
                TxtRam1x3.Text = $"{usedGb:0.0} GB";
            }

            // CPU Kullanımı
            if (GetSystemTimes(out var idle, out var kernel, out var user))
            {
                long idleTime = ((long)idle.dwHighDateTime << 32) | (uint)idle.dwLowDateTime;
                long kernelTime = ((long)kernel.dwHighDateTime << 32) | (uint)kernel.dwLowDateTime;
                long userTime = ((long)user.dwHighDateTime << 32) | (uint)user.dwLowDateTime;

                long usrDiff = userTime - _prevUserTime;
                long kerDiff = kernelTime - _prevKernelTime;
                long idlDiff = idleTime - _prevIdleTime;

                long total = usrDiff + kerDiff;
                if (total > 0 && _prevKernelTime > 0)
                {
                    long cpuUsage = (total - idlDiff) * 100 / total;
                    int cpuPercent = Math.Clamp((int)cpuUsage, 0, 100);
                    TxtCpuVal.Text = $"{cpuPercent}%";
                    PbCpu.Value = cpuPercent;
                    TxtCpu1x1.Text = $"{cpuPercent}%";
                    TxtCpu1x3.Text = $"{cpuPercent}%";
                }

                _prevIdleTime = idleTime;
                _prevKernelTime = kernelTime;
                _prevUserTime = userTime;
            }

            // Disk ve Ağ (1x3 Dashboard)
            try
            {
                var drive = new DriveInfo("C");
                long freeGb = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                TxtDisk1x3.Text = $"{freeGb} GB Boş";
            }
            catch { }
            TxtNet1x3.Text = $"{TxtPingVal.Text} Ping";

            // Batarya Durumu
            try
            {
                var power = Forms.SystemInformation.PowerStatus;
                if (power.BatteryChargeStatus != Forms.BatteryChargeStatus.NoSystemBattery)
                {
                    int batteryPercent = (int)(power.BatteryLifePercent * 100);
                    bool charging = power.PowerLineStatus == Forms.PowerLineStatus.Online;
                    TxtBatteryVal.Text = charging ? $"⚡ {batteryPercent}%" : $"🔋 {batteryPercent}%";
                    SpBattery.Visibility = Visibility.Visible;
                }
                else
                {
                    SpBattery.Visibility = Visibility.Collapsed;
                }
            }
            catch { }
        }

        #endregion

        #region Canlı Hava Durumu (Dinamik Konum Geocoding)

        private async Task FetchWeatherAsync()
        {
            try
            {
                string targetCity = string.IsNullOrWhiteSpace(_settings.WeatherCity) ? "İstanbul" : _settings.WeatherCity.Trim();
                _lastWeatherCity = targetCity;

                double lat = 41.0082;
                double lon = 28.9784;
                string displayName = targetCity;

                // 1. Open-Meteo Geocoding API ile Şehir Adından Koordinat Çözümleme
                try
                {
                    string geoUrl = $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(targetCity)}&count=1&language=tr&format=json";
                    var geoResp = await _httpClient.GetStringAsync(geoUrl);
                    using var geoDoc = JsonDocument.Parse(geoResp);
                    if (geoDoc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
                    {
                        var firstResult = results[0];
                        lat = firstResult.GetProperty("latitude").GetDouble();
                        lon = firstResult.GetProperty("longitude").GetDouble();
                        if (firstResult.TryGetProperty("name", out var n)) displayName = n.GetString() ?? targetCity;
                    }
                }
                catch { }

                // 2. Dinamik Koordinatlarla Canlı Hava Durumu ve Tahmin
                string latStr = lat.ToString(CultureInfo.InvariantCulture);
                string lonStr = lon.ToString(CultureInfo.InvariantCulture);
                string url = $"https://api.open-meteo.com/v1/forecast?latitude={latStr}&longitude={lonStr}&current=temperature_2m&daily=temperature_2m_max&timezone=auto";
                var response = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(response);
                var root = doc.RootElement;
                if (root.TryGetProperty("current", out var current) && current.TryGetProperty("temperature_2m", out var temp))
                {
                    int currentTemp = (int)Math.Round(temp.GetDouble());
                    TxtWeatherTemp.Text = $"{currentTemp}°";
                    TxtWeatherCity.Text = displayName;
                    TxtWeather1x1Temp.Text = $"{currentTemp}°";
                    TxtWeather1x3Temp.Text = $"{currentTemp}° ☀️";
                    TxtWeather1x3City.Text = $"{displayName} • Canlı Hava";
                }

                if (root.TryGetProperty("daily", out var daily) && daily.TryGetProperty("temperature_2m_max", out var dailyMax))
                {
                    if (dailyMax.GetArrayLength() > 1)
                    {
                        int tomMax = (int)Math.Round(dailyMax[1].GetDouble());
                        TxtForecastTomorrow.Text = $"{tomMax}°";
                    }
                }
            }
            catch
            {
                TxtWeatherTemp.Text = "22°";
                TxtWeather1x1Temp.Text = "22°";
                TxtWeather1x3Temp.Text = "22° ☀️";
                TxtWeather1x3City.Text = $"{_settings.WeatherCity} • Parçalı";
                TxtForecastTomorrow.Text = "24°";
                TxtWeatherCity.Text = _settings.WeatherCity;
            }
        }

        private void WidgetWeather_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _ = FetchWeatherAsync();
        }

        #endregion

        #region Hızlı Windows Uygulama Başlatıcı

        private void BtnApp_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string appKey)
            {
                try
                {
                    switch (appKey)
                    {
                        case "explorer":
                            Process.Start("explorer.exe");
                            break;
                        case "terminal":
                            try { Process.Start(new ProcessStartInfo("wt.exe") { UseShellExecute = true }); }
                            catch { Process.Start("cmd.exe"); }
                            break;
                        case "calculator":
                            Process.Start("calc.exe");
                            break;
                        case "notepad":
                            Process.Start("notepad.exe");
                            break;
                        case "taskmgr":
                            Process.Start("taskmgr.exe");
                            break;
                        case "browser":
                            Process.Start(new ProcessStartInfo("https://www.google.com") { UseShellExecute = true });
                            break;
                        case "paint":
                            Process.Start("mspaint.exe");
                            break;
                        case "cmd":
                            Process.Start("cmd.exe");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Uygulama başlatılamadı: {ex.Message}");
                }
            }
        }

        #endregion

        #region Canlı Windows Medya & Müzik Çalar (GSMTC Entegrasyonu)

        private void InitializeMediaController()
        {
            try
            {
                Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        _mediaManager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                        if (_mediaManager != null)
                        {
                            _mediaManager.CurrentSessionChanged += (s, e) => Dispatcher.InvokeAsync(UpdateMediaSessionState);
                            await UpdateMediaSessionState();
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"GSMTC başlatma hatası: {ex.Message}");
                    }
                });
            }
            catch { }
        }

        private async Task UpdateMediaSessionState()
        {
            try
            {
                if (_mediaManager == null) return;
                _currentMediaSession = _mediaManager.GetCurrentSession();

                if (_currentMediaSession != null)
                {
                    _currentMediaSession.MediaPropertiesChanged -= CurrentSession_MediaPropertiesChanged;
                    _currentMediaSession.MediaPropertiesChanged += CurrentSession_MediaPropertiesChanged;
                    _currentMediaSession.PlaybackInfoChanged -= CurrentSession_PlaybackInfoChanged;
                    _currentMediaSession.PlaybackInfoChanged += CurrentSession_PlaybackInfoChanged;

                    var props = await _currentMediaSession.TryGetMediaPropertiesAsync();
                    var playback = _currentMediaSession.GetPlaybackInfo();

                    string title = !string.IsNullOrWhiteSpace(props?.Title) ? props.Title : "Medyayı Başlat";
                    string artist = !string.IsNullOrWhiteSpace(props?.Artist) 
                        ? props.Artist 
                        : (Path.GetFileNameWithoutExtension(_currentMediaSession.SourceAppUserModelId ?? "Medya Çalar"));

                    TxtSongTitle.Text = title;
                    bool isPlaying = playback?.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
                    _isPlaying = isPlaying;
                    TxtPlayPauseIcon.Text = isPlaying ? "⏸" : "▶";
                    TxtMusic1x1Icon.Text = isPlaying ? "⏸" : "▶";
                    TxtSongArtist.Text = isPlaying ? $"{artist} • Çalıyor" : $"{artist} • Duraklatıldı";
                    TxtSongTitle1x3.Text = title;
                    TxtSongArtist1x3.Text = TxtSongArtist.Text;
                    TxtPlayPauseIcon1x3.Text = TxtPlayPauseIcon.Text;
                }
                else
                {
                    _isPlaying = false;
                    TxtSongTitle.Text = "Medya Bekleniyor";
                    TxtSongArtist.Text = "Spotify veya Tarayıcı açın";
                    TxtPlayPauseIcon.Text = "▶";
                    TxtMusic1x1Icon.Text = "▶";
                    TxtSongTitle1x3.Text = "Medya Bekleniyor";
                    TxtSongArtist1x3.Text = "Spotify veya Tarayıcı açın";
                    TxtPlayPauseIcon1x3.Text = "▶";
                    Bar1.Height = 3;
                    Bar2.Height = 3;
                    Bar3.Height = 3;
                    Bar4.Height = 3;
                }
            }
            catch { }
        }

        private void CurrentSession_MediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args)
        {
            Dispatcher.InvokeAsync(UpdateMediaSessionState);
        }

        private void CurrentSession_PlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args)
        {
            Dispatcher.InvokeAsync(UpdateMediaSessionState);
        }

        private static void SendMediaKey(byte vk)
        {
            keybd_event(vk, 0, 0, UIntPtr.Zero);
            keybd_event(vk, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private async void BtnPlayPause_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentMediaSession != null)
                {
                    await _currentMediaSession.TryTogglePlayPauseAsync();
                }
                else
                {
                    SendMediaKey(VK_MEDIA_PLAY_PAUSE);
                }
                _ = UpdateMediaSessionState();
            }
            catch
            {
                SendMediaKey(VK_MEDIA_PLAY_PAUSE);
            }
        }

        private async void BtnPrev_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentMediaSession != null)
                {
                    await _currentMediaSession.TrySkipPreviousAsync();
                }
                else
                {
                    SendMediaKey(VK_MEDIA_PREV_TRACK);
                }
                _ = UpdateMediaSessionState();
            }
            catch
            {
                SendMediaKey(VK_MEDIA_PREV_TRACK);
            }
        }

        private async void BtnNext_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentMediaSession != null)
                {
                    await _currentMediaSession.TrySkipNextAsync();
                }
                else
                {
                    SendMediaKey(VK_MEDIA_NEXT_TRACK);
                }
                _ = UpdateMediaSessionState();
            }
            catch
            {
                SendMediaKey(VK_MEDIA_NEXT_TRACK);
            }
        }

        #endregion

        #region Canlı Ağ Monitörü (Ping & Bağlantı)

        private void InitializeNetworkMonitor()
        {
            _networkTimer.Interval = TimeSpan.FromSeconds(5);
            _networkTimer.Tick += async (s, e) => await CheckNetworkPingAsync();
            _networkTimer.Start();
            _ = CheckNetworkPingAsync();
        }

        private async Task CheckNetworkPingAsync()
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync("1.1.1.1", 1200);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    TxtPingVal.Text = $"{reply.RoundtripTime} ms";
                    TxtPingVal.Foreground = reply.RoundtripTime < 80 
                        ? new SolidColorBrush(MediaColor.FromRgb(52, 211, 153)) 
                        : new SolidColorBrush(MediaColor.FromRgb(251, 191, 36));
                }
                else
                {
                    TxtPingVal.Text = "Koptu";
                    TxtPingVal.Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113));
                }
            }
            catch
            {
                TxtPingVal.Text = "Offline";
            }
        }

        #endregion

        #region Hızlı Notlar

        private string GetNoteFilePath()
        {
            return Path.Combine(AppSettings.GetDataFolder(), "notlar.txt");
        }

        private void InitializeNotes()
        {
            try
            {
                var file = GetNoteFilePath();
                if (File.Exists(file))
                {
                    TxtQuickNote.Text = File.ReadAllText(file);
                }
            }
            catch { }
        }

        private void TxtQuickNote_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                var file = GetNoteFilePath();
                File.WriteAllText(file, TxtQuickNote.Text);
            }
            catch { }
        }

        private void BtnToggleNote_Click(object sender, RoutedEventArgs e)
        {
            PopNote.PlacementTarget = (UIElement)sender;
            PopNote.IsOpen = !PopNote.IsOpen;
        }

        private void BtnCloseNote_Click(object sender, RoutedEventArgs e)
        {
            PopNote.IsOpen = false;
        }

        #endregion

        #region Android Tarzı Widget Boyutlandırma & Yeni Widget Olayları

        private void MenuWidgetSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfMenuItem mi && mi.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length == 2)
                {
                    string widget = parts[0].ToLowerInvariant();
                    string size = parts[1];
                    var profile = _settings.GetActiveProfile();
                    switch (widget)
                    {
                        case "clock": profile.ClockSize = size; break;
                        case "pomodoro": profile.PomodoroSize = size; break;
                        case "weather": profile.WeatherSize = size; break;
                        case "system": profile.SystemSize = size; break;
                        case "apps": profile.AppsSize = size; break;
                        case "customaction": profile.CustomActionSize = size; break;
                        case "network": profile.NetworkSize = size; break;
                        case "music": profile.MusicSize = size; break;
                        case "crypto": profile.CryptoSize = size; break;
                        case "calendar": profile.CalendarSize = size; break;
                        case "quicksearch": profile.QuickSearchSize = size; break;
                        case "volume": profile.VolumeSize = size; break;
                        case "clipboard": profile.ClipboardSize = size; break;
                    }
                    _settings.Save();
                    ApplySettings(_settings);
                }
            }
        }

        // --- Hava Durumu Menüleri ---
        private void WidgetWeather_Refresh_Click(object sender, RoutedEventArgs e) => _ = FetchWeatherAsync();
        private void MenuHideWeather_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowWeather = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Donanım & Sistem Menüleri ---
        private void MenuHideSystem_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowSystemStats = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Hızlı Uygulamalar Menüleri ---
        private void MenuHideApps_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowApps = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Müzik Çalar Menüleri ---
        private void MenuHideMusic_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowMusic = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Canlı Ağ Menüleri ---
        private void MenuHideNetwork_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowNetwork = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Kripto & Piyasa Widget'ı ---
        private async void WidgetCrypto_Refresh_Click(object sender, RoutedEventArgs e)
        {
            await UpdateCryptoQuotesAsync();
        }

        private void MenuHideCrypto_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowCrypto = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        private async Task UpdateCryptoQuotesAsync()
        {
            try
            {
                // Canlı Binance Kripto Fiyat API
                string url = "https://api.binance.com/api/v3/ticker/price?symbols=%5B%22BTCUSDT%22,%22ETHUSDT%22,%22SOLUSDT%22%5D";
                var json = await _httpClient.GetStringAsync(url);
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    string sym = el.GetProperty("symbol").GetString() ?? "";
                    if (decimal.TryParse(el.GetProperty("price").GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
                    {
                        if (sym == "BTCUSDT")
                        {
                            TxtCrypto1x1.Text = $"${(price / 1000):0.0}K";
                            TxtBtcPrice.Text = $"${price:N0}";
                            TxtBtc1x3.Text = $"${price:N0}";
                        }
                        else if (sym == "ETHUSDT")
                        {
                            TxtEthPrice.Text = $"${price:N0}";
                            TxtEth1x3.Text = $"${price:N0}";
                        }
                        else if (sym == "SOLUSDT")
                        {
                            TxtSol1x3.Text = $"${price:N0}";
                        }
                    }
                }
            }
            catch
            {
                // Çevrimdışı / Hızlı simülasyon fallback
                TxtCrypto1x1.Text = "$68.4K";
                TxtBtcPrice.Text = "$68,450";
                TxtEthPrice.Text = "$2,640";
                TxtBtc1x3.Text = "$68,450";
                TxtEth1x3.Text = "$2,640";
                TxtSol1x3.Text = "$182";
                TxtUsdTry1x3.Text = "34.25 ₺";
            }
        }

        // --- Takvim & Ajanda Widget'ı ---
        private void UpdateCalendarVisuals()
        {
            var now = DateTime.Now;
            var tr = new CultureInfo("tr-TR");
            TxtCal1x1.Text = now.ToString("dd MMM", tr);
            TxtCal1x2Date.Text = "📅 " + now.ToString("dd MMMM dddd", tr);
        }

        private void WidgetCalendar_OpenApp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("outlookcal:") { UseShellExecute = true });
            }
            catch
            {
                try { Process.Start(new ProcessStartInfo("https://calendar.google.com") { UseShellExecute = true }); } catch { }
            }
        }

        private void MenuHideCalendar_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowCalendar = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Hızlı Spotlight Arama Widget'ı ---
        private void BtnQuickSearch1x1_Click(object sender, RoutedEventArgs e)
        {
            SearchWeb("", "google");
        }

        private void TxtSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter && sender is TextBox tb && !string.IsNullOrWhiteSpace(tb.Text))
            {
                SearchWeb(tb.Text.Trim(), "google");
                tb.Text = "";
            }
        }

        private void BtnSearchEngine_Click(object sender, RoutedEventArgs e)
        {
            if (sender is WpfButton btn && btn.Tag is string engine)
            {
                string query = TxtSearchBox1x3.Text.Trim();
                if (string.IsNullOrWhiteSpace(query)) query = TxtSearchBox.Text.Trim();
                SearchWeb(query, engine);
            }
        }

        private void SearchWeb(string query, string engine)
        {
            string url = engine.ToLowerInvariant() switch
            {
                "perplexity" => string.IsNullOrWhiteSpace(query) ? "https://www.perplexity.ai" : $"https://www.perplexity.ai/search?q={Uri.EscapeDataString(query)}",
                "chatgpt" => string.IsNullOrWhiteSpace(query) ? "https://chatgpt.com" : $"https://chatgpt.com/?q={Uri.EscapeDataString(query)}",
                _ => string.IsNullOrWhiteSpace(query) ? "https://www.google.com" : $"https://www.google.com/search?q={Uri.EscapeDataString(query)}"
            };
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch { }
        }

        private void MenuHideQuickSearch_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowQuickSearch = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Ses Düzeyi Widget'ı ---
        private const byte VK_VOLUME_MUTE = 0xAD;

        private void BtnVolumeMute_Click(object sender, RoutedEventArgs e)
        {
            keybd_event(VK_VOLUME_MUTE, 0, 0, UIntPtr.Zero);
            keybd_event(VK_VOLUME_MUTE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            TxtVolIcon.Text = TxtVolIcon.Text == "🔊" ? "🔇" : "🔊";
        }

        private void SldVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtVolVal != null) TxtVolVal.Text = $"{(int)e.NewValue}%";
            if (TxtVolume1x1 != null) TxtVolume1x1.Text = $"{(int)e.NewValue}%";
        }

        private void MenuHideVolume_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowVolume = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        // --- Pano Geçmişi Widget'ı ---
        private void UpdateClipboardPreview()
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string txt = System.Windows.Clipboard.GetText().Trim().Replace("\r", " ").Replace("\n", " ");
                    if (!string.IsNullOrWhiteSpace(txt))
                    {
                        TxtClipboardPreview.Text = txt.Length > 16 ? txt.Substring(0, 14) + ".." : txt;
                        TxtClipboardPreview.ToolTip = $"Son Kopyalanan:\n{txt}";
                        return;
                    }
                }
                TxtClipboardPreview.Text = "Pano Boş";
                TxtClipboardPreview.ToolTip = "Pano Boş";
            }
            catch { }
        }

        private void BtnClipboardPaste_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (System.Windows.Clipboard.ContainsText())
                {
                    string txt = System.Windows.Clipboard.GetText();
                    _notifyIcon?.ShowBalloonTip(2000, "OxiDock Pano", $"Kopyalanan öğe hazır: {txt.Substring(0, Math.Min(txt.Length, 50))}", Forms.ToolTipIcon.Info);
                }
            }
            catch { }
        }

        private void MenuHideClipboard_Click(object sender, RoutedEventArgs e)
        {
            var p = _settings.GetActiveProfile();
            p.ShowClipboard = false;
            _settings.Save();
            ApplySettings(_settings);
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _aiServer?.Stop();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            base.OnClosed(e);
        }
    }
}