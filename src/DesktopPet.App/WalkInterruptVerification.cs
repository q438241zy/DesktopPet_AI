using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class WalkInterruptVerification
{
    internal static async Task Run(PetWindow pet,string output)
    {
        Directory.CreateDirectory(output);
        var checks=new List<string>();
        void Check(bool pass,string label){if(!pass)throw new InvalidOperationException(label);checks.Add("PASS "+label);}
        var canvas=(Canvas)pet.Content;
        double VisualX()=>pet.Left+canvas.RenderTransform.Value.OffsetX;
        await Task.Delay(300);
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(pet.Dispatcher,DispatcherPriority.Normal));
        foreach(var c in pet.Catalog.Characters)
        foreach(string outfit in Catalog.BuiltInOutfits)
        foreach(int direction in new[]{-1,1})
        {
            pet.BeginPreview();pet.SelectCharacter(c.Id);pet.State.Outfits[c.Id]=outfit;
            pet.State.Wander=true;pet.State.ReducedMotion=false;pet.State.Size=220;pet.ApplySettings();pet.StopInteraction();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
            pet.StartWalk(true,direction);pet.AdvancePreview(180);
            pet.IsHitTestVisible=true;canvas.UpdateLayout();
            string label=c.Id+"/"+outfit+"/"+direction;
            var idle=c.Resolve(outfit,"idle",0);double height=pet.State.Size*pet.Art.VisibleHeight(pet.Art.Frame(c,idle.Sprite,0));
            Check(ReferenceEquals(canvas.InputHitTest(new Point(280,468-height*.48)),pet.InputSurface),label+": visible walking body receives input");
            var down=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent};
            pet.InputSurface.RaiseEvent(down);
            Check(down.Handled && pet.CurrentAction!="walk" && !pet.IsExploring,label+": routed mouse press cancels walk and exploration");
            double heldX=VisualX();pet.AdvancePreview(1200);
            Check(Math.Abs(VisualX()-heldX)<.01,label+": holding stays still");
            var up=new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent};
            pet.InputSurface.RaiseEvent(up);
            Check(up.Handled && pet.CurrentAction is "headpat" or "poke" or "tickle",label+$": routed click begins care (handled={up.Handled},action={pet.CurrentAction})");
            pet.StopInteraction();pet.StartWalk(false,direction);
            var screen=new Point(pet.Left+280,pet.Top+350);
            Check(pet.BeginPointerGesture(screen),label+": walking can be grabbed");
            Check(pet.MovePointerGesture(screen+new Vector(30,-110)),label+": movement begins lifting");
            pet.EndPointerGesture(false);double top=pet.Top,x=VisualX();pet.AdvancePreview(2200);
            Check(!pet.IsDropping && pet.CurrentAction=="idle" && Math.Abs(pet.Top-top)<.01 && Math.Abs(VisualX()-x)<.01,label+": release in air stays placed");
            pet.StopInteraction();
            await Dispatcher.Yield(DispatcherPriority.Background);
        }
        pet.State.Wander=false;pet.EndPreview();pet.IsHitTestVisible=true;
        foreach(string id in new[]{"claude","gpt-adult"})
        foreach(string outfit in new[]{"original","sports"})
        {
            pet.SelectCharacter(id);pet.State.Outfits[id]=outfit;pet.ApplySettings();
            pet.Left=pet.WorkArea.Left+pet.WorkArea.Width/2-280;pet.Top=pet.WorkArea.Bottom-468;
            pet.StartWalk(true,1);double start=VisualX();await Task.Delay(350);
            Check(pet.WalkUsesRendering && VisualX()>start,id+"/"+outfit+": compositor really moves before interruption");
            pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonDownEvent});
            Check(pet.InputSurface.IsMouseCaptured && !pet.IsExploring,id+"/"+outfit+": stable input owns mouse capture and cancels exploration");
            double held=VisualX();await Task.Delay(350);
            Check(pet.CurrentAction=="idle" && Math.Abs(VisualX()-held)<.01,id+"/"+outfit+": actual dispatcher remains stopped while held");
            pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=UIElement.MouseLeftButtonUpEvent});
            Check(pet.CurrentAction is "headpat" or "poke" or "tickle",id+"/"+outfit+": actual routed release performs care");
            pet.StopInteraction();
        }
        File.WriteAllLines(Path.Combine(output,"walk-interrupt-check.txt"),checks.Append($"{checks.Count} walk interruption checks; 64 appearances, both directions."));
    }
}
