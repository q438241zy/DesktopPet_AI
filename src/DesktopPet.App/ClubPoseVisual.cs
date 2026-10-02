using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Continuous motion measured between authored poses, without blended double limbs.</summary>
internal sealed class ClubPoseVisual : Viewport3D
{
    internal sealed record PoseData(double[] Rect, int[] Pixels, double ReferenceHeight);
    internal sealed record MotionData(int Version, string Sha256, int FlowSize, PoseData[] Poses, Dictionary<string,string[]> Flow);
    private readonly MotionData data;
    private readonly Dictionary<int,byte[][]> fields = [];
    private readonly MeshGeometry3D mesh = new();
    private readonly GeometryModel3D model;
    private readonly ArtCache art;
    private readonly Character character;
    private readonly Sprite sheet;
    private int textureFrame=-1;
    private const int Columns=40, Rows=48;
    internal string Appearance { get; }
    internal ClubPose Current { get; private set; }
    internal ClubPoseVisual(Character character, string outfit, Sprite sheet, ArtCache art)
    {
        this.character=character;this.sheet=sheet;this.art=art;Appearance=character.Id+"/"+outfit;
        // Only built-in, measured club sheets opt in; custom packs keep the standard renderer.
        var path=Path.ChangeExtension(Character.SafeFile(character.Root,sheet.File),".json");
        if (new FileInfo(path).Length > 4*1024*1024) throw new InvalidDataException("动作元数据过大。");
        data=JsonSerializer.Deserialize<MotionData>(File.ReadAllText(path),Json.Options) ?? throw new InvalidDataException("动作元数据为空。");
        if(data.Version!=1 || data.Poses.Length!=24 || data.FlowSize!=72) throw new InvalidDataException("动作元数据版本无效。");
        IsHitTestVisible=false; ClipToBounds=false;
        Camera=new OrthographicCamera(new Point3D(0,0,2),new Vector3D(0,0,-1),new Vector3D(0,1,0),1);
        for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)mesh.TextureCoordinates.Add(new Point(x/(double)Columns,y/(double)Rows));
        for(int y=0;y<Rows;y++)for(int x=0;x<Columns;x++)
        {
            int a=y*(Columns+1)+x,b=a+1,c=a+Columns+1,d=c+1;
            foreach(int index in new[]{a,c,b,b,c,d})mesh.TriangleIndices.Add(index);
        }
        model=new GeometryModel3D { Geometry=mesh };
        var scene=new Model3DGroup();scene.Children.Add(new AmbientLight(Colors.White));scene.Children.Add(model);
        Children.Add(new ModelVisual3D { Content=scene });
    }
    private byte[] ReadField(string encoded)
    {
        using var input=new GZipStream(new MemoryStream(Convert.FromBase64String(encoded)),CompressionMode.Decompress);
        byte[] buffer=new byte[data.FlowSize*data.FlowSize*2];input.ReadExactly(buffer);return buffer;
    }
    private Vector Flow(byte[] field, double x, double y)
    {
        int size=data.FlowSize;
        double px=Math.Clamp(x*size-.5,0,size-1),py=Math.Clamp(y*size-.5,0,size-1);
        int ix=(int)px,iy=(int)py,jx=Math.Min(size-1,ix+1),jy=Math.Min(size-1,iy+1);
        double fx=px-ix,fy=py-iy;
        double Sample(int channel) => ((field[(iy*size+ix)*2+channel]*(1-fx)+field[(iy*size+jx)*2+channel]*fx)*(1-fy)
            +(field[(jy*size+ix)*2+channel]*(1-fx)+field[(jy*size+jx)*2+channel]*fx)*fy-128)/255*.6;
        return new(Sample(0),Sample(1));
    }
    internal void Update(ClubPose pose)
    {
        Current=pose;
        bool first=pose.Amount<.5; int index=first?pose.A:pose.B;
        if(index!=textureFrame)
        {
            textureFrame=index;
            var brush=new ImageBrush(art.Frame(character,sheet,index)) { Stretch=Stretch.Fill };brush.Freeze();
            var material=new DiffuseMaterial(brush);material.Freeze();model.Material=model.BackMaterial=material;
        }
        byte[]? flow=null;
        if(pose.A!=pose.B)
        {
            if(!fields.TryGetValue(pose.A,out var pair)) fields[pose.A]=pair=data.Flow[pose.A.ToString()].Select(ReadField).ToArray();
            flow=pair[first?0:1];
        }
        var rect=data.Poses[index].Rect;double amount=first?pose.Amount:1-pose.Amount;
        var positions=new Point3DCollection((Columns+1)*(Rows+1));
        for(int y=0;y<=Rows;y++)for(int x=0;x<=Columns;x++)
        {
            double u=rect[0]+rect[2]*x/Columns,v=rect[1]+rect[3]*y/Rows;
            var delta=flow is null?new Vector():Flow(flow,u,v)*amount;
            positions.Add(new Point3D(u+delta.X-.5,.5-v-delta.Y,0));
        }
        positions.Freeze();mesh.Positions=positions;
    }
}
