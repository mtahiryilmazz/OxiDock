using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfButton = System.Windows.Controls.Button;
using WpfOrientation = System.Windows.Controls.Orientation;
using WpfHorizontalAlignment = System.Windows.HorizontalAlignment;

namespace OxiDock
{
    public partial class SettingsWindow : Window
    {
        private readonly AppSettings _settings;
        public Action<AppSettings>? OnSettingsChanged;
        public Action<AppSettings>? OnPreviewSettings;
        private bool _isLoaded = false;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadCurrentSettings();
            _isLoaded = true;
        }

        private void Sidebar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void BtnCloseWindow_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadCurrentSettings()
        {
            // 1. Genel
            ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            ChkAutoHide.IsChecked = _settings.AutoHide;
            ChkFullscreenHide.IsChecked = _settings.HideDuringFullscreen;
            ChkDynamicIsland.IsChecked = _settings.EnableDynamicIsland;
            ChkFileShelf.IsChecked = _settings.ShowFileShelf;
            TxtWeatherCity.Text = _settings.WeatherCity;

            // 2. Düzen
            SetComboValue(CmbScenario, _settings.LayoutScenario);
            SetComboValue(CmbPosition, _settings.DockPosition);
            SetComboValue(CmbAlignment, _settings.DockAlignment);

            // 3. Görünüm & Görsel Tema Kartları
            SetComboValue(CmbStyle, _settings.DockStyle);
            HighlightThemeCard(_settings.DockStyle);
            SldScale.Value = _settings.DockScale;
            TxtScaleVal.Text = $"{_settings.DockScale:0.00}x";
            SldSpacing.Value = _settings.WidgetSpacing;
            TxtSpacingVal.Text = $"{_settings.WidgetSpacing} px";
            ChkReduceTransparency.IsChecked = _settings.ReduceTransparency;
            ChkTileBorders.IsChecked = _settings.ShowTileBorders;

            // 4. Profiller & Özel Widget'lar
            RefreshProfilesList();
            RefreshCustomWidgetsManager();

            // 5. Pomodoro
            TxtPomoFocus.Text = _settings.PomodoroFocusMinutes.ToString();
            TxtPomoBreak.Text = _settings.PomodoroBreakMinutes.ToString();
            ChkReminders.IsChecked = _settings.PostureReminderEnabled;

            // 6. AI Asistan
            ChkEnableAi.IsChecked = _settings.EnableAiAssistant;
            ChkAllowAiChanges.IsChecked = _settings.AllowAiChanges;
        }

        private void HighlightThemeCard(string styleKey)
        {
            var cards = new (Border Card, string Key)[]
            {
                (CardThemeDarkGlass, "DarkGlass"),
                (CardThemeLiquidGlass, "LiquidGlass"),
                (CardThemeLightGlass, "LightGlass"),
                (CardThemeCyberpunk, "Cyberpunk"),
                (CardThemeOledBlack, "OledBlack"),
                (CardThemeTransparent, "Transparent")
            };

            foreach (var (card, key) in cards)
            {
                if (card == null) continue;
                bool active = string.Equals(key, styleKey, StringComparison.OrdinalIgnoreCase);
                card.BorderBrush = active
                    ? new SolidColorBrush(MediaColor.FromRgb(56, 189, 248))
                    : new SolidColorBrush(MediaColor.FromArgb(40, 255, 255, 255));
                card.Background = active
                    ? new SolidColorBrush(MediaColor.FromArgb(55, 56, 189, 248))
                    : new SolidColorBrush(MediaColor.FromRgb(20, 28, 43));
            }
        }

        private void ThemeCard_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is Border card && card.Tag is string styleKey)
            {
                SetComboValue(CmbStyle, styleKey);
                HighlightThemeCard(styleKey);
                if (_isLoaded) TriggerLivePreview();
            }
        }

        private void RefreshCustomWidgetsManager()
        {
            if (SpSettingsCustomWidgetsList == null) return;
            SpSettingsCustomWidgetsList.Children.Clear();

            var prof = _settings.GetActiveProfile();
            prof.CustomWidgets ??= new System.Collections.Generic.List<CustomAppWidget>();

            if (prof.CustomWidgets.Count == 0)
            {
                var emptyBorder = new Border
                {
                    Style = (Style)FindResource("SettingCardStyle"),
                    Padding = new Thickness(20)
                };
                var spEmpty = new StackPanel { HorizontalAlignment = WpfHorizontalAlignment.Center };
                spEmpty.Children.Add(new TextBlock
                {
                    Text = "🧩 Bu çalışma alanında henüz özel bir widget yok.",
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = System.Windows.Media.Brushes.White,
                    HorizontalAlignment = WpfHorizontalAlignment.Center
                });
                spEmpty.Children.Add(new TextBlock
                {
                    Text = "Üstteki '+ Yeni Özel Widget Oluştur' butonuna tıklayarak Living Island, Super Tile veya Dual Combo tarzı widget'ınızı saniyeler içinde tasarlayabilirsiniz.",
                    FontSize = 11,
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)),
                    HorizontalAlignment = WpfHorizontalAlignment.Center,
                    Margin = new Thickness(0, 4, 0, 0)
                });
                emptyBorder.Child = spEmpty;
                SpSettingsCustomWidgetsList.Children.Add(emptyBorder);
                return;
            }

            foreach (var cw in prof.CustomWidgets.ToArray())
            {
                var accent = MainWindow.ParseAccentColor(cw.AccentColor);
                var rowCard = new Border
                {
                    Style = (Style)FindResource("SettingCardStyle"),
                    BorderBrush = new SolidColorBrush(MediaColor.FromArgb(90, accent.R, accent.G, accent.B))
                };

                var dp = new DockPanel();

                var spBtns = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                var btnSize = new WpfButton
                {
                    Content = $"📏 {cw.WidgetSize}",
                    Padding = new Thickness(10, 5, 10, 5),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = new SolidColorBrush(MediaColor.FromArgb(35, 255, 255, 255)),
                    Foreground = System.Windows.Media.Brushes.White,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ToolTip = "Boyutu Değiştir (1x1 / 1x2 / 1x3)"
                };
                btnSize.Click += (s, ev) =>
                {
                    cw.WidgetSize = cw.WidgetSize switch
                    {
                        "1x1" => "1x2",
                        "1x2" => "1x3",
                        _ => "1x1"
                    };
                    _settings.Save();
                    RefreshCustomWidgetsManager();
                    TriggerLivePreview();
                };

                var btnEdit = new WpfButton
                {
                    Content = "🎨 Özelleştir / Düzenle",
                    Padding = new Thickness(12, 5, 12, 5),
                    Margin = new Thickness(0, 0, 6, 0),
                    Background = new SolidColorBrush(MediaColor.FromArgb(50, 56, 189, 248)),
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(56, 189, 248)),
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnEdit.Click += (s, ev) =>
                {
                    if (Owner is MainWindow mw)
                    {
                        mw.ShowCustomWidgetEditorDialog(cw);
                        RefreshCustomWidgetsManager();
                        TriggerLivePreview();
                    }
                };

                var btnDelete = new WpfButton
                {
                    Content = "🗑️ Kaldır",
                    Padding = new Thickness(10, 5, 10, 5),
                    Background = new SolidColorBrush(MediaColor.FromArgb(50, 248, 113, 113)),
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(248, 113, 113)),
                    FontWeight = FontWeights.Bold,
                    BorderThickness = new Thickness(0),
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                btnDelete.Click += (s, ev) =>
                {
                    prof.CustomWidgets.Remove(cw);
                    _settings.Save();
                    RefreshCustomWidgetsManager();
                    TriggerLivePreview();
                };

                spBtns.Children.Add(btnSize);
                spBtns.Children.Add(btnEdit);
                spBtns.Children.Add(btnDelete);
                DockPanel.SetDock(spBtns, Dock.Right);
                dp.Children.Add(spBtns);

                var spLeft = new StackPanel { Orientation = WpfOrientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                spLeft.Children.Add(MainWindow.BuildStyledIconElement(cw, 34, 15));

                var spInfo = new StackPanel { Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                spInfo.Children.Add(new TextBlock
                {
                    Text = $"{cw.Title}  •  {cw.Subtitle}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = System.Windows.Media.Brushes.White
                });
                spInfo.Children.Add(new TextBlock
                {
                    Text = $"Şablon: {cw.CardTemplate} | Yüzey: {cw.BackgroundMode} | Hedef: {cw.TargetPath}",
                    FontSize = 10.5,
                    Foreground = new SolidColorBrush(MediaColor.FromRgb(148, 163, 184)),
                    Margin = new Thickness(0, 2, 0, 0)
                });
                spLeft.Children.Add(spInfo);
                dp.Children.Add(spLeft);

                rowCard.Child = dp;
                SpSettingsCustomWidgetsList.Children.Add(rowCard);
            }
        }

        private void BtnOpenWidgetStudio_Click(object sender, RoutedEventArgs e)
        {
            if (Owner is MainWindow mw)
            {
                mw.ShowCustomWidgetEditorDialog(null);
                RefreshCustomWidgetsManager();
                TriggerLivePreview();
            }
        }

        private void RefreshProfilesList()
        {
            CmbActiveProfile.Items.Clear();
            LstProfiles.Items.Clear();

            foreach (var p in _settings.Profiles)
            {
                var item = new ComboBoxItem { Content = p.Name, Tag = p.Id };
                CmbActiveProfile.Items.Add(item);
                if (p.Id == _settings.ActiveProfileId)
                {
                    CmbActiveProfile.SelectedItem = item;
                }

                var lItem = new ListBoxItem { Content = $"🗂️ {p.Name}{(p.Id == _settings.ActiveProfileId ? " (Aktif)" : "")}", Tag = p.Id };
                LstProfiles.Items.Add(lItem);
            }

            if (LstProfiles.Items.Count > 0 && LstProfiles.SelectedItem == null)
            {
                LstProfiles.SelectedIndex = 0;
            }
        }

        private void LstProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstProfiles.SelectedItem is ListBoxItem lItem && lItem.Tag is string pId)
            {
                var prof = _settings.Profiles.Find(p => p.Id == pId);
                if (prof != null)
                {
                    TxtNewProfileName.Text = prof.Name;
                }
            }
        }

        private void BtnAddProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtNewProfileName.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = $"Çalışma Alanı {_settings.Profiles.Count + 1}";

            var newProfile = new DockProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = name
            };
            _settings.Profiles.Add(newProfile);
            _settings.ActiveProfileId = newProfile.Id;
            TxtNewProfileName.Text = "";
            RefreshProfilesList();
            RefreshCustomWidgetsManager();
            TriggerLivePreview();
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_settings.Profiles.Count <= 1)
            {
                System.Windows.MessageBox.Show("En az bir çalışma alanı kalmalıdır. Son çalışma alanı silinemez.", "OxiDock", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string? targetId = null;
            if (LstProfiles.SelectedItem is ListBoxItem lItem && lItem.Tag is string pId)
            {
                targetId = pId;
            }
            else if (CmbActiveProfile.SelectedItem is ComboBoxItem cItem && cItem.Tag is string cId)
            {
                targetId = cId;
            }

            if (!string.IsNullOrEmpty(targetId))
            {
                var prof = _settings.Profiles.Find(p => p.Id == targetId);
                if (prof != null)
                {
                    var result = System.Windows.MessageBox.Show($"'{prof.Name}' çalışma alanını silmek istediğinize emin misiniz?", "Çalışma Alanını Sil", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (result == MessageBoxResult.Yes)
                    {
                        _settings.Profiles.Remove(prof);
                        if (_settings.ActiveProfileId == targetId)
                        {
                            _settings.ActiveProfileId = _settings.Profiles[0].Id;
                        }
                        RefreshProfilesList();
                        RefreshCustomWidgetsManager();
                        TriggerLivePreview();
                    }
                }
            }
        }

        private void BtnRenameProfile_Click(object sender, RoutedEventArgs e)
        {
            string newName = TxtNewProfileName.Text.Trim();
            if (string.IsNullOrEmpty(newName)) return;

            if (LstProfiles.SelectedItem is ListBoxItem lItem && lItem.Tag is string pId)
            {
                var prof = _settings.Profiles.Find(p => p.Id == pId);
                if (prof != null)
                {
                    prof.Name = newName;
                    RefreshProfilesList();
                    TriggerLivePreview();
                }
            }
        }

        private void SetComboValue(System.Windows.Controls.ComboBox combo, string tag)
        {
            foreach (ComboBoxItem item in combo.Items)
            {
                if (item.Tag?.ToString() == tag)
                {
                    combo.SelectedItem = item;
                    break;
                }
            }
        }

        private string GetComboValue(System.Windows.Controls.ComboBox combo, string defaultVal)
        {
            if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                return tag;
            }
            return defaultVal;
        }

        private void Tab_Checked(object sender, RoutedEventArgs e)
        {
            if (PanelGeneral == null || PanelLayout == null || PanelAppearance == null ||
                PanelCustomWidgets == null || PanelProfiles == null || PanelPomodoro == null || PanelAi == null)
                return;

            PanelAppearance.Visibility = TabBtnAppearance.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelCustomWidgets.Visibility = TabBtnCustomWidgets.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelLayout.Visibility = TabBtnLayout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelProfiles.Visibility = TabBtnProfiles.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelGeneral.Visibility = TabBtnGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelPomodoro.Visibility = TabBtnPomodoro.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelAi.Visibility = TabBtnAi.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

            if (TabBtnCustomWidgets.IsChecked == true)
            {
                RefreshCustomWidgetsManager();
            }
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtScaleVal != null)
            {
                TxtScaleVal.Text = $"{e.NewValue:0.00}x";
            }
            if (_isLoaded) TriggerLivePreview();
        }

        private void SldSpacing_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtSpacingVal != null)
            {
                TxtSpacingVal.Text = $"{(int)e.NewValue} px";
            }
            if (_isLoaded) TriggerLivePreview();
        }

        private void CmbStyle_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded) TriggerLivePreview();
        }

        private void ChkReduceTransparency_Click(object sender, RoutedEventArgs e)
        {
            if (_isLoaded) TriggerLivePreview();
        }

        private void CmbAlignment_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded) TriggerLivePreview();
        }

        private void CmbPosition_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoaded) TriggerLivePreview();
        }

        private void TriggerLivePreview()
        {
            try
            {
                if (CmbStyle == null || SldScale == null || ChkReduceTransparency == null) return;

                var preview = new AppSettings
                {
                    AlwaysOnTop = ChkAlwaysOnTop?.IsChecked == true,
                    AutoHide = ChkAutoHide?.IsChecked == true,
                    HideDuringFullscreen = ChkFullscreenHide?.IsChecked == true,
                    EnableDynamicIsland = ChkDynamicIsland?.IsChecked == true,
                    ShowFileShelf = ChkFileShelf?.IsChecked == true,
                    WeatherCity = string.IsNullOrWhiteSpace(TxtWeatherCity?.Text) ? _settings.WeatherCity : TxtWeatherCity.Text.Trim(),
                    LayoutScenario = GetComboValue(CmbScenario, _settings.LayoutScenario),
                    DockPosition = GetComboValue(CmbPosition, _settings.DockPosition),
                    DockAlignment = GetComboValue(CmbAlignment, _settings.DockAlignment),
                    DockStyle = GetComboValue(CmbStyle, _settings.DockStyle),
                    DockScale = Math.Round(SldScale.Value, 2),
                    WidgetSpacing = SldSpacing != null ? (int)SldSpacing.Value : _settings.WidgetSpacing,
                    ReduceTransparency = ChkReduceTransparency.IsChecked == true,
                    ShowTileBorders = ChkTileBorders?.IsChecked ?? true,
                    ActiveProfileId = _settings.ActiveProfileId,
                    Profiles = _settings.Profiles,
                    CustomActionTitle = _settings.CustomActionTitle,
                    CustomActionIcon = _settings.CustomActionIcon,
                    CustomActionPath = _settings.CustomActionPath
                };

                OnPreviewSettings?.Invoke(preview);
            }
            catch { }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 1. Genel
            _settings.AlwaysOnTop = ChkAlwaysOnTop.IsChecked == true;
            _settings.AutoHide = ChkAutoHide.IsChecked == true;
            _settings.HideDuringFullscreen = ChkFullscreenHide.IsChecked == true;
            _settings.EnableDynamicIsland = ChkDynamicIsland.IsChecked == true;
            _settings.ShowFileShelf = ChkFileShelf.IsChecked == true;
            _settings.WeatherCity = string.IsNullOrWhiteSpace(TxtWeatherCity.Text) ? "İstanbul" : TxtWeatherCity.Text.Trim();

            // 2. Düzen
            _settings.LayoutScenario = GetComboValue(CmbScenario, "Floating");
            _settings.DockPosition = GetComboValue(CmbPosition, "Bottom");
            _settings.DockAlignment = GetComboValue(CmbAlignment, "Center");

            // 3. Görünüm
            _settings.DockStyle = GetComboValue(CmbStyle, "DarkGlass");
            _settings.DockScale = Math.Round(SldScale.Value, 2);
            if (SldSpacing != null) _settings.WidgetSpacing = (int)SldSpacing.Value;
            _settings.ReduceTransparency = ChkReduceTransparency.IsChecked == true;
            if (ChkTileBorders != null) _settings.ShowTileBorders = ChkTileBorders.IsChecked == true;

            // 4. Profil
            if (CmbActiveProfile.SelectedItem is ComboBoxItem selItem && selItem.Tag is string pId)
            {
                _settings.ActiveProfileId = pId;
            }

            // 5. Pomodoro
            if (int.TryParse(TxtPomoFocus.Text, out int focusMin)) _settings.PomodoroFocusMinutes = Math.Clamp(focusMin, 1, 120);
            if (int.TryParse(TxtPomoBreak.Text, out int breakMin)) _settings.PomodoroBreakMinutes = Math.Clamp(breakMin, 1, 60);
            _settings.PostureReminderEnabled = ChkReminders.IsChecked == true;

            // 6. AI Asistan
            _settings.EnableAiAssistant = ChkEnableAi.IsChecked == true;
            _settings.AllowAiChanges = ChkAllowAiChanges.IsChecked == true;

            _settings.Save();
            OnSettingsChanged?.Invoke(_settings);
            DialogResult = true;
            Close();
        }
    }
}
