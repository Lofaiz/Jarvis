// Глобальные хоткеи. Окно-получатель сообщений — скрытое окно без UI: система сама шлёт ему WM_HOTKEY, клавиши работают даже когда Джарвис спрятан в трей.
using System.Runtime.InteropServices;

namespace Jarvis;

class HotkeyWindow : NativeWindow, IDisposable
{
    public const int WM_HOTKEY = 0x0312;
    const int ID_VOICE = 0x7A01;
    const int ID_WINDOW = 0x7A02;
    const uint MOD_NOREPEAT = 0x4000; 

    public event Action VoicePressed;
    public event Action WindowPressed;

    public HotkeyWindow()
    {
        CreateHandle(new CreateParams { Caption = "JarvisHotkeys" });
    }

    public bool RegisterVoice(int mods, int vk) =>
        RegisterHotKey(Handle, ID_VOICE, (uint)mods | MOD_NOREPEAT, (uint)vk);
    public bool RegisterWindow(int mods, int vk) =>
        RegisterHotKey(Handle, ID_WINDOW, (uint)mods | MOD_NOREPEAT, (uint)vk);
    public void UnregisterVoice() { try { UnregisterHotKey(Handle, ID_VOICE); } catch { } }
    public void UnregisterWindow() { try { UnregisterHotKey(Handle, ID_WINDOW); } catch { } }
    public void UnregisterAll() { UnregisterVoice(); UnregisterWindow(); }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY)
        {
            int id = m.WParam.ToInt32();
            if (id == ID_VOICE) VoicePressed?.Invoke();
            else if (id == ID_WINDOW) WindowPressed?.Invoke();
        }
        base.WndProc(ref m);
    }

    public void Dispose()
    {
        UnregisterAll();
        try { DestroyHandle(); } catch { }
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
