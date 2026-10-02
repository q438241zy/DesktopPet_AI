namespace DesktopPet.Core;

public enum MembershipTier { Brass, Silver, Gold, Platinum, BlackGold }

public sealed record MemberAccount(string Id, string Username, string Nickname, MembershipTier Tier, DateTimeOffset CreatedAt);
public sealed record AccountResult(bool Success, string Message);
public sealed record MemberAccess(bool Allowed, MembershipTier? RequiredTier)
{
    public string Hint => RequiredTier is { } tier ? $"{Membership.Name(tier)}及以上开放" : "姿态分级待公布";
}

/// <summary>The account provider owns levels. Registration never accepts a level from the caller.</summary>
public interface IAccountService
{
    MemberAccount? CurrentAccount { get; }
    event Action? Changed;
    Task<AccountResult> RegisterAsync(string username, string nickname, string password, string confirmation);
    Task<AccountResult> LoginAsync(string username, string password);
    void Logout();
}

public static class Membership
{
    // Public preview policy is independent of account identity and purchased rank.
    public const bool TestingOpen = true;
    public static IReadOnlyList<MembershipTier> Tiers { get; } = Array.AsReadOnly(new[]
        { MembershipTier.BlackGold, MembershipTier.Platinum, MembershipTier.Gold, MembershipTier.Silver, MembershipTier.Brass });
    // Add the owner's future pose decisions here. No unannounced pose restrictions are invented.
    public static IReadOnlyDictionary<string, MembershipTier> Requirements { get; } =
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, MembershipTier>(new Dictionary<string, MembershipTier>
        { ["chat"] = MembershipTier.Gold });
    public static string Name(MembershipTier tier) => tier switch
    {
        MembershipTier.BlackGold => "黑金", MembershipTier.Platinum => "白金", MembershipTier.Gold => "黄金",
        MembershipTier.Silver => "白银", _ => "黄铜"
    };
    public static MemberAccess Access(string feature, MembershipTier? tier)
        => TestingOpen ? new(true, Requirements.TryGetValue(feature, out var minimum) ? minimum : null) : PlannedAccess(feature, tier);
    public static MemberAccess PlannedAccess(string feature, MembershipTier? tier)
    {
        if (!Requirements.TryGetValue(feature, out var minimum)) return new(true, null);
        return new(tier is { } current && Enum.IsDefined(current) && current >= minimum, minimum);
    }
}
