// В сборке «один exe» нативные библиотеки Whisper .NET распаковывает во временную папку вида %TEMP%\.net\Jarvis\<hash>\runtimes\win-x64. 
// Штатный загрузчик Whisper.net ищет их рядом с exe и сам эту папку не находит — подсказываем путь.
using Whisper.net.LibraryLoader;

namespace Jarvis;

static class NativeRuntime
{
    public static void Configure()
    {
        try
        {
            // Обычная сборка папкой: рядом с exe уже есть runtimes — ничего не делаем.
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "native", "whisper.dll")) ||
                File.Exists(Path.Combine(AppContext.BaseDirectory, "runtimes", "win-x64", "whisper.dll")))
                return;

            foreach (var root in Roots())
            {
                if (!Directory.Exists(root)) continue;

                string best = null;
                DateTime bestTime = DateTime.MinValue;
                foreach (var dir in Directory.GetDirectories(root))
                {
                    var dll = Path.Combine(dir, "runtimes", "win-x64", "whisper.dll");
                    if (!File.Exists(dll)) continue;
                    var time = File.GetLastWriteTimeUtc(dll);
                    if (time > bestTime) { best = dll; bestTime = time; }
                }

                if (best != null)
                {
                    // LibraryPath — путь к файлу; загрузчик берёт его папку как корень поиска.
                    var extractDir = Path.GetFullPath(Path.Combine(best, "..", "..", ".."));
                    RuntimeOptions.LibraryPath = Path.Combine(extractDir, "Jarvis.exe");
                    return;
                }
            }
        }
        catch { }
    }

    static IEnumerable<string> Roots()
    {
        var custom = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR");
        if (!string.IsNullOrWhiteSpace(custom))
            yield return Path.Combine(custom, AppName());

        yield return Path.Combine(Path.GetTempPath(), ".net", AppName());
        yield return Path.Combine(Path.GetTempPath(), ".net", "Jarvis");
    }

    static string AppName()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) return Path.GetFileNameWithoutExtension(exe);
        }
        catch { }
        return "Jarvis";
    }
}
