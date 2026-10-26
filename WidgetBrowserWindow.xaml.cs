using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Button = System.Windows.Controls.Button;
using DataObject = System.Windows.DataObject;
using DragDropEffects = System.Windows.DragDropEffects;
using MediaColor = System.Windows.Media.Color;

namespace OxiDock
{
    public partial class WidgetBrowserWindow : Window
    {
        private readonly AppSettings _settings;
        public Action<AppSettings>? OnSettingsUpdated;

        private Point _dragStartPoint;
        private Border? _dragSourceCard;
        private bool _isDragging;

        private static readonly SolidColorBrush BrushActiveBg = new SolidColorBrush(MediaColor.FromArgb(38, 52, 211, 153));
        private static readonly SolidColorBrush BrushActiveBorder = new SolidColorBrush(MediaColor.FromArgb(100, 52, 211, 153));
        private static readonly SolidColorBrush BrushActiveText = new SolidColorBrush(MediaColor.FromRgb(52, 211, 153));

        private static readonly SolidColorBrush BrushInactiveBg = new SolidColorBrush(MediaColor.FromArgb(20, 255, 255, 255));
        private static readonly SolidColorBrush BrushInactiveBorder = new SolidColorBrush(MediaColor.FromArgb(32, 255, 255, 255));
        private static readonly SolidColorBrush BrushInactiveText = new SolidColorBrush(MediaColor.FromArgb(140, 255, 255, 255));

        private static readonly SolidColorBrush BrushPillActiveBg = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)); // #38BDF8 Sky Blue
        private static readonly SolidColorBrush BrushPillActiveFg = new SolidColorBrush(MediaColor.FromRgb(15, 23, 42)); // Dark
        private static readonly SolidColorBrush BrushPillInactiveBg = new SolidColorBrush(MediaColor.FromArgb(20, 255, 255, 255));
        private static readonly SolidColorBrush BrushPillInactiveFg = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)); // Muted

        public WidgetBrowserWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            HighlightCategoryTab(BtnCatAll);
            LoadWidgetStates();
        }

        public void PositionAboveDock(double dockLeft, double dockTop, double dockWidth, double dockHeight)
        {
            var workArea = SystemParameters.WorkArea;
            Left = Math.Clamp(dockLeft + (dockWidth - Width) / 2, workArea.Left + 10, workArea.Right - Width - 10);

            if (dockTop > workArea.Height / 2)
            {
                Top = Math.Max(workArea.Top + 10, dockTop - Height - 8);
            }
            else
            {
                Top = Math.Min(workArea.Bottom - Height - 10, dockTop + dockHeight + 8);
            }
        }

        private void Window_Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void LoadWidgetStates()
        {
            var p = _settings.GetActiveProfile();

            // 1. Durum Rozetleri
            UpdateBadge(BadgeClock, TxtStatusClock, p.ShowClock);
            UpdateBadge(BadgePomodoro, TxtStatusPomodoro, p.ShowPomodoro);
            UpdateBadge(BadgeWeather, TxtStatusWeather, p.ShowWeather);
            UpdateBadge(BadgeSystem, TxtStatusSystem, p.ShowSystemStats);
            UpdateBadge(BadgeBattery, TxtStatusBattery, p.ShowBattery);
            UpdateBadge(BadgeNetwork, TxtStatusNetwork, p.ShowNetwork);
            UpdateBadge(BadgeMusic, TxtStatusMusic, p.ShowMusic);
            UpdateBadge(BadgeApps, TxtStatusApps, p.ShowApps);
            UpdateBadge(BadgeCustomAction, TxtStatusCustomAction, p.ShowCustomAction);
            UpdateBadge(BadgeNotes, TxtStatusNotes, p.ShowNotes);
            UpdateBadge(BadgeCrypto, TxtStatusCrypto, p.ShowCrypto);
            UpdateBadge(BadgeCalendar, TxtStatusCalendar, p.ShowCalendar);
            UpdateBadge(BadgeQuickSearch, TxtStatusQuickSearch, p.ShowQuickSearch);
            UpdateBadge(BadgeVolume, TxtStatusVolume, p.ShowVolume);
            UpdateBadge(BadgeClipboard, TxtStatusClipboard, p.ShowClipboard);

            // Özel Araç Başlık & İkon
            TxtCardCustomIcon.Text = string.IsNullOrWhiteSpace(_settings.CustomActionIcon) ? "⚡" : _settings.CustomActionIcon;
            TxtCardCustomTitle.Text = string.IsNullOrWhiteSpace(_settings.CustomActionTitle) ? "Özel Araç" : _settings.CustomActionTitle;

            // 2. Android Boyut Hapları (Pills) Vurguları
            UpdateSizePills(BtnClock1x1, BtnClock1x2, BtnClock1x3, p.ClockSize);
            UpdateSizePills(BtnPomodoro1x1, BtnPomodoro1x2, BtnPomodoro1x3, p.PomodoroSize);
            UpdateSizePills(BtnWeather1x1, BtnWeather1x2, BtnWeather1x3, p.WeatherSize);
            UpdateSizePills(BtnSystem1x1, BtnSystem1x2, BtnSystem1x3, p.SystemSize);
            UpdateSizePills(BtnApps1x1, BtnApps1x2, BtnApps1x3, p.AppsSize);
            UpdateSizePills(BtnMusic1x1, BtnMusic1x2, BtnMusic1x3, p.MusicSize);
            UpdateSizePills(BtnCrypto1x1, BtnCrypto1x2, BtnCrypto1x3, p.CryptoSize);
            UpdateSizePills(BtnCalendar1x1, BtnCalendar1x2, BtnCalendar1x3, p.CalendarSize);
            UpdateSizePills(BtnQuickSearch1x1, BtnQuickSearch1x2, BtnQuickSearch1x3, p.QuickSearchSize);
            UpdateSizePills2(BtnVolume1x1, BtnVolume1x2, p.VolumeSize);
            UpdateSizePills2(BtnClipboard1x1, BtnClipboard1x2, p.ClipboardSize);
            UpdateSizePills2(BtnCustomAction1x1, BtnCustomAction1x2, p.CustomActionSize);
            UpdateSizePills2(BtnNetwork1x1, BtnNetwork1x2, p.NetworkSize);
        }

        private static void UpdateSizePills(Button b1, Button b2, Button b3, string current)
        {
            current = string.IsNullOrEmpty(current) ? "1x2" : current;
            SetPillStyle(b1, current == "1x1");
            SetPillStyle(b2, current == "1x2");
            SetPillStyle(b3, current == "1x3");
        }

        private static void UpdateSizePills2(Button b1, Button b2, string current)
        {
            current = string.IsNullOrEmpty(current) ? "1x2" : current;
            SetPillStyle(b1, current == "1x1");
            SetPillStyle(b2, current == "1x2");
        }

        private static void SetPillStyle(Button btn, bool isActive)
        {
            if (btn == null) return;
            btn.Background = isActive ? BrushPillActiveBg : BrushPillInactiveBg;
            btn.Foreground = isActive ? BrushPillActiveFg : BrushPillInactiveFg;
            btn.FontWeight = isActive ? FontWeights.Bold : FontWeights.Normal;
        }

        private static void UpdateBadge(Border badge, TextBlock txt, bool isActive)
        {
            if (badge == null || txt == null) return;
            if (isActive)
            {
                badge.Background = BrushActiveBg;
                badge.BorderBrush = BrushActiveBorder;
                txt.Text = "✓ Aktif";
                txt.Foreground = BrushActiveText;
            }
            else
            {
                badge.Background = BrushInactiveBg;
                badge.BorderBrush = BrushInactiveBorder;
                txt.Text = "+ Ekle";
                txt.Foreground = BrushInactiveText;
            }
        }

        #region Kategori Filtreleme

        private void BtnCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string cat)
            {
                HighlightCategoryTab(btn);
                FilterCategory(cat);
            }
        }

        private void HighlightCategoryTab(Button activeBtn)
        {
            var tabs = new[] { BtnCatAll, BtnCatTime, BtnCatSystem, BtnCatWeb, BtnCatShortcuts };
            foreach (var t in tabs)
            {
                if (t == null) continue;
                if (t == activeBtn)
                {
                    t.Background = new SolidColorBrush(MediaColor.FromArgb(90, 56, 189, 248));
                    t.Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
                    t.BorderBrush = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248));
                }
                else
                {
                    t.Background = new SolidColorBrush(MediaColor.FromArgb(26, 255, 255, 255));
                    t.Foreground = new SolidColorBrush(MediaColor.FromRgb(200, 200, 200));
                    t.BorderBrush = new SolidColorBrush(MediaColor.FromArgb(34, 255, 255, 255));
                }
            }
        }

        private void FilterCategory(string cat)
        {
            bool showAll = cat == "all";
            bool showTime = showAll || cat == "time";
            bool showSystem = showAll || cat == "system";
            bool showWeb = showAll || cat == "web";
            bool showShortcuts = showAll || cat == "shortcuts";

            CardClock.Visibility = showTime ? Visibility.Visible : Visibility.Collapsed;
            CardPomodoro.Visibility = showTime ? Visibility.Visible : Visibility.Collapsed;
            CardCalendar.Visibility = showTime ? Visibility.Visible : Visibility.Collapsed;

            CardSystem.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;
            CardNetwork.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;
            CardBattery.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;
            CardVolume.Visibility = showSystem ? Visibility.Visible : Visibility.Collapsed;

            CardWeather.Visibility = showWeb ? Visibility.Visible : Visibility.Collapsed;
            CardCrypto.Visibility = showWeb ? Visibility.Visible : Visibility.Collapsed;
            CardQuickSearch.Visibility = showWeb ? Visibility.Visible : Visibility.Collapsed;

            CardApps.Visibility = showShortcuts ? Visibility.Visible : Visibility.Collapsed;
            CardCustomAction.Visibility = showShortcuts ? Visibility.Visible : Visibility.Collapsed;
            CardClipboard.Visibility = showShortcuts ? Visibility.Visible : Visibility.Collapsed;
            CardNotes.Visibility = showShortcuts ? Visibility.Visible : Visibility.Collapsed;
            CardMusic.Visibility = (showAll || cat == "time" || cat == "shortcuts") ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion

        #region Android Boyut Değiştirme (Pill Click)

        private void BtnWidgetSize_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length == 2)
                {
                    string widget = parts[0].ToLowerInvariant();
                    string size = parts[1];
                    var p = _settings.GetActiveProfile();

                    switch (widget)
                    {
                        case "clock": p.ClockSize = size; p.ShowClock = true; break;
                        case "pomodoro": p.PomodoroSize = size; p.ShowPomodoro = true; break;
                        case "weather": p.WeatherSize = size; p.ShowWeather = true; break;
                        case "system": p.SystemSize = size; p.ShowSystemStats = true; break;
                        case "apps": p.AppsSize = size; p.ShowApps = true; break;
                        case "music": p.MusicSize = size; p.ShowMusic = true; break;
                        case "crypto": p.CryptoSize = size; p.ShowCrypto = true; break;
                        case "calendar": p.CalendarSize = size; p.ShowCalendar = true; break;
                        case "quicksearch": p.QuickSearchSize = size; p.ShowQuickSearch = true; break;
                        case "volume": p.VolumeSize = size; p.ShowVolume = true; break;
                        case "clipboard": p.ClipboardSize = size; p.ShowClipboard = true; break;
                        case "customaction": p.CustomActionSize = size; p.ShowCustomAction = true; break;
                        case "network": p.NetworkSize = size; p.ShowNetwork = true; break;
                    }

                    _settings.Save();
                    OnSettingsUpdated?.Invoke(_settings);
                    LoadWidgetStates();
                }
            }
        }

        #endregion

        #region Kart Tıklama & Sürükleme (Drag & Drop)

        private void WidgetCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Eğer butonlara (boyut haplarına) tıklandıysa kartı sürükleme
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? cur = dep;
                while (cur != null && cur != sender)
                {
                    if (cur is Button) return;
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }

            if (sender is Border card)
            {
                _dragStartPoint = e.GetPosition(this);
                _dragSourceCard = card;
                _isDragging = false;
            }
        }

        private void WidgetCard_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSourceCard == null)
                return;

            Point currentPoint = e.GetPosition(this);
            Vector diff = _dragStartPoint - currentPoint;

            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                _isDragging = true;
                string widgetKey = _dragSourceCard.Tag?.ToString() ?? "";

                if (!string.IsNullOrEmpty(widgetKey))
                {
                    var data = new DataObject("OxiDockWidget", widgetKey);
                    DragDrop.DoDragDrop(_dragSourceCard, data, DragDropEffects.Copy);
                }

                _dragSourceCard = null;
            }
        }

        private void WidgetCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                return;
            }

            // Butona tıklandıysa kartı açıp kapatma
            if (e.OriginalSource is DependencyObject dep)
            {
                DependencyObject? cur = dep;
                while (cur != null && cur != sender)
                {
                    if (cur is Button) return;
                    cur = VisualTreeHelper.GetParent(cur);
                }
            }

            if (sender is Border card && card.Tag is string tag)
            {
                ToggleWidget(tag);
            }
        }

        private void ToggleWidget(string widgetKey)
        {
            var profile = _settings.GetActiveProfile();

            switch (widgetKey.ToLowerInvariant())
            {
                case "clock":
                    profile.ShowClock = !profile.ShowClock;
                    _settings.ShowClock = profile.ShowClock;
                    break;
                case "pomodoro":
                    profile.ShowPomodoro = !profile.ShowPomodoro;
                    break;
                case "weather":
                    profile.ShowWeather = !profile.ShowWeather;
                    _settings.ShowWeather = profile.ShowWeather;
                    break;
                case "system":
                    profile.ShowSystemStats = !profile.ShowSystemStats;
                    _settings.ShowSystemStats = profile.ShowSystemStats;
                    break;
                case "battery":
                    profile.ShowBattery = !profile.ShowBattery;
                    break;
                case "network":
                    profile.ShowNetwork = !profile.ShowNetwork;
                    _settings.ShowNetwork = profile.ShowNetwork;
                    break;
                case "music":
                    profile.ShowMusic = !profile.ShowMusic;
                    _settings.ShowMusic = profile.ShowMusic;
                    break;
                case "apps":
                    profile.ShowApps = !profile.ShowApps;
                    _settings.ShowApps = profile.ShowApps;
                    break;
                case "customaction":
                    profile.ShowCustomAction = !profile.ShowCustomAction;
                    _settings.ShowCustomAction = profile.ShowCustomAction;
                    break;
                case "notes":
                    profile.ShowNotes = !profile.ShowNotes;
                    _settings.ShowNotes = profile.ShowNotes;
                    break;
                case "crypto":
                    profile.ShowCrypto = !profile.ShowCrypto;
                    break;
                case "calendar":
                    profile.ShowCalendar = !profile.ShowCalendar;
                    break;
                case "quicksearch":
                    profile.ShowQuickSearch = !profile.ShowQuickSearch;
                    break;
                case "volume":
                    profile.ShowVolume = !profile.ShowVolume;
                    break;
                case "clipboard":
                    profile.ShowClipboard = !profile.ShowClipboard;
                    break;
            }

            _settings.Save();
            OnSettingsUpdated?.Invoke(_settings);
            LoadWidgetStates();
        }

        #endregion

        private void BtnCreateCustomWidget_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                mw.ShowCustomWidgetEditorDialog(null);
            }
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
