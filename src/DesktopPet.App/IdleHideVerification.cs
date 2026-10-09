using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using DesktopPet.Core;

namespace DesktopPet.App;

/// <summary>Audit the existing idle-hide behavior against an isolated profile and an actual elapsed minute.</summary>
internal static class IdleHideVerification
{
    internal static async Task Run(PetWindow pet, string output)
    {
        Directory.CreateDirectory(output); var checks = new List<string>(); var timings = new List<object>();
        void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); checks.Add("PASS " + message); File.WriteAllLines(Path.Combine(output, "idle-hide-check.txt"), checks); }
        var idleField = typeof(PetWindow).GetField("lastCompanionActivity", BindingFlags.Instance | BindingFlags.NonPublic)!;
        async Task Until(Func<bool> value, int seconds, string name)
        { var watch = Stopwatch.StartNew(); while (!value()) { if (watch.Elapsed.TotalSeconds > seconds) throw new TimeoutException(name); await Task.Delay(70); } }
        pet.IsHitTestVisible = false; pet.State.Size = 200; pet.State.AutoHide = true; pet.State.ReducedMotion = false; pet.State.Wander = false; pet.ConfigureWork(false, 10);
        pet.SelectCharacter("whale"); pet.StopInteraction(); pet.OpenSettings();
        idleField.SetValue(pet, -61000d); await Task.Delay(1300);
        Require(pet.HideStage is null, "open control panel protects the current session from auto-hide");
        Application.Current.Windows.OfType<SettingsWindow>().Single().Close(); pet.OpenChat();
        idleField.SetValue(pet, -61000d); await Task.Delay(1300);
        Require(pet.HideStage is null, "open inline chat prevents idle hiding"); pet.StopInteraction();
        pet.State.AutoHide = false; idleField.SetValue(pet, -61000d); await Task.Delay(1300);
        Require(pet.HideStage is null, "disabled auto-hide stays disabled");
        foreach (string id in new[] { "whale", "deepseek-adult" })
        {
            pet.State.AutoHide = true; pet.State.Wander = true; pet.SelectCharacter(id); pet.State.Outfits[id] = "sports"; pet.ApplySettings();
            pet.Left = pet.WorkArea.Left + pet.WorkArea.Width / 2 - 280; pet.Top = pet.WorkArea.Bottom - 468;
            pet.StopInteraction(); var watch = Stopwatch.StartNew(); bool walked = false;
            // No clock rewriting in this section: the application receives an uninterrupted real minute.
            while (watch.Elapsed.TotalSeconds < 58)
            {
                if (pet.CurrentAction == "walk") walked = true;
                if (pet.HideStage is not null) throw new InvalidOperationException("Hide began before a full minute: " + id);
                await Task.Delay(100);
            }
            Require(walked, id + ": ordinary autonomous walking does not reset the user-idle clock");
            await Until(() => pet.HideStage is not null, 6, "real minute trigger " + id);
            double began = watch.Elapsed.TotalSeconds; Require(began is >= 59.5 and < 64, id + ": auto-hide begins after a real minute");
            await Until(() => pet.HideStage == HidePhase.Peek, 50, "walk to edge " + id);
            double arrived = watch.Elapsed.TotalSeconds; await Task.Delay(12000);
            Require(pet.HideStage == HidePhase.Peek, id + ": stays hidden beyond ten seconds, awaiting user discovery");
            pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Right) { RoutedEvent = UIElement.MouseRightButtonUpEvent });
            Require(pet.HideStage == HidePhase.Peek && pet.IsMenuOpen, id + ": right click opens menu without finding pet");
            pet.InputSurface.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
            Require(pet.HideStage == HidePhase.Return, id + ": left click finds hidden pet");
            await Until(() => pet.HideStage is null, 6, "return after found " + id);
            Require(pet.CurrentSpeech == "被你找到啦！", id + ": returns with found greeting");
            timings.Add(new { character = id, triggerSeconds = began, edgeSeconds = arrived, heldSeconds = 12 });
        }
        File.WriteAllText(Path.Combine(output, "idle-hide-timing.json"), System.Text.Json.JsonSerializer.Serialize(timings, Json.Options));
        File.AppendAllText(Path.Combine(output, "idle-hide-check.txt"), $"PASS {checks.Count} idle-hide checks with two real-minute trials.\n");
    }
}
