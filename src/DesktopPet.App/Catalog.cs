using System.Text.Json;
using DesktopPet.Core;

namespace DesktopPet.App;

public sealed class Catalog
{
    public static readonly string[] BuiltInFamilies = ["whale", "gpt", "claude", "gemini", "grok", "qwen", "zhipu", "kimi"];
    public static string VariantId(string family, string category) => category == "chibi" ? family : $"{(family == "whale" ? "deepseek" : family)}-{category}";
    public List<Character> Characters { get; } = [];
    public List<string> Warnings { get; } = [];
    private readonly string customRoot;
    public Catalog(string dataRoot)
    {
        customRoot = Path.Combine(dataRoot, "Characters");
        string builtins = Path.Combine(AppContext.BaseDirectory, "Assets", "Characters");
        var ids = new[] { "chibi", "3d", "adult" }.SelectMany(category => BuiltInFamilies.Select(family => VariantId(family, category)));
        foreach (var id in ids) Characters.Add(Character.Load(Path.Combine(builtins, id)));
        if (Directory.Exists(customRoot))
            foreach (string folder in Directory.EnumerateDirectories(customRoot))
            {
                try { var c = Character.Load(folder); if (Characters.Any(x => x.Id == c.Id)) throw new InvalidDataException("角色 ID 重复。"); Characters.Add(c); }
                catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { Warnings.Add(Path.GetFileName(folder) + ": " + ex.Message); }
            }
    }
    public Character Find(string id) => Characters.FirstOrDefault(c => c.Id == id) ?? Characters[0];
    public Character Import(string source)
    {
        var c = Character.Load(source);
        if (Characters.Any(x => x.Id == c.Id)) throw new InvalidDataException("角色 ID 已存在，请给新角色设置不同的 ID。");
        foreach (var s in c.Sprites()) ArtCache.Validate(c, s);
        var files = c.Sprites().Select(s => s.File).Distinct().ToArray();
        if (files.Sum(f => new FileInfo(Character.SafeFile(c.Root, f)).Length) > 180 * 1024 * 1024)
            throw new InvalidDataException("角色包图片总量超过 180 MB。");
        Directory.CreateDirectory(customRoot);
        string staging = Path.Combine(customRoot, ".import-" + Guid.NewGuid().ToString("N"));
        string destination = Path.Combine(customRoot, c.Id);
        Directory.CreateDirectory(staging);
        try
        {
            foreach (string relative in files)
            {
                string target = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(Character.SafeFile(c.Root, relative), target);
            }
            File.WriteAllText(Path.Combine(staging, "pet.json"), JsonSerializer.Serialize(c, Json.Options));
            Directory.Move(staging, destination);
        }
        catch { if (Directory.Exists(staging)) Directory.Delete(staging, true); throw; }
        c.Root = destination; Characters.Add(c); return c;
    }
    public Character ImportPortrait(string image, string name, string category = "chibi")
    {
        string temporary = Path.Combine(Path.GetTempPath(), "DesktopPet-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        try
        {
            string filename = "portrait" + Path.GetExtension(image).ToLowerInvariant();
            File.Copy(image, Path.Combine(temporary, filename));
            var c = new Character { Id = "custom-" + Guid.NewGuid().ToString("N")[..10], Name = name, Category = category, Atlas = new Sprite(filename, 1, 1) };
            File.WriteAllText(Path.Combine(temporary, "pet.json"), JsonSerializer.Serialize(c, Json.Options));
            return Import(temporary);
        }
        finally { Directory.Delete(temporary, true); }
    }
}
