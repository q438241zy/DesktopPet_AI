using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Texture the active outfit on a deformable skeletal mesh, without changing the portrait.</summary>
internal sealed class DanceVisual : Viewport3D
{
    private readonly MeshGeometry3D mesh;
    public DanceRig Rig { get; }
    public BitmapSource Texture { get; }
    public RigPose Pose { get; private set; }
    public DanceVisual(BitmapSource texture, string family, string outfit)
    {
        Texture = texture; Rig = new DanceRig(family, outfit, (double)texture.PixelWidth / texture.PixelHeight);
        Pose = Rig.Pose(0); IsHitTestVisible = false; ClipToBounds = false;
        Camera = new OrthographicCamera(new Point3D(0, 0, 2), new Vector3D(0, 0, -1), new Vector3D(0, 1, 0), 1);
        mesh = new MeshGeometry3D();
        for (int y = 0; y <= DanceRig.Rows; y++)
            for (int x = 0; x <= DanceRig.Columns; x++) mesh.TextureCoordinates.Add(new Point(x / (double)DanceRig.Columns, y / (double)DanceRig.Rows));
        for (int y = 0; y < DanceRig.Rows; y++)
            for (int x = 0; x < DanceRig.Columns; x++)
            {
                int a = y * (DanceRig.Columns + 1) + x, b = a + 1, c = a + DanceRig.Columns + 1, d = c + 1;
                foreach (int index in new[] { a, c, b, b, c, d }) mesh.TriangleIndices.Add(index);
            }
        var brush = new ImageBrush(texture) { Stretch = Stretch.Fill }; brush.Freeze();
        // Emissive-only materials add RGB without coverage in WPF's transparent window renderer.
        // A diffuse texture lit by white ambient light preserves the source's premultiplied alpha.
        var material = new DiffuseMaterial(brush); material.Freeze();
        var scene = new Model3DGroup(); scene.Children.Add(new AmbientLight(Colors.White));
        scene.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        Children.Add(new ModelVisual3D { Content = scene });
        Update(0);
    }
    public void Update(double elapsed)
    {
        Pose = Rig.Pose(elapsed);
        var positions = new Point3DCollection(Rig.Skin(Pose).Select(p => new Point3D(p.X, -p.Y, 0))); positions.Freeze();
        mesh.Positions = positions;
    }
}
