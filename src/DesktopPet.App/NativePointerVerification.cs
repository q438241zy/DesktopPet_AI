using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Checks desktop HWND targeting, independently of WPF's routed-event tests.</summary>
internal static class NativePointerVerification
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint hwnd,int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint hwnd,out uint process);

    internal static async Task Run(PetWindow pet,string output,bool allAppearances=false)
    {
        Directory.CreateDirectory(output);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(pet.Dispatcher));
        var samples=new List<object>();
        int misses=0;
        var checks=new List<string>();
        void Check(bool pass,string label){if(!pass)throw new InvalidOperationException(label);checks.Add(label);}
        var hwnd=new WindowInteropHelper(pet).Handle;
        var appearances=allAppearances
            ? pet.Catalog.Characters.SelectMany(c=>Catalog.BuiltInOutfits.Select(outfit=>(c.Id,outfit)))
            : new[]{("claude","original"),("claude","sports"),("whale","swim"),("deepseek-adult","sports")};
        foreach(var (id,outfit) in appearances)
        foreach(int direction in new[]{-1,1})
        {
            pet.SelectCharacter(id);pet.State.Outfits[id]=outfit;
            pet.State.Wander=false;pet.State.Topmost=true;pet.State.Size=200;pet.State.ReducedMotion=false;
            pet.ApplySettings();pet.StopInteraction();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
            pet.StartWalk(false,direction);
            await Task.Delay(180);
            for(int sample=0;sample<(allAppearances?3:12);sample++)
            {
                foreach(double fraction in new[]{.3,.5,.7})
                {
                    var input=pet.InputSurface;
                    var local=new Point(input.ActualWidth*.5,input.ActualHeight*fraction);
                    var screen=input.PointToScreen(local);
                    var native=WindowFromPoint(new NativePoint{X=(int)Math.Round(screen.X),Y=(int)Math.Round(screen.Y)});
                    GetWindowThreadProcessId(native,out uint process);
                    string targetProcess;
                    try{targetProcess=System.Diagnostics.Process.GetProcessById((int)process).ProcessName;}catch{targetProcess="exited";}
                    bool owns=native==hwnd;
                    if(!owns)misses++;
                    samples.Add(new{id,outfit,direction,sample,fraction,x=screen.X,y=screen.Y,owns,
                        native=native.ToInt64(),process,targetProcess,style=GetWindowLongPtr(hwnd,-20).ToInt64(),
                        input.IsVisible,input.IsHitTestVisible,input.ActualWidth,input.ActualHeight,
                        action=pet.CurrentAction,renderer=pet.ActiveAuthoredVisual is not null?"authored":"image",
                        wpfHit=ReferenceEquals(pet.InputHitTest(input.TranslatePoint(local,pet)),input)});
                }
                await Task.Delay(32);
            }
            pet.Top=pet.WorkArea.Bottom-468;pet.StopInteraction();
            await Task.Delay(35);
            var below=pet.PointToScreen(new Point(280,480));
            Check(WindowFromPoint(new NativePoint{X=(int)below.X,Y=(int)below.Y})!=hwnd,id+"/"+outfit+": input surface does not cover taskbar below feet");
            pet.SetClickThrough(true);
            await Task.Delay(15);
            var at=pet.InputSurface.PointToScreen(new Point(pet.InputSurface.ActualWidth*.5,pet.InputSurface.ActualHeight*.45));
            Check(WindowFromPoint(new NativePoint{X=(int)at.X,Y=(int)at.Y})!=hwnd,id+"/"+outfit+": explicit mouse passthrough remains available");
            pet.SetClickThrough(false);
            pet.StopInteraction();
        }
        File.WriteAllText(Path.Combine(output,"native-pointer-report.json"),JsonSerializer.Serialize(new{scope="HWND hit testing only; real gestures are checked by --verify-system-pointer",samples,misses,checks},Json.Options));
        if(misses>0)throw new InvalidOperationException($"Windows did not target the pet for {misses} walking body samples.");
    }
}
