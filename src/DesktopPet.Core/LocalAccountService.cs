using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DesktopPet.Core;

/// <summary>Local accounts and an in-memory login. Replace this provider for a real server; do not treat local levels as paid entitlements.</summary>
public sealed class LocalAccountService : IAccountService
{
    private const int Iterations = 600_000;
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeProvider time;
    private int failedLogins;
    private DateTimeOffset retryAfter;
    public MemberAccount? CurrentAccount { get; private set; }
    public event Action? Changed;

    public LocalAccountService(string root, TimeProvider? time = null)
    { path = Path.Combine(root, "member-accounts.json"); this.time = time ?? TimeProvider.System; }

    public async Task<AccountResult> RegisterAsync(string username, string nickname, string password, string confirmation)
    {
        username = username.Trim(); nickname = nickname.Trim();
        if (!Regex.IsMatch(username, "^[A-Za-z0-9][A-Za-z0-9_-]{3,23}$", RegexOptions.CultureInvariant))
            return new(false, "账号用 4–24 位字母、数字、下划线或短横线。");
        if (nickname.Length is < 1 or > 20 || nickname.Any(char.IsControl)) return new(false, "昵称需要 1–20 个字符。");
        if (password.Length is < 15 or > 128 || string.IsNullOrWhiteSpace(password)) return new(false, "密码需要 15–128 个字符，可以包含空格。");
        if (password != confirmation) return new(false, "两次密码不一致。");
        await gate.WaitAsync();
        try
        {
            var database = Read();
            if (database.Accounts.Any(a => a.Profile.Username.Equals(username, StringComparison.OrdinalIgnoreCase)))
                return new(false, "这个账号已注册，请登录或换一个账号。");
            if (database.Accounts.Count >= 200) return new(false, "本机账号数量已达上限。");
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32));
            var profile = new MemberAccount(Guid.NewGuid().ToString("N"), username, nickname, MembershipTier.Brass, time.GetUtcNow());
            database.Accounts.Add(new(profile, Convert.ToBase64String(salt), Convert.ToBase64String(hash), Iterations));
            Write(database);
            CryptographicOperations.ZeroMemory(hash);
            CurrentAccount = profile; failedLogins = 0; retryAfter = default;
            Changed?.Invoke(); return new(true, "注册成功，欢迎成为黄铜会员。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { return new(false, "本机会员资料无法读取或保存，原资料已保留。"); }
        finally { gate.Release(); }
    }

    public async Task<AccountResult> LoginAsync(string username, string password)
    {
        if (username.Length > 128 || password.Length > 128) return new(false, "账号或密码不正确。");
        await gate.WaitAsync();
        try
        {
            if (time.GetUtcNow() < retryAfter) return new(false, "尝试次数较多，请一分钟后再试。");
            var account = Read().Accounts.FirstOrDefault(a => a.Profile.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
            // Derive a dummy key too, so an unknown username does not return immediately.
            byte[] salt = account is null ? RandomNumberGenerator.GetBytes(16) : Convert.FromBase64String(account.Salt);
            byte[] expected = account is null ? new byte[32] : Convert.FromBase64String(account.PasswordHash);
            byte[] actual = await Task.Run(() => Rfc2898DeriveBytes.Pbkdf2(password, salt, account?.Iterations ?? Iterations, HashAlgorithmName.SHA256, 32));
            bool valid = CryptographicOperations.FixedTimeEquals(expected, actual) && account is not null;
            CryptographicOperations.ZeroMemory(actual);
            if (!valid)
            {
                if (++failedLogins >= 5) { retryAfter = time.GetUtcNow().AddMinutes(1); failedLogins = 0; }
                return new(false, "账号或密码不正确。");
            }
            CurrentAccount = account!.Profile; failedLogins = 0; retryAfter = default;
            Changed?.Invoke(); return new(true, "登录成功。");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException or FormatException)
        { return new(false, "本机会员资料无法读取，原资料已保留。"); }
        finally { gate.Release(); }
    }

    public void Logout() { CurrentAccount = null; Changed?.Invoke(); }
    private Database Read()
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 2 * 1024 * 1024) throw new InvalidDataException("会员资料过大。");
        var database = JsonSerializer.Deserialize<Database>(File.ReadAllText(path), Json.Options) ?? throw new InvalidDataException();
        if (database.Version != 1 || database.Accounts is null || database.Accounts.Count > 200
            || database.Accounts.Any(a => a is null || a.Profile is null || !Enum.IsDefined(a.Profile.Tier)
                || string.IsNullOrWhiteSpace(a.Profile.Id) || string.IsNullOrWhiteSpace(a.Profile.Username)
                || a.Iterations != Iterations || !ValidBase64(a.Salt, 16) || !ValidBase64(a.PasswordHash, 32))
            || database.Accounts.Select(a => a.Profile.Username).Distinct(StringComparer.OrdinalIgnoreCase).Count() != database.Accounts.Count)
            throw new InvalidDataException("会员资料无效。");
        return database;
    }
    private static bool ValidBase64(string value, int count)
    { try { return value is not null && Convert.FromBase64String(value).Length == count; } catch (FormatException) { return false; } }
    private void Write(Database database)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(database, Json.Options)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private sealed class Database { public Database() { } public int Version { get; set; } = 1; public List<AccountRecord> Accounts { get; set; } = []; }
    private sealed record AccountRecord(MemberAccount Profile, string Salt, string PasswordHash, int Iterations);
}
