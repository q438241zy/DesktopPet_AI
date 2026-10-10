using System.Windows.Media.Imaging;

namespace DesktopPet.App;

internal static class VerificationImages
{
    // RenderTargetBitmap of the test app only; never captures the user's desktop.
    internal static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
        using var file = File.Create(path); encoder.Save(file);
    }
}
