using System.Windows;
using System.Windows.Controls;
using DesktopPet.Core;

namespace DesktopPet.App;

internal static class CareVerification
{
    internal static Task Run(PetWindow pet, string output)
    {
        var checks = new List<string>();
        void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); checks.Add("PASS " + message); }
        pet.State.Wander = pet.State.ReducedMotion = false;
        pet.State.CheckIns.Clear(); pet.ApplySettings(); pet.BeginPreview();
        double time = 0;
        foreach (var c in pet.Catalog.Characters)
        foreach (string outfit in Catalog.BuiltInOutfits)
        {
            pet.SelectCharacter(c.Id); pet.State.Outfits[c.Id] = outfit; pet.ApplySettings();
            string label = c.Id + "/" + outfit;
            var actions = new List<string>();
            for (int i=0; i<6; i++)
            {
                // Hit different heights: gesture choice must follow the care
                // cycle, not texture gutters or style-specific face height.
                var start = new Point(pet.Left+280, pet.Top+390);
                Require(pet.BeginPointerGesture(start), label+": click begins");
                pet.EndPointerGesture(false, i%2 == 0 ? .1 : .7);
                actions.Add(pet.CurrentAction);
                Require(pet.State.CheckIns.Count == 0, label+": care click does not register a breakfast");
                // Gentle clicks are spaced outside the six-in-ten-seconds rough-touch rule.
                pet.AdvancePreview(time += 2100);
            }
            Require(actions.Take(3).Distinct().Count()==3 && actions.Take(3).SequenceEqual(actions.Skip(3)), label+": six clicks rotate through three care gestures");
            foreach (var routine in CareRoutine.All)
            {
                pet.RunInteraction(routine.Key);
                double began = time;
                Require(pet.CurrentCare == routine.Key && pet.CurrentSpeech == routine.Message, label+"/"+routine.Key+": starts its own care interaction");
                double elapsed = 0;
                foreach (var step in routine.Steps)
                {
                    pet.AdvancePreview(time = began + elapsed + step.Duration/2d);
                    Require(pet.CurrentAction == step.Motion && pet.State.Outfit == outfit, label+"/"+routine.Key+": keeps clothes in "+step.Motion);
                    var art = c.Resolve(outfit,step.Motion,step.Duration/2d);
                    var expected = pet.Art.Frame(c,art.Sprite,art.Frame);
                    if (pet.ActiveAuthoredVisual is { } authored)
                    {
                        var clip = c.MotionFor(outfit,step.Motion)!;
                        var blend = Motion.Blend(clip,step.Duration/2d);
                        Require(authored.Appearance == c.Id+"/"+outfit && authored.SheetFile == clip.File && authored.Current == blend,
                            label+"/"+routine.Key+": care uses the current outfit and continuous pose clock in "+step.Motion);
                        // The continuous mesh switches its source texture at the
                        // blend midpoint, rather than the legacy frame boundary.
                        expected = pet.Art.Frame(c,clip,blend.Amount < .5 ? blend.A : blend.B);
                    }
                    var sprite = ((Canvas)pet.Content).Children.OfType<Image>().Single();
                    Require(ReferenceEquals(sprite.Source,expected) && (pet.ActiveMotion is null || ReferenceEquals(pet.ActiveMotion.Texture,expected)), label+"/"+routine.Key+": actually renders the selected outfit in "+step.Motion);
                    elapsed += step.Duration;
                }
                pet.AdvancePreview(time = began + routine.Duration + 100);
                Require(pet.CurrentCare is null && pet.IsResting == routine.FallsAsleep && pet.CurrentAction == (routine.FallsAsleep ? "sleep" : "idle"), label+"/"+routine.Key+": finishes in the intended state");
                if (routine.FallsAsleep)
                {
                    pet.Touch(.3);
                    Require(!pet.IsResting, label+": touching wakes a soothed pet");
                }
                pet.RunInteraction(routine.Key); pet.RunInteraction("snack");
                pet.AdvancePreview(time += 8000);
                Require(pet.CurrentCare is null && !pet.IsResting && pet.CurrentAction != "sleep", label+"/"+routine.Key+": replacement cancels all later care stages");
            }
            pet.Play("idle");
            var screen = new Point(pet.Left+280, pet.Top+390);
            pet.BeginPointerGesture(screen); pet.MovePointerGesture(screen+new Vector(20,-50)); pet.EndPointerGesture(false);
            Require(pet.CurrentAction is "land" or "idle" && pet.CurrentCare is null, label+": dragging does not consume a care gesture");
            pet.Touch(.6);
            Require(pet.CurrentAction == actions[0], label+": drag preserves the next gesture in the cycle");
        }
        pet.EndPreview();
        File.WriteAllLines(Path.Combine(output,"care-check.txt"), checks.Append($"{checks.Count} care checks passed."));
        return Task.CompletedTask;
    }
}
