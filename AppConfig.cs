// Настройки Джарвиса. Файл config.json лежит рядом с программой — приложение портативное, ничего в реестр не пишет.
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Jarvis;

class AppConfig
{
    public string title { get; set; } = "Джарвис";
    public string language { get; set; } = "ru";

    // файл модели распознавания речи внутри папки model рядом с программой
    public string sttModel { get; set; } = "ggml-base-q5_1.bin";

    public bool autoSendVoice { get; set; } = true;   // сразу выполнять распознанное
    public bool startHidden { get; set; } = true;     // запуск в трее

    public string hotkeyVoice { get; set; } = "Num9";
    public string hotkeyWindow { get; set; } = "Num8";

    public string city { get; set; } = "Москва";      // для «какая погода»
    public int shutdownDelay { get; set; } = 30;      // секунд до выключения по голосу

    // свои программы поверх встроенного списка: имя, путь и слова-ключи
    public List<ExtraApp> extraApps { get; set; } = new();

    public static bool FirstRun { get; private set; }

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static AppConfig Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var cfg = JsonSerializer.Deserialize<AppConfig>(
                    File.ReadAllText(path, Encoding.UTF8), JsonOpts) ?? new AppConfig();
                cfg.sttModel = string.IsNullOrWhiteSpace(cfg.sttModel) ? "ggml-base-q5_1.bin" : cfg.sttModel;
                cfg.hotkeyVoice = string.IsNullOrWhiteSpace(cfg.hotkeyVoice) ? "Num9" : cfg.hotkeyVoice;
                cfg.hotkeyWindow = string.IsNullOrWhiteSpace(cfg.hotkeyWindow) ? "Num8" : cfg.hotkeyWindow;
                cfg.city = string.IsNullOrWhiteSpace(cfg.city) ? "Москва" : cfg.city;
                cfg.extraApps ??= new();
                if (cfg.shutdownDelay < 5 || cfg.shutdownDelay > 600) cfg.shutdownDelay = 30;
                return cfg;
            }
        }
        catch { }
        FirstRun = true;
        return new AppConfig();
    }

    public void Save(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts) + Environment.NewLine,
                new UTF8Encoding(true));
        }
        catch { }
    }
}

// запись из config.json → extraApps: свои программы поверх встроенного списка
class ExtraApp
{
    public string name { get; set; } = "";
    public string path { get; set; } = "";
    public List<string> keys { get; set; } = new();
}
