# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

Always respond in Russian. All discussions, explanations, commit messages, PR descriptions, and comments are in Russian. Code (variables, comments in code, git branch names) remains in English.

## Project Overview

**Muslim ON** — Windows desktop app for Islamic prayer times with auto-shutdown/sleep/hibernate. Built with WinUI 3 + C# 12 + .NET 8. Current version: **1.4.0**.

## Build & Run Commands

```bash
# Build (x64)
dotnet build src/PrayerShutdown.UI/PrayerShutdown.UI.csproj -p:Platform=x64

# Run
./src/PrayerShutdown.UI/bin/x64/Debug/net8.0-windows10.0.22621.0/PrayerShutdown.UI.exe

# Run tests
dotnet test tests/PrayerShutdown.Tests/PrayerShutdown.Tests.csproj

# Run a single test
dotnet test tests/PrayerShutdown.Tests/PrayerShutdown.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Publish
dotnet publish src/PrayerShutdown.UI/PrayerShutdown.UI.csproj -c Release -p:Platform=x64 -o publish

# Build installer (requires Inno Setup 6)
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer/setup.iss
```

## Architecture

5-layer Clean Architecture + MVVM. Dependencies flow **down only**: UI → Features → Services → Core ← Common.

```
PrayerShutdown.UI          WinUI 3 XAML pages, shell, tray, theme, converters
PrayerShutdown.Features    ViewModels (CommunityToolkit.Mvvm source generators)
PrayerShutdown.Services    Calculation, Location, Scheduling, Shutdown, Storage, Notification, Logging, Update
PrayerShutdown.Core        Domain: enums, models, settings interfaces (zero external deps)
PrayerShutdown.Common      Constants, Result<T>, LocalizationService singleton
PrayerShutdown.Tests       xUnit + Moq + FluentAssertions
```

Solution: `PrayerShutdown.sln` (6 projects). Namespace is `PrayerShutdown` (planned rename to `MuslimOn`).

### Key Data Flows

**Prayer Calculation**: `SolarMath.cs` (pure astronomy) → `PrayerTimeCalculator.cs` (orchestrator) → `HighLatitudeAdjuster.cs` (>48° latitude). Algorithm: PrayTimes.org (public domain). Supports 9 methods (MWL, Egyptian, Karachi, UmmAlQura, ISNA, MoonsightingCommittee, Turkey, Tehran, Custom) and 2 Asr juristic (Shafi 1:1, Hanafi 2:1).

**Shutdown Flow** (4-phase graduated escalation, v1.2.4+): `PrayerScheduler` sets four timers per prayer:
1. **Phase 1 — Remind** (X min before): `PrayerTimeApproaching` event → topmost overlay "prayer_approaching".
2. **Phase 2 — PrayNow** (at prayer time): `PrayerTimeArrived` event → overlay "prayer_arrived".
3. **Phase 3 — Nudge** (`NudgeIntervalMinutes` after, up to `MaxSnoozeCount` escalations): `PrayerNudge` event → overlay with snooze count.
4. **Phase 4 — Shutdown** (`ShutdownMinutesAfter` after): `ShutdownTriggered` event → 60-sec overlay countdown → `WindowsShutdownService.ExecuteShutdown()` (PowrProf.dll: shutdown/sleep/hibernate).

User actions: **Mark as Prayed** (cancels all timers for this prayer), **Going to Pray** (sets waiting flag, timers keep running), **Snooze** (+NudgeInterval, max 3x), **Dismiss** (closes overlay only). See [OPEN ISSUE drawer in MemPalace `scheduler` room] for the known shutdown race condition.

**Storage**: SQLite + EF Core at `%LOCALAPPDATA%\MuslimON\muslimOn.db`. Settings stored as single-row JSON blob. Prayer times cached by date + location + method hash.

### DI Registration

All services registered in `HostBuilderExtensions.cs`. Calculator, Scheduler, LocationService — Singleton. ViewModels — Transient. DbContext — Scoped.

### Localization

Custom `LocalizationService` singleton (not RESX). Two languages: `en`, `ru`. Access via `Loc.S("key")`. Event `LanguageChanged` triggers runtime UI refresh without restart.

### Design System

Geist (Vercel) adapted to WinUI 3 XAML. Theme files in `src/PrayerShutdown.UI/Theme/`:
- `GeistColors.xaml` — Light/Dark ThemeDictionaries (Background100-300, Gray100-1000, semantic colors)
- `GeistTypography.xaml` — Heading1-5, Label, Body, Mono styles
- `GeistSpacing.xaml` — 4px grid (Space1-8), corner radii (RadiusSmall-RadiusFull)
- `GeistButtonStyles.xaml` — Primary, Secondary, Tertiary, Danger variants

## Code Style

Enforced via `.editorconfig`:
- File-scoped namespaces
- Private fields: `_camelCase`
- `var` for built-in types
- Expression-bodied members, switch expressions, pattern matching preferred
- 4-space indent, CRLF, UTF-8

NuGet versions centralized in `Directory.Packages.props`. SDK pinned to .NET 8.0.300 in `global.json`.

## Dashboard ViewModel

`PrayerDashboardViewModel.cs` is the largest file (~400 lines, 90+ observable properties). Sections: prayer cards, countdown timer (updates every second), 24h timeline canvas, work day overlay, daily wisdom, auto-update status.

## Best Practices & Known Pitfalls

Distilled from versions 1.2.0 – 1.3.0 — real bugs that cost multiple patch releases. Read before touching the listed areas. Full details in MemPalace wing `muslim-on`.

### WinUI 3 — Unpackaged Publish (XBF files)

`dotnet publish` does NOT copy `.xbf` files (compiled XAML) for unpackaged WinUI 3 apps. Missing XBF → `XamlParseException` at runtime when `Application.LoadComponent` loads secondary Windows/Pages. The fix lives in `PrayerShutdown.UI.csproj` as the `CopyXbfToPublish` MSBuild target — **DO NOT remove or modify it**. After publish, verify:

```bash
dotnet publish src/PrayerShutdown.UI/PrayerShutdown.UI.csproj -c Release -p:Platform=x64 -o publish --no-build
find publish -name "*.xbf" | wc -l  # expect 13 files including Dialogs/PrayerOverlayWindow.xbf
```

Alternative Microsoft-recommended approach: deploy directly from `bin/x64/Release/net8.0-windows10.0.22621.0/` and skip `dotnet publish` entirely.

### WinUI 3 — Threading & DispatcherQueue

- `Window.Dispatcher` is **always null** in WinUI 3. Use `Window.DispatcherQueue` or `DispatcherQueue.GetForCurrentThread()`.
- Any callback that might run off the UI thread (`System.Threading.Timer`, service events, task continuations, P/Invoke callbacks) MUST marshal UI updates via `_dispatcher.TryEnqueue(() => { ... })`. See `App.xaml.cs` phase handlers for the canonical pattern.
- Use `DispatcherQueueTimer` (via `DispatcherQueue.CreateTimer()`) for UI-bound ticks — fires on the UI thread automatically. Use `System.Threading.Timer` for domain logic (scheduler) and marshal to UI via `TryEnqueue`.
- Check `DispatcherQueue.HasThreadAccess` when unsure which thread you're on.

### WinUI 3 — Secondary Window Safety Checklist

Every `Window` subclass (overlay, dialog) MUST implement ALL of these. Skipping any is a crash in production. `PrayerOverlayWindow.xaml.cs` is the reference implementation — mirror its structure:

1. **`_isClosed` guard flag** set in `Closed` handler, checked at the start of every timer tick and UI update.
2. **Win32 interop in try/catch** + `hwnd == IntPtr.Zero` guard. Applies to `WindowNative.GetWindowHandle`, `SetWindowPos`, `SetForegroundWindow`, `AppWindow.GetFromWindowId`, `Move`, `Resize`.
3. **`SafeUpdateUI(Action)` wrapper** for every timer-triggered UI update: `if (_isClosed) return; try { ... } catch { Log }`.
4. **`ThemeShadow` requires `Translation="0,0,N"`** — without it, crashes on software rendering and older GPU drivers. Either pair with Translation or remove ThemeShadow entirely (we removed it in v1.2.6).
5. **Do NOT `x:Bind` plain CLR fields** — x:Bind needs `INotifyPropertyChanged`. For code-driven state, set `Element.Visibility`/text directly from code-behind.
6. **XAML resource keys are case-sensitive and must exist at load time.** A typo like `HeadingStyle3` vs `GeistHeading3Style` throws `ResourceNotFoundException` during `InitializeComponent()` — before any try-catch runs, crashing the window silently. Verify Theme dictionary keys before referencing them; build immediately after XAML edits.
7. **Brush lookup via `TryGetValue`** — never throw from a resource lookup: `if (fe.Resources.TryGetValue(key, out var brush)) Element.Foreground = (Brush)brush;`.
8. **Dispose DispatcherQueueTimer on close** — `Stop()` before `Close()`.
9. **Persistent crash logging** to `%LOCALAPPDATA%\MuslimON\logs\overlay_errors.log` (append mode). Hook both `Application.UnhandledException` and `AppDomain.CurrentDomain.UnhandledException` in `App.xaml.cs`.

### CommunityToolkit.Mvvm 8.2 — Source Generator Rules

- Class MUST be `partial` — non-partial classes are silently skipped.
- `[ObservableProperty] private string? _name;` → `public string? Name` with INPC raise.
- `[RelayCommand] private async Task SaveAsync()` → `SaveCommand` as `AsyncRelayCommand`.
- Never define overloads of a `[RelayCommand]` method — MVVMTK0023 error.
- Dependencies: `[NotifyPropertyChangedFor(nameof(OtherProp))]` above the backing field.
- CanExecute: `[RelayCommand(CanExecute = nameof(CanDoIt))]` + `XCommand.NotifyCanExecuteChanged()` when state changes.
- If `Name` doesn't exist, check: (1) class is `partial`, (2) field starts with `_`, (3) project rebuilt, (4) IDE language server not stale. Source gen runs at compile time.
- Inherit from `ObservableObject`; never hand-roll `INotifyPropertyChanged`.

### EF Core 8 + SQLite

- DbContext is **Scoped** — never cache across scopes. For background/timer operations, inject `IServiceScopeFactory` and create a scope per operation.
- Read-only queries: always `.AsNoTracking()`.
- SQLite is NOT thread-safe on a single context — don't share a DbContext across `TryEnqueue` callbacks.
- `context.Database.MigrateAsync()` on app start is fine for local DB. Do NOT mix with `EnsureCreated()`.
- Settings as single-row JSON blob is **intentional** (simplicity > normalization). Don't refactor without discussion.
- DB path: `%LOCALAPPDATA%\MuslimON\muslimOn.db`.

### Auto-Update Script Safety

- **NEVER interpolate paths with trailing `\` into cmd/PowerShell quoted strings.** `"path\"` parses as escaped quote → silent break. Always `path.TrimEnd(Path.DirectorySeparatorChar)` first. This cost us 4 patch releases (v1.2.0–1.2.3).
- **Prefer PowerShell over batch.** Launch via `powershell.exe -NoProfile -ExecutionPolicy Bypass -File update.ps1`. Use `Copy-Item -Recurse -Force`, not xcopy (encoding mismatch UTF-8 vs OEM cp866 + quoted wildcards).
- **Log to persistent location**: `%LOCALAPPDATA%\MuslimON\logs\update.log` — NOT Desktop, NOT temp.
- **Diagnostic output on every operation.** Silent auto-update failures create chicken-and-egg fix-distribution problems.

### Version Bump — Pre-Publish Checklist

1. `grep -rn "OLD_VERSION" src/` — catches hardcoded strings in `LocalizationService.cs` (`about_version` EN + RU), `UpdateService.cs`, installer `setup.iss`, .csproj, readmes.
2. Replace ALL occurrences via multi-file edit, not manually.
3. `dotnet build` → `dotnet publish --no-build` → verify 13 XBF files in `publish/`.
4. Build installer with Inno Setup 6 → smoke test on clean profile (About screen EN + RU, tray tooltip, no update loop).
5. Update MemPalace: `mempalace_kg_invalidate` old `current_version` + `mempalace_kg_add` new + add drawer to `version-history`.
6. Git tag + GitHub Release with changelog + attach installer exe as asset.

### Shutdown Scheduler — OPEN Known Issue (v1.2.7)

`PrayerScheduler.OnShutdownTriggered` (PrayerScheduler.cs:232–239) calls `_shutdownService.ExecuteShutdown()` on the timer thread, racing the overlay window creation. v1.2.6 moved this into `App.xaml.cs` overlay lifecycle but was reverted in v1.2.7 alongside the XBF emergency fix. Before touching this area, read the OPEN ISSUE drawer in MemPalace `scheduler` room.

## MemPalace Integration

This project uses [MemPalace MCP](https://github.com/mempalace) for persistent knowledge storage across sessions. Wing: **`muslim-on`** with 14 rooms.

### When to Update the Palace

**On file creation or significant modification in `src/`:**
- Save via `mempalace_add_drawer` (wing: `"muslim-on"`, room by type)
- Rooms: `architecture`, `calculation-engine`, `scheduler`, `data-storage`, `ui-framework`, `location`, `features`, `tech-stack`, `infrastructure`, `localization`, `decisions`, `roadmap`, `version-history`, `build-deploy`
- Use `mempalace_check_duplicate` before writing to avoid duplicates

**On file or component deletion:**
- Find drawers via `mempalace_search` by file/component name
- Delete stale drawers via `mempalace_delete_drawer`

**On architectural decisions:**
- Record decision + reasoning as drawer in room `decisions`
- Update knowledge graph via `mempalace_kg_add` if stack, architecture, or dependencies change
- If a fact is outdated: `mempalace_kg_invalidate` old → `mempalace_kg_add` new

**On version release:**
- Update `mempalace_kg_invalidate` old version → `mempalace_kg_add` new version
- Add drawer to `version-history` room

**On session end with substantial changes:**
- Write diary via `mempalace_diary_write` (agent: `"claude"`, AAAK format summary)

### When NOT to Update the Palace

- Minor style tweaks or typo fixes
- Files outside `src/` (bin, obj, publish, dist)
- If a drawer with the same content already exists

### Quick Reference

```
# Check palace state
mempalace_status
mempalace_get_taxonomy

# Search
mempalace_search(query="prayer calculation", wing="muslim-on")
mempalace_kg_query(entity="MuslimON")

# Add knowledge
mempalace_add_drawer(wing="muslim-on", room="decisions", content="...")
mempalace_kg_add(subject="MuslimON", predicate="has_feature", object="...")

# Diary
mempalace_diary_write(agent_name="claude", entry="SESSION:YYYY-MM-DD|...", topic="...")
```
