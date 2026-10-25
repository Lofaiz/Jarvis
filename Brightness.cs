// Яркость монитора через DDC/CI (dxva2.dll). Работает на большинстве внешних мониторов; на некоторых ноутбучных панелях команда просто не поддерживается.
using System.Runtime.InteropServices;

namespace Jarvis;

static class Brightness
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }

    delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, MonitorEnumProc callback, IntPtr data);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint count,
        [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool DestroyPhysicalMonitors(uint count, [In] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool GetMonitorBrightness(IntPtr hMonitor, out uint min, out uint current, out uint max);

    [DllImport("dxva2.dll", SetLastError = true)]
    static extern bool SetMonitorBrightness(IntPtr hMonitor, uint value);

    static void ForEachMonitor(Func<IntPtr, bool> action)
    {
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMon, IntPtr hdc, ref RECT r, IntPtr data) =>
        {
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMon, out uint count) || count == 0) return true;
            var monitors = new PHYSICAL_MONITOR[count];
            if (!GetPhysicalMonitorsFromHMONITOR(hMon, count, monitors)) return true;
            try { foreach (var m in monitors) action(m.hPhysicalMonitor); }
            finally { DestroyPhysicalMonitors(count, monitors); }
            return true;
        }, IntPtr.Zero);
    }

    public static int Get()
    {
        int result = -1;
        ForEachMonitor(h =>
        {
            if (result >= 0) return false;
            if (GetMonitorBrightness(h, out uint min, out uint cur, out uint max) && max > min)
                result = (int)Math.Round((cur - min) * 100.0 / (max - min));
            return true;
        });
        return result;
    }

    public static bool Set(int percent)
    {
        percent = Math.Clamp(percent, 0, 100);
        bool ok = false;
        ForEachMonitor(h =>
        {
            if (!GetMonitorBrightness(h, out uint min, out _, out uint max) || max <= min) return true;
            uint value = (uint)Math.Round(min + (max - min) * percent / 100.0);
            if (SetMonitorBrightness(h, value)) ok = true;
            return true;
        });
        return ok;
    }
}
