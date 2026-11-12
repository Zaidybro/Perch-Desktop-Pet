using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Perch
{
    internal partial class DeskPet
    {
        private readonly DispatcherTimer focusTicker = new DispatcherTimer(DispatcherPriority.Background);
        private TextBlock focusLabel;

        private bool FocusRunning { get { return settings.FocusPhase == "Focus" || settings.FocusPhase == "Break"; } }
        private bool FocusPaused { get { return settings.FocusPhase == "PausedFocus" || settings.FocusPhase == "PausedBreak"; } }

        private void ResetDailyFocus()
        {
            if (settings.FocusDay.Date == DateTime.Today) return;
            settings.FocusDay = DateTime.Today;
            settings.TodaySessions = 0;
            settings.TodayMinutes = 0;
        }

        private TimeSpan FocusLeft()
        {
            if (FocusPaused) return TimeSpan.FromSeconds(Math.Max(0, settings.FocusRemainingSeconds));
            return settings.FocusEndsUtc == DateTime.MinValue
                ? TimeSpan.Zero : settings.FocusEndsUtc - DateTime.UtcNow;
        }

        private void StartFocus(string goal, string mode, int minutes, int breakMinutes)
        {
            ResetDailyFocus();
            settings.FocusGoal = string.IsNullOrWhiteSpace(goal) ? "One focused task" : goal.Trim();
            settings.FocusMode = mode;
            settings.FocusMinutes = minutes;
            settings.BreakMinutes = breakMinutes;
            settings.FocusPhase = "Focus";
            settings.FocusRemainingSeconds = 0;
            settings.FocusEndsUtc = DateTime.UtcNow.AddMinutes(minutes);
            Changed();
            ScheduleAlerts();
            UpdateFocusIndicator();
        }

        private void PauseFocus()
        {
            if (!FocusRunning) return;
            settings.FocusRemainingSeconds = Math.Max(0, FocusLeft().TotalSeconds);
            settings.FocusPhase = settings.FocusPhase == "Focus" ? "PausedFocus" : "PausedBreak";
            settings.FocusEndsUtc = DateTime.MinValue;
            Changed();
            ScheduleAlerts();
            UpdateFocusIndicator();
        }

        private void ResumeFocus()
        {
            if (!FocusPaused) return;
            settings.FocusPhase = settings.FocusPhase == "PausedFocus" ? "Focus" : "Break";
            settings.FocusEndsUtc = DateTime.UtcNow.AddSeconds(Math.Max(1, settings.FocusRemainingSeconds));
            settings.FocusRemainingSeconds = 0;
            Changed();
            ScheduleAlerts();
            UpdateFocusIndicator();
        }

        private void EndFocus()
        {
            settings.FocusPhase = "Idle";
            settings.FocusEndsUtc = DateTime.MinValue;
            settings.FocusRemainingSeconds = 0;
            Changed();
            ScheduleAlerts();
            UpdateFocusIndicator();
        }

        private void FinishFocusPhase(DateTime utcNow, List<string> due)
        {
            if (settings.FocusPhase == "Break")
            {
                settings.FocusPhase = "Idle";
                settings.FocusEndsUtc = DateTime.MinValue;
                due.Add("Break finished. Ready to lock in again?");
                if (avatar != null) avatar.Wave();
            }
            else
            {
                ResetDailyFocus();
                settings.FocusSessions++;
                settings.TodaySessions++;
                settings.TodayMinutes += settings.FocusMinutes;
                settings.TotalFocusMinutes += settings.FocusMinutes;
                settings.FocusPhase = settings.BreakMinutes > 0 ? "Break" : "Idle";
                settings.FocusEndsUtc = settings.BreakMinutes > 0
                    ? utcNow.AddMinutes(settings.BreakMinutes) : DateTime.MinValue;
                string reward = settings.FocusSessions == 2 ? " You unlocked Mochi's bow."
                    : settings.FocusSessions == 4 ? " You unlocked a tiny hat." : "";
                due.Add("Focus done: " + settings.FocusGoal + "."
                    + (settings.BreakMinutes > 0 ? " Take a " + settings.BreakMinutes + " minute break." : "") + reward);
                if (avatar != null) avatar.Dance();
            }
            UpdateFocusIndicator();
        }

        private void UpdateFocusIndicator()
        {
            if (focusLabel == null) return;
            if (settings.FocusPhase == "Idle") focusLabel.Text = settings.Name;
            else
            {
                TimeSpan left = FocusLeft();
                if (left < TimeSpan.Zero) left = TimeSpan.Zero;
                string state = settings.FocusPhase == "Break" || settings.FocusPhase == "PausedBreak" ? "BREAK" : "FOCUS";
                focusLabel.Text = (FocusPaused ? "PAUSED " : state + " ")
                    + string.Format("{0:00}:{1:00}", (int)left.TotalMinutes, left.Seconds);
            }
            if (avatar != null)
            {
                avatar.FocusActive = settings.FocusPhase == "Focus";
                avatar.InvalidateVisual();
            }
            if (surface != null && settings.FocusPhase != "Idle")
                surface.ToolTip = "Locked in: " + settings.FocusGoal + "\nClick for focus controls.";
            if (FocusRunning && !focusTicker.IsEnabled) focusTicker.Start();
            else if (!FocusRunning) focusTicker.Stop();
        }
    }
}
