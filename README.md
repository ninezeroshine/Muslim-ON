# Muslim ON

Prayer times, reminders & PC shutdown for Muslims. WinUI 3 desktop app for Windows 10/11.

---

## Quick Start

```bash
# Build
dotnet build src/PrayerShutdown.UI/PrayerShutdown.UI.csproj -p:Platform=x64

# Run
./src/PrayerShutdown.UI/bin/x64/Debug/net8.0-windows10.0.22621.0/PrayerShutdown.UI.exe

# Publish
dotnet publish src/PrayerShutdown.UI/PrayerShutdown.UI.csproj -c Release -p:Platform=x64 -o publish

# Build installer (requires Inno Setup)
"C:\Users\U\AppData\Local\Programs\Inno Setup 6\ISCC.exe" installer/setup.iss
```

---

## Architecture

```
Muslim ON
├── src/
│   ├── PrayerShutdown.Core        ← Domain layer (models, enums, interfaces)
│   ├── PrayerShutdown.Common      ← Shared (localization, constants, Result<T>)
│   ├── PrayerShutdown.Services    ← Infrastructure (calculation, DB, scheduler, shutdown)
│   ├── PrayerShutdown.Features    ← ViewModels + feature models (MVVM)
│   └── PrayerShutdown.UI          ← WinUI 3 app (XAML, theme, navigation, tray)
├── tests/
│   └── PrayerShutdown.Tests       ← xUnit tests
├── installer/
│   └── setup.iss                  ← Inno Setup installer script
├── publish/                       ← Build output (git-ignored)
├── dist/                          ← Installer output (git-ignored)
├── DESIGN.md                      ← Design system documentation
└── README.md                      ← This file
```

### Dependency Flow

```
Core ← Common
  ↑       ↑
Services  Features
  ↑       ↑
  └── UI ─┘
```

- **Core** has zero external dependencies (only CommunityToolkit.Mvvm)
- **Services** depends on Core + Common + EF Core + Windows APIs
- **Features** depends on Core + Common + CommunityToolkit.Mvvm
- **UI** depends on everything above + WinUI 3 + H.NotifyIcon

---

## Key Patterns

### MVVM
- ViewModels use `CommunityToolkit.Mvvm` source generators (`[ObservableProperty]`, `[RelayCommand]`)
- ViewModels registered in DI (`HostBuilderExtensions.cs`)
- Pages resolve ViewModels via `App.Current.Services.GetRequiredService<T>()`

### Localization
- All user-facing strings via `Loc.S("key")` — never hardcode text
- Strings stored in `LocalizationService.cs` as dictionaries (EN + RU)
- To add a language: add one method `XxxStrings()` + one entry in `AvailableLanguages`
- Pages expose `L_*` properties for XAML `x:Bind`
- On language change, `ShellPage` reloads the current page to re-evaluate all `L_*`

### Theme / Design System
- Colors: `GeistColors.xaml` (Light + Dark ThemeDictionaries)
- Typography: `GeistTypography.xaml`
- Spacing: `GeistSpacing.xaml`
- Buttons: `GeistButtonStyles.xaml` (full ControlTemplates with VisualStateManager)
- System accent overridden to enforce our palette regardless of Windows theme
- See `DESIGN.md` for full token reference

### Prayer Calculation
- Algorithm: PrayTimes.org (public domain astronomical formulas)
- `SolarMath.cs` — pure static math functions
- `PrayerTimeCalculator.cs` — orchestrates calculation with method parameters
- `HighLatitudeAdjuster.cs` — handles Fajr/Isha at latitudes >48°
- Supports: MWL, Egyptian, Karachi, UmmAlQura, ISNA, Turkey, Tehran

### Shutdown Flow
1. User enables shutdown per prayer (toggle on dashboard or settings)
2. `PrayerScheduler` sets two timers per prayer: reminder + shutdown
3. 15 min before → reminder event (toast notification planned)
4. 15 min after prayer time → shutdown event
5. User can: Mark as prayed (5-sec undo), Snooze (max 3), Cancel
6. `WindowsShutdownService` executes `shutdown /s /t 60`

### Data Storage
- SQLite via Entity Framework Core (`AppDbContext`)
- DB location: `%LOCALAPPDATA%\MuslimON\muslimOn.db`
- Settings stored as JSON blob in single-row table
- Prayer times cached by date

---

## How to Work on This Project

### Adding a New Feature

1. **Model** — Add properties to existing models in `Core/Domain/` or create new ones
2. **Service** — Add interface in `Core/Interfaces/`, implementation in `Services/`
3. **ViewModel** — Add properties/commands in `Features/`, register in DI
4. **UI** — Add XAML in `UI/Views/`, bind to ViewModel
5. **Localize** — Add EN + RU keys in `LocalizationService.cs`, expose `L_*` in code-behind
6. **Test** — Add tests in `Tests/`

### Adding a New Language

1. Add method `XxxStrings()` in `LocalizationService.cs` with all keys translated
2. Add entry to `AvailableLanguages` list
3. Add `<ComboBoxItem>` in `SettingsPage.xaml` Language ComboBox
4. Done — everything else is automatic

### Adding a New Page

1. Create `NewPage.xaml` + `NewPage.xaml.cs` in `UI/Views/`
2. Add `L_*` properties in code-behind for localized strings
3. Add navigation case in `ShellPage.xaml.cs` `NavView_SelectionChanged`
4. Add `NavigationViewItem` in `ShellPage.xaml`
5. Add nav label in `UpdateNavLabels()` with `Loc.S()` key

### Building an Installer

1. Run publish: `dotnet publish ... -o publish`
2. Copy XBF files: from `bin/x64/Release/.../win-x64/` to `publish/`
3. Run Inno Setup: `ISCC.exe installer/setup.iss`
4. Output: `dist/MuslimON_Setup_v1.0.0.exe`

### Versioning

- Update version in `installer/setup.iss` (`MyAppVersion`)
- Update version in `LocalizationService.cs` (`about_version` keys)
- Commit, tag: `git tag v1.0.1`

---

## Tech Stack

| Layer | Technology |
|---|---|
| Language | C# 12, .NET 8 |
| UI | WinUI 3 (Windows App SDK 1.5) |
| MVVM | CommunityToolkit.Mvvm 8.2 |
| Database | SQLite + Entity Framework Core 8 |
| Tray Icon | H.NotifyIcon.WinUI 2.1 |
| Logging | Serilog |
| Testing | xUnit + Moq + FluentAssertions |
| Installer | Inno Setup 6 |

---

## Future Plans

- [ ] Toast notifications (Windows App SDK)
- [ ] Adhan audio playback
- [ ] Auto-update mechanism
- [ ] MSIX packaging for Microsoft Store
- [ ] Qibla compass
- [ ] Namespace rename: PrayerShutdown → MuslimOn
- [ ] Dark theme polish
- [ ] Onboarding wizard for first-time users
