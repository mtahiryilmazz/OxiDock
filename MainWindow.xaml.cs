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

        public void RenderCustomWidgets(DockProfile profile)
        {
            SpCustomWidgets.Children.Clear();
            if (profile.CustomWidgets == null) return;

            foreach (var widget in profile.CustomWidgets)
            {
                if (!widget.IsEnabled) continue;

                var border = new Border
                {
                    Style = (Style)FindResource("WidgetContainerStyle"),
                    Cursor = WpfCursors.Hand,
                    Tag = widget
                };

                // Tooltip
                border.ToolTip = $"{widget.Title}\n{widget.Subtitle} • Tıkla: Başlat | Sağ Tık: Düzenle / Sil";

                // Tıklama ile çalıştırma
                border.MouseLeftButtonDown += (s, e) =>
                {
                    if (e.ChangedButton == MouseButton.Left)
                    {
                        LaunchCustomWidget(widget);
                    }
                };

                // Sağ tık menüsü (Ayar / Düzenleme / Silme)
                var cm = new WpfContextMenu
                {
                    Background = new SolidColorBrush(MediaColor.FromRgb(24, 24, 30)),
                    Foreground = WpfBrushes.White,
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(40, 255, 255, 255)),
                    BorderThickness = new Thickness(1)
                };

                var miSettings = new WpfMenuItem { Header = $"⚙️  '{widget.Title}' Widget'ını Düzenle / Ayarlar..." };
                miSettings.Click += (s, e) => ShowCustomWidgetEditorDialog(widget);
                cm.Items.Add(miSettings);

                var miSize = new WpfMenuItem { Header = "📏  Boyut Değiştir" };
                var mi1x1 = new WpfMenuItem { Header = "1x1 Kare Kompakt", IsChecked = widget.WidgetSize == "1x1" };
                mi1x1.Click += (s, e) => { widget.WidgetSize = "1x1"; _settings.Save(); ApplySettings(_settings); };
                var mi1x2 = new WpfMenuItem { Header = "1x2 Yatay Standart", IsChecked = widget.WidgetSize == "1x2" };
                mi1x2.Click += (s, e) => { widget.WidgetSize = "1x2"; _settings.Save(); ApplySettings(_settings); };
                var mi1x3 = new WpfMenuItem { Header = "1x3 Genişletilmiş Kart", IsChecked = widget.WidgetSize == "1x3" };
                mi1x3.Click += (s, e) => { widget.WidgetSize = "1x3"; _settings.Save(); ApplySettings(_settings); };
                miSize.Items.Add(mi1x1);
                miSize.Items.Add(mi1x2);
                miSize.Items.Add(mi1x3);
                cm.Items.Add(miSize);

                cm.Items.Add(new WpfSeparator());

                var miDelete = new WpfMenuItem { Header = $"🗑️  '{widget.Title}' Widget'ını Kaldır", Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)) };
                miDelete.Click += (s, e) =>
                {
                    profile.CustomWidgets.Remove(widget);
                    _settings.Save();
                    ApplySettings(_settings);
                };
                cm.Items.Add(miDelete);

                border.ContextMenu = cm;

                // İçerik Görünümü
                var contentGrid = BuildCustomWidgetContent(widget);
                border.Child = contentGrid;

                SpCustomWidgets.Children.Add(border);
            }
        }

        private FrameworkElement BuildCustomWidgetContent(CustomAppWidget w)
        {
            var accentColor = MediaColor.FromRgb(56, 189, 248);
            try
            {
                if (!string.IsNullOrWhiteSpace(w.AccentColor))
                {
                    var converted = System.Windows.Media.ColorConverter.ConvertFromString(w.AccentColor);
                    if (converted is MediaColor mc) accentColor = mc;
                }
            }
            catch { }

            // İkon Gövdesi
            var iconBorder = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(8),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center
            };

            var iconTxt = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(w.IconValue) ? "📄" : w.IconValue,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // İkon Stil Teması (Siyah-Beyaz, Fluent, Material, Renkli)
            switch (w.IconType)
            {
                case "Monochrome":
                    iconBorder.Background = new SolidColorBrush(MediaColor.FromArgb(30, 255, 255, 255));
                    iconBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(80, 255, 255, 255));
                    iconBorder.BorderThickness = new Thickness(1);
                    iconTxt.Foreground = WpfBrushes.White;
                    break;
                case "Material":
                    iconBorder.Background = new SolidColorBrush(MediaColor.FromArgb(40, accentColor.R, accentColor.G, accentColor.B));
                    iconBorder.BorderBrush = new SolidColorBrush(accentColor);
                    iconBorder.BorderThickness = new Thickness(1.2);
                    iconTxt.Foreground = new SolidColorBrush(accentColor);
                    break;
                case "Color":
                    iconBorder.Background = new SolidColorBrush(accentColor);
                    iconTxt.Foreground = WpfBrushes.White;
                    break;
                default: // Fluent
                    iconBorder.Background = new SolidColorBrush(MediaColor.FromArgb(35, accentColor.R, accentColor.G, accentColor.B));
                    iconBorder.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(90, accentColor.R, accentColor.G, accentColor.B));
                    iconBorder.BorderThickness = new Thickness(1);
                    iconTxt.Foreground = new SolidColorBrush(accentColor);
                    break;
            }
            iconBorder.Child = iconTxt;

            if (w.WidgetSize == "1x1")
            {
                // Kare görünüm (En az kare görünümünde kalsın)
                iconBorder.Width = 32;
                iconBorder.Height = 32;
                iconBorder.CornerRadius = new CornerRadius(9);
                iconTxt.FontSize = 15;

                var pnl = new StackPanel
                {
                    Orientation = WpfOrientation.Vertical,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    Margin = new Thickness(8, 2, 8, 2),
                    MinWidth = 52
                };
                pnl.Children.Add(iconBorder);
                pnl.Children.Add(new TextBlock
                {
                    Text = w.Title,
                    FontSize = 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (WpfBrush)FindResource("DockSecondaryForegroundBrush"),
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 54,
                    Margin = new Thickness(0, 3, 0, 0)
                });
                return pnl;
            }
            else if (w.WidgetSize == "1x3")
            {
                // Genişletilmiş Kart
                var dock = new DockPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 2, 8, 2),
                    MinWidth = 130
                };
                DockPanel.SetDock(iconBorder, Dock.Left);
                dock.Children.Add(iconBorder);

                var btnSettings = new WpfButton
                {
                    Style = (Style)FindResource("DockButtonStyle"),
                    Width = 22,
                    Height = 22,
                    Margin = new Thickness(6, 0, 0, 0),
                    Content = new TextBlock { Text = "⚙️", FontSize = 9 },
                    ToolTip = "Widget Ayarları"
                };
                btnSettings.Click += (s, e) => ShowCustomWidgetEditorDialog(w);
                DockPanel.SetDock(btnSettings, Dock.Right);
                dock.Children.Add(btnSettings);

                var spText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(7, 0, 0, 0) };
                spText.Children.Add(new TextBlock
                {
                    Text = w.Title,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (WpfBrush)FindResource("DockForegroundBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 85
                });
                spText.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(w.Subtitle) ? "Özel Uygulama" : w.Subtitle,
                    FontSize = 9,
                    Foreground = new SolidColorBrush(accentColor),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 85
                });
                dock.Children.Add(spText);
                return dock;
            }
            else // 1x2 Standart
            {
                var dock = new DockPanel
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(6, 2, 6, 2),
                    MinWidth = 80
                };
                DockPanel.SetDock(iconBorder, Dock.Left);
                dock.Children.Add(iconBorder);

                var spText = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
                spText.Children.Add(new TextBlock
                {
                    Text = w.Title,
                    FontSize = 11,
                    FontWeight = FontWeights.Bold,
                    Foreground = (WpfBrush)FindResource("DockForegroundBrush"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 65
                });
                spText.Children.Add(new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(w.Subtitle) ? "BAŞLAT" : w.Subtitle,
                    FontSize = 8,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(accentColor),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 65
                });
                dock.Children.Add(spText);
                return dock;
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

                var psi = new ProcessStartInfo
                {
                    FileName = widget.TargetPath,
                    Arguments = widget.Arguments,
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                Forms.MessageBox.Show($"'{widget.Title}' başlatılamadı:\n{ex.Message}\n\nLütfen ayarlar butonuna basarak dosya yolunu düzenleyin.", "OxiDock Uygulama Widget", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
        }

        public void ShowCustomWidgetEditorDialog(CustomAppWidget? widgetToEdit = null)
        {
            var isNew = (widgetToEdit == null);
            var widget = widgetToEdit ?? new CustomAppWidget
            {
                Title = "Word",
                Subtitle = "Belge Düzenleyici",
                IconType = "Fluent",
                IconValue = "W",
                AccentColor = "#2563EB",
                TargetPath = "winword.exe"
            };

            var dlg = new Window
            {
                Title = isNew ? "OxiDock • Yeni Uygulama Widget'ı Oluştur" : $"OxiDock • '{widget.Title}' Widget Ayarları",
                Width = 460,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Background = new SolidColorBrush(MediaColor.FromRgb(20, 20, 28)),
                Foreground = WpfBrushes.White,
                Topmost = true,
                ResizeMode = ResizeMode.NoResize
            };

            var sp = new StackPanel { Margin = new Thickness(18) };

            sp.Children.Add(new TextBlock
            {
                Text = isNew ? "✨ Yeni Uygulama Widget'ı Oluştur" : $"⚙️ '{widget.Title}' Widget'ını Düzenle",
                FontWeight = FontWeights.Bold,
                FontSize = 15,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)),
                Margin = new Thickness(0, 0, 0, 12)
            });

            // Başlık
            sp.Children.Add(new TextBlock { Text = "Widget Başlığı (Örn: Word, Excel, VS Code):", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var txtTitle = new TextBox { Text = widget.Title, Margin = new Thickness(0, 3, 0, 8), Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            sp.Children.Add(txtTitle);

            // Alt Başlık / Açıklama
            sp.Children.Add(new TextBlock { Text = "Alt Başlık / Bilgi (Örn: Tablolar, Kod Editörü, AÇ):", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var txtSubtitle = new TextBox { Text = widget.Subtitle, Margin = new Thickness(0, 3, 0, 8), Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            sp.Children.Add(txtSubtitle);

            // Hedef Yol ve Gözat
            sp.Children.Add(new TextBlock { Text = "Çalıştırılacak Dosya (.exe, .lnk, .bat) veya Web Linki:", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var dpPath = new DockPanel { Margin = new Thickness(0, 3, 0, 8) };
            var btnBrowse = new WpfButton { Content = "Gözat...", Width = 70, Margin = new Thickness(6, 0, 0, 0) };
            var txtPath = new TextBox { Text = widget.TargetPath, Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
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
                    if (string.IsNullOrWhiteSpace(txtTitle.Text))
                    {
                        txtTitle.Text = Path.GetFileNameWithoutExtension(ofd.FileName);
                    }
                }
            };
            DockPanel.SetDock(btnBrowse, Dock.Right);
            dpPath.Children.Add(btnBrowse);
            dpPath.Children.Add(txtPath);
            sp.Children.Add(dpPath);

            // İkon Tasarım Stili ve Değeri
            var gridIcon = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            gridIcon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridIcon.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var spStyle = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            spStyle.Children.Add(new TextBlock { Text = "İkon Tasarım Stili:", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var cbIconStyle = new WpfComboBox { Margin = new Thickness(0, 3, 0, 0) };
            cbIconStyle.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Fluent Modern İkon", Tag = "Fluent" });
            cbIconStyle.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Siyah - Beyaz (Monochrome)", Tag = "Monochrome" });
            cbIconStyle.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Google Material İkon", Tag = "Material" });
            cbIconStyle.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Renkli Vurgulu İkon", Tag = "Color" });
            cbIconStyle.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "Emoji Stili", Tag = "Emoji" });
            cbIconStyle.SelectedIndex = widget.IconType switch
            {
                "Monochrome" => 1,
                "Material" => 2,
                "Color" => 3,
                "Emoji" => 4,
                _ => 0
            };
            spStyle.Children.Add(cbIconStyle);
            Grid.SetColumn(spStyle, 0);
            gridIcon.Children.Add(spStyle);

            var spIconVal = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            spIconVal.Children.Add(new TextBlock { Text = "İkon Karakteri / Emoji:", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var txtIcon = new TextBox { Text = widget.IconValue, Margin = new Thickness(0, 3, 0, 0), Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            spIconVal.Children.Add(txtIcon);
            Grid.SetColumn(spIconVal, 1);
            gridIcon.Children.Add(spIconVal);

            sp.Children.Add(gridIcon);

            // Renk Vurgusu ve Boyut
            var gridColorSize = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            gridColorSize.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            gridColorSize.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var spColor = new StackPanel { Margin = new Thickness(0, 0, 6, 0) };
            spColor.Children.Add(new TextBlock { Text = "Vurgulayıcı Renk (Hex):", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var txtColor = new TextBox { Text = widget.AccentColor, Margin = new Thickness(0, 3, 0, 0), Background = new SolidColorBrush(MediaColor.FromRgb(30, 30, 40)), Foreground = WpfBrushes.White, Padding = new Thickness(6, 4, 6, 4) };
            spColor.Children.Add(txtColor);
            Grid.SetColumn(spColor, 0);
            gridColorSize.Children.Add(spColor);

            var spSize = new StackPanel { Margin = new Thickness(6, 0, 0, 0) };
            spSize.Children.Add(new TextBlock { Text = "Widget Boyutu:", FontSize = 11, Foreground = new SolidColorBrush(MediaColor.FromArgb(190, 255, 255, 255)) });
            var cbSize = new WpfComboBox { Margin = new Thickness(0, 3, 0, 0) };
            cbSize.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "1x1 Kare Kompakt", Tag = "1x1" });
            cbSize.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "1x2 Standart Kart", Tag = "1x2" });
            cbSize.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "1x3 Genişletilmiş Kart", Tag = "1x3" });
            cbSize.SelectedIndex = widget.WidgetSize switch
            {
                "1x2" => 1,
                "1x3" => 2,
                _ => 0
            };
            spSize.Children.Add(cbSize);
            Grid.SetColumn(spSize, 1);
            gridColorSize.Children.Add(spSize);

            sp.Children.Add(gridColorSize);

            // Hızlı Şablonlar (Word, Excel, VS Code, Spotify, Discord)
            var spPresets = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            spPresets.Children.Add(new TextBlock { Text = "⚡ Hızlı Uygulama Şablonları:", FontSize = 10, FontWeight = FontWeights.Bold, Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)), Margin = new Thickness(0, 0, 0, 4) });
            var wpPresets = new WrapPanel();
            void AddPresetBtn(string label, string title, string sub, string icon, string color, string path)
            {
                var b = new WpfButton
                {
                    Content = label,
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(0, 0, 4, 4),
                    Background = new SolidColorBrush(MediaColor.FromArgb(25, 255, 255, 255)),
                    Foreground = WpfBrushes.White,
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
                };
                wpPresets.Children.Add(b);
            }
            AddPresetBtn("Word", "Word", "Belgeler", "W", "#2563EB", "winword.exe");
            AddPresetBtn("Excel", "Excel", "Tablolar", "X", "#16A34A", "excel.exe");
            AddPresetBtn("PowerPoint", "PowerPoint", "Sunumlar", "P", "#EA580C", "powerpnt.exe");
            AddPresetBtn("VS Code", "VS Code", "Kod Editörü", "⚡", "#0284C7", "code.cmd");
            AddPresetBtn("Chrome", "Chrome", "Web Tarayıcı", "🌐", "#EAB308", "chrome.exe");
            AddPresetBtn("Spotify", "Spotify", "Müzik", "🎵", "#22C55E", "spotify.exe");
            spPresets.Children.Add(wpPresets);
            sp.Children.Add(spPresets);

            // Kaydet & Kapat Butonları
            var dpAction = new DockPanel();
            var btnSave = new WpfButton
            {
                Content = isNew ? "✨ Widget'ı Oluştur ve Dock'a Ekle" : "💾 Değişiklikleri Kaydet",
                Padding = new Thickness(16, 8, 16, 8),
                Background = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)),
                Foreground = WpfBrushes.Black,
                FontWeight = FontWeights.Bold,
                Cursor = WpfCursors.Hand
            };
            btnSave.Click += (s, e) =>
            {
                widget.Title = string.IsNullOrWhiteSpace(txtTitle.Text) ? "Uygulama" : txtTitle.Text.Trim();
                widget.Subtitle = txtSubtitle.Text.Trim();
                widget.TargetPath = txtPath.Text.Trim();
                widget.IconValue = string.IsNullOrWhiteSpace(txtIcon.Text) ? "📄" : txtIcon.Text.Trim();
                widget.AccentColor = string.IsNullOrWhiteSpace(txtColor.Text) ? "#38BDF8" : txtColor.Text.Trim();
                widget.IconType = (cbIconStyle.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "Fluent";
                widget.WidgetSize = (cbSize.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Tag?.ToString() ?? "1x1";
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
            dpAction.Children.Add(btnSave);
            sp.Children.Add(dpAction);

            dlg.Content = sp;
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