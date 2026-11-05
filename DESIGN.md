# OxiDock Design System Specification
## Living Glass & Continuous Geometry (Inspired by Xiaomi HyperOS 3 & Huawei HarmonyOS 3)

- **Document Version**: 2.3.0
- **Target Application**: OxiDock (.NET 10 / C# 13 WPF with Windows 10/11 DWM Interop)
- **Status**: Canonical Design System Reference
- **Last Updated**: 2026-11-04

---

## 1. Executive Design Vision & Core Philosophy

OxiDock serves as a primary desktop companion on Windows 10 and 11—unifying glanceable metrics, media transport controls, quick application launching, workspace profiles, and local AI agent management.

The OxiDock Design System synthesizes the best modern mobile operating system aesthetics—specifically **Xiaomi HyperOS 3** and **Huawei HarmonyOS 3**—into an idiomatic, high-performance **WPF XAML Design Architecture**.

```
  ┌────────────────────────────────────────────────────────────────────────┐
  │                           THE 4 PILLARS                                │
  │                                                                        │
  │   ✦ DEPTH          Multi-layer optical hierarchy & recessed wells      │
  │   ✦ FLUIDITY       Continuous squircle curvature & physics easing      │
  │   ✦ GLASSMORPHISM  45° specular light refraction & frosted acrylic     │
  │   ✦ VIBRANCY       5 functional micro-accent palettes for contrast     │
  └────────────────────────────────────────────────────────────────────────┘
```

### The Four Pillars

1. **Optical Depth**: Eliminates flat, monotone slabs. Every card possesses physical substance: an ambient substrate, a refracting liquid glass fill, a recessed dark optical well, and resting ambient shadows.
2. **Fluid Geometry (Squircles)**: Moves away from rigid circular arcs. Adheres to organic continuous curvature (superellipse $n \approx 3.0-3.5$) with concentric corner radius scaling between parent cards and child containers.
3. **Glassmorphism (Liquid Glass)**: Optical materials that refract light. Edges capture directional light from a 45-degree angle, producing a bright top-left specular rim that softens toward the bottom-right shadow.
4. **Functional Vibrancy**: Replaces drab monochrome icons with 5 vivid dual-tone gradient palettes that convey instant semantic meaning (time, health, media, focus, finance).

---

## 2. Form, Geometry & Squircle Specifications

Standard WPF `Border.CornerRadius` uses simple circular arcs. In mobile operating systems (iOS, HyperOS 3, HarmonyOS 3), continuous curvature avoids abrupt tangent changes where edges meet curves.

### 2.1 The Squircle Ratio & Concentricity Rules

1. **Curvature Ratio**: Outer card corner radius is balanced to ~12–16% of total element dimensions ($18\text{px}$ radius on a $156\text{px}$ library card or $56\text{px}$ dock tile).
2. **Concentric Padding Formula**: When nesting a container or preview well inside a card, the inner radius must satisfy:
   $$R_{\text{inner}} = R_{\text{outer}} - \text{Padding}$$
   For an $18\text{px}$ outer card with $7\text{px}$ inner well offset: $R_{\text{inner}} = 18 - 7 = 11\text{px}$.
3. **Specular Softening**: Illuminating the top-left curve with higher luminance visually diffuses the curve, smoothing circular transitions into an organic squircle.

### 2.2 Corner Radius Token Hierarchy

| Token Name | WPF Value | Intended Usage | Example Element |
| :--- | :--- | :--- | :--- |
| `RadiusMasterDock` | `CornerRadius="26"` | Main floating dock border plate | `MainDockBorder` |
| `RadiusWindowFrame` | `CornerRadius="22"` | Top-level dialog and settings frames | `SettingsWindow` border |
| `RadiusDynamicIsland`| `CornerRadius="20"` | Centered status & notification pill | `DynamicIslandPill` |
| `RadiusCardMaster` | `CornerRadius="18"` | Standard widget cards in library | `WidgetCardStyle` |
| `RadiusCardDock` | `CornerRadius="16"` | Standard widget cards in dock bar | `WidgetContainerStyle` |
| `RadiusSettingCard` | `CornerRadius="16"` | Settings tab option cards | `SettingCardStyle` |
| `RadiusPreviewWell` | `CornerRadius="11"` | Recessed inner optical wells | `CardPreviewWellStyle` |
| `RadiusIconBadge` | `CornerRadius="10"` | Squircle icon backdrop boxes | `HyperIconBadgeStyle` |
| `RadiusStatusChip` | `CornerRadius="10"` | Status indicators ("✓ Aktif", "CANLI") | `HyperStatusChipStyle` |
| `RadiusSizePill` | `CornerRadius="8"` | Android-style size selectors (`1x1`) | `HyperSizePillStyle` |

---

## 3. Optical Layers & Material Physics

```
┌────────────────────────────────────────────────────────┐  ◄── Top Specular Highlight (#60FFFFFF / #8038BDF8)
│  ┌──────────┐                                          │
│  │ Icon Sq. │  Card Title               [ ✓ Aktif ]   │  ◄── Micro-Accent Status Chip
│  └──────────┘  Subtitle / Telemetry                   │
│ ────────────────────────────────────────────────────── │  ◄── Hairline Translucent Divider (#20FFFFFF)
│  ┌──────────────────────────────────────────────────┐  │
│  │   Live Metric / Recessed Optical Preview Well    │  │  ◄── Dark Frosted Well (#D90A0D14)
│  └──────────────────────────────────────────────────┘  │
│                                                        │
│  [ 1x1 ] [ 1x2 ] [ 1x3 ]                 [ Action ]    │  ◄── Size Pills & Action Button
└────────────────────────────────────────────────────────┘  ◄── Shadow Rim (#0DFFFFFF)
 └───────────────────────────────────────────────────────┘  ◄── Ambient Drop Shadow (Blur: 22, Depth: 5)
```

### Layer Breakdown

1. **Layer 1 — Base Translucent Substrate**:
   - High-performance transparent window foundation (`AllowsTransparency="True"`).
   - Semi-transparent dark/light crystal fill that lets the underlying Windows desktop blur through.
2. **Layer 2 — Liquid Glass Linear Gradient**:
   - Linear gradient fill from `#2C202B3E` (top luster) to `#20131B2A` (deep refraction).
   - On hover: shifts to `#3D2B3D58` $\to$ `#2A1A263B`.
3. **Layer 3 — 45° Specular Directional Border Rim**:
   - `BorderThickness="1.2"` with `LinearGradientBrush` (`StartPoint="0,0"`, `EndPoint="1,1"`).
   - Top-left light catch: `#60FFFFFF` (normal) or `#B338BDF8` (hover).
   - Bottom-right shadow fade: `#0DFFFFFF`.
4. **Layer 4 — Recessed Optical Dark Well**:
   - Recessed well (`Background="#D90A0D14"`, `BorderThickness="1"`, `BorderBrush="#1FFFFFFF"`).
   - Provides deep optical contrast for metrics, live text, and wave equalizers.
5. **Layer 5 — Ambient Shadow & Interaction Glow**:
   - Resting: `BlurRadius="22"`, `ShadowDepth="5"`, `Color="#000000"`, `Opacity="0.60"`.
   - Hover Elevation: `BlurRadius="30"`, `ShadowDepth="8"`, `Color="#38BDF8"`, `Opacity="0.32"`, paired with $1.02\times$ scale transform.

---

## 4. Color Token System & Theme Palettes

OxiDock supports 5 distinct global themes via dynamic resource resolution (`{DynamicResource ...}`), ensuring instant switching without application restarts:

### 4.1 Global Theme Surface Matrix

| UI Token Role | `DarkGlass` (Default) | `LiquidGlass` (Modern) | `LightGlass` (Quartz) | `Cyberpunk` (Neon) | `OledBlack` (Pure) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **Window Substrate** | `#F40D131F` | `#F40C1222` | `#F0F8FAFC` | `#F40E081C` | `#F6000000` |
| **Card Surface (Rest)** | `#20181E2E` | `#2C202B3E` | `#E6FFFFFF` | `#28140B28` | `#1A0A0A0A` |
| **Card Surface (Hover)**| `#3024344E` | `#3D2B3D58` | `#F5FFFFFF` | `#3D1B103D` | `#26141414` |
| **Specular Border (0,0)**| `#60FFFFFF` | `#8038BDF8` | `#A0FFFFFF` | `#B3C084FC` | `#40FFFFFF` |
| **Specular Border (1,1)**| `#0DFFFFFF` | `#1A38BDF8` | `#2094A3B8` | `#15EC4899` | `#08FFFFFF` |
| **Primary Text** | `#FFFFFF` | `#FFFFFF` | `#0F172A` | `#F8FAFC` | `#FFFFFF` |
| **Secondary Text** | `#94A3B8` | `#94A3B8` | `#475569` | `#C084FC` | `#71717A` |
| **Recessed Well** | `#D9080B12` | `#D90A0D14` | `#E2E8F0` | `#D907040D` | `#0D000000` |
| **Hover Ambient Glow** | `#4038BDF8` | `#5038BDF8` | `#300284C7` | `#60A855F7` | `#4038BDF8` |

### 4.2 The 5 Functional Micro-Accent Palettes

```
┌────────────────────────────────────────────────────────────────────────────────────────┐
│ 1. ⚡ Electric Sky     #38BDF8 ──► #06B6D4    Glow: #4038BDF8  Time / Clock / Primary  │
│ 2. 🌿 Emerald Mint     #34D399 ──► #10B981    Glow: #4010B981  Health / RAM / Status   │
│ 3. 🔮 Neon Violet      #C084FC ──► #8B5CF6    Glow: #408B5CF6  AI Asistan / Medya      │
│ 4. 🌅 Solar Coral      #FB7185 ──► #F97316    Glow: #40EF4444  Pomodoro / Isı / Alarm  │
│ 5. 🌕 Amber Gold       #FBBF24 ──► #F59E0B    Glow: #40F59E0B  Finans / Kripto / Güneş │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Electric Sky (Cyan 400 $\to$ 500)**: Time, Calendar, Network, and primary system actions.
- **Emerald Mint (Emerald 400 $\to$ 500)**: CPU / RAM under normal loads, "✓ Aktif" status pills, OxiShelf drop highlights.
- **Neon Violet (Purple 400 $\to$ Violet 500)**: AI Assistant (MCP server), GSMTC media playback, creative tool launchers.
- **Solar Coral (Rose 400 $\to$ Orange 500)**: Pomodoro focus sessions, high CPU warning, critical notifications.
- **Amber Gold (Amber 400 $\to$ 500)**: Binance crypto tickers, world time indicators, weather solar states.

---

## 5. Canonical WPF XAML Resource Definitions

The following XAML code defines the complete, production-ready HyperOS 3 & HarmonyOS 3 resource dictionary for OxiDock.

### 5.1 Color & Gradient Brushes

```xml
<!-- ======================================================== -->
<!-- HYPEROS 3 & HARMONYOS 3 MASTER RESOURCE DICTIONARY       -->
<!-- ======================================================== -->

<!-- Base Surfaces -->
<SolidColorBrush x:Key="HyperDarkSurfaceBase" Color="#0D111A"/>
<SolidColorBrush x:Key="HyperDarkCardBg" Color="#181E2E"/>
<SolidColorBrush x:Key="HyperDarkCardRecessed" Color="#0A0D14"/>

<!-- Liquid Glass Gradient Card Fill -->
<LinearGradientBrush x:Key="HyperLiquidCardBgBrush" StartPoint="0,0" EndPoint="0,1">
    <GradientStop Color="#2C202B3E" Offset="0.0"/>
    <GradientStop Color="#20131B2A" Offset="1.0"/>
</LinearGradientBrush>

<LinearGradientBrush x:Key="HyperLiquidCardBgHoverBrush" StartPoint="0,0" EndPoint="0,1">
    <GradientStop Color="#3D2B3D58" Offset="0.0"/>
    <GradientStop Color="#2A1A263B" Offset="1.0"/>
</LinearGradientBrush>

<!-- 45° Specular Directional Border Brushes -->
<LinearGradientBrush x:Key="HyperGlassBorderBrush" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#60FFFFFF" Offset="0.0"/>
    <GradientStop Color="#25FFFFFF" Offset="0.45"/>
    <GradientStop Color="#0DFFFFFF" Offset="1.0"/>
</LinearGradientBrush>

<LinearGradientBrush x:Key="HyperGlassBorderHoverBrush" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#B338BDF8" Offset="0.0"/>
    <GradientStop Color="#5538BDF8" Offset="0.40"/>
    <GradientStop Color="#1A38BDF8" Offset="1.0"/>
</LinearGradientBrush>

<!-- Micro-Accent Gradients -->
<LinearGradientBrush x:Key="HyperAccentCyanGradient" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#38BDF8" Offset="0.0"/>
    <GradientStop Color="#06B6D4" Offset="1.0"/>
</LinearGradientBrush>

<LinearGradientBrush x:Key="HyperAccentMintGradient" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#34D399" Offset="0.0"/>
    <GradientStop Color="#10B981" Offset="1.0"/>
</LinearGradientBrush>

<LinearGradientBrush x:Key="HyperAccentVioletGradient" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#C084FC" Offset="0.0"/>
    <GradientStop Color="#8B5CF6" Offset="1.0"/>
</LinearGradientBrush>

<LinearGradientBrush x:Key="HyperAccentCoralGradient" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#FB7185" Offset="0.0"/>
    <GradientStop Color="#F97316" Offset="1.0"/>
</LinearGradientBrush>

<LinearGradientBrush x:Key="HyperAccentGoldGradient" StartPoint="0,0" EndPoint="1,1">
    <GradientStop Color="#FBBF24" Offset="0.0"/>
    <GradientStop Color="#F59E0B" Offset="1.0"/>
</LinearGradientBrush>
```

### 5.2 Card & Container Styles

```xml
<!-- 1. Master Reusable Card Style -->
<Style x:Key="HyperCardStyle" TargetType="Border">
    <Setter Property="Background" Value="{StaticResource HyperLiquidCardBgBrush}"/>
    <Setter Property="BorderBrush" Value="{StaticResource HyperGlassBorderBrush}"/>
    <Setter Property="BorderThickness" Value="1.2"/>
    <Setter Property="CornerRadius" Value="18"/>
    <Setter Property="Padding" Value="14,12"/>
    <Setter Property="Margin" Value="6"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="UseLayoutRounding" Value="True"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="RenderTransformOrigin" Value="0.5,0.5"/>
    <Setter Property="RenderTransform">
        <Setter.Value>
            <ScaleTransform ScaleX="1.0" ScaleY="1.0"/>
        </Setter.Value>
    </Setter>
    <Setter Property="Effect">
        <Setter.Value>
            <DropShadowEffect BlurRadius="22" ShadowDepth="5" Direction="270" 
                              Color="#000000" Opacity="0.60" RenderingBias="Performance"/>
        </Setter.Value>
    </Setter>
    <Style.Triggers>
        <Trigger Property="IsMouseOver" Value="True">
            <Setter Property="Background" Value="{StaticResource HyperLiquidCardBgHoverBrush}"/>
            <Setter Property="BorderBrush" Value="{StaticResource HyperGlassBorderHoverBrush}"/>
            <Setter Property="Effect">
                <Setter.Value>
                    <DropShadowEffect BlurRadius="30" ShadowDepth="8" Direction="270" 
                                      Color="#38BDF8" Opacity="0.32" RenderingBias="Performance"/>
                </Setter.Value>
            </Setter>
        </Trigger>
    </Style.Triggers>
</Style>

<!-- 2. Animated Card Style with Spring Elevation Storyboards -->
<Style x:Key="HyperAnimatedCardStyle" TargetType="Border" BasedOn="{StaticResource HyperCardStyle}">
    <Style.Triggers>
        <EventTrigger RoutedEvent="MouseEnter">
            <BeginStoryboard>
                <Storyboard>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                     To="1.02" Duration="0:0:0.18">
                        <DoubleAnimation.EasingFunction>
                            <CubicEase EasingMode="EaseOut"/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                                     To="1.02" Duration="0:0:0.18">
                        <DoubleAnimation.EasingFunction>
                            <CubicEase EasingMode="EaseOut"/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.ShadowDepth)"
                                     To="8" Duration="0:0:0.18"/>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)"
                                     To="30" Duration="0:0:0.18"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
        <EventTrigger RoutedEvent="MouseLeave">
            <BeginStoryboard>
                <Storyboard>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleX)"
                                     To="1.00" Duration="0:0:0.22">
                        <DoubleAnimation.EasingFunction>
                            <CubicEase EasingMode="EaseOut"/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.RenderTransform).(ScaleTransform.ScaleY)"
                                     To="1.00" Duration="0:0:0.22">
                        <DoubleAnimation.EasingFunction>
                            <CubicEase EasingMode="EaseOut"/>
                        </DoubleAnimation.EasingFunction>
                    </DoubleAnimation>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.ShadowDepth)"
                                     To="5" Duration="0:0:0.22"/>
                    <DoubleAnimation Storyboard.TargetProperty="(UIElement.Effect).(DropShadowEffect.BlurRadius)"
                                     To="22" Duration="0:0:0.22"/>
                </Storyboard>
            </BeginStoryboard>
        </EventTrigger>
    </Style.Triggers>
</Style>

<!-- 3. Recessed Optical Preview Well -->
<Style x:Key="HyperCardPreviewWellStyle" TargetType="Border">
    <Setter Property="Background" Value="#D90A0D14"/>
    <Setter Property="BorderBrush" Value="#1FFFFFFF"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="11"/>
    <Setter Property="Margin" Value="0,8"/>
    <Setter Property="Padding" Value="10,8"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
</Style>

<!-- 4. Squircle Icon Backdrop Badge -->
<Style x:Key="HyperIconBadgeStyle" TargetType="Border">
    <Setter Property="Width" Value="32"/>
    <Setter Property="Height" Value="32"/>
    <Setter Property="CornerRadius" Value="10"/>
    <Setter Property="Background" Value="#2238BDF8"/>
    <Setter Property="BorderBrush" Value="#4438BDF8"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="SnapsToDevicePixels" Value="True"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
    <Setter Property="HorizontalAlignment" Value="Center"/>
</Style>

<!-- 5. Status Indicator Chip / Pill -->
<Style x:Key="HyperStatusChipStyle" TargetType="Border">
    <Setter Property="CornerRadius" Value="10"/>
    <Setter Property="Padding" Value="8,2.5"/>
    <Setter Property="Background" Value="#2034D399"/>
    <Setter Property="BorderBrush" Value="#4434D399"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="VerticalAlignment" Value="Center"/>
</Style>

<!-- 6. Multi-Size Selector Pill Button -->
<Style x:Key="HyperSizePillStyle" TargetType="Button">
    <Setter Property="Background" Value="#18FFFFFF"/>
    <Setter Property="Foreground" Value="#94A3B8"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="BorderBrush" Value="#25FFFFFF"/>
    <Setter Property="Padding" Value="8,3"/>
    <Setter Property="Margin" Value="2,0"/>
    <Setter Property="FontSize" Value="9.5"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="pillBorder" Background="{TemplateBinding Background}" 
                        BorderBrush="{TemplateBinding BorderBrush}" 
                        BorderThickness="{TemplateBinding BorderThickness}" 
                        CornerRadius="8" Padding="{TemplateBinding Padding}"
                        SnapsToDevicePixels="True">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="pillBorder" Property="Background" Value="#3038BDF8"/>
                        <Setter TargetName="pillBorder" Property="BorderBrush" Value="#6638BDF8"/>
                        <Setter Property="Foreground" Value="#38BDF8"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="pillBorder" Property="Background" Value="#5538BDF8"/>
                        <Setter Property="Foreground" Value="#FFFFFF"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## 6. Implementation Blueprints across OxiDock Windows

### 6.1 `WidgetBrowserWindow.xaml` (3-Column Catalog Overhaul)
- Apply `HyperAnimatedCardStyle` to each widget card container (`CardClock`, `CardWeather`, `CardSystem`, `CardMusic`, etc.).
- Structure each card into HarmonyOS 3-tier layout:
  - **Tier 1 (Header)**: Squircle icon with micro-accent tint, dual-line title/subtitle, status chip.
  - **Tier 2 (Body)**: Recessed dark well (`HyperCardPreviewWellStyle`) with live gauge/metric numbers.
  - **Tier 3 (Footer)**: Size selector pills (`1x1`, `1x2`, `1x3`) styled with `HyperSizePillStyle`.
- Keep all existing drag-and-drop mouse events intact (`PreviewMouseLeftButtonDown`, `PreviewMouseMove`, `MouseLeftButtonUp`).

### 6.2 `SettingsWindow.xaml` (Preferences & Theme Cards)
- Update `SettingCardStyle` to use `CornerRadius="16"`, `HyperLiquidCardBgBrush`, and `HyperGlassBorderBrush`.
- Update `ThemePreviewCardStyle` to include squircle previews and specular rims that glow when selected.
- Update category tab navigation with squircle selection indicators (`CornerRadius="12"`).

### 6.3 `MainWindow.xaml` (Floating Dock & Dynamic Island)
- **`MainDockBorder`**: Master squircle pill with `CornerRadius="26"`, `BorderThickness="1.2"`, and `HyperGlassBorderBrush`.
- **`WidgetContainerStyle`**: Each dock widget container receives `CornerRadius="16"`, `BorderThickness="1.2"`, and `HyperGlassBorderBrush`.
- **`DynamicIslandPill`**: Floating status pill with `CornerRadius="20"`, specular rim, and pulsating mint indicator dot.
- **`FileShelfWidget` (OxiShelf)**: Liquid glass staging container with dashed border and drag-over highlight.

---

## 7. Performance & High-DPI Display Guidelines

1. **Pixel Snapping**: Every border and container must specify `SnapsToDevicePixels="True"` and `UseLayoutRounding="True"` to avoid fractional pixel blur on high-DPI Windows displays (125%, 150%, 200%).
2. **GPU Composition**: `DropShadowEffect` uses `RenderingBias="Performance"` to ensure smooth 60fps animations without CPU burden.
3. **No Redundant Overlays**: Avoid stacking multiple opaque borders. Rely on the 45° specular gradient to deliver edge definition and light reflection in a single render pass.
4. **Theme Binding**: Dynamic theme brushes must always be referenced via `{DynamicResource ...}` to support immediate runtime theme switching.
