// Разбор голосовых команд. Никакого ИИ: правила, списки приложений и сайтов,
// разбор чисел, времени и длительности. Всё, что не распознано, честно уходит
// в «не понял» — Джарвис не притворяется умнее, чем есть.
using System.Globalization;
using System.Text;

namespace Jarvis;

enum Kind
{
    None,
    Help, Stop, Repeat, Clear, Exit,
    Time, Date, Weekday,
    OpenApp, OpenSite, OpenFolder,
    VolumeUp, VolumeDown, VolumeSet, VolumeGet,
    Mute, Unmute,
    BrightnessUp, BrightnessDown, BrightnessSet, BrightnessGet,
    Screenshot, Lock, Sleep, Shutdown, Restart, CancelShutdown,
    Timer, Remind, CancelTimers, ListReminders,
    MediaPlayPause, MediaNext, MediaPrev, MediaStop,
    CloseWindow, ShowDesktop, SwitchWindow,
    InsertText, Calc, Weather, Currency,
}

class Intent
{
    public Kind Kind;
    public string Arg;      // приложение, сайт, папка, текст, город, код валюты
    public int Number;      // проценты, секунды
    public string Say;      // готовый ответ для простых команд
    public bool HasNumber;
}

static class Parser
{
    static Intent Make(Kind kind, string say = null) => new() { Kind = kind, Say = say };

    public static Intent Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var t = Text.Norm(raw);
        if (t.Length == 0) return null;

        // ---------------------------------------------------- простые команды
        if (Eq(t, "стоп", "остановись", "останови", "стой", "хватит", "прекрати", "замолчи",
                   "не надо", "отставить", "stop", "enough"))
            return Make(Kind.Stop);

        if (Eq(t, "повтори", "повтори еще раз", "еще раз", "скажи еще раз", "повтори ответ",
                   "что ты сказал", "repeat", "say it again"))
            return Make(Kind.Repeat);

        if (Eq(t, "помощь", "что ты умеешь", "что умеешь", "команды", "список команд",
                   "что можешь", "как пользоваться", "help"))
            return Make(Kind.Help);

        if (Eq(t, "очисти", "очисти чат", "очисти историю", "сотри историю", "clear"))
            return Make(Kind.Clear);

        if (Eq(t, "выход", "выключись", "закройся", "пока", "до свидания", "exit", "quit"))
            return Make(Kind.Exit);

        // ---------------------------------------------------- время и дата
        if (Eq(t, "сколько времени", "сколько сейчас времени", "который час", "время",
                   "скажи время", "текущее время", "what time is it"))
            return Make(Kind.Time);

        if (Eq(t, "какое сегодня число", "какая сегодня дата", "какая дата", "какое число",
                   "сегодняшняя дата", "дата", "what is the date"))
            return Make(Kind.Date);

        if (Eq(t, "какой сегодня день", "какой день недели", "какой сегодня день недели",
                   "день недели", "what day is it"))
            return Make(Kind.Weekday);

        // ---------------------------------------------------- медиа
        if (Eq(t, "пауза", "плей", "плей пауза", "продолжи", "играй", "включи музыку",
                   "поставь музыку", "включи плеер", "музыка", "play", "pause"))
            return Make(Kind.MediaPlayPause);

        if (Eq(t, "следующий трек", "следующая песня", "следующая композиция", "дальше",
                   "переключи трек", "следующий", "next track"))
            return Make(Kind.MediaNext);

        if (Eq(t, "предыдущий трек", "предыдущая песня", "предыдущая композиция",
                   "верни трек", "назад трек", "previous track"))
            return Make(Kind.MediaPrev);

        if (Eq(t, "останови музыку", "выключи музыку", "стоп музыка", "останови плеер"))
            return Make(Kind.MediaStop);

        // ---------------------------------------------------- система
        if (Eq(t, "скриншот", "сделай скриншот", "снимок экрана", "скрин", "screenshot"))
            return Make(Kind.Screenshot);

        if (Eq(t, "заблокируй компьютер", "заблокируй", "блокировка", "lock"))
            return Make(Kind.Lock);

        if (Eq(t, "спящий режим", "усни", "спать", "сон", "режим сна", "sleep"))
            return Make(Kind.Sleep);

        if (Eq(t, "выключи компьютер", "заверши работу", "выключение", "shutdown"))
            return Make(Kind.Shutdown);

        if (Eq(t, "перезагрузи компьютер", "перезагрузка", "перезагрузи", "restart", "reboot"))
            return Make(Kind.Restart);

        if (Eq(t, "отмена", "отмени", "отмени выключение", "отмени перезагрузку", "cancel"))
            return Make(Kind.CancelShutdown);

        if (Eq(t, "закрой окно", "закрой активное окно", "закрыть окно", "close window"))
            return Make(Kind.CloseWindow);

        if (Eq(t, "сверни окна", "сверни все окна", "сверни все", "покажи рабочий стол",
                   "show desktop"))
            return Make(Kind.ShowDesktop);

        if (Eq(t, "переключи окно", "следующее окно", "переключение окон", "альт таб", "switch window"))
            return Make(Kind.SwitchWindow);

        // ---------------------------------------------------- звук
        if (Eq(t, "выключи звук", "без звука", "заглуши", "мут", "mute"))
            return Make(Kind.Mute);

        if (Eq(t, "включи звук", "верни звук", " unmute", "unmute"))
            return Make(Kind.Unmute);

        if (Eq(t, "громкость", "какая громкость", "уровень звука", "volume"))
            return Make(Kind.VolumeGet);

        if (Eq(t, "громче", "погромче", "сделай громче", "громче звук", "прибавь звук",
                   "прибавь громкость", "увеличь громкость", "увеличь звук", "louder"))
            return Volume(Kind.VolumeUp, 10);

        if (Eq(t, "тише", "потише", "сделай тише", "тише звук", "убавь звук",
                   "убавь громкость", "уменьши громкость", "уменьши звук", "quieter"))
            return Volume(Kind.VolumeDown, 10);

        if (TryNumberAfter(t, new[] { "громкость на", "громкость", "звук на", "звук" }, out var vol))
            return Volume(Kind.VolumeSet, vol);

        // ---------------------------------------------------- яркость
        if (Eq(t, "яркость", "какая яркость", "уровень яркости"))
            return Make(Kind.BrightnessGet);

        if (Eq(t, "ярче", "поярче", "сделай ярче", "прибавь яркость", "увеличь яркость"))
            return Brightness(Kind.BrightnessUp, 10);

        if (Eq(t, "темнее", "потемнее", "сделай темнее", "убавь яркость", "уменьши яркость"))
            return Brightness(Kind.BrightnessDown, 10);

        if (TryNumberAfter(t, new[] { "яркость на", "яркость" }, out var bright))
            return Brightness(Kind.BrightnessSet, bright);

        // ---------------------------------------------------- таймеры
        if (Eq(t, "отмени таймер", "сбрось таймер", "стоп таймер", "убери таймер"))
            return Make(Kind.CancelTimers);

        if (Eq(t, "мои напоминания", "напоминания", "список напоминаний", "что по напоминаниям"))
            return Make(Kind.ListReminders);

        foreach (var p in new[] { "поставь таймер на", "поставь таймер", "таймер на", "таймер", "засеки" })
        {
            if (t == p) return Make(Kind.Timer);
            if (t.StartsWith(p + " "))
            {
                var rest = t[(p.Length + 1)..].Trim();
                if (ParseDuration(rest, out int seconds, out _) && seconds > 0)
                {
                    var i = Make(Kind.Timer);
                    i.Number = seconds;
                    i.HasNumber = true;
                    return i;
                }
                return Make(Kind.Timer, "Не понял, на сколько ставить таймер. Скажи, например: таймер на десять минут.");
            }
        }

        // ---------------------------------------------------- напоминания
        foreach (var p in new[] { "напомни мне", "напомни пожалуйста", "напомни" })
        {
            if (t == p) return Make(Kind.Remind, "Что и когда напомнить? Например: напомни через час позвонить.");
            if (t.StartsWith(p + " "))
            {
                var rest = t[(p.Length + 1)..].Trim();
                return ParseReminder(rest);
            }
        }

        // ---------------------------------------------------- погода и валюта
        if (t == "погода" || t == "какая погода" || t == "погода на улице" || t == "weather")
            return Make(Kind.Weather);

        if (t.StartsWith("погода в "))
        {
            var i = Make(Kind.Weather);
            i.Arg = CityName(t[8..]);
            return i;
        }
        if (t.StartsWith("какая погода в "))
        {
            var i = Make(Kind.Weather);
            i.Arg = CityName(t[16..]);
            return i;
        }

        if (Eq(t, "курс валют", "курсы валют", "курс доллара и евро", "какой курс"))
        {
            var i = Make(Kind.Currency);
            i.Arg = "";
            return i;
        }
        foreach (var (word, code) in new[]
        {
            ("доллар", "USD"), ("доллара", "USD"), ("бакс", "USD"),
            ("евро", "EUR"), ("юань", "CNY"), ("юаня", "CNY"),
            ("фунт", "GBP"), ("фунта", "GBP"), ("иена", "JPY"), ("иены", "JPY"),
            ("тенге", "KZT"), ("гривна", "UAH"),
        })
        {
            if (t == "курс " + word || t == word + " сколько" || t == "сколько " + word ||
                t == "курс " + word + " сегодня")
            {
                var i = Make(Kind.Currency);
                i.Arg = code;
                return i;
            }
        }

        // ---------------------------------------------------- калькулятор
        if (Calc.TrySolve(raw, out var calc))
            return Make(Kind.Calc, calc);

        // ---------------------------------------------------- открыть
        var open = TryOpen(t);
        if (open != null) return open;

        // ---------------------------------------------------- вставка текста
        if (TryInsertText(raw, out var insert))
            return Make(Kind.InsertText, insert);

        return null;
    }

    // ------------------------------------------------------------------ открыть
    static Intent TryOpen(string t)
    {
        string[] prefixes = { "открой", "открыть", "открывай", "запусти", "запустить",
                              "запускай", "включи", "включить", "open", "launch", "start", "run" };
        foreach (var p in prefixes)
        {
            if (t == p) return Make(Kind.Help, "Что открыть? Например: открой ютуб.");
            if (!t.StartsWith(p + " ")) continue;

            var target = t[(p.Length + 1)..].Trim();
            foreach (var junk in new[] { "сайт ", "страницу ", "вкладку ", "the site ", "the page " })
                if (target.StartsWith(junk)) target = target[junk.Length..].Trim();

            // папка
            foreach (var f in Folders)
                if (MatchKey(target, f.Keys))
                    return new Intent
                    {
                        Kind = Kind.OpenFolder,
                        Arg = f.Explicit ?? Environment.GetFolderPath(f.Dir),
                        Say = f.Name,
                    };

            // приложение
            foreach (var a in Apps.Concat(ExtraApps))
                if (MatchKey(target, a.Keys))
                    return new Intent { Kind = Kind.OpenApp, Arg = ResolvePath(a), Say = a.Name };

            // сайт
            foreach (var s in Sites)
                if (MatchKey(target, s.Keys))
                    return new Intent { Kind = Kind.OpenSite, Arg = s.Url, Say = s.Name };

            // домен или ссылка
            if (LooksLikeUrl(target))
                return new Intent { Kind = Kind.OpenSite, Arg = target, Say = target };

            // неизвестное приложение — попробуем запустить как есть
            return new Intent { Kind = Kind.OpenApp, Arg = target, Say = target };
        }
        return null;
    }

    static bool LooksLikeUrl(string s) =>
        !s.Contains(' ') && (s.Contains('.') || s.StartsWith("http"));

    // у Roblox путь содержит номер версии — берём самый свежий каталог
    static string ResolvePath(AppDef a)
    {
        var path = a.Path;
        if (a.Glob == null) return path;
        try
        {
            if (path != null && File.Exists(path)) return path;
            var dir = Path.GetDirectoryName(a.Glob);
            var mask = Path.GetFileName(a.Glob);
            if (dir == null || !Directory.Exists(dir)) return path;
            return Directory.EnumerateDirectories(dir)
                .Select(d => Path.Combine(d, mask))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault() ?? path;
        }
        catch { return path; }
    }

    static string CityName(string s)
    {
        s = s.Trim();
        if (s.StartsWith("городе ")) s = s[7..].Trim();
        if (s.StartsWith("город ")) s = s[6..].Trim();
        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s);
    }

    // ------------------------------------------------------------------ текст
    static bool TryInsertText(string raw, out string text)
    {
        text = null;
        var s = raw.Trim();
        foreach (var p in new[] { "вставь ", "напечатай ", "напиши ", "введи ", "insert " })
        {
            if (s.StartsWith(p, StringComparison.OrdinalIgnoreCase))
            {
                text = s[p.Length..].Trim();
                return text.Length > 0;
            }
        }
        return false;
    }

    // ------------------------------------------------------------------ напоминание
    static Intent ParseReminder(string rest)
    {
        var i = Make(Kind.Remind);

        // «через <время> <текст>»
        if (rest.StartsWith("через "))
        {
            if (!ParseDuration(rest[6..], out int seconds, out var what))
                return Make(Kind.Remind, "Не понял время. Скажи, например: напомни через час позвонить.");
            var due = DateTime.Now.AddSeconds(seconds);
            i.Number = seconds;
            i.HasNumber = true;
            i.Arg = string.IsNullOrWhiteSpace(what) ? "напоминание" : what;
            i.Say = $"{due:HH:mm}";
            return i;
        }

        // «в 18:30 <текст>», «завтра в 9 <текст>»
        if (TryParseClock(rest, out var dueTime, out var what2))
        {
            i.Arg = string.IsNullOrWhiteSpace(what2) ? "напоминание" : what2;
            i.Number = (int)Math.Round((dueTime - DateTime.Now).TotalSeconds);
            i.HasNumber = true;
            if (i.Number <= 0) i.Number = 60;
            i.Say = $"{dueTime:HH:mm}";
            return i;
        }

        return Make(Kind.Remind, "Не понял, когда напомнить. Скажи, например: напомни через час позвонить.");
    }

    static bool TryParseClock(string s, out DateTime due, out string what)
    {
        due = default;
        what = "";

        bool tomorrow = false, dayAfter = false;
        if (s.StartsWith("завтра ")) { tomorrow = true; s = s[7..].Trim(); }
        else if (s.StartsWith("послезавтра ")) { dayAfter = true; s = s[12..].Trim(); }

        if (s.StartsWith("в ")) s = s[2..].Trim();
        else if (!tomorrow && !dayAfter) return false;

        int hour, minute = 0;
        var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return false;

        // 18:30 / 18.30 / 18 30
        var head = parts[0];
        if (head.Contains(':') || head.Contains('.'))
        {
            var two = head.Split(':', '.');
            if (two.Length != 2 || !int.TryParse(two[0], out hour) || !int.TryParse(two[1], out minute))
                return false;
            what = string.Join(' ', parts.Skip(1));
        }
        else if (int.TryParse(head, out hour))
        {
            int idx = 1;
            // «18 часов 30 минут»
            if (parts.Length > 2 && parts[1] is "часов" or "часа" or "час" &&
                int.TryParse(parts[2], out var m))
            { minute = m; idx = 3; }
            else if (parts.Length > 1 && int.TryParse(parts[1], out var m2) && m2 < 60)
            { minute = m2; idx = 2; }
            what = string.Join(' ', parts.Skip(idx));
        }
        else return false;

        // «в 9 вечера» / «в 9 утра»
        if (!string.IsNullOrWhiteSpace(what))
        {
            var first = what.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if ((first is "вечера" or "дня") && hour < 12) hour += 12;
            else if (first is "ночи" && hour == 12) hour = 0;
            else if (first is "утра" && hour == 12) hour = 0;
            if (first is "вечера" or "дня" or "утра" or "ночи")
                what = what[(first.Length + 1)..].Trim();
        }

        if (hour < 0 || hour > 23 || minute < 0 || minute > 59) return false;

        var now = DateTime.Now;
        var date = now.Date;
        if (dayAfter) date = date.AddDays(2);
        else if (tomorrow) date = date.AddDays(1);
        due = date.AddHours(hour).AddMinutes(minute);

        // без «завтра»: если время прошло — считаем, что это следующие сутки
        if (!tomorrow && !dayAfter && due <= now) due = due.AddDays(1);
        return true;
    }

    // ------------------------------------------------------------------ числа
    static Intent Volume(Kind kind, int n) =>
        new() { Kind = kind, Number = n, HasNumber = true, Say = null };

    static Intent Brightness(Kind kind, int n) =>
        new() { Kind = kind, Number = n, HasNumber = true, Say = null };

    static bool TryNumberAfter(string t, string[] prefixes, out int number)
    {
        number = 0;
        foreach (var p in prefixes)
        {
            if (t == p) continue;
            if (!t.StartsWith(p + " ")) continue;
            var rest = t[(p.Length + 1)..].Trim();
            rest = rest.Replace("процентов", "").Replace("процента", "").Replace("процент", "")
                       .Replace("%", "").Trim();
            if (int.TryParse(rest, out number) && number >= 0 && number <= 100) return true;
            if (TryWords(rest, out number) && number >= 0 && number <= 100) return true;
        }
        return false;
    }

    public static bool TryWords(string s, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        double acc = 0;
        foreach (var w in s.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!NumberWords.TryGetValue(w, out var v)) return false;
            acc += v;
        }
        if (acc <= 0 || acc > 1000000) return false;
        value = (int)acc;
        return true;
    }

    // ------------------------------------------------------------------ время
    // «через 10 минут», «на 2 часа 30 минут», «полчаса»; остаток — текст напоминания
    public static bool ParseDuration(string s, out int seconds, out string rest)
    {
        seconds = 0;
        rest = "";
        var words = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        double total = 0;
        int i = 0;

        while (i < words.Length)
        {
            var w = words[i];
            if (w is "полчаса" or "получаса") { total += 1800; i++; continue; }
            if ((w is "полторы" or "полтора") && i + 1 < words.Length && TryUnit(words[i + 1], out var half))
            { total += half * 1.5; i += 2; continue; }

            if (TryUnit(w, out var only)) { total += only; i++; continue; }

            if (!TryNumWord(w, out var num)) break;
            i++;
            if (i < words.Length && TryUnit(words[i], out var unit)) { total += num * unit; i++; continue; }
            i--;
            break;
        }

        if (total <= 0 || total > 7 * 86400) return false;
        seconds = (int)Math.Round(total);
        rest = string.Join(' ', words.Skip(i)).Trim();
        foreach (var p in new[] { "чтобы ", "to " })
            if (rest.StartsWith(p)) rest = rest[p.Length..].Trim();
        return true;
    }

    static bool TryNumWord(string w, out double value)
    {
        value = 0;
        if (double.TryParse(w, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) { value = d; return true; }
        if (NumberWords.TryGetValue(w, out var v)) { value = v; return true; }
        return false;
    }

    static bool TryUnit(string w, out int seconds)
    {
        seconds = w switch
        {
            "секунда" or "секунды" or "секунд" or "секунду" or "сек" => 1,
            "минута" or "минуты" or "минут" or "минуту" or "минутку" or "мин" => 60,
            "час" or "часа" or "часов" or "часик" or "часика" or "часу" => 3600,
            _ => 0,
        };
        return seconds > 0;
    }

    static readonly Dictionary<string, double> NumberWords = new()
    {
        ["ноль"] = 0, ["один"] = 1, ["одна"] = 1, ["одну"] = 1, ["два"] = 2, ["две"] = 2,
        ["три"] = 3, ["четыре"] = 4, ["пять"] = 5, ["шесть"] = 6, ["семь"] = 7,
        ["восемь"] = 8, ["девять"] = 9, ["десять"] = 10, ["одиннадцать"] = 11,
        ["двенадцать"] = 12, ["тринадцать"] = 13, ["четырнадцать"] = 14, ["пятнадцать"] = 15,
        ["шестнадцать"] = 16, ["семнадцать"] = 17, ["восемнадцать"] = 18, ["девятнадцать"] = 19,
        ["двадцать"] = 20, ["тридцать"] = 30, ["сорок"] = 40, ["пятьдесят"] = 50,
        ["шестьдесят"] = 60, ["семьдесят"] = 70, ["восемьдесят"] = 80, ["девяносто"] = 90,
        ["сто"] = 100, ["двести"] = 200, ["триста"] = 300, ["четыреста"] = 400,
        ["пятьсот"] = 500, ["шестьсот"] = 600, ["семьсот"] = 700, ["восемьсот"] = 800,
        ["девятьсот"] = 900,
    };

    // ------------------------------------------------------------------ утилиты
    static bool Eq(string t, params string[] phrases) => Array.IndexOf(phrases, t) >= 0;

    static bool MatchKey(string name, string[] keys)
    {
        name = name.Trim();
        foreach (var raw in keys)
        {
            var k = Text.Norm(raw);
            if (k.Length == 0) continue;
            if (name == k) return true;

            // падежи: «оперу» от «опера», «хрома» от «хром»
            if (k.Length >= 4 && name.Length > k.Length && name.Length - k.Length <= 2 &&
                name.StartsWith(k, StringComparison.Ordinal)) return true;

            // гласная в конце ключа: «файлзилла» → «файлзиллу», «лм студию»
            if (k.Length >= 5 && name.Length > k.Length - 1 &&
                name.StartsWith(k[..^1], StringComparison.Ordinal) &&
                name.Length - (k.Length - 1) <= 2) return true;
        }
        return false;
    }

    // ------------------------------------------------------------------ списки
    sealed class AppDef
    {
        public string Name;
        public string Path;
        public string[] Keys;
        public string Glob;
    }

    sealed class SiteDef
    {
        public string Name;
        public string Url;
        public string[] Keys;
    }

    sealed class FolderDef
    {
        public string Name;
        public Environment.SpecialFolder Dir;
        public string Explicit;
        public string[] Keys;
    }

    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string Roam = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static readonly string Prof = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    static readonly string Pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    static readonly string Pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    static readonly FolderDef[] Folders =
    {
        new() { Name = "Рабочий стол", Dir = Environment.SpecialFolder.DesktopDirectory,
                Keys = new[] { "рабочий стол", "стол", "desktop" } },
        new() { Name = "Документы", Dir = Environment.SpecialFolder.MyDocuments,
                Keys = new[] { "документы", "документ", "мои документы", "documents" } },
        new() { Name = "Загрузки", Dir = Environment.SpecialFolder.UserProfile,
                Explicit = Path.Combine(Prof, "Downloads"),
                Keys = new[] { "загрузки", "загрузка", "скачанное", "downloads", "downloads folder" } },
        new() { Name = "Изображения", Dir = Environment.SpecialFolder.MyPictures,
                Keys = new[] { "изображения", "картинки", "фото", "pictures" } },
        new() { Name = "Музыка", Dir = Environment.SpecialFolder.MyMusic,
                Keys = new[] { "музыка", "музыку", "music" } },
        new() { Name = "Видео", Dir = Environment.SpecialFolder.MyVideos,
                Keys = new[] { "видео", "видеофайлы", "videos" } },
        new() { Name = "Домашняя папка", Dir = Environment.SpecialFolder.UserProfile,
                Keys = new[] { "домашняя папка", "домашнюю папку", "профиль", "home" } },
    };

    static readonly SiteDef[] Sites =
    {
        new() { Name = "YouTube", Url = "https://www.youtube.com", Keys = new[] { "ютуб", "ютьюб", "youtube", "ютюб" } },
        new() { Name = "Rutube", Url = "https://rutube.ru", Keys = new[] { "рутуб", "rutube", "ру туб" } },
        new() { Name = "ВКонтакте", Url = "https://vk.com", Keys = new[] { "вконтакте", "вк", "vk", "vkontakte" } },
        new() { Name = "Одноклассники", Url = "https://ok.ru", Keys = new[] { "одноклассники", "ок", "ok" } },
        new() { Name = "Telegram Web", Url = "https://web.telegram.org", Keys = new[] { "телеграм веб", "телеграм в браузере", "telegram web" } },
        new() { Name = "WhatsApp Web", Url = "https://web.whatsapp.com", Keys = new[] { "ватсап", "whatsapp", "вотсап" } },
        new() { Name = "Яндекс", Url = "https://ya.ru", Keys = new[] { "яндекс", "yandex", "яру" } },
        new() { Name = "Google", Url = "https://www.google.com", Keys = new[] { "гугл", "google" } },
        new() { Name = "GitHub", Url = "https://github.com", Keys = new[] { "гитхаб", "github" } },
        new() { Name = "Steam", Url = "https://store.steampowered.com", Keys = new[] { "стим стор", "steam store", "магазин стим" } },
        new() { Name = "Twitch", Url = "https://www.twitch.tv", Keys = new[] { "твич", "twitch" } },
        new() { Name = "Кинопоиск", Url = "https://www.kinopoisk.ru", Keys = new[] { "кинопоиск", "кино" } },
        new() { Name = "Ozon", Url = "https://www.ozon.ru", Keys = new[] { "озон", "ozon" } },
        new() { Name = "Авито", Url = "https://www.avito.ru", Keys = new[] { "авито", "avito" } },
        new() { Name = "Wildberries", Url = "https://www.wildberries.ru", Keys = new[] { "вайлдберриз", "вайлдберис", "wildberries", "вб" } },
        new() { Name = "Gmail", Url = "https://mail.google.com", Keys = new[] { "гмейл", "джимейл", "gmail", "почта гугл" } },
        new() { Name = "ChatGPT", Url = "https://chatgpt.com", Keys = new[] { "чат гпт", "чатгпт", "chatgpt", "чейт джипити" } },
        new() { Name = "Википедия", Url = "https://ru.wikipedia.org", Keys = new[] { "википедия", "wikipedia", "вики" } },
        new() { Name = "Hh", Url = "https://hh.ru", Keys = new[] { "хедхантер", "hh", "работа" } },
        new() { Name = "Дзен", Url = "https://dzen.ru", Keys = new[] { "дзен", "dzen" } },
    };

    static readonly AppDef[] Apps =
    {
        new() { Name = "Opera GX", Path = Path.Combine(Local, "Programs", "Opera GX", "opera.exe"),
                Keys = new[] { "опера", "оперу", "опере", "оперы", "опера gx", "opera", "opera gx", "браузер" } },
        new() { Name = "Google Chrome", Path = Path.Combine(Pf, "Google", "Chrome", "Application", "chrome.exe"),
                Keys = new[] { "хром", "хрома", "гугл хром", "chrome", "google chrome" } },
        new() { Name = "Microsoft Edge", Path = Path.Combine(Pf86, "Microsoft", "Edge", "Application", "msedge.exe"),
                Keys = new[] { "эдж", "край", "edge", "microsoft edge", "msedge" } },
        new() { Name = "Discord", Path = Path.Combine(Local, "Discord", "Discord.exe"),
                Keys = new[] { "дискорд", "discord" } },
        new() { Name = "Telegram", Path = Path.Combine(Roam, "Telegram Desktop", "Telegram.exe"),
                Keys = new[] { "телеграм", "телеграмм", "телега", "telegram" } },
        new() { Name = "Steam", Path = Path.Combine(Pf86, "Steam", "steam.exe"),
                Keys = new[] { "стим", "steam" } },
        new() { Name = "Epic Games", Path = Path.Combine(Pf86, "Epic Games", "Launcher", "Portal", "Binaries", "Win32", "EpicGamesLauncher.exe"),
                Keys = new[] { "эпик", "эпик геймс", "epic", "epic games" } },
        new() { Name = "Riot Client", Path = @"C:\Riot Games\Riot Client\RiotClientServices.exe",
                Keys = new[] { "риот", "riot", "лига", "league" } },
        new() { Name = "Albion Online", Path = Path.Combine(Local, "AlbionOnline", "launcher", "AlbionLauncher.exe"),
                Keys = new[] { "альбион", "albion" } },
        new() { Name = "Roblox", Glob = Path.Combine(Local, "Roblox", "Versions", "*", "RobloxPlayerBeta.exe"),
                Keys = new[] { "роблокс", "roblox" } },
        new() { Name = "Modrinth", Path = Path.Combine(Local, "Modrinth App", "Modrinth App.exe"),
                Keys = new[] { "модринт", "modrinth" } },
        new() { Name = "Minecraft", Path = Path.Combine(Roam, ".tlauncher", "legacy", "Minecraft", "TL.exe"),
                Keys = new[] { "майнкрафт", "майн", "minecraft", "тлаунчер" } },
        new() { Name = "LM Studio", Path = Path.Combine(Local, "Programs", "lm-studio", "LM Studio.exe"),
                Keys = new[] { "лм студио", "лм студия", "lm studio" } },
        new() { Name = "IntelliJ IDEA", Path = Path.Combine(Pf, "JetBrains", "IntelliJ IDEA Community Edition 2025.2", "bin", "idea64.exe"),
                Keys = new[] { "идея", "идею", "интелидж", "intellij", "idea" } },
        new() { Name = "Sublime Text", Path = Path.Combine(Pf, "Sublime Text", "sublime_text.exe"),
                Keys = new[] { "саблайм", "sublime", "блокнот", "notepad" } },
        new() { Name = "qBittorrent", Path = Path.Combine(Pf, "qBittorrent", "qbittorrent.exe"),
                Keys = new[] { "кьюбит", "qbittorrent", "торрент", "torrent" } },
        new() { Name = "GIMP", Path = Path.Combine(Pf, "GIMP 3", "bin", "gimp-3.exe"),
                Keys = new[] { "гимп", "gimp" } },
        new() { Name = "Audacity", Path = Path.Combine(Pf, "Audacity", "Audacity.exe"),
                Keys = new[] { "аудасити", "audacity" } },
        new() { Name = "Parsec", Path = Path.Combine(Pf, "Parsec", "parsecd.exe"),
                Keys = new[] { "парсек", "parsec" } },
        new() { Name = "WinRAR", Path = Path.Combine(Pf, "WinRAR", "WinRAR.exe"),
                Keys = new[] { "винрар", "winrar" } },
        new() { Name = "FileZilla", Path = Path.Combine(Pf, "FileZilla FTP Client", "filezilla.exe"),
                Keys = new[] { "файлзилла", "файлзиллу", "filezilla" } },
        new() { Name = "7-Zip", Path = Path.Combine(Pf, "7-Zip", "7zFM.exe"),
                Keys = new[] { "7zip", "7 зип", "семерка", "семерку" } },
        new() { Name = "Калькулятор", Path = "calc.exe",
                Keys = new[] { "калькулятор", "calc", "calculator" } },
        new() { Name = "Проводник", Path = "explorer.exe",
                Keys = new[] { "проводник", "файлы", "explorer", "files" } },
        new() { Name = "Диспетчер задач", Path = "taskmgr.exe",
                Keys = new[] { "диспетчер", "диспетчер задач", "task manager", "taskmgr" } },
        new() { Name = "Параметры", Path = "ms-settings:",
                Keys = new[] { "настройки", "параметры", "settings" } },
        new() { Name = "Командная строка", Path = "cmd.exe",
                Keys = new[] { "командная строка", "консоль", "cmd" } },
        new() { Name = "Paint", Path = "mspaint.exe",
                Keys = new[] { "пейнт", "paint" } },
    };

    // свои программы из config.json — добавляются к встроенному списку
    static AppDef[] ExtraApps = Array.Empty<AppDef>();

    public static void SetExtraApps(IEnumerable<ExtraApp> list)
    {
        ExtraApps = (list ?? Enumerable.Empty<ExtraApp>())
            .Where(a => a != null && !string.IsNullOrWhiteSpace(a.path))
            .Select(a => new AppDef
            {
                Name = string.IsNullOrWhiteSpace(a.name) ? Path.GetFileNameWithoutExtension(a.path) : a.name,
                Path = Environment.ExpandEnvironmentVariables(a.path),
                Keys = (a.keys ?? new List<string>())
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Select(k => k.Trim())
                    .ToArray(),
            })
            .Where(a => a.Keys.Length > 0)
            .ToArray();
    }
}
