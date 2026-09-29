namespace DesktopPet.Core;

/// <summary>Two maintained art styles. Legacy portrait categories remain readable.</summary>
public static class CharacterStyles
{
    public const string Chibi = "chibi", Realistic = "realistic";
    public static readonly string[] All = [Chibi, Realistic];
    public static string Normalize(string category) => category is "3d" or "adult" ? Realistic : category;
    public static string Name(string category) => Normalize(category) == Realistic ? "3D真人" : "Q版";
}
