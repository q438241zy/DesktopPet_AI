namespace DesktopPet.Core;

public sealed record BondEvent(string Label, int Delta, DateTimeOffset At);

public sealed class CompanionBond
{
    public int Score { get; set; }
    public double Seconds { get; set; }
    public int Interactions { get; set; }
    public int TimeAwards { get; set; }
    public List<BondEvent> History { get; set; } = [];
    public Dictionary<string, long> Awards { get; set; } = [];
    public Dictionary<string, int> Daily { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public string Name => Score <= -60 ? "需要一些空间" : Score < 0 ? "慢慢修复默契" : Score < 20 ? "初次相遇" : Score < 50 ? "渐渐熟悉" : Score < 80 ? "默契伙伴" : "亲密搭档";
}

public sealed partial class PetState
{
    public Dictionary<string, CompanionBond> Companions { get; set; } = [];
    public int MakeupCards { get; set; }
    public SortedSet<string> MakeupCheckIns { get; set; } = [];
    public string CalendarView { get; set; } = "month";
    public bool AutoHide { get; set; } = true;
    public bool WorkModeEnabled { get; set; }
    public int WorkMinutes { get; set; } = 10;

    public CompanionBond Companion(string family)
    {
        if (!Companions.TryGetValue(family, out var bond)) Companions[family] = bond = new();
        return bond;
    }
    public int Affect(string family, int delta, string label, DateTimeOffset now, string key, TimeSpan cooldown)
    {
        var bond = Companion(family); long milliseconds = now.ToUnixTimeMilliseconds();
        if (bond.Awards.TryGetValue(key, out long previous) && milliseconds - previous < cooldown.TotalMilliseconds) return 0;
        string day = now.LocalDateTime.ToString("yyyy-MM-dd"); int earned = bond.Daily.GetValueOrDefault(day);
        if (delta > 0) delta = Math.Min(delta, Math.Max(0, 20 - earned));
        int before = bond.Score; bond.Score = Math.Clamp(bond.Score + delta, -100, 100); int actual = bond.Score - before;
        bond.Awards[key] = milliseconds;
        if (actual > 0) bond.Daily[day] = earned + actual;
        if (actual != 0) { bond.Interactions++; bond.History.Add(new(label, actual, now)); bond.History = bond.History.TakeLast(40).ToList(); }
        foreach (string old in bond.Daily.Keys.Where(k => string.CompareOrdinal(k, now.LocalDateTime.AddDays(-32).ToString("yyyy-MM-dd")) < 0).ToArray()) bond.Daily.Remove(old);
        return actual;
    }
    public void Accompany(string family, double seconds, DateTimeOffset now)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        var bond = Companion(family); bond.Seconds += Math.Min(seconds, 2);
        int reached = (int)(bond.Seconds / 300);
        if (reached > bond.TimeAwards) { bond.TimeAwards = reached; Affect(family, 1, "安静陪伴五分钟", now, "time", TimeSpan.FromMinutes(5)); }
    }
    public bool Makeup(DateOnly date, DateOnly today)
    {
        if (date >= today || MakeupCards <= 0 || CheckedIn(date)) return false;
        MakeupCards--; string key = date.ToString("yyyy-MM-dd"); CheckIns.Add(key); MakeupCheckIns.Add(key); return true;
    }
    private void ValidateCompanions()
    {
        Companions ??= []; MakeupCheckIns ??= [];
        MakeupCards = Math.Clamp(MakeupCards, 0, 99999); WorkMinutes = Math.Clamp(WorkMinutes, 1, 180); CalendarView = CalendarView == "week" ? "week" : "month";
        MakeupCheckIns.RemoveWhere(key => !CheckIns.Contains(key));
        foreach (var key in Companions.Keys.ToArray())
        {
            if (Companions[key] is not { } bond) { Companions.Remove(key); continue; }
            bond.Score = Math.Clamp(bond.Score, -100, 100); bond.Seconds = double.IsFinite(bond.Seconds) ? Math.Clamp(bond.Seconds, 0, 315360000) : 0;
            bond.TimeAwards = Math.Clamp(bond.TimeAwards, 0, (int)(bond.Seconds / 300)); bond.Interactions = Math.Max(0, bond.Interactions);
            bond.History = (bond.History ?? []).Where(e => e is not null && !string.IsNullOrWhiteSpace(e.Label)).TakeLast(40).ToList(); bond.Awards ??= []; bond.Daily ??= [];
        }
    }
}

public sealed class WorkReminderClock
{
    public bool Enabled { get; private set; }
    public bool Paused { get; private set; }
    public double Next { get; private set; }
    private double interval, remaining;
    public void Start(double now, int minutes) { interval = Math.Clamp(minutes, 1, 180) * 60000d; Enabled = true; Paused = false; Next = now + interval; remaining = interval; }
    public void Stop() { Enabled = false; Next = remaining = 0; Paused = false; }
    public void Pause(double now) { if (!Paused) remaining = Math.Max(0, Next - now); Paused = true; }
    public void Resume(double now) { if (Paused) Next = now + remaining; Paused = false; }
    public bool Tick(double now)
    {
        if (!Enabled || Paused || now < Next) return false;
        Next += (Math.Floor((now - Next) / interval) + 1) * interval; return true;
    }
}

public static class CompanionCalendar
{
    public static DateOnly Start(DateOnly date, bool week) => week ? date.AddDays(-((int)date.DayOfWeek + 6) % 7) : new(date.Year, date.Month, 1);
    public static IReadOnlyList<DateOnly?> Days(DateOnly date, bool week)
    {
        var start = Start(date, week); int blanks = week ? 0 : ((int)start.DayOfWeek + 6) % 7, days = week ? 7 : DateTime.DaysInMonth(date.Year, date.Month);
        return Enumerable.Range(0, week ? 7 : (int)Math.Ceiling((blanks + days) / 7d) * 7).Select(i => i < blanks || i >= blanks + days ? (DateOnly?)null : start.AddDays(i - blanks)).ToArray();
    }
}
