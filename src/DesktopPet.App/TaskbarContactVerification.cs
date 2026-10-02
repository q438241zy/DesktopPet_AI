using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Uses the real dispatcher and compositor after release instead of advancing the preview clock.</summary>
internal static class TaskbarContactVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output);
        var checks = new List<string>();
        void Require(bool pass, string label) { if (!pass) throw new InvalidOperationException(label); checks.Add("PASS " + label); }
        var canvas = (Canvas)pet.Content;
        var sprite = canvas.Children.OfType<Image>().Single();
        double VisualX() => pet.Left + canvas.RenderTransform.Value.OffsetX;
        object Snapshot() => new { pet.IsLoaded, pet.IsVisible, windowState = pet.WindowState.ToString(), action = pet.CurrentAction,
            pet.State.Wander, pet.State.ReducedMotion, pet.State.LastSeen, pet.Left, pet.Top, floor = pet.WorkArea.Bottom - 468,
            pet.WalkUsesRendering, pet.IsDropping, pet.IsResting, dataRoot = output };
        await Task.Delay(800);
        File.WriteAllText(Path.Combine(output, "startup-snapshot.json"), JsonSerializer.Serialize(Snapshot(), Json.Options));
        Require(pet.IsLoaded && pet.IsVisible, "normal startup loads and shows the desktop pet");
        Require(pet.State.LastSeen == DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"), "normal startup executes the welcome and updates the running date");
        if (pet.State.Wander && !pet.State.ReducedMotion && Math.Abs(pet.Top + 468 - pet.WorkArea.Bottom) < 1)
        {
            Require(pet.CurrentAction == "walk" && pet.WalkUsesRendering, "startup at the taskbar uses the real walking compositor");
            double x = VisualX(); var frame = sprite.Source; await Task.Delay(700);
            Require(Math.Abs(VisualX() - x) > 2 && !ReferenceEquals(frame, sprite.Source), "startup walking actually changes position and rendered pose");
        }
        pet.IsHitTestVisible = false; pet.StopInteraction();
        var savedCheckIns = pet.State.CheckIns.ToArray();
        pet.State.CheckIns.Remove(DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"));
        try
        {
            foreach (var character in pet.Catalog.Characters)
            foreach (string outfit in new[] { "original", "swim", "wedding" })
            {
                pet.SelectCharacter(character.Id); pet.State.Outfits[character.Id] = outfit;
                pet.State.Wander = true; pet.State.ReducedMotion = false; pet.ApplySettings(); pet.StopInteraction();
                double x = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280;
                double floor = pet.WorkArea.Bottom - 468;
                pet.Left = x; pet.Top = floor - 130;
                pet.BeginPointerGesture(new Point(x + 280, floor + 230));
                pet.MovePointerGesture(new Point(x + 280, floor + 348)); pet.EndPointerGesture(false);
                Require(pet.CurrentAction == "land", character.Id + "/" + outfit + ": real-time floor placement starts the landing buffer");
                await Task.Delay(550);
                Require(pet.CurrentAction == "walk" && pet.WalkUsesRendering, character.Id + "/" + outfit + ": real dispatcher completes landing and starts walking");
                double startX = VisualX(); var startFrame = sprite.Source; await Task.Delay(230);
                Require(Math.Abs(VisualX() - startX) > 2 && !ReferenceEquals(startFrame, sprite.Source), character.Id + "/" + outfit + ": automatic walk actually moves and animates");
                pet.StopInteraction();
                pet.Left = x; pet.Top = floor - 130;
                pet.BeginPointerGesture(new Point(x + 280, floor + 230));
                pet.MovePointerGesture(new Point(x + 280, floor + 360));
                pet.CancelInput(true); await Task.Delay(550);
                Require(pet.CurrentAction == "walk", character.Id + "/" + outfit + ": taskbar capture interruption still starts floor walking");
                pet.StopInteraction();
                if (character.FamilyId == "whale")
                {
                    pet.Left = x; pet.Top = floor;
                    pet.BeginPointerGesture(new Point(x + 280, floor + 360));
                    pet.MovePointerGesture(new Point(x + 280, floor + 230)); pet.EndPointerGesture(true);
                    Require(pet.IsDropping, character.Id + "/" + outfit + ": shared mouse-up path accepts Shift gravity");
                    var deadline = Stopwatch.StartNew();
                    while (pet.IsDropping) { if (deadline.ElapsedMilliseconds > 3000) throw new InvalidOperationException("Shift fall timed out"); await Task.Delay(16); }
                    Require(pet.CurrentAction == "land", character.Id + "/" + outfit + ": real Shift fall has a distinct landing buffer");
                    await Task.Delay(550);
                    Require(pet.CurrentAction == "walk" && pet.WalkUsesRendering, character.Id + "/" + outfit + ": shared Shift-release path walks after landing");
                    pet.StopInteraction(); pet.Left = x; pet.Top = floor;
                    pet.BeginPointerGesture(new Point(x + 280, floor + 360));
                    pet.MovePointerGesture(new Point(x + 280, floor + 230)); pet.CancelInput(true); await Task.Delay(550);
                    Require(pet.CurrentAction == "idle" && !pet.IsDropping && Math.Abs(pet.Top - floor + 130) < 1, character.Id + "/" + outfit + ": capture loss above floor stays at chosen height without gravity");
                }
            }
        }
        finally { pet.State.CheckIns.Clear(); pet.State.CheckIns.UnionWith(savedCheckIns); pet.State.Wander = false; pet.StopInteraction(); }
        File.WriteAllLines(Path.Combine(output, "taskbar-contact-check.txt"), checks.Append($"{checks.Count} real-time taskbar contact checks passed."));
    }
}
