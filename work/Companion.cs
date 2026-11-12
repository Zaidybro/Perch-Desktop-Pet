using System;
using System.Windows;
using System.Windows.Controls;

namespace Perch
{
    internal partial class DeskPet
    {
        private FrameworkElement CustomizationControls(Avatar preview)
        {
            var section = new StackPanel();
            Add(section, Text("Make Mochi yours", 15, Ink, true), 7);
            Add(section, Text("Pixel theme", 12, Muted), 4);
            var fur = new ComboBox { MinHeight = 34 };
            foreach (string choice in new[] { "Honey", "Cream", "Cocoa", "Strawberry", "Midnight", "Rainy day" }) fur.Items.Add(choice);
            fur.SelectedItem = fur.Items.Contains(settings.Fur) ? settings.Fur : "Honey";
            fur.SelectionChanged += delegate
            {
                settings.Fur = (string)fur.SelectedItem;
                if (avatar != null) { avatar.Fur = settings.Fur; avatar.InvalidateVisual(); }
                if (preview != null) { preview.Fur = settings.Fur; preview.InvalidateVisual(); }
                Changed();
            };
            Add(section, fur, 9);
            Add(section, Text("Accessory", 12, Muted), 4);
            var accessory = new ComboBox { MinHeight = 34 };
            foreach (string choice in new[] { "None", "Bandana" }) accessory.Items.Add(choice);
            if (settings.FocusSessions >= 2) accessory.Items.Add("Bow");
            if (settings.FocusSessions >= 4) accessory.Items.Add("Tiny hat");
            accessory.SelectedItem = accessory.Items.Contains(settings.Accessory) ? settings.Accessory : "None";
            accessory.SelectionChanged += delegate
            {
                settings.Accessory = (string)accessory.SelectedItem;
                if (avatar != null) { avatar.Accessory = settings.Accessory; avatar.InvalidateVisual(); }
                if (preview != null) { preview.Accessory = settings.Accessory; preview.InvalidateVisual(); }
                Changed();
            };
            Add(section, accessory, 8);
            Add(section, Text(settings.FocusSessions < 2
                ? "Finish 2 focus sessions to unlock the bow. The tiny hat unlocks after 4."
                : settings.FocusSessions < 4 ? "Bow unlocked! The tiny hat unlocks after 4 sessions."
                : "All accessories unlocked!", 11, Muted), 0);
            return section;
        }

        private FrameworkElement FocusPage()
        {
            var section = new StackPanel();
            ResetDailyFocus();
            bool active = FocusRunning || FocusPaused;
            Add(section, Text(active
                ? settings.FocusPhase == "Break" || settings.FocusPhase == "PausedBreak" ? "Break time. Mochi will watch the clock." : "Locked in. One task at a time."
                : "Pick one thing to work on. Mochi will stay with you.", 13, Muted), 12);
            if (active)
                Add(section, Text(settings.FocusGoal, 16, Ink, true), 10);
            var display = Text("", 32, Ink, true);
            display.TextAlignment = TextAlignment.Center;
            Add(section, display, 7);
            var progress = new ProgressBar { Minimum = 0, Maximum = 100, Height = 9, Margin = new Thickness(0, 0, 0, 12), Visibility = active ? Visibility.Visible : Visibility.Collapsed };
            Add(section, progress, 6);
            if (!active)
            {
                Add(section, Text("Today: " + settings.TodaySessions + " blocks · " + settings.TodayMinutes + " focused minutes", 12, Muted), 13);
                Add(section, Text("What are you locking in for?", 12, Ink, true), 5);
                var goal = Input(settings.FocusGoal ?? "", "Focus goal");
                goal.MaxLength = 80;
                Add(section, goal, 10);
                var mode = new ComboBox { MinHeight = 34 };
                mode.Items.Add("Study"); mode.Items.Add("Work");
                mode.SelectedItem = settings.FocusMode == "Work" ? "Work" : "Study";
                Add(section, Text("Mode", 12, Ink, true), 4);
                Add(section, mode, 10);
                int[] lengths = { 5, 15, 25, 45, 60 };
                var duration = new ComboBox { MinHeight = 34 };
                foreach (int minutes in lengths) duration.Items.Add(minutes + " minutes");
                duration.SelectedIndex = Math.Max(0, Array.IndexOf(lengths, settings.FocusMinutes));
                Add(section, Text("Focus length", 12, Ink, true), 4);
                Add(section, duration, 10);
                int[] breaks = { 0, 5, 10, 15 };
                var rest = new ComboBox { MinHeight = 34 };
                foreach (int minutes in breaks) rest.Items.Add(minutes == 0 ? "No break" : minutes + " minute break");
                rest.SelectedIndex = Math.Max(0, Array.IndexOf(breaks, settings.BreakMinutes));
                Add(section, Text("After focus", 12, Ink, true), 4);
                Add(section, rest, 12);
                var nudges = new CheckBox { Content = "Nudge me on distracting sites", IsChecked = settings.DistractionNudges, Margin = new Thickness(0, 0, 0, 12) };
                nudges.Checked += delegate { settings.DistractionNudges = true; Changed(); };
                nudges.Unchecked += delegate { settings.DistractionNudges = false; Changed(); };
                Add(section, nudges, 4);
                Add(section, Button("Start lock-in", delegate
                {
                    StartFocus(goal.Text, (string)mode.SelectedItem, lengths[duration.SelectedIndex], breaks[rest.SelectedIndex]);
                    ShowPage("Focus");
                }, Green, White), 5);
            }
            else
            {
                Add(section, Text(settings.FocusPhase == "Focus" ? "FOCUS · " + settings.FocusMode.ToUpperInvariant()
                    : settings.FocusPhase == "Break" ? "BREAK"
                    : "PAUSED", 12, Green, true), 10);
                if (FocusPaused)
                    Add(section, Button("Resume", delegate { ResumeFocus(); ShowPage("Focus"); }, Green, White), 8);
                else
                    Add(section, Button("Pause", delegate { PauseFocus(); ShowPage("Focus"); }, Green, White), 8);
                if (settings.FocusPhase == "Break" || settings.FocusPhase == "PausedBreak")
                    Add(section, Button("Skip break", delegate { EndFocus(); ShowPage("Focus"); }), 8);
                Add(section, Button("End session", delegate { EndFocus(); ShowPage("Focus"); }), 8);
                Add(section, Text("Today: " + settings.TodaySessions + " blocks · " + settings.TodayMinutes + " focused minutes", 12, Muted), 0);
            }
            SetReadout(delegate
            {
                TimeSpan left = active ? FocusLeft() : TimeSpan.FromMinutes(settings.FocusMinutes);
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;
                display.Text = string.Format("{0:00}:{1:00}", (int)left.TotalMinutes, left.Seconds);
                double total = (settings.FocusPhase == "Break" || settings.FocusPhase == "PausedBreak")
                    ? settings.BreakMinutes * 60 : settings.FocusMinutes * 60;
                progress.Value = active && total > 0 ? Math.Max(0, Math.Min(100, 100 * (1 - left.TotalSeconds / total))) : 0;
            }, FocusRunning ? 1000 : 0);
            return section;
        }
    }
}
