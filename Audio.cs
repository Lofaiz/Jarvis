// Громкость через NAudio (CoreAudio): точное значение в процентах и mute.
using NAudio.CoreAudioApi;

namespace Jarvis;

static class Audio
{
    static MMDevice Device(MMDeviceEnumerator en) =>
        en.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

    public static int GetVolume()
    {
        try
        {
            var en = new MMDeviceEnumerator();
            var dev = Device(en);
            return (int)Math.Round(dev.AudioEndpointVolume.MasterVolumeLevelScalar * 100);
        }
        catch { return -1; }
    }

    public static bool GetMute()
    {
        try
        {
            var en = new MMDeviceEnumerator();
            return Device(en).AudioEndpointVolume.Mute;
        }
        catch { return false; }
    }

    public static bool SetVolume(int percent)
    {
        try
        {
            percent = Math.Clamp(percent, 0, 100);
            var en = new MMDeviceEnumerator();
            var dev = Device(en);
            dev.AudioEndpointVolume.MasterVolumeLevelScalar = percent / 100f;
            if (percent > 0 && dev.AudioEndpointVolume.Mute) dev.AudioEndpointVolume.Mute = false;
            return true;
        }
        catch { return false; }
    }

    public static bool SetMute(bool mute)
    {
        try
        {
            var en = new MMDeviceEnumerator();
            Device(en).AudioEndpointVolume.Mute = mute;
            return true;
        }
        catch { return false; }
    }
}
