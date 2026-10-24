// Главное окно и вся логика Джарвиса: трей, глобальные клавиши, голосовой ввод,
// выполнение команд. Никакой сети, кроме погоды и курса валют, никакого ИИ.
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Jarvis;

class MainForm : Form
{
    internal AppConfig Cfg;
    readonly string configPath;

    SttEngine stt;
    HotkeyWindow hotkeys;
    internal HotKeyDef HkVoice, HkWindow;
    internal bool HotkeyOk, WindowOk;

    NotifyIcon tray;
    RichTextBox chat;
    TextBox input;
    Label status;
    Button btnSend, btnStop, btnVoice, btnSettings;
    System.Windows.Forms.Timer holdTimer, remTimer;

    readonly ReminderList reminders = new();

    bool exiting;
    bool holdMode = true, pendingVoice, voiceWarned, voiceHeld, voiceByKey;
    string lastReply;
    Icon appIcon;
    Font boldFont, regularFont;
    bool firstShowHandled;

    internal string ModelDir => Path.Combine(AppContext.BaseDirectory, "model");
    internal bool SttReady => stt != null && stt.Ready;

    public MainForm(string[] args)
    {
        configPath = Path.Combine(AppContext.BaseDirectory, "config.json");
        Cfg = AppConfig.Load(configPath);
        Parser.SetExtraApps(Cfg.extraApps);

        appIcon = LoadIcon();
        Icon = appIcon;

        Text = "Джарвис";
        Size = new Size(520, 620);
        MinimumSize = new Size(420, 420);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Bg;
        ForeColor = Theme.Text;
        KeyPreview = true;

        BuildUi();

        // ---------------------------------------------------------- хоткеи
        HkVoice = KeyMap.Parse(Cfg.hotkeyVoice) ?? KeyMap.Parse("Num9");
        HkWindow = KeyMap.Parse(Cfg.hotkeyWindow) ?? KeyMap.Parse("Num8");
        Cfg.hotkeyVoice = HkVoice.Text;
        Cfg.hotkeyWindow = HkWindow.Text;

        hotkeys = new HotkeyWindow();
        hotkeys.VoicePressed += () => VoiceDown(true);
        hotkeys.WindowPressed += ToggleWindow;
        HotkeyOk = ApplyHotkey("voice");
        WindowOk = ApplyHotkey("window");

        // ---------------------------------------------------------- трей
        tray = new NotifyIcon
        {
            Icon = appIcon,
            Text = "Джарвис",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu(),
        };
        tray.DoubleClick += (s, e) => ShowWindow();
        tray.BalloonTipClicked += (s, e) => ShowWindow();

        // ---------------------------------------------------------- таймеры
        holdTimer = new System.Windows.Forms.Timer { Interval = 120 };
        holdTimer.Tick += (s, e) => HoldTick();
        holdTimer.Start();

        remTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        remTimer.Tick += (s, e) => CheckReminders();
        remTimer.Start();

        // ---------------------------------------------------------- распознавание
        var models = SttEngine.ListModels(ModelDir);
        var wanted = models.FirstOrDefault(m => m.File == Cfg.sttModel)?.File ?? models.FirstOrDefault()?.File;
        if (wanted != null)
        {
            if (wanted != Cfg.sttModel) { Cfg.sttModel = wanted; SaveConfigQuiet(); }
            stt = new SttEngine(ModelDir, "ru");
            stt.Recognized += text => UI(() => OnRecognized(text));
            stt.Failed += msg => UI(() => OnVoiceFailed(msg));
            var file = wanted;
            Task.Run(() => stt.Load(file));
        }

        // ---------------------------------------------------------- запуск
        var _ = Handle;

        if (!Cfg.startHidden || AppConfig.FirstRun) ShowWindow();
        if (AppConfig.FirstRun)
            Balloon($"Я готов. {HkWindow.Text} — показать окно, {HkVoice.Text} — говорить (удерживай).",
                ToolTipIcon.Info, 6000);
        if (!HotkeyOk || !WindowOk)
        {
            var busy = new List<string>();
            if (!HotkeyOk) busy.Add(HkVoice.Text);
            if (!WindowOk) busy.Add(HkWindow.Text);
            Balloon("Клавиши заняты другой программой: " + string.Join(", ", busy) +
                    ". Назначь другие в настройках.", ToolTipIcon.Warning, 8000);
        }

        // диагностика: --say-file <wav> — распознать файл как голосовую команду
        var sayIdx = Array.IndexOf(args, "--say-file");
        if (sayIdx >= 0 && sayIdx + 1 < args.Length && stt != null)
        {
            var file = args[sayIdx + 1];
            Task.Run(() =>
            {
                for (int i = 0; i < 300 && !stt.Ready; i++) Thread.Sleep(100);
                try { var text = stt.RecognizeFile(file); UI(() => OnRecognized(text)); }
                catch (Exception ex) { UI(() => OnVoiceFailed(ex.Message)); }
            });
        }
    }

    // ------------------------------------------------------------------ иконка
    static Icon LoadIcon()
    {
        try
        {
            using var s = typeof(MainForm).Assembly.GetManifestResourceStream("Jarvis.Resources.jarvis.ico");
            if (s != null) return new Icon(s);
        }
        catch { }
        try
        {
            var p = Path.Combine(AppContext.BaseDirectory, "jarvis.ico");
            if (File.Exists(p)) return new Icon(p);
        }
        catch { }
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe)) return Icon.ExtractAssociatedIcon(exe);
        }
        catch { }
        return SystemIcons.Application;
    }

    // ------------------------------------------------------------------ интерфейс
    void BuildUi()
    {
        boldFont = new Font("Segoe UI", 10, FontStyle.Bold);
        regularFont = new Font("Segoe UI", 10);

        var header = new Panel { Dock = DockStyle.Top, Height = 46, BackColor = Theme.Bg };
        var title = new Label
        {
            Text = "ДЖАРВИС",
            Font = new Font("Segoe UI Semibold", 14),
            ForeColor = Theme.Accent,
            AutoSize = true,
            Location = new Point(12, 10),
        };
        var sub = new Label
        {
            Text = "локальный голосовой помощник",
            Font = new Font("Segoe UI", 8),
            ForeColor = Theme.Dim,
            AutoSize = true,
            Location = new Point(14, 33),
        };
        header.Controls.Add(title);
        header.Controls.Add(sub);

        chat = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = new Font("Segoe UI", 10),
            HideSelection = false,
            Margin = new Padding(8, 4, 8, 4),
        };

        input = new TextBox
        {
            Multiline = true,
            Dock = DockStyle.Fill,
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.FixedSingle,
            Font = new Font("Segoe UI", 10),
            Margin = new Padding(8, 4, 8, 0),
            ScrollBars = ScrollBars.None,
        };
        input.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && !e.Control && !e.Alt && !e.Shift)
            {
                e.SuppressKeyPress = true;
                SendCurrent();
            }
        };

        var btnRow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Theme.Bg,
            Padding = new Padding(6),
        };

        btnSend = Theme.DarkButton("Отправить");
        btnStop = Theme.DarkButton("Стоп");
        btnVoice = Theme.DarkButton("Говорить");
        btnSettings = Theme.DarkButton("Настройки");

        status = new Label
        {
            Text = "Готов.",
            AutoSize = false,
            Dock = DockStyle.Fill,
            ForeColor = Theme.Dim,
            BackColor = Theme.Bg,
            Padding = new Padding(10, 0, 8, 6),
            TextAlign = ContentAlignment.MiddleLeft,
            Height = 24,
        };

        var tips = new ToolTip { AutoPopDelay = 8000, InitialDelay = 400, ReshowDelay = 200 };
        tips.SetToolTip(btnSend, "Выполнить команду из поля ввода");
        tips.SetToolTip(btnStop, "Прекратить запись");
        tips.SetToolTip(btnVoice, "Удерживай и говори. Отпусти — Джарвис выполнит команду");
        tips.SetToolTip(btnSettings, "Модель распознавания, клавиши, город");

        btnSend.Click += (s, e) => SendCurrent();
        btnStop.Click += (s, e) => StopAll();
        btnVoice.MouseDown += (s, e) => VoiceDown(false);
        btnVoice.MouseUp += (s, e) => VoiceUp();
        btnSettings.Click += (s, e) => ShowSettings();

        btnRow.Controls.Add(btnSend);
        btnRow.Controls.Add(btnStop);
        btnRow.Controls.Add(btnVoice);
        btnRow.Controls.Add(btnSettings);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            BackColor = Theme.Bg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 74));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(chat, 0, 0);
        root.Controls.Add(input, 0, 1);
        root.Controls.Add(btnRow, 0, 2);
        root.Controls.Add(status, 0, 3);

        Controls.Add(root);
        Controls.Add(header);

        AppendChat("Джарвис", "Слушаю. Скажи «что ты умеешь» — расскажу про команды.", Theme.Accent);
    }

    ContextMenuStrip BuildTrayMenu()
    {
        var menu = new ContextMenuStrip
        {
            Renderer = new DarkRenderer(),
            BackColor = Theme.Panel,
            ForeColor = Theme.Text,
        };

        var show = new ToolStripMenuItem("Показать окно");
        show.Click += (s, e) => ShowWindow();

        var voice = new ToolStripMenuItem($"Говорить ({Cfg.hotkeyVoice})");
        voice.Click += (s, e) => TrayVoice();

        var stop = new ToolStripMenuItem("Остановить запись");
        stop.Click += (s, e) => StopAll();

        var auto = new ToolStripMenuItem("Сразу выполнять распознанное")
        {
            CheckOnClick = true,
            Checked = Cfg.autoSendVoice,
        };
        auto.CheckedChanged += (s, e) => { Cfg.autoSendVoice = auto.Checked; SaveConfigQuiet(); };

        var settings = new ToolStripMenuItem("Настройки…");
        settings.Click += (s, e) => ShowSettings();

        var exit = new ToolStripMenuItem("Выход");
        exit.Click += (s, e) => ExitApp();

        menu.Items.Add(show);
        menu.Items.Add(stop);
        menu.Items.Add(voice);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(auto);
        menu.Items.Add(settings);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exit);

        foreach (ToolStripItem it in menu.Items) it.ForeColor = Theme.Text;
        return menu;
    }

    protected override void SetVisibleCore(bool value)
    {
        if (!firstShowHandled)
        {
            firstShowHandled = true;
            if (Cfg.startHidden) value = false;
        }
        base.SetVisibleCore(value);
    }

    internal void ShowWindow()
    {
        if (!Visible) Show();
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        Activate();
        try { input.Focus(); } catch { }
        SetDarkTitle();
    }

    internal void ToggleWindow()
    {
        if (Visible && WindowState == FormWindowState.Minimized) { ShowWindow(); return; }
        if (Visible && ContainsFocus) Hide(); else ShowWindow();
    }

    internal void Balloon(string text, ToolTipIcon icon = ToolTipIcon.Info, int ms = 5000)
    {
        try { tray.ShowBalloonTip(ms, "Джарвис", text, icon); } catch { }
    }

    void SetDarkTitle()
    {
        try { int v = 1; DwmSetWindowAttribute(Handle, 20, ref v, 4); } catch { }
    }

    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        SetDarkTitle();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!exiting && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnFormClosing(e);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        try { input.Focus(); } catch { }
    }

    void UI(Action action)
    {
        if (IsDisposed) return;
        try { if (InvokeRequired) BeginInvoke(action); else action(); } catch { }
    }

    // ------------------------------------------------------------------ голос
    internal void VoiceDown(bool byKey)
    {
        voiceHeld = true;
        voiceByKey = byKey;
        StartVoice(true, byKey);
    }

    internal void VoiceUp()
    {
        voiceHeld = false;
        if (holdMode) StopVoice();
    }

    internal void StartVoice(bool hold, bool byKey = true)
    {
        if (stt == null || !stt.Ready)
        {
            WarnVoiceUnavailable();
            return;
        }
        if (stt.Busy)
        {
            pendingVoice = true;
            voiceByKey = byKey;
            return;
        }

        holdMode = hold;
        voiceByKey = byKey;
        if (stt.Start())
        {
            SetStatus("Слушаю… отпусти клавишу, когда закончишь.");
            btnVoice.Text = "● Идёт запись";
        }
    }

    void StopVoice()
    {
        if (stt == null || !stt.Recording) { pendingVoice = false; return; }
        SetStatus("Распознаю…");
        btnVoice.Text = "🎤 Говорить";
        stt.Stop();
    }

    void TrayVoice()
    {
        if (stt != null && stt.Recording) { StopVoice(); return; }
        StartVoice(false);
    }

    void WarnVoiceUnavailable()
    {
        if (voiceWarned) return;
        voiceWarned = true;
        var msg = stt != null && stt.LoadError != null
            ? "Распознавание недоступно: " + stt.LoadError
            : "Не найдена модель распознавания в папке model.";
        Balloon(msg, ToolTipIcon.Warning, 8000);
        SetStatus("Распознавание недоступно");
    }

    void HoldTick()
    {
        if (stt == null) return;

        // отпустили клавишу — заканчиваем запись
        if (stt.Recording && holdMode && voiceByKey && !KeyState.Down(HkVoice.Vk))
            StopVoice();

        // ждали, пока освободится распознаватель
        if (pendingVoice && !stt.Busy && stt.Ready)
        {
            pendingVoice = false;
            if (voiceHeld || !holdMode) StartVoice(holdMode, voiceByKey);
        }
    }

    void OnRecognized(string text)
    {
        btnVoice.Text = "🎤 Говорить";
        if (string.IsNullOrWhiteSpace(text))
        {
            SetStatus("Не расслышал. Попробуй ещё раз.");
            return;
        }

        SetStatus("Распознано.");
        if (Cfg.autoSendVoice) SendText(text);
        else
        {
            input.Text = text;
            input.SelectionStart = input.TextLength;
            SetStatus("Текст распознан — нажми «Отправить».");
        }
    }

    void OnVoiceFailed(string msg)
    {
        btnVoice.Text = "🎤 Говорить";
        SetStatus("Ошибка распознавания");
        AppendChat("Ошибка", msg, Theme.Error);
    }

    // ------------------------------------------------------------------ команды
    void SendCurrent()
    {
        var t = input.Text;
        if (string.IsNullOrWhiteSpace(t)) return;
        input.Clear();
        SendText(t);
    }

    internal async void SendText(string text)
    {
        text = text.Trim();
        if (text.Length == 0) return;
        AppendChat("Вы", text, Theme.User);

        var intent = Parser.Parse(text);
        if (intent == null)
        {
            Reply("Не понял команду. Скажи «что ты умеешь» — покажу список.");
            return;
        }

        try { await RunCommand(intent); }
        catch (Exception ex) { Reply("Не получилось: " + ex.Message); }
    }

    async Task RunCommand(Intent i)
    {
        switch (i.Kind)
        {
            case Kind.Stop:
                SetStatus("Готов.");
                break;

            case Kind.Repeat:
                Reply(lastReply ?? "Мне нечего повторять.");
                break;

            case Kind.Help:
                Reply(HelpText);
                break;

            case Kind.Clear:
                chat.Clear();
                lastReply = null;
                SetStatus("История очищена.");
                break;

            case Kind.Exit:
                Reply("Выключаюсь.");
                await Task.Delay(500);
                ExitApp();
                break;

            case Kind.Time:
                Reply("Сейчас " + DateTime.Now.ToString("HH:mm") + ".");
                break;

            case Kind.Date:
                Reply(DateTime.Now.ToString("d MMMM yyyy года, dddd", new System.Globalization.CultureInfo("ru-RU")) + ".");
                break;

            case Kind.Weekday:
                Reply("Сегодня " + DateTime.Now.ToString("dddd", new System.Globalization.CultureInfo("ru-RU")) + ".");
                break;

            case Kind.OpenApp:
                if (TryStart(i.Arg)) Reply("Открываю " + (i.Say ?? i.Arg) + ".");
                else Reply($"Не нашёл «{i.Say ?? i.Arg}». Проверь путь в списке приложений.");
                break;

            case Kind.OpenSite:
                if (TryOpenUrl(i.Arg)) Reply("Открываю " + (i.Say ?? i.Arg) + ".");
                else Reply("Не удалось открыть ссылку.");
                break;

            case Kind.OpenFolder:
                if (Directory.Exists(i.Arg) && TryStart(i.Arg)) Reply("Открываю " + i.Say + ".");
                else Reply("Папка не найдена: " + i.Arg);
                break;

            case Kind.VolumeUp:
            case Kind.VolumeDown:
            case Kind.VolumeSet:
            {
                var v = Audio.GetVolume();
                if (v < 0) { Reply("Не вижу устройства вывода звука."); break; }
                if (i.Kind == Kind.VolumeUp) v = Math.Min(100, v + i.Number);
                else if (i.Kind == Kind.VolumeDown) v = Math.Max(0, v - i.Number);
                else v = Math.Clamp(i.Number, 0, 100);
                Audio.SetVolume(v);
                Reply($"Громкость {v} процентов.");
                break;
            }

            case Kind.VolumeGet:
            {
                var v = Audio.GetVolume();
                Reply(v < 0 ? "Не вижу устройства вывода звука." : $"Громкость {v} процентов.");
                break;
            }

            case Kind.Mute:
                Audio.SetMute(true);
                Reply("Звук выключен.");
                break;

            case Kind.Unmute:
                Audio.SetMute(false);
                Reply("Звук включён.");
                break;

            case Kind.BrightnessUp:
            case Kind.BrightnessDown:
            case Kind.BrightnessSet:
            {
                var b = Brightness.Get();
                if (b < 0) { Reply("Этот монитор не поддерживает управление яркостью."); break; }
                if (i.Kind == Kind.BrightnessUp) b = Math.Min(100, b + i.Number);
                else if (i.Kind == Kind.BrightnessDown) b = Math.Max(0, b - i.Number);
                else b = Math.Clamp(i.Number, 0, 100);
                if (Brightness.Set(b)) Reply($"Яркость {b} процентов.");
                else Reply("Не удалось изменить яркость монитора.");
                break;
            }

            case Kind.BrightnessGet:
            {
                var b = Brightness.Get();
                Reply(b < 0 ? "Этот монитор не поддерживает управление яркостью." : $"Яркость {b} процентов.");
                break;
            }

            case Kind.Screenshot:
            {
                var file = SystemActions.Screenshot();
                Reply("Скриншот сохранён: " + file);
                break;
            }

            case Kind.Lock:
                Reply("Блокирую компьютер.");
                await Task.Delay(400);
                SystemActions.Lock();
                break;

            case Kind.Sleep:
                Reply("Усыпляю компьютер.");
                await Task.Delay(500);
                SystemActions.Sleep();
                break;

            case Kind.Shutdown:
            {
                var sec = Cfg.shutdownDelay;
                if (SystemActions.Shutdown(sec))
                    Reply($"Выключаю через {sec} секунд. Скажи «отмена», чтобы остановить.");
                else Reply("Не удалось запланировать выключение.");
                break;
            }

            case Kind.Restart:
            {
                var sec = Cfg.shutdownDelay;
                if (SystemActions.Restart(sec))
                    Reply($"Перезагружаю через {sec} секунд. Скажи «отмена», чтобы остановить.");
                else Reply("Не удалось запланировать перезагрузку.");
                break;
            }

            case Kind.CancelShutdown:
                Reply(SystemActions.CancelShutdown()
                    ? "Выключение отменено."
                    : "Нечего отменять: выключение не запланировано.");
                break;

            case Kind.CloseWindow:
                if (ContainsFocus) { Hide(); await Task.Delay(200); }
                InputSim.CloseWindow();
                Reply("Закрываю окно.");
                break;

            case Kind.ShowDesktop:
                InputSim.ShowDesktop();
                Reply("Свернул окна.");
                break;

            case Kind.SwitchWindow:
                InputSim.SwitchWindow();
                Reply("Переключаю окно.");
                break;

            case Kind.MediaPlayPause:
                InputSim.PlayPause();
                Reply("Готово.");
                break;

            case Kind.MediaNext:
                InputSim.NextTrack();
                Reply("Следующий трек.");
                break;

            case Kind.MediaPrev:
                InputSim.PrevTrack();
                Reply("Предыдущий трек.");
                break;

            case Kind.MediaStop:
                InputSim.StopMedia();
                Reply("Остановил музыку.");
                break;

            case Kind.InsertText:
                var ok = SystemActions.InsertText(i.Say);
                Reply(ok ? "Вставил текст." : "Не удалось вставить текст.");
                break;

            case Kind.Calc:
                Reply(i.Say);
                break;

            case Kind.Weather:
            {
                var city = string.IsNullOrWhiteSpace(i.Arg) ? Cfg.city : i.Arg;
                SetStatus("Смотрю погоду…");
                try { Reply(await WebTools.WeatherAsync(city)); }
                catch { Reply("Не удалось получить погоду. Проверь интернет."); }
                break;
            }

            case Kind.Currency:
            {
                SetStatus("Смотрю курс…");
                try { Reply(await WebTools.CurrencyAsync(i.Arg)); }
                catch { Reply("Не удалось получить курс. Проверь интернет."); }
                break;
            }

            case Kind.Timer:
                if (!i.HasNumber) { Reply(i.Say ?? "На сколько поставить таймер?"); break; }
                reminders.Add(DateTime.Now.AddSeconds(i.Number), "Таймер", true);
                Reply("Таймер на " + DurationText(i.Number) + ".");
                break;

            case Kind.Remind:
            {
                if (!i.HasNumber) { Reply(i.Say ?? "Когда и о чём напомнить?"); break; }
                var due = DateTime.Now.AddSeconds(i.Number);
                var what = string.IsNullOrWhiteSpace(i.Arg) ? "напоминание" : i.Arg;
                reminders.Add(due, what, false);
                Reply($"Напомню в {due:HH:mm}: {what}.");
                break;
            }

            case Kind.CancelTimers:
            {
                var n = reminders.CancelTimers();
                Reply(n > 0 ? $"Отменил таймеров: {n}." : "Активных таймеров нет.");
                break;
            }

            case Kind.ListReminders:
                Reply(reminders.Upcoming());
                break;

            default:
                Reply("Не понял команду. Скажи «что ты умеешь» — покажу список.");
                break;
        }
    }

    void Reply(string text)
    {
        lastReply = text;
        AppendChat("Джарвис", text, Theme.Accent);
        SetStatus("Готов.");
    }

    void AppendChat(string who, string text, Color color)
    {
        chat.SelectionStart = chat.TextLength;
        chat.SelectionLength = 0;
        chat.SelectionColor = color;
        chat.SelectionFont = boldFont;
        chat.AppendText(who + "\r\n");
        chat.SelectionFont = regularFont;
        chat.SelectionColor = Theme.Text;
        chat.AppendText(text + "\r\n\r\n");
        chat.SelectionStart = chat.TextLength;
        chat.ScrollToCaret();
    }

    internal void SetStatus(string s)
    {
        try { status.Text = s; status.Update(); } catch { }
    }

    void StopAll()
    {
        if (stt != null && stt.Recording) stt.Stop();
        SetStatus("Готов.");
        btnVoice.Text = "🎤 Говорить";
    }

    void CheckReminders()
    {
        while (true)
        {
            var r = reminders.TakeDue(DateTime.Now);
            if (r == null) break;
            var text = r.IsTimer ? "Таймер сработал." : "Напоминание: " + r.Text;
            Reply(text);
            Balloon(text, ToolTipIcon.Info, 8000);
        }
    }

    // ------------------------------------------------------------------ утилиты
    static bool TryStart(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            return true;
        }
        catch { }

        // запасной путь: файл не найден по полному пути — пробуем запустить по имени
        // (сработает, если программа есть в PATH или в App Paths реестра)
        try
        {
            if (target.Contains('\\') || target.Contains('/'))
            {
                var name = Path.GetFileNameWithoutExtension(target);
                if (!string.IsNullOrWhiteSpace(name) && !name.Contains(':'))
                {
                    Process.Start(new ProcessStartInfo(name) { UseShellExecute = true });
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    static bool TryOpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase)) url = "https://" + url;
        return TryStart(url);
    }

    static string DurationText(int seconds)
    {
        var h = seconds / 3600;
        var m = seconds % 3600 / 60;
        var s = seconds % 60;
        var parts = new List<string>();
        if (h > 0) parts.Add(h + " " + Plural(h, "час", "часа", "часов"));
        if (m > 0) parts.Add(m == 1 ? "минуту" : m + " " + Plural(m, "минута", "минуты", "минут"));
        if (s > 0) parts.Add(s == 1 ? "секунду" : s + " " + Plural(s, "секунда", "секунды", "секунд"));
        if (parts.Count == 0) return "ноль секунд";
        return string.Join(' ', parts);
    }

    static string Plural(int n, string one, string few, string many)
    {
        var n10 = n % 10;
        var n100 = n % 100;
        if (n10 == 1 && n100 != 11) return one;
        if (n10 >= 2 && n10 <= 4 && (n100 < 12 || n100 > 14)) return few;
        return many;
    }

    // ------------------------------------------------------------------ хоткеи
    internal bool ApplyHotkey(string which)
    {
        var hk = which == "voice" ? HkVoice : HkWindow;
        if (which == "voice") hotkeys.UnregisterVoice(); else hotkeys.UnregisterWindow();
        bool ok = which == "voice"
            ? hotkeys.RegisterVoice(hk.Mods, hk.Vk)
            : hotkeys.RegisterWindow(hk.Mods, hk.Vk);
        if (which == "voice") HotkeyOk = ok; else WindowOk = ok;
        return ok;
    }

    // ------------------------------------------------------------------ настройки
    internal void ShowSettings()
    {
        using var dlg = new SettingsForm(Cfg, ModelDir, HkVoice.Text, HkWindow.Text);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var voice = KeyMap.Parse(dlg.VoiceHotkey);
        var window = KeyMap.Parse(dlg.WindowHotkey);
        if (voice == null || !KeyMap.IsAllowed(voice.Mods, voice.Vk))
        {
            Balloon("Клавиша «говорить» не подходит: нужна F1–F24, цифровой блок или сочетание с Ctrl/Alt/Shift.",
                ToolTipIcon.Warning, 8000);
            return;
        }
        if (window == null || !KeyMap.IsAllowed(window.Mods, window.Vk))
        {
            Balloon("Клавиша окна не подходит: нужна F1–F24, цифровой блок или сочетание с Ctrl/Alt/Shift.",
                ToolTipIcon.Warning, 8000);
            return;
        }

        HkVoice = voice;
        HkWindow = window;
        Cfg.hotkeyVoice = voice.Text;
        Cfg.hotkeyWindow = window.Text;
        ApplyHotkey("voice");
        ApplyHotkey("window");

        Cfg.autoSendVoice = dlg.AutoSend;
        Cfg.city = dlg.City;
        Cfg.shutdownDelay = dlg.ShutdownDelay;

        if (dlg.SttModel != Cfg.sttModel)
        {
            Cfg.sttModel = dlg.SttModel;
            Task.Run(() => stt?.Load(Cfg.sttModel));
        }

        SaveConfigQuiet();
        Parser.SetExtraApps(Cfg.extraApps);
        RebuildTrayLabels();
        Reply("Настройки сохранены.");
        SetStatus("Настройки сохранены");
    }

    void RebuildTrayLabels()
    {
        try
        {
            if (tray?.ContextMenuStrip != null)
                tray.ContextMenuStrip.Dispose();
            tray.ContextMenuStrip = BuildTrayMenu();
        }
        catch { }
    }

    internal void SaveConfigQuiet() => Cfg.Save(configPath);

    internal void ExitApp()
    {
        exiting = true;
        try { stt?.Abort(); } catch { }
        try { hotkeys?.Dispose(); } catch { }
        try { tray.Visible = false; tray.Dispose(); } catch { }
        try { stt?.Dispose(); } catch { }
        Application.Exit();
    }

    // ------------------------------------------------------------------ справка
    const string HelpText =
        "Что я умею:\n" +
        "• Открывать: «открой ютуб», «открой хром», «открой загрузки»\n" +
        "• Звук: «громче», «тише», «громкость 40», «выключи звук»\n" +
        "• Яркость: «ярче», «темнее», «яркость 70»\n" +
        "• Таймеры: «таймер на 10 минут», «отмени таймер»\n" +
        "• Напоминания: «напомни через час позвонить», «напомни в 18:30 выключить плиту»\n" +
        "• Система: «скриншот», «заблокируй компьютер», «спящий режим», «выключи компьютер»\n" +
        "• Окна: «закрой окно», «сверни окна», «переключи окно»\n" +
        "• Медиа: «пауза», «следующий трек», «предыдущий трек»\n" +
        "• Справка: «который час», «какая погода», «курс доллара», «сколько будет 15 плюс 7»\n" +
        "• Ввод: «вставь <текст>» — напечатает в активное окно\n" +
        "• Служебное: «повтори» — повторить ответ, «выход» — закрыть Джарвиса";
}
