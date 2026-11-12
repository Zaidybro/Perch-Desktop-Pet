using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace Perch
{
    internal partial class DeskPet
    {
        private Note selectedNote;
        private StackPanel scheduleList;

        private FrameworkElement NotesPage(bool create)
        {
            var section = new StackPanel();
            if (create)
            {
                selectedNote = new Note();
                settings.Notes.Insert(0, selectedNote);
                Changed();
                if (avatar != null) avatar.NoteSaved();
            }
            if (!settings.Notes.Contains(selectedNote))
                selectedNote = settings.Notes.FirstOrDefault();

            var newNote = Button("+ New note", () => ShowPage("Notes", true), Green, White);
            Add(section, newNote, 8);
            if (selectedNote == null)
            {
                Add(section, Card(Text("Your notes will appear here.", 14, Muted), Soft));
                return section;
            }

            Note current = selectedNote;
            var picker = new ComboBox { MinHeight = 32, Padding = new Thickness(7, 3, 7, 3) };
            System.Windows.Automation.AutomationProperties.SetName(picker, "Saved notes");
            foreach (Note note in settings.Notes)
                picker.Items.Add(new ComboBoxItem { Content = NoteTitle(note) });
            picker.SelectedIndex = settings.Notes.IndexOf(current);
            picker.SelectionChanged += delegate
            {
                if (picker.SelectedIndex < 0)
                    return;
                selectedNote = settings.Notes[picker.SelectedIndex];
                ShowPage("Notes");
            };
            Add(section, picker, 8);

            var editor = Input(current.Text, "Note text");
            string lastAnimated = current.Text;
            editor.MinHeight = 105;
            editor.MaxHeight = 180;
            editor.MaxLength = 20000;
            editor.VerticalContentAlignment = VerticalAlignment.Top;
            editor.AcceptsReturn = true;
            editor.TextWrapping = TextWrapping.Wrap;
            editor.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            editor.TextChanged += delegate
            {
                current.Text = editor.Text;
                Changed(true);
            };
            editor.LostKeyboardFocus += delegate
            {
                if (current.Text != lastAnimated && !string.IsNullOrWhiteSpace(current.Text))
                {
                    lastAnimated = current.Text;
                    if (avatar != null) avatar.NoteSaved();
                }
                int index = settings.Notes.IndexOf(current);
                if (index >= 0 && index < picker.Items.Count)
                    ((ComboBoxItem)picker.Items[index]).Content = NoteTitle(current);
            };
            Add(section, editor, 7);
            Button remove = null;
            remove = Button("Delete note", delegate
            {
                if ((string)remove.Content != "Confirm delete")
                {
                    remove.Content = "Confirm delete";
                    return;
                }
                settings.Notes.Remove(current);
                Changed();
                ShowPage("Notes");
            });
            var copy = Button("Copy note", delegate
            {
                if (!string.IsNullOrEmpty(editor.Text))
                {
                    try { Clipboard.SetText(editor.Text); }
                    catch (System.Runtime.InteropServices.ExternalException) { Notify("The clipboard is busy. Try Copy note again."); }
                }
            });
            section.Children.Add(Row(copy, remove));
            if (create)
                editor.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => editor.Focus()));
            return section;
        }

        private static string NoteTitle(Note note)
        {
            string line = note.Text.Split('\n').Select(s => s.Trim()).FirstOrDefault(s => s.Length > 0);
            if (line == null)
                return "Untitled note";
            return line.Length <= 48 ? line : line.Substring(0, 47) + "…";
        }

        private FrameworkElement ClockPage()
        {
            var section = new StackPanel();
            var tabs = new WrapPanel();
            foreach (string item in new[] { "Timer", "Stopwatch", "Alarm", "World clock" })
            {
                string target = item;
                tabs.Children.Add(Button(item, delegate { clockPage = target; ShowPage("Clock"); },
                    item == clockPage ? Soft : White));
            }
            Add(section, tabs, 10);
            switch (clockPage)
            {
                case "Stopwatch": section.Children.Add(StopwatchPane()); break;
                case "Alarm": section.Children.Add(SchedulePane(true)); break;
                case "World clock": section.Children.Add(WorldClockPane()); break;
                default: section.Children.Add(TimerPane()); break;
            }
            return section;
        }

        private static TextBlock Readout()
        {
            var readout = Text("", 39, Green, true);
            readout.FontFamily = new System.Windows.Media.FontFamily("Consolas");
            return readout;
        }

        private FrameworkElement TimerPane()
        {
            var pane = new StackPanel();
            var readout = Readout();
            Add(pane, readout, 5);
            var status = Text("", 13, Muted);
            Add(pane, status, 15);
            Add(pane, Text("Duration (hours : minutes : seconds)", 12, Muted), 5);
            var input = Input(ClockMath.Readout(TimeSpan.FromSeconds(settings.TimerInitial > 0 ? settings.TimerInitial : 300), false), "Timer duration", 122);
            input.HorizontalAlignment = HorizontalAlignment.Left;
            Add(pane, input, 9);
            var error = Text("", 12, Red);
            Button start = null;
            start = Button("Start", delegate
            {
                if (settings.TimerEndsUtc != DateTime.MinValue)
                {
                    settings.TimerSeconds = settings.TimerLeft(DateTime.UtcNow);
                    if (settings.TimerSeconds == 0)
                    {
                        CheckAlerts();
                        return;
                    }
                    settings.TimerEndsUtc = DateTime.MinValue;
                }
                else
                {
                    double duration = settings.TimerSeconds;
                    if (duration <= 0)
                    {
                        var parts = input.Text.Trim().Split(':');
                        int h, m, s;
                        if (parts.Length != 3 || !int.TryParse(parts[0], out h) || !int.TryParse(parts[1], out m) ||
                            !int.TryParse(parts[2], out s) || h < 0 || h > 99 || m < 0 || m > 59 || s < 0 || s > 59 ||
                            (duration = h * 3600 + m * 60 + s) == 0)
                        {
                            error.Text = "Enter a duration such as 00:05:00.";
                            return;
                        }
                        settings.TimerInitial = duration;
                    }
                    settings.TimerEndsUtc = DateTime.UtcNow.AddSeconds(duration);
                    settings.TimerSeconds = 0;
                }
                error.Text = "";
                Changed();
                ScheduleAlerts();
                SetReadout(refreshReadout, settings.TimerEndsUtc == DateTime.MinValue ? 0 : 250);
            }, Green, White);
            var reset = Button("Reset", delegate
            {
                settings.TimerEndsUtc = DateTime.MinValue;
                settings.TimerSeconds = 0;
                error.Text = "";
                Changed();
                ScheduleAlerts();
                SetReadout(refreshReadout);
            });
            Add(pane, Row(start, reset), 5);
            pane.Children.Add(error);
            SetReadout(delegate
            {
                bool running = settings.TimerEndsUtc != DateTime.MinValue;
                if (!running)
                    readouts.Stop();
                bool paused = !running && settings.TimerSeconds > 0;
                readout.Text = ClockMath.Readout(TimeSpan.FromSeconds(Math.Ceiling(settings.TimerLeft(DateTime.UtcNow))), false);
                status.Text = running ? "Counting down" : paused ? "Paused" : "Ready to start";
                start.Content = running ? "Pause" : paused ? "Resume" : "Start";
                input.IsEnabled = !running && !paused;
                reset.IsEnabled = running || paused;
            }, settings.TimerEndsUtc == DateTime.MinValue ? 0 : 250);
            return Card(pane);
        }

        private FrameworkElement StopwatchPane()
        {
            var pane = new StackPanel();
            var readout = Readout();
            Add(pane, readout, 11);
            var lapList = new StackPanel();
            Action updateLaps = delegate
            {
                lapList.Children.Clear();
                for (int i = laps.Count - 1; i >= 0; i--)
                {
                    TimeSpan split = laps[i] - (i > 0 ? laps[i - 1] : TimeSpan.Zero);
                    Add(lapList, Text("Lap " + (i + 1) + "     " + ClockMath.Readout(split, true), 14, Muted), 6);
                }
            };
            Button start = Button("Start", delegate
            {
                if (stopwatch.IsRunning)
                    stopwatch.Stop();
                else
                    stopwatch.Start();
                SetReadout(refreshReadout, stopwatch.IsRunning ? 65 : 0);
            }, Green, White);
            Button lap = Button("Lap", delegate { laps.Add(stopwatch.Elapsed); updateLaps(); });
            Button reset = Button("Reset", delegate { stopwatch.Reset(); laps.Clear(); updateLaps(); refreshReadout(); });
            Add(pane, Row(start, lap, reset), 11);
            pane.Children.Add(lapList);
            SetReadout(delegate
            {
                readout.Text = ClockMath.Readout(stopwatch.Elapsed, true);
                start.Content = stopwatch.IsRunning ? "Stop" : stopwatch.Elapsed > TimeSpan.Zero ? "Resume" : "Start";
                lap.IsEnabled = stopwatch.IsRunning;
                reset.IsEnabled = stopwatch.Elapsed > TimeSpan.Zero;
            }, stopwatch.IsRunning ? 65 : 0);
            updateLaps();
            return Card(pane);
        }

        private FrameworkElement SchedulePane(bool isAlarm)
        {
            var pane = new StackPanel();
            var editor = new StackPanel();
            scheduleList = new StackPanel();
            Action<Alert> edit = null;
            edit = delegate(Alert existing)
            {
                editor.Children.Clear();
                Add(editor, Text(existing == null ? (isAlarm ? "New alarm" : "New reminder") : "Edit " + (isAlarm ? "alarm" : "reminder"), 16, Ink, true), 9);
                Add(editor, Text("Label", 12, Muted), 4);
                var label = Input(existing == null ? "" : existing.Label, "Alert label");
                label.MaxLength = 100;
                Add(editor, label, 9);
                DateTime initial = existing == null ? DateTime.Now.AddMinutes(5) : existing.Due;
                var day = new DatePicker { SelectedDate = initial.Date, Width = 110, Margin = new Thickness(0, 0, 5, 4) };
                System.Windows.Automation.AutomationProperties.SetName(day, "Reminder date");
                var time = Input(initial.ToString("HH:mm", CultureInfo.InvariantCulture), "Time in 24-hour format", 65);
                time.Margin = new Thickness(0, 0, 5, 4);
                var repeat = new ComboBox { Width = 86, MinHeight = 31, VerticalContentAlignment = VerticalAlignment.Center };
                foreach (string value in new[] { "Once", "Daily", "Weekdays" })
                    repeat.Items.Add(value);
                repeat.SelectedItem = existing == null ? "Once" : existing.Repeat;
                System.Windows.Automation.AutomationProperties.SetName(repeat, "Repeat");
                Add(editor, Text(isAlarm ? "Time (24-hour) and repeat" : "Date, time (24-hour), and repeat", 12, Muted), 4);
                var fields = new WrapPanel();
                if (!isAlarm)
                    fields.Children.Add(day);
                fields.Children.Add(time);
                fields.Children.Add(repeat);
                Add(editor, fields, 9);
                var error = Text("", 12, Red);
                var save = Button(existing == null ? (isAlarm ? "Add alarm" : "Add reminder") : "Save changes", delegate
                {
                    TimeSpan at;
                    if (!ClockMath.TryTime(time.Text, out at))
                    {
                        error.Text = "Enter a time from 00:00 to 23:59.";
                        return;
                    }
                    DateTime now = DateTime.Now;
                    if (!isAlarm && day.SelectedDate == null)
                    {
                        error.Text = "Choose a date.";
                        return;
                    }
                    DateTime due = (isAlarm ? now.Date : day.SelectedDate.Value).Add(at);
                    string recurrence = (string)repeat.SelectedItem;
                    if (isAlarm && due <= now)
                        due = due.AddDays(1);
                    if (recurrence == "Weekdays")
                        while (due.DayOfWeek == DayOfWeek.Saturday || due.DayOfWeek == DayOfWeek.Sunday)
                            due = due.AddDays(1);
                    if (due <= now)
                    {
                        error.Text = "Choose a time in the future.";
                        return;
                    }
                    if (!isAlarm && string.IsNullOrWhiteSpace(label.Text))
                    {
                        error.Text = "Give this reminder a label.";
                        return;
                    }
                    var alert = existing ?? new Alert { IsAlarm = isAlarm };
                    alert.Label = string.IsNullOrWhiteSpace(label.Text) ? "Alarm" : label.Text.Trim();
                    alert.Due = due;
                    alert.Repeat = recurrence;
                    alert.Enabled = true;
                    if (existing == null)
                        settings.Alerts.Add(alert);
                    Changed();
                    ScheduleAlerts();
                    edit(null);
                    RefreshScheduleList();
                }, Green, White);
                var actions = Row(save);
                if (existing != null)
                    actions.Children.Add(Button("Cancel edit", () => edit(null)));
                Add(editor, actions, 3);
                editor.Children.Add(error);
            };
            scheduleEdit = edit;
            scheduleAlarms = isAlarm;
            edit(null);
            Add(pane, Card(editor), 11);
            pane.Children.Add(scheduleList);
            RefreshScheduleList();
            return pane;
        }

        private Action<Alert> scheduleEdit;
        private bool scheduleAlarms;

        private void RefreshScheduleList()
        {
            if (scheduleList == null)
                return;
            scheduleList.Children.Clear();
            var alerts = settings.Alerts.Where(a => a.IsAlarm == scheduleAlarms)
                .OrderByDescending(a => a.Enabled).ThenBy(a => a.Due).ToList();
            if (alerts.Count == 0)
            {
                Add(scheduleList, Text(scheduleAlarms ? "No alarms yet." : "No reminders yet.", 13, Muted), 4);
                return;
            }
            foreach (Alert alert in alerts)
            {
                var row = new StackPanel();
                var actions = Row(
                    Button(alert.Enabled ? "Off" : "On", delegate
                    {
                        if (!alert.Enabled && alert.Due <= DateTime.Now)
                        {
                            if (alert.Repeat == "Once")
                            {
                                scheduleEdit(alert);
                                return;
                            }
                            alert.Fire(DateTime.Now);
                        }
                        alert.Enabled = !alert.Enabled;
                        Changed();
                        ScheduleAlerts();
                        RefreshScheduleList();
                    }),
                    Button("Edit", () => scheduleEdit(alert)),
                    Button("Delete", delegate
                    {
                        settings.Alerts.Remove(alert);
                        Changed();
                        ScheduleAlerts();
                        scheduleEdit(null);
                        RefreshScheduleList();
                    }));
                row.Children.Add(Text(alert.Label, 14, Ink, true));
                string detail = alert.Enabled ? alert.Due.ToString("ddd d MMM, HH:mm") : "Off";
                Add(row, Text(detail + " · " + alert.Repeat, 12, Muted), 5);
                row.Children.Add(actions);
                Add(scheduleList, Card(row, White, 8), 6);
            }
        }

        private FrameworkElement WorldClockPane()
        {
            var pane = new StackPanel();
            var entries = new StackPanel();
            var zones = TimeZoneInfo.GetSystemTimeZones();
            var picker = new ComboBox
            {
                ItemsSource = zones, DisplayMemberPath = "DisplayName", Width = 235, MinHeight = 33,
                VerticalContentAlignment = VerticalAlignment.Center, IsTextSearchEnabled = true
            };
            System.Windows.Automation.AutomationProperties.SetName(picker, "Time zone");
            var readouts = new Dictionary<TimeZoneInfo, TextBlock>();
            Action build = delegate
            {
                entries.Children.Clear();
                readouts.Clear();
                var selected = new[] { TimeZoneInfo.Local }.Concat(settings.Zones.Select(id => zones.FirstOrDefault(z => z.Id == id)))
                    .Where(z => z != null).GroupBy(z => z.Id).Select(group => group.First());
                foreach (TimeZoneInfo zone in selected)
                {
                    var row = new DockPanel();
                    bool local = zone.Id == TimeZoneInfo.Local.Id;
                    if (!local)
                    {
                        var remove = Button("Remove", delegate { settings.Zones.Remove(zone.Id); Changed(); ShowPage("Clock"); });
                        DockPanel.SetDock(remove, Dock.Right);
                        row.Children.Add(remove);
                    }
                    var readout = Text("", 18, Green, true);
                    readouts.Add(zone, readout);
                    var column = new StackPanel();
                    column.Children.Add(readout);
                    column.Children.Add(Text(local ? "Local time" : zone.DisplayName, 12, Muted));
                    row.Children.Add(column);
                    Add(entries, Card(row), 7);
                }
            };
            var add = Button("Add zone", delegate
            {
                var zone = picker.SelectedItem as TimeZoneInfo;
                if (zone == null || zone.Id == TimeZoneInfo.Local.Id || settings.Zones.Contains(zone.Id))
                    return;
                settings.Zones.Add(zone.Id);
                Changed();
                build();
                refreshReadout();
            }, Green, White);
            add.IsEnabled = false;
            picker.SelectionChanged += delegate
            {
                var zone = picker.SelectedItem as TimeZoneInfo;
                add.IsEnabled = zone != null && zone.Id != TimeZoneInfo.Local.Id && !settings.Zones.Contains(zone.Id);
            };
            add.Click += delegate { picker.SelectedIndex = -1; };
            var input = new WrapPanel();
            picker.Margin = new Thickness(0, 0, 6, 6);
            input.Children.Add(picker);
            input.Children.Add(add);
            Add(pane, input, 8);
            pane.Children.Add(entries);
            build();
            SetReadout(delegate
            {
                DateTime now = DateTime.UtcNow;
                foreach (var item in readouts)
                    item.Value.Text = TimeZoneInfo.ConvertTimeFromUtc(now, item.Key).ToString("HH:mm:ss  ·  ddd d MMM");
            }, 1000);
            return pane;
        }
    }
}
