using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Exercises automatic floor walking without pre-checking the daily breakfast.</summary>
internal static class FloorWalkVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        var originalCheckIns = pet.State.CheckIns.ToArray();
        string today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        var canvas = (Canvas)pet.Content;
        double tolerance = 1 / VisualTreeHelper.GetDpi(pet).DpiScaleY + .01;
        double VisualX() => pet.Left + canvas.RenderTransform.Value.OffsetX;
        try
        {
            pet.State.CheckIns.Remove(today);
            foreach (var character in pet.Catalog.Characters)
            foreach (string outfit in Catalog.BuiltInOutfits)
            {
                string label = character.Id + "/" + outfit;
                double now = 0;
                void Advance(double ms) { now += ms; pet.AdvancePreview(now); }
                pet.BeginPreview(); pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit;
                pet.State.Wander = true; pet.State.ReducedMotion = false; pet.ApplySettings(); pet.StopInteraction();
                double x = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280, floor = pet.WorkArea.Bottom - 468, air = floor - 130;
                pet.Left = x; pet.Top = floor;
                pet.BeginLift(); pet.MoveLift(x, air); pet.ReleaseLift(); Advance(1000);
                Require(pet.CurrentAction == "idle" && !pet.IsDropping && Math.Abs(pet.Top - air) < tolerance, label + ": unchecked manual placement stays awake and elevated");

                pet.BeginLift(); pet.ReleaseLift(true);
                for (int i = 0; pet.IsDropping && i < 150; i++) Advance(16);
                Require(!pet.IsDropping && pet.CurrentAction == "land" && Math.Abs(pet.Top - floor) < tolerance, label + ": Shift drop reaches floor and buffers before walking");
                Advance(430);
                Require(pet.CurrentAction == "walk" && !pet.IsResting, label + ": landing automatically starts walking without breakfast");
                double beforeWalk = VisualX(); Advance(200);
                Require(Math.Abs(VisualX() - beforeWalk) > 2, label + ": automatic walking actually moves the window");
                Require(!pet.State.CheckedIn(DateOnly.FromDateTime(DateTime.Now)), label + ": walking never inserts a daily check-in");

                pet.BeginLift(); pet.MoveLift(x, floor - 12); pet.ReleaseLift(); Advance(430);
                Require(pet.CurrentAction == "walk", label + ": ordinary placement in the floor magnet resumes walking too");
                pet.BeginLift(); pet.MoveLift(x, air); pet.ReleaseLift(true); Advance(80);
                pet.BeginLift(); pet.MoveLift(x, air); pet.ReleaseLift(); Advance(1000);
                Require(pet.CurrentAction == "idle" && !pet.IsDropping && Math.Abs(pet.Top - air) < tolerance, label + ": catching and placing above floor cancels queued automatic walking");

                pet.State.Wander = false; pet.BeginLift(); pet.MoveLift(x, floor); pet.ReleaseLift(); Advance(500);
                Require(pet.CurrentAction == "idle", label + ": disabled automatic walking leaves an awake idle pose");
                pet.StartWalk(false, 1);
                Require(pet.CurrentAction == "walk", label + ": explicit walking also works without breakfast");
                pet.StopInteraction(); pet.State.Wander = true; pet.State.ReducedMotion = true;
                pet.BeginLift(); pet.MoveLift(x, air); pet.ReleaseLift(true); Advance(60000);
                Require(pet.CurrentAction == "idle" && !pet.IsDropping && Math.Abs(pet.Top - floor) < tolerance, label + ": reduced motion lands awake and never forces walking");

                pet.State.ReducedMotion = false; pet.RunInteraction("rest"); Advance(1600); Advance(60000);
                Require(pet.IsResting && pet.CurrentAction == "sleep", label + ": explicitly requested rest stays asleep on the taskbar");
                pet.StopInteraction(); Advance(45001);
                Require(pet.CurrentAction == "walk", label + ": later idle walking remains available without breakfast");
                pet.StopInteraction();
                pet.Left = x; pet.Top = air;
                pet.BeginPointerGesture(new Point(x + 280, air + 360));
                pet.MovePointerGesture(new Point(x + 280, floor + 348)); pet.EndPointerGesture(false); Advance(430);
                Require(pet.CurrentAction == "walk", label + ": mouse gesture release on floor clears drag flags and walks");
                pet.StopInteraction(); pet.Left = x; pet.Top = air;
                pet.BeginPointerGesture(new Point(x + 280, air + 360));
                pet.MovePointerGesture(new Point(x + 280, floor + 360)); pet.CancelInput(true); Advance(430);
                Require(pet.CurrentAction == "walk", label + ": interrupted floor gesture still buffers and walks");
                pet.StopInteraction(); pet.Left = x; pet.Top = floor;
                pet.BeginPointerGesture(new Point(x + 280, floor + 360));
                pet.MovePointerGesture(new Point(x + 280, air + 360)); pet.CancelInput(true); Advance(1000);
                Require(pet.CurrentAction == "idle" && !pet.IsDropping && Math.Abs(pet.Top - air) < tolerance, label + ": interrupted air gesture stays placed without Shift gravity");
                pet.StopInteraction();
                await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            }
        }
        finally
        {
            pet.State.CheckIns.Clear(); pet.State.CheckIns.UnionWith(originalCheckIns);
            pet.State.Wander = false; pet.State.ReducedMotion = false; pet.EndPreview();
        }
        File.WriteAllLines(Path.Combine(output, "floor-walk-check.txt"), checks.Append($"{checks.Count} unchecked floor-walk checks passed across {pet.Catalog.Characters.Count*Catalog.BuiltInOutfits.Length} appearances."));
    }
}
