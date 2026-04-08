# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Language

Always respond in Russian. All discussions, explanations, commit messages, PR descriptions, and comments are in Russian. Code (variables, comments in code, git branch names) remains in English.

## Project Overview

**Muslim ON** — Windows desktop app for Islamic prayer times with auto-shutdown/sleep/hibernate. Built with WinUI 3 + C# 12 + .NET 8. Current version: **1.2.1**.

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

**Shutdown Flow**: `PrayerScheduler` sets two timers per prayer — reminder (15 min before) and shutdown (15 min after). User can: mark as prayed (cancel), snooze (max 3x, +5 min), or cancel. Shutdown via `WindowsShutdownService` (PowrProf.dll).

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
