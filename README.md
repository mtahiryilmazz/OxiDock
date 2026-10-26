<div align="center">

# OxiDock

**Windows için modern, canlı widget'lı ve "Liquid Glass" görünümlü masaüstü dock'u**

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?logo=windows)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![UI](https://img.shields.io/badge/UI-WPF-5C2D91)
![Version](https://img.shields.io/badge/version-2.0.0-22c55e)
![License](https://img.shields.io/badge/license-MIT-blue)

</div>

---

OxiDock; saat, hava durumu, sistem istatistikleri, müzik kontrolü, Pomodoro ve daha fazlasını tek bir şık, yarı saydam çubukta toplayan bir Windows masaüstü uygulamasıdır. Widget'lar profillere göre düzenlenebilir, sürükle-bırak ile eklenebilir ve yerel bir AI asistan API'si üzerinden kontrol edilebilir.

## ✨ Özellikler

### 🧩 Canlı Widget'lar
| Widget | Açıklama |
|---|---|
| 🕒 **Saat** | Dijital, Retro Flip, Analog ve Dünya Saati stilleri |
| 🌤️ **Hava Durumu** | Şehir adına göre canlı hava durumu ([Open-Meteo](https://open-meteo.com), API anahtarı gerekmez) |
| 📊 **Sistem** | Canlı CPU / RAM kullanımı |
| 🔋 **Batarya** | Şarj seviyesi ve durumu |
| 🎵 **Müzik** | Windows medya oturumu (GSMTC) ile çalan parça, oynat/duraklat, ileri/geri — Spotify, tarayıcı vb. ile çalışır |
| 🍅 **Pomodoro** | 25-5 odak/mola döngüsü, tur takibi, ilerleme çubuğu, postür ve su hatırlatıcıları |
| 📝 **Hızlı Notlar** | Otomatik kaydedilen not alanı |
| 🚀 **Uygulamalar** | Sık kullanılan Windows uygulamalarını hızlı başlatma |
| ⚡ **Özel Araç** | Kendi komutunuzu/uygulamanızı (ör. Windows Terminal) tek tıkla çalıştırın |
| 🌐 **Ağ** | Ping ve bağlantı durumu |
| ₿ **Kripto** | BTC / ETH / SOL anlık fiyatları (Binance) |
| 📅 **Takvim** | Tarih görünümü ve Google Takvim kısayolu |
| 🔎 **Hızlı Arama** | Google, ChatGPT veya Perplexity'de doğrudan arama |
| 🔊 **Ses** | Sistem sesini kapatma/açma (mute) ve ses kaydırıcısı |
| 📋 **Pano** | Son kopyalanan metnin önizlemesi |

### 🎨 Görünüm ve Yerleşim
- **Stiller:** Dark Glass, Light Glass, Liquid Glass, Transparent, Black
- **Konum:** Alt / Üst, Ortalı / Sol / Sağ hizalama
- **Yerleşim senaryoları:** Floating, Fixed, Swap, Reveal
- Ölçek (0.8× – 1.25×), widget aralığı, otomatik gizleme, her zaman üstte, tam ekranda gizleme
- Android tarzı widget boyutları: `1x1`, `1x2`, `1x3`

### 🗂️ Profiller (Çalışma Alanları)
Her profil kendi widget setini ve boyutlarını saklar. Varsayılan profiller: **Standart**, **İş & Geliştirici**, **Odaklanma**. Profil ekleme, yeniden adlandırma ve silme desteklenir.

### 📚 Widget Kütüphanesi
Kategorilere ayrılmış widget tarayıcısı; kartları dock'a **sürükle-bırak** ile ekleyin, boyutlarını tek tıkla değiştirin.

### 🤖 Yerel AI Asistan API'si (MCP uyumlu)
OxiDock, yalnızca `127.0.0.1` üzerinde dinleyen hafif bir HTTP sunucusu barındırır (varsayılan port **48199**). Claude, Cursor, Antigravity gibi yerel AI istemcileri veya kendi betikleriniz dock'u inceleyip kontrol edebilir.

| Metot | Uç Nokta | Açıklama |
|---|---|---|
| `GET` | `/api/status` | Aktif profil, ayarlar, Pomodoro durumu, CPU/RAM |
| `GET` | `/mcp` | MCP araç tanımları (`inspect_dock`, `switch_profile`, `toggle_widget`, `control_pomodoro`) |
| `POST` | `/api/profile` | Profil değiştir — `{ "profileId": "default-2" }` |
| `POST` | `/api/widget/toggle` | Widget aç/kapat — `{ "widget": "weather" }` |
| `POST` | `/api/pomodoro` | Pomodoro'yu başlat / duraklat |

```powershell
# Örnek: durum sorgulama ve hava durumu widget'ını açıp kapatma
Invoke-RestMethod http://127.0.0.1:48199/api/status
Invoke-RestMethod http://127.0.0.1:48199/api/widget/toggle -Method Post -Body '{"widget":"weather"}' -ContentType 'application/json'
```

> [!NOTE]
> AI asistan sunucusu Ayarlar → **AI Asistan** bölümünden kapatılabilir ve portu değiştirilebilir.

## 📦 Kurulum

### Gereksinimler
- Windows 10 (19041 / 2004) veya Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Kaynaktan derleme ve çalıştırma
```powershell
git clone https://github.com/mtahiryilmazz/OxiDock.git
cd OxiDock
dotnet run -c Release
```

### Tek dosyalık çalıştırılabilir dosya oluşturma
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```
Çıktı: `publish\OxiDock.exe`

## ⚙️ Kullanım
- **Dock'a sağ tık** → Widget Kütüphanesi, profil değiştirme, hızlı tema, Pomodoro kontrolü, Ayarlar
- **Çalışma alanı sekmesine sağ tık** → Yeniden adlandır, klonla, yeni ekle, sil
- **Sistem tepsisi simgesi** → Çift tık ile göster/gizle; sağ tık menüsü
- **Özel Araç widget'ına sağ tık** → Çalıştırılacak uygulamayı/komutu yapılandırın
- Widget Kütüphanesi'nden kartları dock'a **sürükleyip bırakın**, boyutları kart üzerindeki `1x1 / 1x2 / 1x3` seçicileriyle değiştirin

## 💾 Veri Konumu
Ayarlar ve notlar şu klasörde saklanır:
```
%LocalAppData%\OxiDock\
├── ayarlar.json   # Tüm ayarlar ve profiller
└── notlar.txt     # Hızlı notlar
```
> Önceki sürümden (`CoolDockWindows`) yükseltiyorsanız, verileriniz ilk açılışta otomatik olarak yeni klasöre taşınır.

## 🗂️ Proje Yapısı
```
OxiDock/
├── App.xaml(.cs)                  # Uygulama giriş noktası
├── MainWindow.xaml(.cs)           # Dock penceresi ve tüm widget mantığı
├── SettingsWindow.xaml(.cs)       # Ayarlar penceresi
├── WidgetBrowserWindow.xaml(.cs)  # Widget kütüphanesi (sürükle-bırak)
├── AppSettings.cs                 # Ayar/profil modeli ve kalıcılık
├── Services/
│   └── LocalAiAssistantServer.cs  # Yerel AI asistan HTTP/MCP sunucusu
└── OxiDock.csproj
```

## 🛠️ Teknolojiler
- **C# / .NET 10**, **WPF** (+ Windows Forms tepsi simgesi)
- **Windows Runtime API'leri** — `GlobalSystemMediaTransportControls` (medya kontrolü)
- **Open-Meteo** (hava durumu), **Binance Public API** (kripto fiyatları)
- `HttpListener` tabanlı yerel REST / MCP sunucusu

## 🤝 Katkıda Bulunma
1. Depoyu fork'layın
2. Yeni bir dal açın: `git checkout -b ozellik/yeni-widget`
3. Değişikliklerinizi commit'leyin: `git commit -m "feat: yeni widget eklendi"`
4. Dalınızı push'layın ve bir Pull Request açın

## 📄 Lisans
Bu proje [MIT Lisansı](LICENSE) ile lisanslanmıştır.
