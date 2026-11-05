<div align="center">

# OxiDock

**Windows için modern, canlı widget'lı ve "Liquid Glass" görünümlü masaüstü dock'u**

![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078D4?logo=windows)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)
![UI](https://img.shields.io/badge/UI-WPF-5C2D91)
![Version](https://img.shields.io/badge/version-2.3.0-22c55e)
![License](https://img.shields.io/badge/license-MIT-blue)
![Design](https://img.shields.io/badge/Design-HyperOS%203%20%7C%20HarmonyOS%203-ff69b4)

</div>

---

OxiDock; saat, hava durumu, sistem istatistikleri, medya kontrolü, Pomodoro odak sayacı, kripto para takibi ve hızlı uygulama fırlatıcılarını tek bir şık, yarı saydam çubukta toplayan modern bir Windows masaüstü uygulamasıdır. Widget'lar profillere göre düzenlenebilir, zengin 3 sütunlu kütüphaneden sürükle-bırak ile eklenebilir, Android boyutlandırma kurallarıyla (`1x1`, `1x2`, `1x3`) yapılandırılabilir ve yerel AI asistan HTTP/MCP API'si (port **48199**) üzerinden yönetilebilir.

---

## 🎨 HyperOS 3 & HarmonyOS 3 Tasarım Dili ve Kart Mimarisi

OxiDock v2.3.0, Xiaomi **HyperOS 3** ve Huawei **HarmonyOS 3** işletim sistemlerinin modern kart mimarisi ve sıvı cam (Liquid Glass) estetiğini Windows masaüstüne taşır:

- **Squircle (Süper Elips) Sürekli Geometrisi**: Klasik dairesel köşe yaylarının ötesinde, organik eğrilik sürekliliği sağlayan squircle form faktörü benimsenmiştir. Standart kartlar için `CornerRadius="14px–20px"`, dock ana gövdesi için `CornerRadius="24px–28px"` kullanılır. İç ve dış köşe yarıçapları $R_{\text{iç}} = R_{\text{dış}} - \text{Padding}$ kuralıyla optik uyum içinde dengelenir.
- **Speküler Doğrusal Gradyan Kenarlıklar (45° Işık Kırılması)**: Kart sınırları, sol üstten sağ alta uzanan 45 derecelik speküler `LinearGradientBrush` kenarlıkla (`BorderThickness="1.2"`) aydınlatılır (`#60FFFFFF` veya `#B338BDF8` üst ışık vurgusundan, alt kenarda ortam gölgesine gömülen `#0DFFFFFF` değerine geçiş).
- **Çok Katmanlı Buzlu Akrilik Cam Efekti (Liquid Glassmorphism)**: Saydam alt tabaka üzerinde derinlik hissi veren çoklu duraklı akrilik gradyanlar (`#2C202B3E` $\to$ `#20131B2A`), kart içi gömülü koyu önizleme havuzları (`#D90A0D14`) ve Windows masaüstü arka planı ile bütünleşen optik kırılma simülasyonu.
- **5 Fonksiyonel Canlı Mikro-Vurgu Paleti**:
  1. ⚡ **Electric Sky / HyperOS Cyan** (`#38BDF8` $\to$ `#06B6D4`): Zamanlayıcı, saat ve birincil sistem bileşenleri.
  2. 🌿 **Emerald Mint / Canlılık Yeşili** (`#34D399` $\to$ `#10B981`): Donanım sağlığı, normal yük göstergeleri ve "✓ Aktif" durum kapsülleri.
  3. 🔮 **Neon Violet / Royal Amethyst** (`#C084FC` $\to$ `#8B5CF6`): Yerel AI asistanı (MCP), medya oynatıcı ve yaratıcı araçlar.
  4. 🌅 **Solar Coral / Sunset Glow** (`#FB7185` $\to$ `#F97316` / `#EF4444`): Pomodoro odak döngüleri ve kritik sistem uyarıları.
  5. 🌕 **Amber Gold** (`#FBBF24` $\to$ `#F59E0B`): Kripto para ve finansal göstergeler, dünya saatleri.
- **Dinamik Etkileşim Yükseltmeleri ve Ortam Işıması**: Fare üzerine geldiğinde (hover) akıcı `CubicEase` yumuşatma ile $1.02\times$ mikro ölçeklenme, derinleşen gölge mesafesi (`BlurRadius="22"` $\to$ `"30"`) ve seçili tema renginde reaktif çevre ışıması (ambient glow).

Ayrıntılı tasarım sistemi spesifikasyonu ve XAML kaynakları için [DESIGN.md](DESIGN.md) ve [replica/design/components.md](replica/design/components.md) belgelerine göz atabilirsiniz.

---

## ✨ Özellikler

### 🧩 Canlı Widget'lar
| Widget | Açıklama |
|---|---|
| 🕒 **Saat & Dünya Saatleri** | Dijital, Retro Flip, Analog ve Dünya Saati stilleri |
| 🌤️ **Hava Durumu** | Şehir adına göre canlı hava durumu ([Open-Meteo](https://open-meteo.com), API anahtarı gerekmez) |
| 📊 **Sistem İstatistikleri** | Canlı CPU ve RAM kullanımı, gerçek zamanlı performans barları |
| 🔋 **Batarya & Güç** | Şarj seviyesi, güç kaynağı ve şarj olma durumu |
| 🎵 **Medya Kontrolü** | Windows GSMTC (`GlobalSystemMediaTransportControls`) ile Spotify, YouTube ve tarayıcı oturumlarını doğrudan kontrol etme |
| 🍅 **Pomodoro Odak Sayacı** | 25-5 odak/mola döngüsü, tur takibi, dairesel ilerleme, postür ve su hatırlatıcıları |
| 📝 **Hızlı Notlar** | Otomatik kaydedilen, minimalist not alanı |
| 🚀 **Uygulamalar** | Sık kullanılan Windows uygulamalarını tek tıkla başlatma |
| ⚡ **Özel Araç** | Kendi komutunuzu veya uygulamanızı (ör. Windows Terminal, PowerShell) tek tıkla çalıştırma |
| 🧩 **Kendi Uygulama Widget'larınızı Ekleyin** | Word, Excel, VS Code veya dilediğiniz uygulamayı özel başlık, simge stili (Fluent, Siyah-Beyaz, Material, Renkli, NativeExe), vurgu rengi ve Android boyutuyla (`1x1`, `1x2`, `1x3`) dock'a ekleyin |
| 🌐 **Ağ Monitörü** | Canlı ping, gecikme ve bağlantı durumu |
| ₿ **Kripto Göstergesi** | BTC / ETH / SOL anlık fiyatları ve değişim oranları (Binance API) |
| 📅 **Takvim** | Tarih görünümü, haftanın günü ve Google Takvim hızlı kısayolu |
| 🔎 **Hızlı Arama** | Google, ChatGPT veya Perplexity'de doğrudan arama çubuğu |
| 🔊 **Ses Düzeyi** | Sistem sesini kapatma/açma (mute) ve ses kaydırıcısı |
| 📋 **Pano Geçmişi** | Son kopyalanan metnin güvenli önizlemesi |
| 📁 **OxiShelf (Dosya Rafı)** | Masaüstü ve uygulamalar arasında dosya sürükleyip bırakarak geçici depolama ve hızlı aktarım alanı |

### 🎨 Görünüm ve Yerleşim
- **Temalar:** Dark Glass, Light Glass, Liquid Glass, Cyberpunk Neon, Oled Black
- **Konum:** Alt / Üst, Ortalı / Sol / Sağ hizalama seçenekleri
- **Yerleşim Senaryoları:** Floating (Yüzen), Fixed (Sabit), Swap (Yer Değiştiren), Reveal (Gizlenip Açılan)
- **Ölçekleme ve Aralık:** Ölçek (0.8× – 1.25×), widget aralığı (2–20px), otomatik gizleme, her zaman üstte kalma, tam ekranda gizlenme
- **Android Tarzı Boyutlandırma:** `1x1` (Kare / Minimal), `1x2` (Standart / Çift Hücre), `1x3` (Genişletilmiş / Çoklu Bilgi)

### 🗂️ Profiller (Çalışma Alanları)
Her profil bağımsız bir widget dizilimini, özel uygulama widget'larını ve boyutları saklar. Varsayılan profiller:
- **1. Standart**: Günlük masaüstü kullanımı (Saat, Hava Durumu, Sistem, Müzik, Notlar, Pomodoro).
- **2. İş & Geliştirici**: Kodlama ve teknik çalışma odaklı düzen.
- **3. Odaklanma**: Minimalist, dikkat dağıtmayan bileşenler.
Profil ekleme, klonlama, yeniden adlandırma ve silme tam olarak desteklenir.

### 📚 Widget Kütüphanesi (3x Sütun & Zengin Önizlemeler)
Windows 11 ve HyperOS / HarmonyOS estetiğinde dikey kaydırmalı 3 sütunlu (`UniformGrid Columns="3"`) kütüphane paneli. Her widget için gerçek zamanlı kart içi önizleme havuzları, anlık boyut değiştirme hapları (`1x1`, `1x2`, `1x3`) ve doğrudan dock'a **sürükle-bırak** desteği sunar. Ayrıca kütüphane üzerinden tek tıkla yeni özel uygulama widget'ı tanımlanabilir.

---

## 🤖 Yerel AI Asistan Sunucusu & MCP Entegrasyonu (Port 48199)

OxiDock, yerel makinede yalnızca `127.0.0.1` adresini dinleyen hafif, asenkron bir HTTP ve Model Context Protocol (MCP) sunucusu barındırır. Bu arayüz sayesinde yerel AI asistanları (**OxiBot**, Claude Desktop, Cursor, Antigravity) veya harici betikler dock durumunu okuyabilir ve kontrol edebilir.

### API Uç Noktaları

| Metot | Uç Nokta | Açıklama | Örnek İstek Gövdesi |
|---|---|---|---|
| `GET` | `/api/status` veya `/status` | Aktif profil, widget listesi, Pomodoro durumu, CPU/RAM | - |
| `GET` | `/mcp` | MCP araç tanımları (`inspect_dock`, `switch_profile`, `toggle_widget`, `control_pomodoro`) | - |
| `POST` | `/api/profile` | Aktif çalışma alanı profilini değiştirir | `{"profileId": "default-2"}` |
| `POST` | `/api/widget/toggle` | Belirtilen widget'ı açar veya kapatır | `{"widget": "weather"}` |
| `POST` | `/api/pomodoro` | Pomodoro sayacını başlatır, durdurur veya sıfırlar | `{"action": "start"}` |

### PowerShell ile Hızlı Test

```powershell
# 1. Dock durumunu sorgulama
Invoke-RestMethod http://127.0.0.1:48199/api/status

# 2. Hava durumu widget'ını açıp kapatma
Invoke-RestMethod http://127.0.0.1:48199/api/widget/toggle `
    -Method Post `
    -Body '{"widget":"weather"}' `
    -ContentType 'application/json'

# 3. Odaklanma profiline geçiş yapma
Invoke-RestMethod http://127.0.0.1:48199/api/profile `
    -Method Post `
    -Body '{"profileId":"default-3"}' `
    -ContentType 'application/json'
```

> [!NOTE]
> AI Asistan sunucusu **Ayarlar → AI Asistan** sekmesinden kolayca etkinleştirilebilir/devre dışı bırakılabilir ve dinleme portu değiştirilebilir. Güvenlik gereği yalnızca yerel geri döngü (`localhost` / `127.0.0.1`) istekleri kabul edilir; CORS başlıkları yerel araçlarla uyumludur.

---

## 📦 Kurulum ve Çalıştırma

### Gereksinimler
- Windows 10 (19041 / 2004 veya üzeri) veya Windows 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) (`net10.0-windows10.0.19041.0`)

### Kaynaktan Derleme ve Çalıştırma
```powershell
git clone https://github.com/mtahiryilmazz/OxiDock.git
cd OxiDock
dotnet build -c Release
dotnet run -c Release
```

### Tek Dosyalık (Single-File) Dağıtım
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```
Çıktı yürütülebilir dosyası: `publish\OxiDock.exe`

---

## ⚙️ Kullanım İpuçları
- **Dock Üzerine Sağ Tık** → Widget Kütüphanesi, profil değiştirme menüsü, hızlı tema seçici, Pomodoro kontrolleri, Genel Ayarlar.
- **Çalışma Alanı Sekmesine Sağ Tık** → Yeniden adlandır, klonla, yeni profil oluştur veya sil.
- **Sistem Tepsisi (Tray) Simgesi** → Çift tıklama ile dock'u anında göster/gizle; sağ tık ile hızlı menüye eriş.
- **Özel Araç Widget'ına Sağ Tık** → Çalıştırılacak uygulamayı, parametreleri ve simgeyi yapılandır.
- **Widget Kütüphanesi** → Kartları dock'a **sürükleyip bırakın**; kart üzerindeki `1x1 / 1x2 / 1x3` butonlarıyla anında boyut değiştirin.

---

## 💾 Veri Konumu
OxiDock, ayarları ve kullanıcı notlarını standart Windows yerel uygulama veri dizininde saklar:
```
%LocalAppData%\OxiDock\
├── ayarlar.json   # Tüm genel tercihler, profiller ve özel widget modelleri
└── notlar.txt     # Hızlı notlar widget verisi
```
> OxiDock, yapılandırma ve profil verilerini taşınabilir JSON biçiminde saklar; önceki sürümlerden gelen yapılandırmalar otomatik olarak algılanıp kesintisiz bir biçimde yeni biçime yükseltilir.

---

## 🗂️ Proje Yapısı

```
OxiDock/
├── App.xaml(.cs)                  # Uygulama giriş noktası ve global HyperOS 3 / HarmonyOS 3 kaynakları
├── MainWindow.xaml(.cs)           # Ana dock penceresi, dinamik ada ve widget mimarisi
├── SettingsWindow.xaml(.cs)       # Çok sekmeli ayarlar ve tema önizleme kartları
├── WidgetBrowserWindow.xaml(.cs)  # 3 sütunlu Widget Kütüphanesi ve sürükle-bırak motoru
├── AppSettings.cs                 # Ayar, profil ve özel widget veri modelleri
├── DESIGN.md                      # OxiDock kanonik tasarım sistemi ve XAML spesifikasyonu
├── Services/
│   └── LocalAiAssistantServer.cs  # Yerel AI asistan HTTP/MCP sunucusu (Port 48199)
├── replica/                       # Temiz oda tersine mühendislik haritası ve tasarım token'ları
│   ├── recon.md                   # .NET 10 / WPF mimari rekonstrüksiyon haritası
│   ├── features.csv               # Fonksiyon ve yetenek matrisi
│   └── design/
│       ├── components.md          # Living Island, Super Tile ve HyperCard bileşen kılavuzu
│       └── tokens.json            # Tasarım token'ları (renkler, tipografi, eğrilik)
├── renders/                       # Logo konseptleri ve render varlıkları
└── OxiDock.csproj                 # .NET 10 SDK proje tanımlayıcısı
```

---

## 🛠️ Teknolojiler
- **C# 13 / .NET 10**, **WPF** (Windows Presentation Foundation) + Windows Forms tepsi entegrasyonu
- **Windows Runtime API'leri** — `GlobalSystemMediaTransportControls` (medya oturumu entegrasyonu)
- **Open-Meteo REST API** (hava durumu), **Binance Public API** (kripto para fiyatları)
- **Asenkron `HttpListener`** tabanlı yerel REST ve MCP sunucusu

---

## 🤝 Katkıda Bulunma
1. Depoyu forklayın (`fork`)
2. Yeni bir özellik dalı açın: `git checkout -b ozellik/yeni-widget`
3. Değişikliklerinizi commit'leyin: `git commit -m "feat: yeni kart tasarımı ve widget eklendi"`
4. Dalınızı uzak depoya gönderin: `git push origin ozellik/yeni-widget`
5. Bir Pull Request açın

---

## 📄 Lisans
Bu proje [MIT Lisansı](LICENSE) ile lisanslanmıştır.
