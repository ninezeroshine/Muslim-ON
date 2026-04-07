namespace PrayerShutdown.Common.Localization;

public sealed class LocalizationService
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new();

    private Dictionary<string, string> _strings = new();
    private string _currentLanguage = "en";

    public string CurrentLanguage => _currentLanguage;
    public event EventHandler? LanguageChanged;

    public void SetLanguage(string lang)
    {
        if (_currentLanguage == lang) return;
        _currentLanguage = lang;
        _strings = GetStrings(lang);
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public string Get(string key)
    {
        if (_strings.Count == 0)
            _strings = GetStrings(_currentLanguage);
        return _strings.TryGetValue(key, out var value) ? value : key;
    }

    public static string S(string key) => Instance.Get(key);

    public static IReadOnlyList<LanguageOption> AvailableLanguages { get; } =
    [
        new("en", "English", "English"),
        new("ru", "Русский", "Russian"),
    ];

    private static Dictionary<string, string> GetStrings(string lang) => lang switch
    {
        "ru" => RussianStrings(),
        _ => EnglishStrings()
    };

    // ════════════════════════════════════════════
    //  ENGLISH
    // ════════════════════════════════════════════
    private static Dictionary<string, string> EnglishStrings() => new()
    {
        // Greetings
        ["greeting_morning"] = "Good morning",
        ["greeting_afternoon"] = "Good afternoon",
        ["greeting_evening"] = "Good evening",
        ["greeting_default"] = "Assalamu Alaikum",

        // Dashboard
        ["todays_prayers"] = "Today's Prayers",
        ["until"] = "until",
        ["all_prayers_completed"] = "All prayers completed",
        ["all_prayers_message"] = "Masha'Allah! See you tomorrow, In sha Allah.",
        ["set_location_title"] = "Set Your Location",
        ["set_location_desc"] = "Go to Settings to choose your city and see accurate prayer times.",

        // Prayer names
        ["prayer_fajr"] = "Fajr",
        ["prayer_sunrise"] = "Sunrise",
        ["prayer_dhuhr"] = "Dhuhr",
        ["prayer_asr"] = "Asr",
        ["prayer_maghrib"] = "Maghrib",
        ["prayer_isha"] = "Isha",

        // Statuses
        ["status_passed"] = "Passed",
        ["status_now"] = "Now",
        ["status_upcoming"] = "Upcoming",

        // Settings
        ["settings"] = "Settings",
        ["location"] = "Location",
        ["location_desc"] = "Prayer times are calculated based on your geographic location.",
        ["current_city"] = "Current city",
        ["search_city"] = "Search city…",
        ["location_not_set"] = "Not set",
        ["calculation_method"] = "Calculation Method",
        ["calculation_method_desc"] = "Different Islamic authorities use different angles for Fajr and Isha.",
        ["method"] = "Method",
        ["asr_calculation"] = "Asr Calculation",
        ["high_latitude_rule"] = "High Latitude Rule",
        ["high_lat_desc"] = "Used for locations above 48° where Fajr/Isha may not occur in summer.",
        ["shutdown_rules"] = "Shutdown Rules",
        ["shutdown_rules_desc"] = "Your PC will shut down after the prayer reminder if you don't respond.",
        ["notifications"] = "Notifications",
        ["notifications_desc"] = "Control how you are notified when prayer time arrives.",
        ["toast_notifications"] = "Toast Notifications",
        ["adhan_sound"] = "Adhan Sound",
        ["appearance"] = "Appearance",
        ["language"] = "Language",
        ["language_hint"] = "Language change takes effect after saving and reopening a page.",
        ["start_with_windows"] = "Start with Windows",
        ["start_minimized"] = "Start minimized to tray",
        ["save_settings"] = "Save Prayer Settings",
        ["saved"] = "Saved",
        ["settings_saved"] = "Settings saved successfully",
        ["location_set_to"] = "Location set to",

        // Calculation methods
        ["method_mwl"] = "MWL (Muslim World League)",
        ["method_egyptian"] = "Egyptian General Authority",
        ["method_karachi"] = "University of Karachi",
        ["method_ummAlQura"] = "Umm al-Qura, Makkah",
        ["method_isna"] = "ISNA (North America)",
        ["method_turkey"] = "Diyanet (Turkey)",
        ["method_tehran"] = "Tehran Institute",

        // Asr methods
        ["asr_shafi"] = "Shafi'i / Maliki / Hanbali (shadow = 1x)",
        ["asr_hanafi"] = "Hanafi (shadow = 2x)",

        // High lat rules
        ["highlat_angle"] = "Angle-Based (recommended)",
        ["highlat_middle"] = "Middle of the Night",
        ["highlat_seventh"] = "One-Seventh of the Night",

        // Navigation
        ["nav_dashboard"] = "Prayer Times",
        ["nav_settings"] = "Settings",
        ["nav_log"] = "Activity Log",
        ["nav_about"] = "About",

        // Activity Log
        ["activity_log"] = "Activity Log",
        ["clear"] = "Clear",
        ["no_activity"] = "No activity yet",
        ["no_activity_desc"] = "Prayer reminders and actions will appear here",

        // About
        ["about_title"] = "Muslim ON",
        ["about_version"] = "Version 1.0.0",
        ["about_desc"] = "Automatic prayer time reminders with optional PC shutdown. Helps you maintain your daily prayers.",
        ["about_tech"] = "Built with WinUI 3 + .NET 8",
        ["about_algorithm"] = "Prayer times: PrayTimes.org algorithm",

        // Error
        ["error_load_failed"] = "Failed to load prayer times",

        // Shutdown flow
        ["shutdown_enabled"] = "Shutdown",
        ["shutdown_how_title"] = "How Shutdown Works",
        ["shutdown_step1"] = "Reminder {0} min before prayer",
        ["shutdown_step2"] = "Prayer time arrives",
        ["shutdown_step3"] = "No response \u2192 PC shuts down {0} min after",
        ["shutdown_step4"] = "You can cancel or snooze up to {0} times",
        ["shutdown_got_it"] = "Got It",
        ["mark_prayed"] = "I Prayed",
        ["shutdown_on"] = "ON",
        ["shutdown_off"] = "OFF",
        ["shutdown_at"] = "Shutdown at",
        ["prayed_check"] = "Prayed",
        ["undo"] = "Undo",
        ["wisdom_title"] = "Daily Wisdom",
        ["jumuah_notice"] = "Today is Jumu'ah \u2014 don't forget the congregational prayer",

        // Update
        ["update_available"] = "Update available",
        ["update_version"] = "Version {0} is ready to install",
        ["update_now"] = "Update Now",
        ["updating"] = "Updating\u2026",
    };

    // ════════════════════════════════════════════
    //  RUSSIAN
    // ════════════════════════════════════════════
    private static Dictionary<string, string> RussianStrings() => new()
    {
        // Приветствия
        ["greeting_morning"] = "Доброе утро",
        ["greeting_afternoon"] = "Добрый день",
        ["greeting_evening"] = "Добрый вечер",
        ["greeting_default"] = "Ассаляму алейкум",

        // Дашборд
        ["todays_prayers"] = "Намазы на сегодня",
        ["until"] = "до",
        ["all_prayers_completed"] = "Все намазы совершены",
        ["all_prayers_message"] = "МашаАллах! Увидимся завтра, ин шаа Аллах.",
        ["set_location_title"] = "Укажите местоположение",
        ["set_location_desc"] = "Перейдите в Настройки, чтобы выбрать город и увидеть точные времена намазов.",

        // Названия намазов
        ["prayer_fajr"] = "Фаджр",
        ["prayer_sunrise"] = "Восход",
        ["prayer_dhuhr"] = "Зухр",
        ["prayer_asr"] = "Аср",
        ["prayer_maghrib"] = "Магриб",
        ["prayer_isha"] = "Иша",

        // Статусы
        ["status_passed"] = "Прошёл",
        ["status_now"] = "Сейчас",
        ["status_upcoming"] = "Впереди",

        // Настройки
        ["settings"] = "Настройки",
        ["location"] = "Местоположение",
        ["location_desc"] = "Времена намазов рассчитываются на основе вашего местоположения.",
        ["current_city"] = "Текущий город",
        ["search_city"] = "Поиск города…",
        ["location_not_set"] = "Не указан",
        ["calculation_method"] = "Метод расчёта",
        ["calculation_method_desc"] = "Разные исламские организации используют разные углы для расчёта Фаджра и Иши.",
        ["method"] = "Метод",
        ["asr_calculation"] = "Расчёт Асра",
        ["high_latitude_rule"] = "Правило высоких широт",
        ["high_lat_desc"] = "Применяется для широт выше 48°, где Фаджр и Иша могут не наступать летом.",
        ["shutdown_rules"] = "Правила выключения",
        ["shutdown_rules_desc"] = "ПК выключится после напоминания о намазе, если вы не ответите.",
        ["notifications"] = "Уведомления",
        ["notifications_desc"] = "Настройте способ получения напоминаний о намазе.",
        ["toast_notifications"] = "Уведомления Windows",
        ["adhan_sound"] = "Звук азана",
        ["appearance"] = "Внешний вид",
        ["language"] = "Язык",
        ["language_hint"] = "Смена языка вступит в силу после сохранения и перехода на другую страницу.",
        ["start_with_windows"] = "Запускать вместе с Windows",
        ["start_minimized"] = "Запускать свёрнутым в трей",
        ["save_settings"] = "Сохранить настройки",
        ["saved"] = "Сохранено",
        ["settings_saved"] = "Настройки успешно сохранены",
        ["location_set_to"] = "Установлен город",

        // Методы расчёта
        ["method_mwl"] = "MWL (Всемирная мусульманская лига)",
        ["method_egyptian"] = "Египетское управление",
        ["method_karachi"] = "Университет Карачи",
        ["method_ummAlQura"] = "Умм аль-Кура, Мекка",
        ["method_isna"] = "ISNA (Северная Америка)",
        ["method_turkey"] = "Диянет (Турция)",
        ["method_tehran"] = "Тегеранский институт",

        // Методы Асра
        ["asr_shafi"] = "Шафии / Малики / Ханбали (тень = 1x)",
        ["asr_hanafi"] = "Ханафи (тень = 2x)",

        // Правила высоких широт
        ["highlat_angle"] = "По углу (рекомендуется)",
        ["highlat_middle"] = "Середина ночи",
        ["highlat_seventh"] = "Одна седьмая ночи",

        // Навигация
        ["nav_dashboard"] = "Времена намазов",
        ["nav_settings"] = "Настройки",
        ["nav_log"] = "Журнал",
        ["nav_about"] = "О приложении",

        // Журнал активности
        ["activity_log"] = "Журнал активности",
        ["clear"] = "Очистить",
        ["no_activity"] = "Пока нет записей",
        ["no_activity_desc"] = "Здесь будут отображаться напоминания и действия",

        // О приложении
        ["about_title"] = "Muslim ON",
        ["about_version"] = "Версия 1.0.0",
        ["about_desc"] = "Автоматические напоминания о намазе с возможностью выключения ПК. Помогает соблюдать ежедневные молитвы.",
        ["about_tech"] = "Создано на WinUI 3 + .NET 8",
        ["about_algorithm"] = "Расчёт времён: алгоритм PrayTimes.org",

        // Ошибки
        ["error_load_failed"] = "Не удалось загрузить времена намазов",

        // Выключение ПК
        ["shutdown_enabled"] = "Выключение",
        ["shutdown_how_title"] = "Как работает выключение",
        ["shutdown_step1"] = "Напоминание за {0} мин до намаза",
        ["shutdown_step2"] = "Наступает время намаза",
        ["shutdown_step3"] = "Нет ответа \u2192 ПК выключится через {0} мин",
        ["shutdown_step4"] = "Можно отменить или отложить до {0} раз",
        ["shutdown_got_it"] = "Понятно",
        ["mark_prayed"] = "Я помолился",
        ["shutdown_on"] = "ВКЛ",
        ["shutdown_off"] = "ВЫКЛ",
        ["shutdown_at"] = "Выключение в",
        ["prayed_check"] = "Совершён",
        ["undo"] = "Отменить",
        ["wisdom_title"] = "Мудрость дня",
        ["jumuah_notice"] = "Сегодня Джума \u2014 не забудьте о коллективной молитве",

        // Обновление
        ["update_available"] = "Доступно обновление",
        ["update_version"] = "Версия {0} готова к установке",
        ["update_now"] = "Обновить",
        ["updating"] = "Обновление\u2026",
    };
}

public record LanguageOption(string Code, string NativeName, string EnglishName);

public static class Loc
{
    public static string S(string key) => LocalizationService.S(key);
}
