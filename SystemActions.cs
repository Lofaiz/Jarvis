// Системные действия: блокировка, сон, выключение, скриншот, буфер обмена.
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Jarvis;

static class SystemActions
{
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool LockWorkStation();

    [DllImport("powrprof.dll", SetLastError = true)]
    static extern bool SetSuspendState(bool hibernate, bool forceCritical, bool disableWakeEvent);

    public static bool Lock() => LockWorkStation();

    public static bool Sleep() => SetSuspendState(false, false, false);

    static bool Run(string file, string args)
    {
        try
        {
            var psi = new ProcessStartInfo(file, args)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            using var p = Process.Start(psi);
            if (p == null) return false;
            p.WaitForExit(5000);
            return p.HasExited && p.ExitCode == 0;
        }
        catch { return false; }
    }

    public static bool Shutdown(int seconds) => Run("shutdown.exe", $"/s /t {seconds} /c \"Джарвис: выключение по голосовой команде\"");
    public static bool Restart(int seconds) => Run("shutdown.exe", $"/r /t {seconds} /c \"Джарвис: перезагрузка по голосовой команде\"");
    public static bool CancelShutdown() => Run("shutdown.exe", "/a");

    // Снимок всех мониторов сразу, файл в «Изображения\Скриншоты».
    public static string Screenshot()
    {
        var screen = SystemInformation.VirtualScreen;
        if (screen.Width <= 0 || screen.Height <= 0) throw new InvalidOperationException("не удалось определить размер экрана");

        using var bmp = new Bitmap(screen.Width, screen.Height, PixelFormat.Format24bppRgb);
        using (var g = Graphics.FromImage(bmp))
            g.CopyFromScreen(screen.Left, screen.Top, 0, 0, bmp.Size, CopyPixelOperation.SourceCopy);

        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Скриншоты");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "Джарвис_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + ".png");
        bmp.Save(file, ImageFormat.Png);
        return file;
    }

    // Вставка текста: в буфер обмена, затем Ctrl+V в активное окно.
    public static bool InsertText(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        try
        {
            Clipboard.SetText(text);
            Thread.Sleep(60);
            InputSim.Chord(Keys.ControlKey, Keys.V);
            return true;
        }
        catch { return false; }
    }
}
