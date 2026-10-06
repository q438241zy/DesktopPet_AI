namespace DesktopPet.Core;

/// <summary>Measured full-body drawings. Every atlas retains a fixed anatomical scale.</summary>
public sealed record FivePose(string Atlas, int Frame, double FootX, double FootY,
    Dictionary<string, SpriteAnchor> Anchors, double FrontY = 0, double BoxWidth = 0);

public sealed class FiveArtwork
{
    public Dictionary<string, Sprite> Atlases { get; set; } = [];
    public Dictionary<string, FivePose> Poses { get; set; } = [];
    public void Validate()
    {
        if (Atlases is null || Poses is null || Atlases.Count is <1 or >8 || Poses.Count is <20 or >24
            || FiveInteraction.PoseNames.Any(n=>!Poses.ContainsKey(n)))
            throw new InvalidDataException("互动图集须含全部20个姿势。");
        foreach(var p in Poses.Values)
        {
            if(p is null || !Atlases.TryGetValue(p.Atlas,out var atlas) || atlas is null || atlas.Cells is null
                || atlas.ReferenceHeightPixels<=0 || p.Frame<0 || p.Frame>=atlas.Cells.Length || p.Anchors is null || p.Anchors.Count>6)
                throw new InvalidDataException("互动姿势或图集无效。");
            var cell=atlas.Cells[p.Frame];
            if(cell is null || !double.IsFinite(p.FootX) || !double.IsFinite(p.FootY) || p.FootX<0 || p.FootX>cell.Width || p.FootY<0 || p.FootY>cell.Height
                || !double.IsFinite(p.FrontY) || !double.IsFinite(p.BoxWidth) || Math.Abs(p.FrontY)>6144 || p.BoxWidth is <0 or >6144
                || p.Anchors.Values.Any(a=>a is null || !double.IsFinite(a.X) || !double.IsFinite(a.Y) || Math.Abs(a.X)>6144 || Math.Abs(a.Y)>6144))
                throw new InvalidDataException("互动接触坐标无效。");
        }
        foreach(var (pose,anchor) in new[]{("high-prep","hand"),("high-ready","hand"),("high-contact","hand"),("high-recoil","hand"),("receive","receive"),("gift","gift"),("gift-empty","giftSlot"),("read","book"),("page","book")})
            if(!Poses[pose].Anchors.ContainsKey(anchor))throw new InvalidDataException("互动缺少实际接触坐标。");
    }
}
