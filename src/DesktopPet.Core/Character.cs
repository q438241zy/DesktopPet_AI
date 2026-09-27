using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet.Core;

/// <summary>Palm center relative to the visible silhouette; span controls the held toy size.</summary>
public sealed record HandContact(double Height, double Offset = 0, double Span = .11);
public sealed record SpriteCell(int X, int Y, int Width, int Height);
/// <summary>A sheet with optional authored crop regions, frame order and contact points.</summary>
public sealed record Sprite(string File, int Columns = 3, int Rows = 2, int[]? FrameMs = null, string Facing = "right",
    int[]? Frames = null, bool Loop = true, bool BakedProps = false, HandContact?[]? Hands = null, SpriteCell[]? Cells = null);

/// <summary>A self-contained appearance. Missing motions never borrow another outfit's art.</summary>
public sealed class Outfit
{
    public string Name { get; set; } = "";
    public Sprite? Idle { get; set; }
    public Dictionary<string, Sprite> Motions { get; set; } = [];
}

/// <summary>Portable, declarative character pack. It never contains executable code.</summary>
public sealed class Character
{
    public int Version { get; set; } = 1;
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Accent { get; set; } = "#EE9177";
    public string Category { get; set; } = "chibi";
    public string Family { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string FamilyId => string.IsNullOrEmpty(Family) ? Id : Family;
    public string Description { get; set; } = "";
    public bool Demo { get; set; }
    public Sprite Atlas { get; set; } = new("atlas.png");
    public Sprite? Dizzy { get; set; }
    public Dictionary<string, Sprite> Motions { get; set; } = [];
    public Dictionary<string, Outfit> Outfits { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore] public string Root { get; set; } = "";

    public IEnumerable<Sprite> Sprites() => new[] { Atlas }.Concat(Dizzy is null ? [] : new[] { Dizzy })
        .Concat(Motions.Values).Concat(Outfits.Values.SelectMany(o =>
            (o.Idle is null ? Enumerable.Empty<Sprite>() : new[] { o.Idle }).Concat(o.Motions.Values)));

    public static Character Load(string folder)
    {
        string manifest = Path.Combine(folder, "pet.json");
        if (new FileInfo(manifest).Length > 262144) throw new InvalidDataException("角色清单超过 256 KB。");
        var c = JsonSerializer.Deserialize<Character>(File.ReadAllText(manifest), Json.Options)
            ?? throw new InvalidDataException("角色清单为空。");
        c.Root = Path.GetFullPath(folder);
        if (c.Version != 1 || !Regex.IsMatch(c.Id ?? "", "^[a-z0-9][a-z0-9-]{0,47}$")
            || string.IsNullOrWhiteSpace(c.Name) || c.Name.Length > 60 || c.Atlas is null
            || c.Motions is null || c.Outfits is null || c.Motions.Count > 40 || c.Outfits.Count > 12
            || c.Category is not ("chibi" or "3d" or "adult")
            || (!string.IsNullOrEmpty(c.Family) && !Regex.IsMatch(c.Family, "^[a-z0-9][a-z0-9-]{0,47}$")))
            throw new InvalidDataException("角色 ID、名称或版本无效。");
        if (!Regex.IsMatch(c.Accent ?? "", "^#[0-9a-fA-F]{6}$")) c.Accent = "#EE9177";
        if (c.Outfits.Values.Any(o => o is null || o.Motions is null || string.IsNullOrWhiteSpace(o.Name)
            || (o.Idle is null && o.Motions.Count == 0)))
            throw new InvalidDataException("服装清单无效。");
        foreach (var s in c.Sprites())
        {
            if (s is null || string.IsNullOrWhiteSpace(s.File) || s.Columns < 1 || s.Rows < 1
                || s.Columns > 6 || s.Rows > 6 || s.Columns * s.Rows > 24 || s.Facing is not ("left" or "right")
                || (s.Frames is { } order && (order.Length is < 1 or > 48 || order.Any(i => i < 0 || i >= s.Columns * s.Rows)))
                || (s.Cells is { } cells && (cells.Length != s.Columns * s.Rows || cells.Any(c => c is null || c.X < 0 || c.Y < 0
                    || c.Width < 1 || c.Height < 1 || (long)c.X + c.Width > 6144 || (long)c.Y + c.Height > 6144)))
                || (s.FrameMs is { } ms && (ms.Length != (s.Frames?.Length ?? s.Columns * s.Rows) || ms.Any(t => t < 40 || t > 5000)))
                || (s.Hands is { } hands && (hands.Length != s.Columns * s.Rows || hands.Any(h => h is not null
                    && (!double.IsFinite(h.Height) || !double.IsFinite(h.Offset) || !double.IsFinite(h.Span)
                        || h.Height is < 0 or > 1 || h.Offset is < -.5 or > .5 || h.Span is < .02 or > .4)))))
                throw new InvalidDataException("图集尺寸或帧时长无效。");
            string file = SafeFile(c.Root, s.File);
            if (!File.Exists(file) || new FileInfo(file).Length > 24 * 1024 * 1024)
                throw new InvalidDataException($"图片不存在或超过 24 MB：{s.File}");
        }
        return c;
    }

    /// <summary>Resolve an image without allowing rooted paths, traversal or links outside the pack.</summary>
    public static string SafeFile(string root, string relative)
    {
        if (Path.IsPathRooted(relative) || relative.Contains(':') || relative.Split('/', '\\').Contains(".."))
            throw new InvalidDataException("图片必须位于角色包内。");
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(prefix, relative));
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || !new[] { ".png", ".webp", ".jpg", ".jpeg" }.Contains(Path.GetExtension(full).ToLowerInvariant()))
            throw new InvalidDataException("仅支持角色包内的 PNG、WebP 或 JPEG 图片。");
        for (var part = new FileInfo(full) as FileSystemInfo; part is not null; part = Directory.GetParent(part.FullName))
        {
            if (part.Exists && (part.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("角色包不能含有符号链接或目录联接。");
            if (string.Equals(part.FullName.TrimEnd('\\', '/'), root.TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)) break;
        }
        return full;
    }

    /// <summary>Only an actual motion in the selected appearance grants an animation capability.</summary>
    public Sprite? MotionFor(string outfit, string action)
    {
        var motions = Outfits.TryGetValue(outfit, out var clothes) ? clothes.Motions : Motions;
        var fallback = action switch { "meal" => "eat", "pounce" => "chat", "poke" => "headpat", "tickle" => "jump", "kick" => "jump", _ => action };
        return motions.GetValueOrDefault(action) ?? motions.GetValueOrDefault(fallback);
    }

    public bool CanWalk(string outfit) => MotionFor(outfit, "walk") is { } walk
        && (walk.Frames?.Distinct().Count() ?? walk.Columns * walk.Rows) >= 2;

    public (Sprite Sprite, int Frame) Resolve(string outfit, string action, double elapsed, bool reducedMotion = false)
    {
        var clip = MotionFor(outfit, action);
        if (clip is not null) return (clip, Motion.Frame(clip, reducedMotion ? 0 : elapsed));
        var pose = action switch { "sleep" => 3, "dizzy" or "faint" => 4, "sad" => 5, "happy" => 1, "headpat" or "poke" => 2, _ => 0 };
        if (Outfits.TryGetValue(outfit, out var clothes))
        {
            if (clothes.Idle is { } idle) return (idle, Math.Min(pose, idle.Columns * idle.Rows - 1));
            // Older imported packs may contain only outfit motions. Their first frame is a safe idle.
            if (clothes.Motions.Values.FirstOrDefault() is { } still) return (still, Motion.Frame(still, 0));
        }
        if (action == "dizzy" && Dizzy is not null) return (Dizzy, 0);
        return (Atlas, Math.Min(pose, Atlas.Columns * Atlas.Rows - 1));
    }
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
}

public static class Motion
{
    public static int Frame(Sprite sprite, double elapsed)
    {
        int count = sprite.Frames?.Length ?? sprite.Columns * sprite.Rows;
        var times = sprite.FrameMs ?? Enumerable.Repeat(240, count).ToArray();
        double t = Math.Max(0, elapsed), total = times.Sum();
        int Cell(int i) => sprite.Frames?[i] ?? i;
        if (!sprite.Loop && t >= total) return Cell(count - 1);
        t %= total;
        for (int i = 0; i < times.Length; i++) { if (t < times[i]) return Cell(i); t -= times[i]; }
        return Cell(count - 1);
    }
}
