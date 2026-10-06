using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Records real compositor callbacks, including native window pixel rounding.</summary>
internal static class WalkVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Require(bool pass, string label) { if (!pass) throw new InvalidOperationException(label); checks.Add("PASS " + label); }
        if (!pet.State.CheckedIn(DateOnly.FromDateTime(DateTime.Now)) && pet.State.Wander && !pet.State.ReducedMotion && pet.CanWalk && Math.Abs(pet.Top + 468 - pet.WorkArea.Bottom) < 1)
            Require(pet.CurrentAction == "walk" && !pet.IsResting, "unchecked startup at taskbar walks without forcing sleep or adding a check-in");
        pet.EndPreview();
        pet.IsHitTestVisible = false;
        pet.State.Size = 200; pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIn(DateOnly.FromDateTime(DateTime.Now));
        var canvas = (Canvas)pet.Content;
        var reports = new List<object>();
        foreach (string id in new[] { "whale", "deepseek-adult" })
        {
            pet.SelectCharacter(id); pet.State.Outfits[id] = "original"; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            pet.StartWalk(false, 1);
            await Task.Delay(400); // Exclude the first texture upload and startup work.
            var clock = Stopwatch.StartNew();
            var samples = new List<(double Time, double X, int Frame)>();
            TimeSpan lastPresentation = TimeSpan.MinValue;
            void Observe(object? sender, EventArgs args)
            {
                if (args is not RenderingEventArgs render || render.RenderingTime == lastPresentation) return;
                lastPresentation = render.RenderingTime;
                samples.Add((clock.Elapsed.TotalMilliseconds, pet.Left + canvas.RenderTransform.Value.OffsetX, pet.DrawnFrame));
            }
            CompositionTarget.Rendering += Observe;
            try { await Task.Delay(2400); }
            finally { CompositionTarget.Rendering -= Observe; pet.StopInteraction(); }
            var changes = samples.Zip(samples.Skip(1), (a,b) => new { ms = b.Time-a.Time, dx = b.X-a.X }).ToArray();
            var moved = samples.Where((s,i) => i == 0 || Math.Abs(s.X-samples[i-1].X) > .001).ToArray();
            double[] gaps = moved.Zip(moved.Skip(1), (a,b) => b.Time-a.Time).Order().ToArray();
            Require(changes.All(c=>c.dx>0), id+": each observed presentation advances without reverse or repeated positions");
            Require(!pet.WalkUsesRendering,id+": ending a walk detaches the compositor callback");
            reports.Add(new { character=id, dpi=VisualTreeHelper.GetDpi(pet).DpiScaleX, presentations=samples.Count,
                stationaryPresentations=changes.Count(c=>Math.Abs(c.dx)<.001), backwardSteps=changes.Count(c=>c.dx<-.001),
                distance=samples[^1].X-samples[0].X, expectedDistance=DesktopWalk.Speed(200,pet.Character.MotionFor("original","walk")!)*(samples[^1].Time-samples[0].Time)/1000,
                movementGapMedianMs=gaps[gaps.Length/2], movementGapP95Ms=gaps[(int)(gaps.Length*.95)],
                samples=samples.Select(s=>new { ms=s.Time,x=s.X,frame=s.Frame }) });
        }
        File.WriteAllText(Path.Combine(output,"walk-timing.json"),JsonSerializer.Serialize(reports,Json.Options));
        pet.StartWalk(false,1);
        Require(pet.WalkUsesRendering,"active walking subscribes to frame presentation");
        pet.ToggleVisible();
        Require(!pet.WalkUsesRendering && !pet.IsVisible,"hiding releases the presentation callback immediately");
        pet.ToggleVisible();
        Require(!pet.WalkUsesRendering && pet.IsVisible,"showing an idle pet does not keep a walk render loop");
        var sprite = canvas.Children.OfType<Image>().Single();
        double VisualCenter() => pet.Left + canvas.RenderTransform.Value.OffsetX + 280;
        pet.BeginPreview();
        foreach(var c in pet.Catalog.Characters)
        foreach(string outfit in Catalog.BuiltInOutfits)
        {
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id] = outfit; pet.ApplySettings();
            var clip = c.MotionFor(outfit,"walk")!;
            foreach(int direction in new[] { -1, 1 })
            {
                pet.StopInteraction(); pet.BeginPreview();
                pet.MoveLift(pet.WorkArea.Left+pet.WorkArea.Width/2-280,pet.WorkArea.Bottom-468);
                pet.StartWalk(false,direction);
                double initial=VisualCenter(),elapsed=0;
                string key=$"{c.Id}/{outfit}/{direction}";
                // Uneven presentation times include frames slower than the old
                // 50 ms cap. Distance, pose and native placement must still agree.
                foreach(double dt in new[] { 8d,17,90,120,15,250,10,240,250 })
                {
                    elapsed+=dt; pet.AdvancePreview(elapsed);
                    Require(Math.Abs(VisualCenter()-initial-direction*DesktopWalk.Speed(200,clip)*elapsed/1000)<1e-6,key+$": subpixel distance at {elapsed} ms");
                    Require(Math.Abs(pet.WalkPhaseMilliseconds-elapsed)<1e-6,key+$": gait follows elapsed travel at {elapsed} ms");
                    Require(ReferenceEquals(sprite.Source,pet.Art.Frame(c,clip,pet.DrawnFrame)),key+": own outfit supplies the walking pose");
                }
                Require(((ScaleTransform)sprite.RenderTransform).ScaleX==DesktopWalk.ScaleX(direction,clip.Facing),key+": whole character faces travel");
                double beforeStop=VisualCenter(); pet.StopInteraction(); pet.AdvancePreview(1100);
                Require(Math.Abs(VisualCenter()-beforeStop)<1e-6,key+": stopping preserves the visible position");
                pet.BeginLift(); double left=pet.Left+40,top=pet.Top-100;
                pet.MoveLift(left,top); pet.ReleaseLift(); pet.AdvancePreview(2000);
                Require(!pet.IsDropping && Math.Abs(pet.Top-top)<1 && Math.Abs(canvas.RenderTransform.Value.OffsetX)<1e-9,key+": dragging cancels walking and keeps manual placement");
            }
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
        }
        pet.EndPreview();
        await FloorWalkVerification.Run(pet, output);
        File.WriteAllLines(Path.Combine(output,"walk-check.txt"),checks.Append($"{checks.Count} walking checks passed."));
    }
}
