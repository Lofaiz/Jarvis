// Распознавание речи целиком внутри приложения: запись микрофона (NAudio,
// WASAPI) и Whisper (whisper.cpp через Whisper.net). Никаких внешних программ.
// Логика повторяет SttEngine из Милли, но без зависимости от OpenCode и Lang.
using System.Text;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Whisper.net;

namespace Jarvis;

class SttEngine : IDisposable
{
    readonly string modelDir;

    // Модель, встроенная в сборку (вариант «один exe»).
    public const string EmbeddedModelName = "ggml-base-q5_1.bin";
    const string EmbeddedModelResource = "Jarvis.Resources.ggml-base-q5_1.bin";

    WhisperFactory factory;
    WasapiCapture capture;
    MemoryStream pcm;
    WaveFormat deviceFormat;
    DateTime startedAt;
    int stopping;
    int autoStop;
    readonly object loadLock = new();
    byte[] embeddedBytes;

    public string Language { get; set; } = "ru";

    public bool Ready { get; private set; }
    public string LoadError { get; private set; }
    public bool Recording => capture != null;
    public bool Busy { get; private set; }
    public string ModelFile { get; private set; }

    public event Action<string> Recognized;
    public event Action<string> Failed;

    public SttEngine(string modelDir, string language)
    {
        this.modelDir = modelDir;
        Language = language;
    }

    public class ModelChoice
    {
        public string File;
        public long Size;
        public string Note;
        public bool Embedded;
    }

    public static bool EmbeddedModelAvailable
    {
        get
        {
            try
            {
                using var s = typeof(SttEngine).Assembly.GetManifestResourceStream(EmbeddedModelResource);
                return s != null;
            }
            catch { return false; }
        }
    }

    public static long EmbeddedModelSize
    {
        get
        {
            try
            {
                using var s = typeof(SttEngine).Assembly.GetManifestResourceStream(EmbeddedModelResource);
                return s?.Length ?? 0;
            }
            catch { return 0; }
        }
    }

    public static List<ModelChoice> ListModels(string dir)
    {
        var list = new List<ModelChoice>();
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (Directory.Exists(dir))
            {
                foreach (var f in Directory.GetFiles(dir, "ggml-*.bin"))
                {
                    var name = Path.GetFileName(f);
                    names.Add(name);
                    list.Add(new ModelChoice
                    {
                        File = name,
                        Size = new FileInfo(f).Length,
                        Note = Describe(name),
                    });
                }
            }
        }
        catch { }

        if (EmbeddedModelAvailable && !names.Contains(EmbeddedModelName))
        {
            list.Add(new ModelChoice
            {
                File = EmbeddedModelName,
                Size = EmbeddedModelSize,
                Note = Describe(EmbeddedModelName) + ", встроена в программу",
                Embedded = true,
            });
        }

        list.Sort((a, b) => a.Size.CompareTo(b.Size));
        return list;
    }

    public static string Describe(string file)
    {
        var n = (file ?? "").ToLowerInvariant();
        if (n.Contains("tiny")) return "самая быстрая, низкая точность";
        if (n.Contains("base")) return "быстрая, средняя точность";
        if (n.Contains("small")) return "точная, но медленнее";
        if (n.Contains("medium")) return "очень точная, требует мощного CPU";
        if (n.Contains("large")) return "максимальная точность, очень медленная";
        return "";
    }

    public static string ShortName(string file)
    {
        var n = (file ?? "").Replace("ggml-", "").Replace(".bin", "");
        var i = n.IndexOf('-');
        return i > 0 ? n[..i] : n;
    }

    public void Load(string fileName = null)
    {
        lock (loadLock)
        {
            if (!string.IsNullOrEmpty(fileName)) ModelFile = fileName;
            if (string.IsNullOrEmpty(ModelFile) && EmbeddedModelAvailable) ModelFile = EmbeddedModelName;
            try
            {
                var path = Path.Combine(modelDir, ModelFile ?? "");
                WhisperFactory fresh;
                if (File.Exists(path))
                {
                    fresh = WhisperFactory.FromPath(path);
                }
                else if (string.Equals(ModelFile, EmbeddedModelName, StringComparison.OrdinalIgnoreCase) &&
                         EmbeddedModelAvailable)
                {
                    embeddedBytes ??= ReadEmbeddedModel();
                    fresh = WhisperFactory.FromBuffer(new Memory<byte>(embeddedBytes));
                }
                else
                {
                    Ready = false;
                    LoadError = $"Модель «{ModelFile}» не найдена: нет файла в папке model и нет встроенной модели.";
                    return;
                }
                var old = factory;
                factory = fresh;
                Ready = true;
                LoadError = null;
                try { if (!Busy) old?.Dispose(); } catch { }
            }
            catch (Exception ex)
            {
                Ready = false;
                LoadError = ex.Message;
            }
        }
    }

    static byte[] ReadEmbeddedModel()
    {
        using var s = typeof(SttEngine).Assembly.GetManifestResourceStream(EmbeddedModelResource)
            ?? throw new InvalidOperationException("Встроенная модель не найдена в сборке.");
        var data = new byte[s.Length];
        int read = 0;
        while (read < data.Length)
        {
            int n = s.Read(data, read, data.Length - read);
            if (n <= 0) break;
            read += n;
        }
        return data;
    }

    public bool Start()
    {
        if (capture != null || !Ready) return false;
        try
        {
            var cap = new WasapiCapture();
            pcm = new MemoryStream();
            deviceFormat = cap.WaveFormat;
            startedAt = DateTime.Now;
            stopping = 0;
            Interlocked.Exchange(ref autoStop, 0);
            cap.DataAvailable += OnData;
            cap.RecordingStopped += OnStopped;
            cap.StartRecording();
            capture = cap;
            Busy = true;
            return true;
        }
        catch (Exception ex)
        {
            capture = null;
            pcm = null;
            Failed?.Invoke($"Не удалось открыть микрофон: {ex.Message}");
            return false;
        }
    }

    public void Stop()
    {
        var cap = capture;
        if (cap == null) return;
        if (Interlocked.Exchange(ref stopping, 1) != 0) return;
        try { cap.StopRecording(); } catch { }
    }

    public void Abort()
    {
        var cap = capture;
        capture = null;
        if (cap != null)
        {
            try { cap.DataAvailable -= OnData; cap.RecordingStopped -= OnStopped; } catch { }
            try { cap.StopRecording(); } catch { }
            try { cap.Dispose(); } catch { }
        }
        try { pcm?.Dispose(); } catch { }
        pcm = null;
        Busy = false;
        Interlocked.Exchange(ref stopping, 0);
    }

    void OnData(object sender, WaveInEventArgs e)
    {
        var buf = pcm;
        if (buf == null) return;
        try { buf.Write(e.Buffer, 0, e.BytesRecorded); } catch { }
        // записи дольше 60 с быть не может: останавливаемся сами
        if ((DateTime.Now - startedAt).TotalSeconds > 60 && Interlocked.Exchange(ref autoStop, 1) == 0)
            Task.Run(Stop);
    }

    void OnStopped(object sender, StoppedEventArgs e)
    {
        var cap = capture;
        capture = null;
        if (cap != null)
        {
            try { cap.DataAvailable -= OnData; cap.RecordingStopped -= OnStopped; } catch { }
            try { cap.Dispose(); } catch { }
        }

        var bytes = Array.Empty<byte>();
        var fmt = deviceFormat;
        try { if (pcm != null) bytes = pcm.ToArray(); } catch { }
        try { pcm?.Dispose(); } catch { }
        pcm = null;
        Interlocked.Exchange(ref stopping, 0);

        var stopError = e?.Exception;
        Task.Run(() =>
        {
            try
            {
                if (stopError != null) throw stopError;
                var text = Recognize(bytes, fmt);
                Busy = false;
                Recognized?.Invoke(text);
            }
            catch (Exception ex)
            {
                Busy = false;
                Failed?.Invoke($"Ошибка распознавания: {ex.Message}");
            }
        });
    }

    // устройство может писать 44-48 кГц стерео во float — приводим к 16 кГц моно
    string Recognize(byte[] bytes, WaveFormat fmt)
    {
        if (bytes.Length == 0) return "";
        float[] samples;
        using (var ms = new MemoryStream(bytes))
        using (var src = new RawSourceWaveStream(ms, fmt))
        using (var resampler = new MediaFoundationResampler(src, new WaveFormat(16000, 1)))
        {
            var list = new List<float>(bytes.Length / 4 + 1024);
            var buf = new byte[16384];
            int n;
            while ((n = resampler.Read(buf.AsSpan())) > 0)
            {
                for (int i = 0; i + 1 < n; i += 2)
                {
                    short s = (short)(buf[i] | (buf[i + 1] << 8));
                    list.Add(s / 32768f);
                }
            }
            samples = list.ToArray();
        }
        // короче 0.25 с — это щелчок клавиши, а не речь
        if (samples.Length < 4000) return "";

        var f = factory;
        if (f == null) throw new InvalidOperationException("Модель распознавания не загружена.");

        var threads = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));

        var sb = new StringBuilder();
        using var processor = f.CreateBuilder()
            .WithLanguage(Language)
            .WithThreads(threads)
            .WithSegmentEventHandler(seg => sb.Append(seg.Text))
            .Build();
        processor.Process(samples);
        return sb.ToString().Trim();
    }

    public string RecognizeFile(string path)
    {
        using var reader = new WaveFileReader(path);
        var fmt = reader.WaveFormat;
        using var ms = new MemoryStream();
        reader.CopyTo(ms);
        return Recognize(ms.ToArray(), fmt);
    }

    public void Dispose()
    {
        Abort();
        try { factory?.Dispose(); } catch { }
        factory = null;
        Ready = false;
    }
}
