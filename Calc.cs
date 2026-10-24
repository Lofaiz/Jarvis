// Калькулятор для голосовых команд: понимает «15 плюс 7», «двадцать умножить
// на три», «15 процентов от 200». Считает без ИИ, обычным разбором выражения.
using System.Globalization;
using System.Text;

namespace Jarvis;

static class Calc
{
    static readonly Dictionary<string, double> NumberWords = new()
    {
        ["ноль"] = 0, ["один"] = 1, ["одна"] = 1, ["одну"] = 1, ["два"] = 2, ["две"] = 2,
        ["три"] = 3, ["четыре"] = 4, ["пять"] = 5, ["шесть"] = 6, ["семь"] = 7,
        ["восемь"] = 8, ["девять"] = 9, ["десять"] = 10, ["одиннадцать"] = 11,
        ["двенадцать"] = 12, ["тринадцать"] = 13, ["четырнадцать"] = 14, ["пятнадцать"] = 15,
        ["шестнадцать"] = 16, ["семнадцать"] = 17, ["восемнадцать"] = 18, ["девятнадцать"] = 19,
        ["двадцать"] = 20, ["тридцать"] = 30, ["сорок"] = 40, ["пятьдесят"] = 50,
        ["шестьдесят"] = 60, ["семьдесят"] = 70, ["восемьдесят"] = 80, ["девяносто"] = 90,
        ["сто"] = 100, ["двести"] = 200, ["триста"] = 300, ["четыреста"] = 400,
        ["пятьсот"] = 500, ["шестьсот"] = 600, ["семьсот"] = 700, ["восемьсот"] = 800,
        ["девятьсот"] = 900,
    };

    public static bool TrySolve(string text, out string result)
    {
        result = null;
        var s = Text.Norm(text);
        s = StripLead(s);
        s = WordsToDigits(s);
        s = ReplaceOps(s);

        // остались только цифры, операторы, точка, скобки и минус
        if (!s.Any(char.IsDigit)) return false;
        foreach (var ch in s)
            if (!(char.IsDigit(ch) || ch is '+' or '-' or '*' or '/' or '^' or '.' or '(' or ')' or ' '))
                return false;

        try
        {
            int pos = 0;
            double value = ParseExpr(s, ref pos);
            Skip(s, ref pos);
            if (pos != s.Length) return false;
            if (double.IsNaN(value) || double.IsInfinity(value)) return false;
            result = Format(value);
            return true;
        }
        catch { return false; }
    }

    static string StripLead(string s)
    {
        foreach (var p in new[] { "сколько будет", "сколько получится", "сколько это", "чему равно",
                                  "посчитай", "вычисли", "калькулятор", "сколько" })
            if (s == p) return "";
            else if (s.StartsWith(p + " ")) return s[(p.Length + 1)..].Trim();
        return s;
    }

    // «двадцать пять» → «25»; одиночные числа тоже
    static string WordsToDigits(string s)
    {
        var tokens = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var output = new List<string>();
        double acc = 0;
        bool has = false;

        void Flush()
        {
            if (!has) return;
            output.Add(acc.ToString("0.###", CultureInfo.InvariantCulture));
            acc = 0;
            has = false;
        }

        foreach (var raw in tokens)
        {
            var w = raw.Trim();
            if (NumberWords.TryGetValue(w, out var v))
            {
                acc += v;
                has = true;
                continue;
            }
            if (w is "тысяча" or "тысячи" or "тысяч")
            {
                acc = (acc == 0 ? 1 : acc) * 1000;
                has = true;
                continue;
            }
            if (w is "полтора" or "полторы")
            {
                acc += 1.5;
                has = true;
                continue;
            }
            if (w is "пол" or "половина")
            {
                acc += 0.5;
                has = true;
                continue;
            }
            Flush();
            output.Add(w);
        }
        Flush();
        return string.Join(' ', output);
    }

    static string ReplaceOps(string s)
    {
        foreach (var (from, to) in new[]
        {
            ("умножить на", "*"), ("разделить на", "/"), ("поделить на", "/"), ("делить на", "/"),
            ("процентов от", "* 0.01 *"), ("процента от", "* 0.01 *"), ("процент от", "* 0.01 *"),
            ("плюс", "+"), ("минус", "-"),
            ("умножить", "*"), ("разделить", "/"), ("поделить", "/"), ("делить", "/"),
        })
            s = s.Replace(from, to);

        // разговорное «15 на 7» = 15 × 7; выполняем последним, чтобы не съесть
        // «умножить на» и «разделить на»
        s = s.Replace(" на ", " * ").Replace(" x ", " * ").Replace(" х ", " * ");
        s = s.Replace(",", ".");
        return string.Join(' ', s.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    static string Format(double v)
    {
        if (Math.Abs(v - Math.Round(v)) < 1e-9) return ((long)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
        return Math.Round(v, 4).ToString("0.####", CultureInfo.InvariantCulture).Replace('.', ',');
    }

    // ------------------------------------------------------------------ разбор
    static void Skip(string s, ref int i)
    {
        while (i < s.Length && s[i] == ' ') i++;
    }

    static double ParseExpr(string s, ref int i)
    {
        double v = ParseTerm(s, ref i);
        while (true)
        {
            Skip(s, ref i);
            if (i >= s.Length) break;
            if (s[i] == '+') { i++; v += ParseTerm(s, ref i); }
            else if (s[i] == '-') { i++; v -= ParseTerm(s, ref i); }
            else break;
        }
        return v;
    }

    static double ParseTerm(string s, ref int i)
    {
        double v = ParsePower(s, ref i);
        while (true)
        {
            Skip(s, ref i);
            if (i >= s.Length) break;
            if (s[i] == '*') { i++; v *= ParsePower(s, ref i); }
            else if (s[i] == '/')
            {
                i++;
                double d = ParsePower(s, ref i);
                if (Math.Abs(d) < 1e-12) throw new DivideByZeroException();
                v /= d;
            }
            else break;
        }
        return v;
    }

    static double ParsePower(string s, ref int i)
    {
        double v = ParseFactor(s, ref i);
        Skip(s, ref i);
        if (i < s.Length && s[i] == '^')
        {
            i++;
            v = Math.Pow(v, ParsePower(s, ref i));
        }
        return v;
    }

    static double ParseFactor(string s, ref int i)
    {
        Skip(s, ref i);
        if (i >= s.Length) throw new FormatException();
        if (s[i] == '-') { i++; return -ParseFactor(s, ref i); }
        if (s[i] == '+') { i++; return ParseFactor(s, ref i); }
        if (s[i] == '(')
        {
            i++;
            double v = ParseExpr(s, ref i);
            Skip(s, ref i);
            if (i >= s.Length || s[i] != ')') throw new FormatException();
            i++;
            return v;
        }

        int start = i;
        while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
        if (i == start) throw new FormatException();
        var num = s[start..i].Replace(',', '.');
        if (num.Count(c => c == '.') > 1) throw new FormatException();
        return double.Parse(num, CultureInfo.InvariantCulture);
    }
}

// Нормализация текста: строчные, «ё» → «е», знаки → пробелы.
static class Text
{
    public static string Norm(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Trim().ToLowerInvariant())
        {
            if (ch == 'ё') { sb.Append('е'); continue; }
            if (ch is '\'' or '’' or '`' or '«' or '»' or '"') { continue; }
            sb.Append(char.IsLetterOrDigit(ch) ? ch : ' ');
        }
        return string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
