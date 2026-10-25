// Имена клавиш. В config.json клавиши лежат человекочитаемо: «Num9», «F9», «Ctrl+Alt+V». Таблица имён нужна, чтобы разобрать строку в модификаторы + виртуальный код и собрать обратно.
using System.Windows.Forms;

namespace Jarvis;

record HotKeyDef(int Mods, int Vk, string Text);

static class KeyMap
{
    public const int MOD_ALT = 0x1, MOD_CTRL = 0x2, MOD_SHIFT = 0x4, MOD_WIN = 0x8;

    static readonly (string Name, Keys Vk)[] Names =
    {
        ("Num*", Keys.Multiply), ("Num+", Keys.Add), ("Num-", Keys.Subtract),
        ("Num.", Keys.Decimal), ("Num/", Keys.Divide),
        ("Space", Keys.Space), ("Tab", Keys.Tab), ("Enter", Keys.Enter),
        ("Backspace", Keys.Back), ("Insert", Keys.Insert), ("Delete", Keys.Delete),
        ("Home", Keys.Home), ("End", Keys.End), ("PageUp", Keys.PageUp),
        ("PageDown", Keys.PageDown), ("Left", Keys.Left), ("Up", Keys.Up),
        ("Right", Keys.Right), ("Down", Keys.Down), ("Pause", Keys.Pause),
        ("ScrollLock", Keys.Scroll), ("CapsLock", Keys.CapsLock),
        ("-", Keys.OemMinus), ("=", Keys.Oemplus), ("[", Keys.OemOpenBrackets),
        ("]", Keys.OemCloseBrackets), ("\\", Keys.Oem5), (";", Keys.Oem1),
        ("'", Keys.Oem7), (",", Keys.Oemcomma), (".", Keys.OemPeriod),
        ("/", Keys.Oem2), ("`", Keys.Oemtilde),
    };

    static readonly Dictionary<string, Keys> NameToVk = new(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<Keys, string> VkToName = new();

    static void Add(string name, Keys vk)
    {
        if (!VkToName.ContainsKey(vk)) VkToName[vk] = name;
        if (!NameToVk.ContainsKey(name)) NameToVk[name] = vk;
    }

    static KeyMap()
    {
        for (char c = 'A'; c <= 'Z'; c++) Add(c.ToString(), (Keys)c);
        for (int i = 0; i <= 9; i++) Add(i.ToString(), (Keys)((int)Keys.D0 + i));
        for (int i = 0; i <= 9; i++) Add("Num" + i, (Keys)((int)Keys.NumPad0 + i));
        for (int i = 1; i <= 24; i++) Add("F" + i, (Keys)((int)Keys.F1 + i - 1));
        foreach (var (n, vk) in Names) Add(n, vk);
    }

    public static string Name(Keys vk) =>
        VkToName.TryGetValue(vk, out var n) ? n : ((int)vk).ToString();

    public static bool Known(int vk) => VkToName.ContainsKey((Keys)vk);

    public static string Format(int mods, int vk)
    {
        var parts = new List<string>();
        if ((mods & MOD_CTRL) != 0) parts.Add("Ctrl");
        if ((mods & MOD_ALT) != 0) parts.Add("Alt");
        if ((mods & MOD_SHIFT) != 0) parts.Add("Shift");
        if ((mods & MOD_WIN) != 0) parts.Add("Win");
        parts.Add(Name((Keys)vk));
        return string.Join("+", parts);
    }

    // 'Ctrl+Alt+V' → HotKeyDef или null, если не разобрали
    public static HotKeyDef Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        int mods = 0;
        string rest = text.Trim();
        while (true)
        {
            string matched = null;
            foreach (var (mn, mv) in new[] { ("Ctrl", MOD_CTRL), ("Alt", MOD_ALT), ("Shift", MOD_SHIFT), ("Win", MOD_WIN) })
            {
                if (rest.Length > mn.Length + 1 &&
                    rest.StartsWith(mn, StringComparison.OrdinalIgnoreCase) && rest[mn.Length] == '+')
                { matched = mn; mods |= mv; break; }
            }
            if (matched == null) break;
            rest = rest[(matched.Length + 1)..];
        }
        if (rest.Length == 0) return null;
        if (!NameToVk.TryGetValue(rest, out var vk) || vk == Keys.None) return null;
        return new HotKeyDef(mods, (int)vk, Format(mods, (int)vk));
    }

    // Клавишу без модификаторов глобально перехватывать можно только
    // «безопасную»: иначе она перестала бы печататься во всех программах.
    public static bool IsAllowed(int mods, int vk)
    {
        if (mods != 0) return true;
        if (vk >= (int)Keys.F1 && vk <= (int)Keys.F24) return true;
        if (vk >= (int)Keys.NumPad0 && vk <= (int)Keys.Divide) return true;
        if (vk == (int)Keys.Pause || vk == (int)Keys.Scroll) return true;
        return false;
    }
}

// Состояние клавиши: RegisterHotKey сообщает только о нажатии, а для режима
// «удержания» нужно ещё и отпускание — опрашиваем через GetAsyncKeyState.
static class KeyState
{
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);
    public static bool Down(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;
}
