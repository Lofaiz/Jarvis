// Погода и курс валют: данные берутся из открытых API без ключей.
// Это единственное место, где Джарвису нужен интернет; без сети он честно скажет об этом.
using System.Globalization;
using System.Net.Http;
using System.Text.Json;

namespace Jarvis;

static class WebTools
{
    static readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(12) };

    static WebTools()
    {
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Jarvis/1.0 (Windows)");
    }

    // ------------------------------------------------------------------ погода
    // Основной источник — wttr.in: отвечает быстро и без ключа. Если он недоступен,
    // пробуем Open-Meteo (медленнее, но с прогнозом на день).
    public static async Task<string> WeatherAsync(string city)
    {
        city = string.IsNullOrWhiteSpace(city) ? "Москва" : city.Trim();

        try
        {
            var quick = await WeatherWttrAsync(city);
            if (!string.IsNullOrWhiteSpace(quick)) return quick;
        }
        catch { }

        return await WeatherOpenMeteoAsync(city);
    }

    static readonly Dictionary<string, (DateTime At, string Text)> weatherCache = new(StringComparer.OrdinalIgnoreCase);

    static async Task<string> WeatherWttrAsync(string city)
    {
        if (weatherCache.TryGetValue(city, out var cached) &&
            (DateTime.Now - cached.At).TotalMinutes < 10)
            return cached.Text;

        var name = NormalizeCity(city);
        var url = "https://wttr.in/" + Uri.EscapeDataString(name)
                + "?format=%l|%C|%t|%f|%w&lang=ru";
        var raw = (await http.GetStringAsync(url)).Trim();
        if (raw.Length == 0 || raw.Contains("Unknown location")) return null;

        var parts = raw.Split('|');
        if (parts.Length < 5) return null;

        var place = parts[0].Trim();
        var condition = parts[1].Trim();
        int temp = ParseTemp(parts[2]);
        int feels = ParseTemp(parts[3]);
        int wind = ParseDigits(parts[4]);

        var sb = new System.Text.StringBuilder();
        sb.Append($"В городе {place} сейчас {Degrees(temp)}, ощущается как {Degrees(feels)}. ");
        if (!string.IsNullOrWhiteSpace(condition)) sb.Append(char.ToUpper(condition[0]) + condition[1..] + ". ");
        sb.Append($"Ветер {wind} {Plural(wind, "километр", "километра", "километров")} в час.");

        var text = sb.ToString();
        weatherCache[city] = (DateTime.Now, text);
        return text;
    }

    static int ParseTemp(string s)
    {
        var digits = new string(s.Where(c => char.IsDigit(c) || c == '-').ToArray());
        return int.TryParse(digits, out var v) ? (s.Contains('-') ? -Math.Abs(v) : v) : 0;
    }

    static int ParseDigits(string s)
    {
        var digits = new string(s.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var v) ? v : 0;
    }

    // «Питер» → «Санкт-Петербург», «Мск» → «Москва»: wttr.in не понимает разговорные формы
    static string NormalizeCity(string city)
    {
        var c = city.Trim().ToLowerInvariant();
        return c switch
        {
            "питер" or "петербург" or "спб" => "Санкт-Петербург",
            "мск" or "москва" or "москве" => "Москва",
            "екб" or "екатеринбург" => "Екатеринбург",
            "нск" or "новосибирск" => "Новосибирск",
            _ => city.Trim(),
        };
    }

    static async Task<string> WeatherOpenMeteoAsync(string city)
    {
        // «в Питере», «в Москве» — пробуем исходную форму и простые варианты
        JsonDocument geoDoc = null;
        foreach (var candidate in CityCandidates(city))
        {
            var geoSql = "https://geocoding-api.open-meteo.com/v1/search?count=1&language=ru&format=json&name="
                         + Uri.EscapeDataString(candidate);
            var doc = JsonDocument.Parse(await http.GetStringAsync(geoSql));
            if (doc.RootElement.TryGetProperty("results", out var r) &&
                r.ValueKind == JsonValueKind.Array && r.GetArrayLength() > 0)
            {
                geoDoc = doc;
                break;
            }
            doc.Dispose();
        }

        if (geoDoc == null)
            return $"Не нашёл город «{city}». Попробуй назвать иначе.";

        using (geoDoc)
        {
            var place = geoDoc.RootElement.GetProperty("results")[0];
            var name = place.TryGetProperty("name", out var n) ? n.GetString() : city;
            double lat = place.GetProperty("latitude").GetDouble();
            double lon = place.GetProperty("longitude").GetDouble();

            var forecast = "https://api.open-meteo.com/v1/forecast"
                + "?current=temperature_2m,apparent_temperature,weather_code,wind_speed_10m,relative_humidity_2m"
                + "&daily=temperature_2m_max,temperature_2m_min"
                + "&timezone=auto&forecast_days=1"
                + "&latitude=" + lat.ToString(CultureInfo.InvariantCulture)
                + "&longitude=" + lon.ToString(CultureInfo.InvariantCulture);

            using var doc = JsonDocument.Parse(await http.GetStringAsync(forecast));
            var cur = doc.RootElement.GetProperty("current");
            double temp = cur.GetProperty("temperature_2m").GetDouble();
            double feels = cur.GetProperty("apparent_temperature").GetDouble();
            double wind = cur.GetProperty("wind_speed_10m").GetDouble();
            int code = cur.GetProperty("weather_code").GetInt32();

            double min = temp, max = temp;
            if (doc.RootElement.TryGetProperty("daily", out var daily))
            {
                if (daily.TryGetProperty("temperature_2m_min", out var mn) && mn.ValueKind == JsonValueKind.Array)
                    min = mn[0].GetDouble();
                if (daily.TryGetProperty("temperature_2m_max", out var mx) && mx.ValueKind == JsonValueKind.Array)
                    max = mx[0].GetDouble();
            }

            var sb = new System.Text.StringBuilder();
            sb.Append($"В городе {name} сейчас {Degrees(temp)}, ощущается как {Degrees(feels)}. ");
            sb.Append(Describe(code)).Append(". ");
            sb.Append($"Ветер {Meters(wind)} в секунду. ");
            sb.Append($"Сегодня от {Degrees(min)} до {Degrees(max)}.");
            return sb.ToString();
        }
    }

    // «Питере» → «Питере», «Питер», «Питера»; «Москве» → «Москве», «Москв», «Москва»
    static string[] CityCandidates(string city)
    {
        var list = new List<string> { city };
        var last = city[^1];
        if ("еёиыоуаяю".IndexOf(last) >= 0 && city.Length > 3)
        {
            list.Add(city[..^1]);
            if (last == 'е') list.Add(city[..^1] + "а");
            if (last == 'у') list.Add(city[..^1] + "а");
            if (last == 'ы') list.Add(city[..^1]);
        }
        return list.Distinct().ToArray();
    }

    static string Degrees(double t)
    {
        var v = (int)Math.Round(t);
        var word = Plural(Math.Abs(v), "градус", "градуса", "градусов");
        return v < 0 ? $"минус {Math.Abs(v)} {word}" : $"{v} {word}";
    }

    static string Meters(double v)
    {
        var n = (int)Math.Round(v);
        return n + " " + Plural(n, "метр", "метра", "метров");
    }

    static string Describe(int code) => code switch
    {
        0 => "Ясно",
        1 => "В основном ясно",
        2 => "Переменная облачность",
        3 => "Пасмурно",
        45 or 48 => "Туман",
        51 or 53 or 55 => "Морось",
        56 or 57 => "Ледяная морось",
        61 or 63 or 65 => "Дождь",
        66 or 67 => "Ледяной дождь",
        71 or 73 or 75 => "Снег",
        77 => "Снежная крупа",
        80 or 81 or 82 => "Ливень",
        85 or 86 => "Снегопад",
        95 => "Гроза",
        96 or 99 => "Гроза с градом",
        _ => "Погода без описания",
    };

    // ------------------------------------------------------------------ валюта
    public static async Task<string> CurrencyAsync(string code)
    {
        var json = await http.GetStringAsync("https://www.cbr-xml-daily.ru/daily_json.js");
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("Valute", out var valute))
            return "Не удалось получить курс валют.";

        if (string.IsNullOrEmpty(code))
        {
            var parts = new List<string>();
            foreach (var c in new[] { "USD", "EUR", "CNY" })
                if (valute.TryGetProperty(c, out var v))
                    parts.Add($"{NameOf(c)} {Value(v):0.00} рубля");
            return parts.Count == 0 ? "Не удалось получить курс валют." : "Курс ЦБ: " + string.Join(", ", parts) + ".";
        }

        if (!valute.TryGetProperty(code, out var item))
            return "Такой валюты в списке ЦБ нет.";

        var value = Value(item);
        return $"Курс ЦБ: один {NameOf(code)} — {value:0.00} рубля.";
    }

    static double Value(JsonElement item)
    {
        double nominal = item.TryGetProperty("Nominal", out var n) ? n.GetDouble() : 1;
        double value = item.GetProperty("Value").GetDouble();
        return nominal <= 0 ? value : value / nominal;
    }

    static string NameOf(string code) => code switch
    {
        "USD" => "доллар",
        "EUR" => "евро",
        "CNY" => "юань",
        "GBP" => "фунт",
        "JPY" => "иена",
        "KZT" => "тенге",
        "BYN" => "белорусский рубль",
        "UAH" => "гривна",
        _ => code,
    };

    static string Plural(int n, string one, string few, string many)
    {
        var n10 = n % 10;
        var n100 = n % 100;
        if (n10 == 1 && n100 != 11) return one;
        if (n10 >= 2 && n10 <= 4 && (n100 < 12 || n100 > 14)) return few;
        return many;
    }
}
