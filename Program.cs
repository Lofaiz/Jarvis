// Джарвис — автономный голосовой помощник. Работает целиком локально: распознавание речи (Whisper.net) и правила команд.
using System.Threading;

namespace Jarvis;

static class Program
{
    const string MutexName = @"Local\Jarvis.SingleInstance";
    const string ShowEventName = @"Local\Jarvis.Show";

    [STAThread]
    static void Main(string[] args)
    {
        NativeRuntime.Configure();

        // самопроверка компонентов без запуска окна
        if (args.Contains("--selftest"))
        {
            SelfTest.Run();
            return;
        }

        // диагностика: --stt-file <wav> — только распознать файл, без выполнения команды
        var sttIdx = Array.IndexOf(args, "--stt-file");
        if (sttIdx >= 0 && sttIdx + 1 < args.Length)
        {
            var outFile = Path.Combine(Path.GetTempPath(), "jarvis-stt.txt");
            try
            {
                var dir = Path.Combine(AppContext.BaseDirectory, "model");
                var models = SttEngine.ListModels(dir);
                if (models.Count == 0)
                {
                    File.WriteAllText(outFile, "NO MODEL", new System.Text.UTF8Encoding(true));
                    return;
                }
                var chosen = models.FirstOrDefault(m => m.Embedded) ?? models[0];
                using var engine = new SttEngine(dir, "ru");
                engine.Load(chosen.File);
                var text = engine.RecognizeFile(args[sttIdx + 1]);
                File.WriteAllText(outFile, text, new System.Text.UTF8Encoding(true));
            }
            catch (Exception ex)
            {
                File.WriteAllText(outFile, "ERROR: " + ex.Message, new System.Text.UTF8Encoding(true));
            }
            return;
        }

        // диагностика без окна: --parse "фраза" — разобрать команду и записать результат
        var parseIdx = Array.IndexOf(args, "--parse");
        if (parseIdx >= 0 && parseIdx + 1 < args.Length)
        {
            Parser.SetExtraApps(AppConfig.Load(
                Path.Combine(AppContext.BaseDirectory, "config.json")).extraApps);
            var parsed = Parser.Parse(args[parseIdx + 1]);
            var outFile = Path.Combine(Path.GetTempPath(), "jarvis-parse.txt");
            var text = parsed == null
                ? "NONE"
                : $"Kind={parsed.Kind}; Arg={parsed.Arg}; Number={parsed.Number}; HasNumber={parsed.HasNumber}; Say={parsed.Say}";
            File.WriteAllText(outFile, text, new System.Text.UTF8Encoding(true));
            return;
        }

        using var mutex = new Mutex(true, MutexName, out bool first);
        if (!first)
        {
            try { using var ev = EventWaitHandle.OpenExisting(ShowEventName); ev.Set(); } catch { }
            return;
        }

        ApplicationConfiguration.Initialize();

        var form = new MainForm(args);
        var ctx = new ApplicationContext();
        ctx.MainForm = form;

        using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        var listener = new Thread(() =>
        {
            while (true)
            {
                showEvent.WaitOne();
                try { form.BeginInvoke(form.ToggleWindow); } catch { break; }
            }
        }) { IsBackground = true };
        listener.Start();

        Application.Run(ctx);
    }
}
