namespace DesktopPet.Core;

public enum ItemKind { Sport, Everyday }
public sealed record Collectible(string Id, string Name, ItemKind Kind, bool Edible = false,
    double Diameter = 30, double Bounce = .65, double Gravity = 850, double Drag = .08);

public static class Collectibles
{
    public static IReadOnlyList<Collectible> All { get; } = Array.AsReadOnly<Collectible>([
        new("baseball", "棒球", ItemKind.Sport, Diameter: 27, Bounce: .57),
        new("basketball", "篮球", ItemKind.Sport, Diameter: 34, Bounce: .78),
        new("football", "足球", ItemKind.Sport, Diameter: 33, Bounce: .69),
        new("volleyball", "排球", ItemKind.Sport, Diameter: 32, Bounce: .73),
        new("tennis", "网球", ItemKind.Sport, Diameter: 25, Bounce: .83),
        new("pingpong", "乒乓球", ItemKind.Sport, Diameter: 22, Bounce: .9, Gravity: 620, Drag: .24),
        new("badminton", "羽毛球", ItemKind.Sport, Diameter: 34, Bounce: .22, Gravity: 430, Drag: 1.1),
        new("rugby", "橄榄球", ItemKind.Sport, Diameter: 35, Bounce: .51),
        new("golf", "高尔夫球", ItemKind.Sport, Diameter: 22, Bounce: .62),
        new("bowling", "保龄球", ItemKind.Sport, Diameter: 34, Bounce: .28, Gravity: 1080),
        new("bread", "面包", ItemKind.Everyday, true),
        new("rice", "米饭", ItemKind.Everyday, true),
        new("apple", "苹果", ItemKind.Everyday, true),
        new("banana", "香蕉", ItemKind.Everyday, true),
        new("cookie", "饼干", ItemKind.Everyday, true),
        new("milk", "牛奶", ItemKind.Everyday, true),
        new("shell", "贝壳", ItemKind.Everyday),
        new("leaf", "树叶", ItemKind.Everyday),
        new("pencil", "铅笔", ItemKind.Everyday),
        new("key", "钥匙", ItemKind.Everyday)
    ]);
    public static IReadOnlyList<Collectible> Sports { get; } = All.Where(x => x.Kind == ItemKind.Sport).ToArray();
    public static IReadOnlyList<Collectible> Food { get; } = All.Where(x => x.Edible).ToArray();
    public static Collectible Get(string id) => All.First(x => x.Id == id);
    public static Collectible? FromSavedName(string name) => All.FirstOrDefault(x => x.Name == name || x.Id == name
        || x.Id == "shell" && name == "一枚贝壳" || x.Id == "leaf" && name == "一片四叶草");
}

/// <summary>Shuffle a complete bag before drawing: every item appears once per round, with no immediate repeat across rounds.</summary>
public sealed class ItemDrawBag(IReadOnlyList<Collectible> items, Random? random = null)
{
    private readonly Random rng = random ?? Random.Shared;
    private readonly Queue<Collectible> bag = new();
    private string? previous;
    public Collectible Draw()
    {
        if (items.Count == 0) throw new InvalidOperationException("The draw bag is empty.");
        if (bag.Count == 0)
        {
            var shuffled = items.ToArray();
            for (int i = shuffled.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]); }
            if (shuffled.Length > 1 && shuffled[0].Id == previous) (shuffled[0], shuffled[1]) = (shuffled[1], shuffled[0]);
            foreach (var item in shuffled) bag.Enqueue(item);
        }
        var result = bag.Dequeue(); previous = result.Id; return result;
    }
}

public readonly record struct ToyFlight(double X, double Y, double Vx, double Vy, double Angle);
public static class ToyPhysics
{
    public static ToyFlight Step(ToyFlight state, Collectible toy, double seconds, double left, double right, double floor)
    {
        double dt = Math.Clamp(seconds, 0, .05), damping = Math.Exp(-toy.Drag * dt);
        double vx = state.Vx * damping, vy = (state.Vy + toy.Gravity * dt) * damping;
        double x = state.X + vx * dt, y = state.Y + vy * dt;
        if (y > floor - toy.Diameter) { y = floor - toy.Diameter; vy = -Math.Abs(vy) * toy.Bounce; vx *= .88; if (Math.Abs(vy) < 18) vy = 0; }
        if (x < left || x > right - toy.Diameter) { x = Math.Clamp(x, left, Math.Max(left, right - toy.Diameter)); vx *= -.7; }
        return new(x, y, vx, vy, (state.Angle + vx / toy.Diameter * dt * 75) % 360);
    }
}
