using System.Text.Json;

namespace DesktopPet.Core;

/// <summary>Local progress only: missed days never remove earned affection or collectibles.</summary>
public sealed partial class PetState
{
    public int Version { get; set; } = 1;
    public string Character { get; set; } = "whale";
    public Dictionary<string, string> Outfits { get; set; } = [];
    public Dictionary<string, string> Postures { get; set; } = [];
    public double Size { get; set; } = 200;
    public double Opacity { get; set; } = 1;
    public double? Left { get; set; }
    public double? Top { get; set; }
    public bool Topmost { get; set; } = true;
    public bool ReducedMotion { get; set; }
    public bool Wander { get; set; }
    public string AdoptedAt { get; set; } = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
    public string? LastSeen { get; set; }
    public string? LastCelebration { get; set; }
    public SortedSet<string> CheckIns { get; set; } = [];
    public List<string> Treasures { get; set; } = [];
    public Dictionary<string,int> ReadStories { get; set; } = [];
    public List<string> GiftDrawRemaining { get; set; } = Collectibles.All.Select(x=>x.Id).ToList();
    public string? LastGift { get; set; }
    public string? LastReadStory { get; set; }

    public static readonly int[] BondDays = [0, 3, 7, 14, 30, 60, 100, 200, 365];
    public static readonly string[] BondNames = ["初次相遇", "渐渐熟悉", "桌边伙伴", "默契朋友", "亲密搭档", "心有灵犀", "长久陪伴", "不可替代", "一生挚友"];
    [System.Text.Json.Serialization.JsonIgnore] public int BondLevel => Array.FindLastIndex(BondDays, d => CheckIns.Count >= d);
    [System.Text.Json.Serialization.JsonIgnore] public string BondName => BondNames[BondLevel];
    [System.Text.Json.Serialization.JsonIgnore] public string Outfit => Outfits.GetValueOrDefault(Character, "original");
    public bool CheckedIn(DateOnly day) => CheckIns.Contains(day.ToString("yyyy-MM-dd"));
    public bool CheckIn(DateOnly day) => CheckIns.Add(day.ToString("yyyy-MM-dd"));
    public int Streak(DateOnly today)
    {
        var day = CheckedIn(today) ? today : today.AddDays(-1);
        int count = 0;
        while (CheckedIn(day)) { count++; day = day.AddDays(-1); }
        return count;
    }
    public string? Anniversary(DateOnly today)
    {
        if (!DateOnly.TryParse(AdoptedAt, out var adopted) || today <= adopted) return null;
        int days = today.DayNumber - adopted.DayNumber;
        if (days is 100 or 1000) return $"相伴第 {days} 天";
        return today.Month == adopted.Month && today.Day == adopted.Day ? $"相伴 {today.Year - adopted.Year} 周年" : null;
    }
    public int DaysAway(DateOnly today) => DateOnly.TryParse(LastSeen, out var seen) ? Math.Max(0, today.DayNumber - seen.DayNumber) : 0;

    /// <summary>The currently worn legacy outfit wins; other merged entries keep the target's existing choice.</summary>
    public void MigrateCharacters(IReadOnlyDictionary<string, string> aliases)
    {
        foreach (var (oldId, newId) in aliases)
        {
            if (Character == oldId) Outfits[newId] = Outfits.GetValueOrDefault(oldId, "original");
            else if (!Outfits.ContainsKey(newId) && Outfits.TryGetValue(oldId, out var outfit)) Outfits[newId] = outfit;
            Outfits.Remove(oldId);
        }
        if (aliases.TryGetValue(Character, out var replacement)) Character = replacement;
    }

    public void Validate()
    {
        if (Version != 1 || Outfits is null || CheckIns is null || Treasures is null
            || CheckIns.Any(x => !DateOnly.TryParseExact(x, "yyyy-MM-dd", out _))
            || !DateOnly.TryParseExact(AdoptedAt, "yyyy-MM-dd", out _))
            throw new InvalidDataException("存档版本或日期无效。");
        Size = double.IsFinite(Size) ? Math.Clamp(Size, 120, 280) : 200;
        Opacity = double.IsFinite(Opacity) ? Math.Clamp(Opacity, .3, 1) : 1;
        if (Left is { } x && !double.IsFinite(x)) Left = null;
        if (Top is { } y && !double.IsFinite(y)) Top = null;
        ReadStories ??= [];
        if (StoryLibrary.Find(LastReadStory) is null) LastReadStory = null;
        Postures ??= [];
        foreach (string family in Postures.Keys.ToArray()) Postures[family] = IdlePosture.Normalize(Postures[family]);
        foreach(string id in ReadStories.Keys.ToArray())
            if(StoryLibrary.Find(id) is null || ReadStories[id] is <1 or >100000) ReadStories.Remove(id);
        if(GiftDrawRemaining is null || GiftDrawRemaining.Count>20 || GiftDrawRemaining.Distinct().Count()!=GiftDrawRemaining.Count
            || GiftDrawRemaining.Any(id=>!Collectibles.All.Any(x=>x.Id==id)))
            GiftDrawRemaining=Collectibles.All.Select(x=>x.Id).ToList();
        if(!Collectibles.All.Any(x=>x.Id==LastGift)) LastGift=null;
        ValidateCompanions();
    }
}

/// <summary>Atomic replacement keeps the previous complete save when writing is interrupted.</summary>
public sealed class StateStore(string root)
{
    public string Root { get; } = root;
    public string? Warning { get; private set; }
    public PetState Load()
    {
        string path = Path.Combine(Root, "state.json");
        if (!File.Exists(path)) return new();
        try
        {
            if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("存档过大。");
            var result = JsonSerializer.Deserialize<PetState>(File.ReadAllText(path), Json.Options) ?? throw new InvalidDataException("存档为空。");
            result.Validate();
            return result;
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            var backup = Path.Combine(Root, $"state-unreadable-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            File.Copy(path, backup);
            Warning = "旧存档无法读取，已保留原文件备份。";
            return new();
        }
    }
    public void Save(PetState state)
    {
        state.Validate();
        Directory.CreateDirectory(Root);
        string file = Path.Combine(Root, "state.json"), temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, Json.Options));
        File.Move(temp, file, true);
    }
}
