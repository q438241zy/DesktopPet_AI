using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Maps the generator's canonical pose/expression filenames to static pet actions.</summary>
public static class Storyboard
{
    public static Character Import(Catalog catalog, string folder, string category = "chibi")
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, MaxRecursionDepth = 5 };
        var files = Directory.EnumerateFiles(folder, "*.png", options).Take(1201)
            .Where(f => Regex.IsMatch(Path.GetFileName(f), @"__P\d{2}-.*__E\d{2}-")).Order(StringComparer.Ordinal).ToArray();
        if (files.Length == 0 || files.Length > 1200) throw new InvalidDataException("未找到生成器标准命名的 PNG。请选择成果目录，或使用单张图片导入。");
        string? Pick(string pose, string? expression = null) => files.FirstOrDefault(f => Path.GetFileName(f).Contains("__" + pose + "-") && (expression is null || Path.GetFileName(f).Contains("__" + expression + "-")));
        string idle = Pick("P01", "E01") ?? Pick("P17") ?? Pick("P01") ?? files[0];
        string temp = Path.Combine(Path.GetTempPath(), "DesktopPet-Storyboard-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            Sprite Copy(string source)
            {
                string name = Path.GetFileName(source); File.Copy(source, Path.Combine(temp, name), true); return new Sprite(name, 1, 1);
            }
            string display = Path.GetFileName(idle).Split("__")[0];
            var c = new Character { Id = "story-" + Guid.NewGuid().ToString("N")[..10], Name = display, Category = category, Atlas = Copy(idle) };
            foreach (var (motion, pose) in new[] { ("walk", "P11"), ("jump", "P10"), ("chat", "P06"), ("farewell", "P06"), ("curl", "P24"), ("sleep", "P23"), ("think", "P17"), ("pickup", "P01") })
                if (Pick(pose) is { } file) c.Motions[motion] = Copy(file);
            foreach (var (motion, expression) in new[] { ("happy", "E01"), ("angry", "E02"), ("sad", "E03") })
                if (Pick("P01", expression) is { } file) c.Motions[motion] = Copy(file);
            File.WriteAllText(Path.Combine(temp, "pet.json"), JsonSerializer.Serialize(c, Json.Options));
            return catalog.Import(temp);
        }
        finally { Directory.Delete(temp, true); }
    }
}
