# Muslim ON — Design System

Based on Vercel Geist guidelines, adapted for WinUI 3 desktop.

---

## Color System

All colors defined in `src/PrayerShutdown.UI/Theme/GeistColors.xaml` with Light & Dark theme variants.

### Backgrounds
| Token | Light | Dark | Usage |
|-------|-------|------|-------|
| `Background100` | `#FFFFFF` | `#0A0A0A` | App background |
| `Background200` | `#FAFAFA` | `#111111` | Card / elevated surface |
| `Background300` | `#F5F5F5` | `#1A1A1A` | Hover background |

### Gray Scale
| Token | Light | Dark | Usage |
|-------|-------|------|-------|
| `Gray100` | `#F2F2F2` | `#1A1A1A` | Component bg |
| `Gray200` | `#EBEBEB` | `#1F1F1F` | Component bg hover |
| `Gray300` | `#E6E6E6` | `#292929` | Component bg active |
| `Gray400` | `#EBEBEB` | `#2E2E2E` | Border default |
| `Gray500` | `#E0E0E0` | `#454545` | Border hover |
| `Gray600` | `#C9C9C9` | `#5E5E5E` | Tertiary text, icons |
| `Gray700` | `#8F8F8F` | `#6E6E6E` | Secondary text |
| `Gray800` | `#7D7D7D` | `#7C7C7C` | Button pressed |
| `Gray900` | `#666666` | `#A0A0A0` | Body text |
| `Gray1000` | `#171717` | `#EDEDED` | Headings, primary text |

### Semantic
| Token | Value | Usage |
|-------|-------|-------|
| `Error` | `#E5484D` | Danger, destructive actions |
| `Warning` | `#F5A623` | Countdown urgency 10-30 min |
| `Success` | `#45A557` | Passed prayers, confirmations |
| `Blue700` | `#0070F3` | Active state, links, focus ring |

### Usage Rules
- **Never hardcode hex values** in C# or XAML — always use `{ThemeResource GeistXxxBrush}`
- **Color is never the only indicator** — always pair with text labels
- **Interactions increase contrast** — hover/active/focus more prominent than rest

---

## Typography

Defined in `src/PrayerShutdown.UI/Theme/GeistTypography.xaml`.

| Style | Size | Weight | Usage |
|-------|------|--------|-------|
| `GeistHeading1Style` | 48px | Bold | - |
| `GeistHeading2Style` | 36px | SemiBold | Page hero text |
| `GeistHeading3Style` | 24px | SemiBold | Page titles |
| `GeistHeading4Style` | 20px | SemiBold | Section titles |
| `GeistHeading5Style` | 16px | SemiBold | Card headers |
| `GeistLabelDefaultStyle` | 14px | Medium | UI labels, nav items |
| `GeistLabelSmallStyle` | 13px | Medium | Badges, metadata |
| `GeistBodyDefaultStyle` | 14px | Normal | Body text (Gray900) |
| `GeistBodySmallStyle` | 13px | Normal | Descriptions (Gray700) |
| `GeistMonoLargeStyle` | 48px | Bold | Countdown timer |
| `GeistMonoDefaultStyle` | 14px | Normal | Prayer times, numbers |

### Fonts
- **Primary**: Segoe UI Variable (system), fallback Segoe UI
- **Monospace**: Cascadia Mono, fallback Consolas
- **Tabular numbers**: Use Mono font for time displays, countdowns, comparisons

---

## Spacing

4px base grid. Defined in `src/PrayerShutdown.UI/Theme/GeistSpacing.xaml`.

| Token | Value | Usage |
|-------|-------|-------|
| `Space1` | 4px | Tight internal spacing |
| `Space2` | 8px | Default element gap |
| `Space3` | 12px | Card internal padding |
| `Space4` | 16px | Section spacing |
| `Space6` | 24px | Page padding |
| `Space8` | 32px | Section gaps |

### Corner Radii
| Token | Value | Usage |
|-------|-------|-------|
| `RadiusSmall` | 4px | Badges, pills |
| `RadiusDefault` | 6px | Inputs |
| `RadiusMedium` | 8px | Buttons |
| `RadiusLarge` | 12px | Cards |
| `RadiusXLarge` | 16px | Hero sections |
| `RadiusFull` | 9999px | Status dots, pill badges |

---

## Component Patterns

### Buttons (4 variants)
All buttons use full `ControlTemplate` with `VisualStateManager`.

| Variant | Normal | Hover | Pressed | Usage |
|---------|--------|-------|---------|-------|
| **Primary** | Gray1000 bg, Background100 text | Gray900 bg | Gray800 bg | Main CTA |
| **Secondary** | Background100 bg, Gray400 border | Background300 bg | Gray200 bg | Secondary actions |
| **Tertiary** | Transparent | GrayAlpha100 bg | GrayAlpha400 bg | Ghost buttons |
| **Danger** | Error bg, white text | `#D13438` bg | `#B92A2E` bg | Destructive actions |

- **Disabled**: 40% opacity on all variants
- **Focus**: 2px Blue700 ring, -3px margin offset
- **Height**: 40px (Primary/Secondary/Danger), 36px (Tertiary)
- **Labels**: Specific — "Save Prayer Settings" not "Save"

### Cards
```xml
<Border Background="{ThemeResource GeistBackground200Brush}"
        CornerRadius="12" Padding="20"
        BorderBrush="{ThemeResource GeistGrayAlpha100Brush}"
        BorderThickness="1" />
```

### Status Badges
```xml
<Border CornerRadius="9999" Padding="8,3,8,3">
    <Border.Background>
        <SolidColorBrush Color="{StatusColor}" Opacity="0.12" />
    </Border.Background>
    <TextBlock Text="{StatusText}" FontSize="11" FontWeight="Medium" />
</Border>
```

### Empty States
Centered layout: Icon (48px, Gray600) → Heading (H4) → Description (BodyDefault, Gray700)

---

## Shadows

- **Hero cards**: `<ThemeShadow />` with `Translation="0,0,16"`
- **Elevated cards**: No shadow, use `GrayAlpha100Brush` border instead
- **Principle**: Layered shadows — ambient (broad, low opacity) + direct (tight, higher opacity)

---

## Animations

- **Page transitions**: `SlideNavigationTransitionInfo` (FromRight)
- **Countdown updates**: Instant (no animation), tabular mono font
- **State transitions**: 150-200ms, ease-out
- **Only animate**: Opacity, Transform (compositor-friendly)
- **Respect**: `UISettings.AnimationsEnabled` — skip when disabled

---

## Accessibility

- **Focus rings**: Blue700, 2px, on ALL interactive elements
- **Touch targets**: Minimum 36px height (buttons), recommended 40px
- **Color redundancy**: Status always has text label + color (never color alone)
- **Icon-only buttons**: Must have `ToolTip` and `AutomationProperties.Name`
- **Tab order**: Logical on every page

---

## Localization

- All user-facing strings via `Loc.S("key")` — never hardcode
- Available: English, Russian
- To add language: add one method in `LocalizationService.cs`
- Navigation labels update on language change via `LanguageChanged` event

---

## Copy Guidelines

- **Active voice**: "Save Prayer Settings" not "Settings will be saved"
- **Specific labels**: "Search city…" not "Search…"
- **Ellipsis**: Use `…` (Unicode) not `...` for placeholders and loading
- **Error messages**: Include fix — "Go to Settings to choose your city"
- **Positive framing**: "All prayers completed" not "No more prayers"
