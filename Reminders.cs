// Таймеры и напоминания: список с проверкой раз в секунду.
namespace Jarvis;

class Reminder
{
    public DateTime Due;
    public string Text;
    public bool IsTimer;
}

class ReminderList
{
    readonly List<Reminder> items = new();

    public int Count => items.Count;
    public int TimerCount => items.Count(r => r.IsTimer);

    public void Add(DateTime due, string text, bool timer)
    {
        items.Add(new Reminder { Due = due, Text = text, IsTimer = timer });
    }

    // ближайшее просроченное — и убираем его из списка
    public Reminder TakeDue(DateTime now)
    {
        var r = items.Where(x => x.Due <= now).OrderBy(x => x.Due).FirstOrDefault();
        if (r != null) items.Remove(r);
        return r;
    }

    public int CancelTimers()
    {
        var n = items.RemoveAll(x => x.IsTimer);
        return n;
    }

    public int CancelAll()
    {
        var n = items.Count;
        items.Clear();
        return n;
    }

    public string Upcoming()
    {
        if (items.Count == 0) return "Активных напоминаний нет.";
        var parts = items.OrderBy(x => x.Due)
            .Select(x => $"{x.Due:HH:mm} — {x.Text}");
        return "Напоминания: " + string.Join("; ", parts) + ".";
    }
}
