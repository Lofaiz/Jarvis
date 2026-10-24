// Имитация клавиатуры: медиа-клавиши, Alt+F4, Win+D, Alt+Tab, Ctrl+V.
using System.Runtime.InteropServices;

namespace Jarvis;

static class InputSim
{
    const uint KEYUP = 0x0002;

    [DllImport("user32.dll")]
    static extern void keybd_event(byte vk, byte scan, uint flags, UIntPtr extra);

    static void Down(Keys k) => keybd_event((byte)k, 0, 0, UIntPtr.Zero);
    static void Up(Keys k) => keybd_event((byte)k, 0, KEYUP, UIntPtr.Zero);

    public static void Key(Keys k)
    {
        Down(k);
        Thread.Sleep(30);
        Up(k);
    }

    public static void Chord(params Keys[] keys)
    {
        foreach (var k in keys) { Down(k); Thread.Sleep(20); }
        for (int i = keys.Length - 1; i >= 0; i--) { Up(keys[i]); Thread.Sleep(20); }
    }

    public static void PlayPause() => Key(Keys.MediaPlayPause);
    public static void NextTrack() => Key(Keys.MediaNextTrack);
    public static void PrevTrack() => Key(Keys.MediaPreviousTrack);
    public static void StopMedia() => Key(Keys.MediaStop);

    public static void CloseWindow() => Chord(Keys.Menu, Keys.F4);            // Alt+F4
    public static void ShowDesktop() => Chord(Keys.LWin, Keys.D);             // Win+D
    public static void SwitchWindow() { Chord(Keys.Menu, Keys.Tab); }         // Alt+Tab
}
