using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Opt-in OS input regression, restricted to the isolated test pet HWND.</summary>
internal static class SystemPointerVerification
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X,Y; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X,Y;public uint Data,Flags,Time;public nint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardInput { public ushort Key,Scan;public uint Flags,Time;public nint Extra; }
    [StructLayout(LayoutKind.Explicit)] private struct InputData { [FieldOffset(0)] public MouseInput Mouse;[FieldOffset(0)] public KeyboardInput Keyboard; }
    [StructLayout(LayoutKind.Sequential)] private struct Input { public uint Type;public InputData Data; }
    [DllImport("user32.dll")] private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x,int y);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(NativePoint point);
    [DllImport("user32.dll")] private static extern nint GetCapture();
    [DllImport("user32.dll")] private static extern nint SetCapture(nint hwnd);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    internal static async Task Run(PetWindow pet,string output,bool all=false)
    {
        Directory.CreateDirectory(output);
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(pet.Dispatcher));
        if((GetAsyncKeyState(1)&0x8000)!=0)throw new InvalidOperationException("Release the mouse before running the OS pointer check.");
        GetCursorPos(out var previous);var hwnd=new WindowInteropHelper(pet).Handle;
        var checks=new List<object>();bool buttonHeld=false,shiftHeld=false,rightHeld=false;NativePoint last=previous;
        void Check(bool pass,string label){checks.Add(new{pass,label,action=pet.CurrentAction});if(!pass)throw new InvalidOperationException(label);}
        void Button(bool down)
        {
            var input=new Input{Data=new InputData{Mouse=new MouseInput{Flags=down?2u:4u}}};
            if(SendInput(1,[input],Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("OS did not accept the test mouse input.");
            buttonHeld=down;
        }
        void Shift(bool down)
        {
            var input=new Input{Type=1,Data=new InputData{Keyboard=new KeyboardInput{Key=0x10,Flags=down?0u:2u}}};
            if(SendInput(1,[input],Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("OS did not accept the test Shift key.");
            shiftHeld=down;
        }
        void Right(bool down)
        {
            var input=new Input{Data=new InputData{Mouse=new MouseInput{Flags=down?8u:16u}}};
            if(SendInput(1,[input],Marshal.SizeOf<Input>())!=1)throw new InvalidOperationException("OS did not accept right mouse input.");
            rightHeld=down;
        }
        void Move(Point point)
        {
            last=new NativePoint{X=(int)Math.Round(point.X),Y=(int)Math.Round(point.Y)};
            if(!SetCursorPos(last.X,last.Y))throw new InvalidOperationException("Unable to position test pointer.");
            GetCursorPos(out var actual);
            checks.Add(new{label="OS cursor placement",requested=new[]{last.X,last.Y},actual=new[]{actual.X,actual.Y}});
        }
        void RequireUnchangedCursor()
        {
            GetCursorPos(out var cursor);
            if(Math.Abs(cursor.X-last.X)>1 || Math.Abs(cursor.Y-last.Y)>1)
                throw new OperationCanceledException("External pointer input interrupted the isolated check; no application failure is inferred.");
        }
        async Task<Point> Press()
        {
            pet.Activate();await Task.Delay(60);
            if((GetAsyncKeyState(1)&0x8000)!=0)throw new OperationCanceledException("External mouse press interrupted the isolated check.");
            var input=pet.InputSurface;
            var at=input.PointToScreen(new Point(input.ActualWidth*.5,input.ActualHeight*.45));
            Move(at);await Task.Delay(25);
            RequireUnchangedCursor();
            Check(WindowFromPoint(last)==hwnd,"OS pointer is over the isolated test pet before pressing");
            Button(true);await Task.Delay(120);
            GetCursorPos(out var actual);
            checks.Add(new{label="After OS press",mouseDown=(GetAsyncKeyState(1)&0x8000)!=0,capture=GetCapture().ToInt64(),hwnd=hwnd.ToInt64(),action=pet.CurrentAction,expected=new[]{last.X,last.Y},actual=new[]{actual.X,actual.Y}});
            RequireUnchangedCursor();
            return at;
        }
        try
        {
            var appearances=all ? pet.Catalog.Characters.SelectMany(c=>Catalog.BuiltInOutfits.Select(outfit=>(c.Id,outfit)))
                : new[]{("claude","original"),("claude","sports"),("whale","sports"),("deepseek-adult","sports")};
            int phase=0;
            foreach(var (id,outfit) in appearances)
            foreach(int direction in new[]{-1,1})
            {
                pet.SelectCharacter(id);pet.State.Outfits[id]=outfit;pet.State.Wander=false;pet.State.Topmost=true;pet.State.Size=200;
                pet.ApplySettings();pet.StopInteraction();pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;
                pet.Top=pet.WorkArea.Bottom-468;
                pet.StartWalk(false,direction);await Task.Delay(160+(phase++%5)*115);
                string label=id+"/"+outfit+"/"+direction;
                Check(pet.CurrentAction=="walk",label+": walking before system input");
                var clickAt=await Press();
                Check(pet.CurrentAction=="idle",label+": actual mouse down stops walking");
                Check(GetCapture()==hwnd,label+": actual mouse down captures the test window");
                GetCursorPos(out var clickCursor);
                Check(Math.Abs(clickCursor.X-clickAt.X)<2 && Math.Abs(clickCursor.Y-clickAt.Y)<2,label+": test click had no external cursor movement");
                Button(false);await Task.Delay(120);
                Check(pet.CurrentAction is "headpat" or "poke" or "tickle",label+": actual release starts touch interaction");
                pet.StopInteraction();pet.StartWalk(false,direction);await Task.Delay(120);
                var at=await Press();
                Move(at+new Vector(40,-130));await Task.Delay(120);
                RequireUnchangedCursor();
                checks.Add(new{label="After OS drag",mouseDown=(GetAsyncKeyState(1)&0x8000)!=0,capture=GetCapture().ToInt64(),action=pet.CurrentAction});
                Check(pet.CurrentAction=="pickup",label+": actual mouse movement lifts walking pet");
                Button(false);await Task.Delay(120);double top=pet.Top,left=pet.Left;
                await Task.Delay(180);
                Check(pet.CurrentAction=="idle"&&!pet.IsDropping&&Math.Abs(pet.Top-top)<.01&&Math.Abs(pet.Left-left)<.01,label+": ordinary release keeps chosen position");
            }
            if(!all)
            {
                Check((GetAsyncKeyState(0x10)&0x8000)==0,"Shift is free before isolated modifier check");
                foreach(var id in new[]{"whale","deepseek-adult"})
                {
                    pet.SelectCharacter(id);pet.State.Outfits[id]="sports";pet.State.Wander=true;pet.ApplySettings();pet.StopInteraction();
                    pet.Top=pet.WorkArea.Bottom-468;pet.StartWalk(false,1);await Task.Delay(180);
                    var at=await Press();Move(at+new Vector(40,-180));await Task.Delay(140);
                    Check(pet.CurrentAction=="pickup",id+": lifted before Shift release");
                    Shift(true);await Task.Delay(30);Button(false);await Task.Delay(70);Shift(false);
                    Check(pet.IsDropping,id+": physical Shift release requests falling");
                    await Task.Delay(1700);
                    Check(pet.CurrentAction=="walk"&&!pet.IsDropping,id+": physical Shift landing resumes taskbar walking");
                    pet.SetClickThrough(true);await Task.Delay(40);
                    at=pet.InputSurface.PointToScreen(new Point(pet.InputSurface.ActualWidth*.5,pet.InputSurface.ActualHeight*.45));
                    Check(WindowFromPoint(new NativePoint{X=(int)Math.Round(at.X),Y=(int)Math.Round(at.Y)})!=hwnd,id+": explicit click-through still excludes the pet");
                    pet.SetClickThrough(false);pet.StopInteraction();
                    pet.Top=pet.WorkArea.Bottom-468;pet.StartWalk(false,1);await Task.Delay(180);
                    var body=pet.InputSurface;
                    Move(body.PointToScreen(new Point(body.ActualWidth*.5,body.ActualHeight*.45)));await Task.Delay(30);
                    Check(WindowFromPoint(last)==hwnd,id+": right click targets isolated pet");
                    Right(true);await Task.Delay(50);Right(false);await Task.Delay(160);
                    Check(pet.IsMenuOpen && pet.CurrentAction=="walk",id+": physical right click opens menu without stopping walking");
                    pet.HandleRightClick();

                    var probe=new Window{Title="Isolated pointer check",Width=100,Height=90,Left=pet.WorkArea.Left+40,Top=pet.WorkArea.Top+40,
                        Background=System.Windows.Media.Brushes.LightGray,Topmost=true,ShowInTaskbar=false,WindowStyle=WindowStyle.None};
                    try
                    {
                        probe.Show();await Task.Delay(60);var probeHandle=new WindowInteropHelper(probe).Handle;
                        Move(probe.PointToScreen(new Point(40,40)));await Task.Delay(30);
                        Check(WindowFromPoint(last)==probeHandle,id+": drag-in starts in isolated sibling window");
                        Button(true);await Task.Delay(40);SetCapture(probeHandle);
                        Move(body.PointToScreen(new Point(body.ActualWidth*.5,body.ActualHeight*.45)));await Task.Delay(100);
                        Check(GetCapture()==probeHandle && pet.CurrentAction=="walk",id+": held mouse entering pet is not stolen");
                        Button(false);await Task.Delay(50);ReleaseCapture();
                    }
                    finally{probe.Close();}
                    pet.Activate();pet.StopInteraction();pet.Top=pet.WorkArea.Bottom-468;pet.StartWalk(false,1);await Task.Delay(140);
                    at=await Press();Move(at+new Vector(30,-140));await Task.Delay(100);ReleaseCapture();await Task.Delay(30);Button(false);await Task.Delay(120);
                    Check(!pet.IsDropping && pet.CurrentAction=="idle",id+": cancelled capture safely leaves the pet in the air");
                }
            }
        }
        finally
        {
            if(buttonHeld)Button(false);
            if(shiftHeld)Shift(false);
            if(rightHeld)Right(false);
            GetCursorPos(out var current);
            if(current.X==last.X&&current.Y==last.Y)SetCursorPos(previous.X,previous.Y);
            File.WriteAllText(Path.Combine(output,"system-pointer-report.json"),JsonSerializer.Serialize(checks,DesktopPet.Core.Json.Options));
        }
    }
}
