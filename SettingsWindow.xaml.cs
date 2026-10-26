using System;
using System.Windows;
using System.Windows.Controls;

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

        private void LoadCurrentSettings()
        {
            // 1. Genel
            ChkAlwaysOnTop.IsChecked = _settings.AlwaysOnTop;
            ChkAutoHide.IsChecked = _settings.AutoHide;
            ChkFullscreenHide.IsChecked = _settings.HideDuringFullscreen;
            TxtWeatherCity.Text = _settings.WeatherCity;

            // 2. Düzen
            SetComboValue(CmbScenario, _settings.LayoutScenario);
            SetComboValue(CmbPosition, _settings.DockPosition);
            SetComboValue(CmbAlignment, _settings.DockAlignment);

            // 3. Görünüm
            SetComboValue(CmbStyle, _settings.DockStyle);
            SldScale.Value = _settings.DockScale;
            TxtScaleVal.Text = $"{_settings.DockScale:0.00}x";
            ChkReduceTransparency.IsChecked = _settings.ReduceTransparency;

            // 4. Profiller
            RefreshProfilesList();

            // 5. Pomodoro
            TxtPomoFocus.Text = _settings.PomodoroFocusMinutes.ToString();
            TxtPomoBreak.Text = _settings.PomodoroBreakMinutes.ToString();
            ChkReminders.IsChecked = _settings.PostureReminderEnabled;

            // 6. AI Asistan
            ChkEnableAi.IsChecked = _settings.EnableAiAssistant;
            ChkAllowAiChanges.IsChecked = _settings.AllowAiChanges;
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

                var lItem = new ListBoxItem { Content = $"📁 {p.Name}{(p.Id == _settings.ActiveProfileId ? " (Aktif)" : "")}", Tag = p.Id };
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
                PanelProfiles == null || PanelPomodoro == null || PanelAi == null)
                return;

            PanelGeneral.Visibility = TabBtnGeneral.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelLayout.Visibility = TabBtnLayout.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelAppearance.Visibility = TabBtnAppearance.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelProfiles.Visibility = TabBtnProfiles.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelPomodoro.Visibility = TabBtnPomodoro.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            PanelAi.Visibility = TabBtnAi.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SldScale_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (TxtScaleVal != null)
            {
                TxtScaleVal.Text = $"{e.NewValue:0.00}x";
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
                    WeatherCity = string.IsNullOrWhiteSpace(TxtWeatherCity?.Text) ? _settings.WeatherCity : TxtWeatherCity.Text.Trim(),
                    LayoutScenario = GetComboValue(CmbScenario, _settings.LayoutScenario),
                    DockPosition = GetComboValue(CmbPosition, _settings.DockPosition),
                    DockAlignment = GetComboValue(CmbAlignment, _settings.DockAlignment),
                    DockStyle = GetComboValue(CmbStyle, _settings.DockStyle),
                    DockScale = Math.Round(SldScale.Value, 2),
                    ReduceTransparency = ChkReduceTransparency.IsChecked == true,
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
            _settings.WeatherCity = string.IsNullOrWhiteSpace(TxtWeatherCity.Text) ? "İstanbul" : TxtWeatherCity.Text.Trim();

            // 2. Düzen
            _settings.LayoutScenario = GetComboValue(CmbScenario, "Floating");
            _settings.DockPosition = GetComboValue(CmbPosition, "Bottom");
            _settings.DockAlignment = GetComboValue(CmbAlignment, "Center");

            // 3. Görünüm
            _settings.DockStyle = GetComboValue(CmbStyle, "DarkGlass");
            _settings.DockScale = Math.Round(SldScale.Value, 2);
            _settings.ReduceTransparency = ChkReduceTransparency.IsChecked == true;

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
