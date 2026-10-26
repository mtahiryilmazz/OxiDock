using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace OxiDock
{
    public class DockProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Standart";

        // Widget Görünürlükleri
        public bool ShowClock { get; set; } = true;
        public bool ShowWeather { get; set; } = true;
        public bool ShowSystemStats { get; set; } = true;
        public bool ShowApps { get; set; } = true;
        public bool ShowMusic { get; set; } = true;
        public bool ShowNotes { get; set; } = true;
        public bool ShowPomodoro { get; set; } = true;
        public bool ShowBattery { get; set; } = true;
        public bool ShowCustomAction { get; set; } = true;
        public bool ShowNetwork { get; set; } = false;
        public bool ShowCrypto { get; set; } = false;
        public bool ShowCalendar { get; set; } = false;
        public bool ShowQuickSearch { get; set; } = false;
        public bool ShowVolume { get; set; } = false;
        public bool ShowClipboard { get; set; } = false;

        // Android Tarzı Widget Boyut / Varyantları: "1x1", "1x2", "1x3"
        public string ClockSize { get; set; } = "1x2";
        public string WeatherSize { get; set; } = "1x2";
        public string SystemSize { get; set; } = "1x2";
        public string AppsSize { get; set; } = "1x2";
        public string MusicSize { get; set; } = "1x2";
        public string PomodoroSize { get; set; } = "1x2";
        public string BatterySize { get; set; } = "1x1";
        public string CustomActionSize { get; set; } = "1x1";
        public string NetworkSize { get; set; } = "1x1";
        public string NotesSize { get; set; } = "1x1";
        public string CryptoSize { get; set; } = "1x2";
        public string CalendarSize { get; set; } = "1x2";
        public string QuickSearchSize { get; set; } = "1x2";
        public string VolumeSize { get; set; } = "1x2";
        public string ClipboardSize { get; set; } = "1x2";

        // Kullanıcının Oluşturduğu Özel Uygulama Widget'ları
        public List<CustomAppWidget> CustomWidgets { get; set; } = new();
    }

    public class CustomAppWidget
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Title { get; set; } = "Uygulama";
        public string Subtitle { get; set; } = "Hızlı Başlat";
        public string TargetPath { get; set; } = "";
        public string Arguments { get; set; } = "";
        public string IconType { get; set; } = "Fluent"; // "Emoji", "Monochrome", "Fluent", "Material", "Color"
        public string IconValue { get; set; } = "📄"; // Emoji, Harf/Sembol veya Glyph
        public string AccentColor { get; set; } = "#38BDF8"; // Widget vurgusu
        public string WidgetSize { get; set; } = "1x1"; // "1x1", "1x2", "1x3"
        public bool IsEnabled { get; set; } = true;
    }

    public class AppSettings
    {
        // Konum & Düzen
        public string DockPosition { get; set; } = "Bottom"; // "Bottom", "Top"
        public string DockAlignment { get; set; } = "Center"; // "Center", "Left", "Right"
        public string LayoutScenario { get; set; } = "Floating"; // "Floating", "Fixed", "Swap", "Reveal"
        public string DockStyle { get; set; } = "DarkGlass"; // "DarkGlass", "LightGlass", "Transparent", "Black", "LiquidGlass"
        public double DockScale { get; set; } = 1.0; // 0.8 - 1.25
        public int WidgetSpacing { get; set; } = 8; // 2 - 20
        public bool AlwaysOnTop { get; set; } = true;
        public bool AutoHide { get; set; } = false;
        public bool ReduceTransparency { get; set; } = false;
        public bool ShowTileBorders { get; set; } = true;
        public bool HideDuringFullscreen { get; set; } = true;

        // Profiller (OxiDock Profiles System)
        public string ActiveProfileId { get; set; } = "default-1";
        public List<DockProfile> Profiles { get; set; } = new()
        {
            new DockProfile { Id = "default-1", Name = "1. Standart", ShowClock = true, ShowWeather = true, ShowSystemStats = true, ShowApps = true, ShowMusic = true, ShowNotes = true, ShowPomodoro = true, ShowBattery = true },
            new DockProfile { Id = "default-2", Name = "2. İş & Geliştirici", ShowClock = true, ShowWeather = false, ShowSystemStats = true, ShowApps = true, ShowMusic = false, ShowNotes = true, ShowPomodoro = true, ShowBattery = false },
            new DockProfile { Id = "default-3", Name = "3. Odaklanma", ShowClock = true, ShowWeather = false, ShowSystemStats = false, ShowApps = false, ShowMusic = true, ShowNotes = true, ShowPomodoro = true, ShowBattery = false }
        };

        // Aktif Profile Yönlendirmeli Widget Görünürlükleri
        public bool ShowClock { get => GetActiveProfile().ShowClock; set => GetActiveProfile().ShowClock = value; }
        public bool ShowWeather { get => GetActiveProfile().ShowWeather; set => GetActiveProfile().ShowWeather = value; }
        public bool ShowSystemStats { get => GetActiveProfile().ShowSystemStats; set => GetActiveProfile().ShowSystemStats = value; }
        public bool ShowApps { get => GetActiveProfile().ShowApps; set => GetActiveProfile().ShowApps = value; }
        public bool ShowMusic { get => GetActiveProfile().ShowMusic; set => GetActiveProfile().ShowMusic = value; }
        public bool ShowNotes { get => GetActiveProfile().ShowNotes; set => GetActiveProfile().ShowNotes = value; }
        public bool ShowPomodoro { get => GetActiveProfile().ShowPomodoro; set => GetActiveProfile().ShowPomodoro = value; }
        public bool ShowBattery { get => GetActiveProfile().ShowBattery; set => GetActiveProfile().ShowBattery = value; }
        public bool ShowCustomAction { get => GetActiveProfile().ShowCustomAction; set => GetActiveProfile().ShowCustomAction = value; }
        public bool ShowNetwork { get => GetActiveProfile().ShowNetwork; set => GetActiveProfile().ShowNetwork = value; }
        public bool ShowCrypto { get => GetActiveProfile().ShowCrypto; set => GetActiveProfile().ShowCrypto = value; }
        public bool ShowCalendar { get => GetActiveProfile().ShowCalendar; set => GetActiveProfile().ShowCalendar = value; }
        public bool ShowQuickSearch { get => GetActiveProfile().ShowQuickSearch; set => GetActiveProfile().ShowQuickSearch = value; }
        public bool ShowVolume { get => GetActiveProfile().ShowVolume; set => GetActiveProfile().ShowVolume = value; }
        public bool ShowClipboard { get => GetActiveProfile().ShowClipboard; set => GetActiveProfile().ShowClipboard = value; }

        // Kişiselleştirilmiş Özel Araç / İşlem Ayarları
        public string CustomActionTitle { get; set; } = "Terminal";
        public string CustomActionPath { get; set; } = "wt.exe";
        public string CustomActionIcon { get; set; } = "⚡";
        public string CustomActionArgs { get; set; } = "";

        // Pomodoro & Wellness Ayarları
        public int PomodoroFocusMinutes { get; set; } = 25;
        public int PomodoroBreakMinutes { get; set; } = 5;
        public int PomodoroLongBreakMinutes { get; set; } = 15;
        public bool PostureReminderEnabled { get; set; } = true;
        public bool WaterReminderEnabled { get; set; } = true;

        // Hava Durumu & Saat
        public string WeatherCity { get; set; } = "İstanbul";
        public string ClockStyle { get; set; } = "Digital"; // "Digital", "World", "Analog"

        // Yerel AI Asistan (OxiDock MCP / Assistant API)
        public bool EnableAiAssistant { get; set; } = true;
        public int AiAssistantPort { get; set; } = 48199;
        public bool AllowAiChanges { get; set; } = true;

        // Aktif profil helper
        public DockProfile GetActiveProfile()
        {
            var p = Profiles.Find(x => x.Id == ActiveProfileId);
            if (p != null) return p;
            if (Profiles.Count > 0)
            {
                ActiveProfileId = Profiles[0].Id;
                return Profiles[0];
            }
            var def = new DockProfile { Id = "default-1", Name = "Standart" };
            Profiles.Add(def);
            ActiveProfileId = def.Id;
            return def;
        }

        /// <summary>
        /// Uygulama veri klasörü (%LocalAppData%\OxiDock). Eski "CoolDockWindows" klasörü varsa verileri bir kez taşır.
        /// </summary>
        public static string GetDataFolder()
        {
            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(baseDir, "OxiDock");
            var legacy = Path.Combine(baseDir, "CoolDock" + "Windows");
            try
            {
                if (!Directory.Exists(folder) && Directory.Exists(legacy))
                {
                    Directory.Move(legacy, folder);
                }
            }
            catch { }
            Directory.CreateDirectory(folder);
            return folder;
        }

        private static string GetConfigPath()
        {
            return Path.Combine(GetDataFolder(), "ayarlar.json");
        }

        public static AppSettings Load()
        {
            try
            {
                var path = GetConfigPath();
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        if (settings.Profiles == null || settings.Profiles.Count == 0)
                        {
                            settings.Profiles = new List<DockProfile>
                            {
                                new DockProfile { Id = "default-1", Name = "1. Standart" },
                                new DockProfile { Id = "default-2", Name = "2. İş & Geliştirici" },
                                new DockProfile { Id = "default-3", Name = "3. Odaklanma" }
                            };
                            settings.ActiveProfileId = "default-1";
                        }
                        foreach (var prof in settings.Profiles)
                        {
                            prof.CustomWidgets ??= new List<CustomAppWidget>();
                        }
                        return settings;
                    }
                }
            }
            catch { }
            return new AppSettings();
        }

        public void Save()
        {
            try
            {
                var path = GetConfigPath();
                var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
