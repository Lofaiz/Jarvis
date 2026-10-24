// Самопроверка: --selftest проверяет компоненты и пишет отчёт в %TEMP%\jarvis-selftest.txt.
// Нужна, чтобы после сборки убедиться, что звук, яркость, модель речи и сеть живые.
using System.Text;

namespace Jarvis;

static class SelfTest
{
    public static void Run()
    {
        var report = new List<string>();
        void Check(string name, Func<string> action)
        {
            try { report.Add("OK   " + name + ": " + action()); }
            catch (Exception ex) { report.Add("FAIL " + name + ": " + ex.Message); }
        }

        Check("звук: чтение", () => Audio.GetVolume() + "%");
        Check("звук: установка 30%", () =>
        {
            int before = Audio.GetVolume();
            if (!Audio.SetVolume(30)) return "не удалось";
            int after = Audio.GetVolume();
            Audio.SetVolume(before < 0 ? 50 : before);
            return $"{after}% (вернул {before}%)";
        });
        Check("звук: mute", () =>
        {
            bool ok = Audio.SetMute(true);
            bool muted = Audio.GetMute();
            Audio.SetMute(false);
            return ok && muted ? "работает" : "не работает";
        });

        Check("яркость: чтение", () =>
        {
            int b = Brightness.Get();
            return b < 0 ? "монитор не поддерживает DDC/CI" : b + "%";
        });
        Check("яркость: установка", () =>
        {
            int before = Brightness.Get();
            if (before < 0) return "пропущено (нет поддержки)";
            bool ok = Brightness.Set(Math.Max(10, before - 10));
            Brightness.Set(before);
            return ok ? "работает" : "не удалось";
        });

        Check("скриншот", () =>
        {
            var file = SystemActions.Screenshot();
            return File.Exists(file) ? file : "файл не создан";
        });

        Check("буфер обмена", () =>
        {
            Clipboard.SetText("Джарвис: проверка буфера");
            return Clipboard.GetText();
        });

        Check("модель распознавания", () =>
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "model");
            var models = SttEngine.ListModels(dir);
            if (models.Count == 0) return "модель не найдена ни в папке model, ни внутри программы";
            var chosen = models.FirstOrDefault(m => m.Embedded) ?? models[0];
            using var stt = new SttEngine(dir, "ru");
            stt.Load(chosen.File);
            if (!stt.Ready) return "ошибка: " + stt.LoadError;
            return chosen.File + " загружена" + (chosen.Embedded ? " (встроенная)" : "");
        });

        Check("погода", () => WebTools.WeatherAsync("Москва").GetAwaiter().GetResult());
        Check("курс валют", () => WebTools.CurrencyAsync("USD").GetAwaiter().GetResult());

        Check("калькулятор: 15 + 7", () => Calc.TrySolve("сколько будет 15 плюс 7", out var r) ? r : "не посчитал");
        Check("калькулятор: 15% от 200", () => Calc.TrySolve("15 процентов от 200", out var r) ? r : "не посчитал");
        Check("калькулятор: двадцать на три", () => Calc.TrySolve("посчитай двадцать умножить на три", out var r) ? r : "не посчитал");

        Check("парсер: открой ютуб", () => Describe(Parser.Parse("открой ютуб")));
        Check("парсер: таймер на 10 минут", () => Describe(Parser.Parse("таймер на 10 минут")));
        Check("парсер: напомни через час позвонить", () => Describe(Parser.Parse("напомни через час позвонить")));
        Check("парсер: громкость 40", () => Describe(Parser.Parse("громкость 40")));
        Check("парсер: мусор", () => Describe(Parser.Parse("бла бла бла")));

        var path = Path.Combine(Path.GetTempPath(), "jarvis-selftest.txt");
        File.WriteAllText(path, string.Join(Environment.NewLine, report), new UTF8Encoding(true));
    }

    static string Describe(Intent i) => i == null
        ? "NONE"
        : $"{i.Kind}, Arg={i.Arg ?? "-"}, Number={i.Number}, Say={i.Say ?? "-"}";
}
