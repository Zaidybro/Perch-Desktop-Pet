using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Serialization;

namespace Perch
{
    public class Note
    {
        public string Text = "";
    }

    public class Alert
    {
        public string Label;
        public bool IsAlarm;
        public string Repeat;
        public DateTime Due;
        public bool Enabled = true;

        public void Fire(DateTime now)
        {
            if (Repeat == "Once")
            {
                Enabled = false;
                return;
            }
            Due = now.Date.Add(Due.TimeOfDay);
            if (Due <= now)
                Due = Due.AddDays(1);
            while (Repeat == "Weekdays" &&
                   (Due.DayOfWeek == DayOfWeek.Saturday || Due.DayOfWeek == DayOfWeek.Sunday))
                Due = Due.AddDays(1);
        }
    }

    public class Settings
    {
        public string Pet;
        public string Name;
        public string Fur = "Honey";
        public string Accessory = "None";
        public DateTime FocusEndsUtc;
        public int FocusMinutes = 25;
        public int FocusSessions;
        public string FocusGoal = "";
        public string FocusMode = "Study";
        public string FocusPhase = "Idle";
        public bool DistractionNudges = true;
        public int BreakMinutes = 5;
        public double FocusRemainingSeconds;
        public DateTime FocusDay;
        public int TodaySessions;
        public int TodayMinutes;
        public int TotalFocusMinutes;
        public double X = double.NaN;
        public double Y = double.NaN;
        public List<Note> Notes = new List<Note>();
        public List<Alert> Alerts = new List<Alert>();
        public List<string> Zones = new List<string>();
        public DateTime TimerEndsUtc;
        public double TimerSeconds;
        public double TimerInitial;

        public double TimerLeft(DateTime utcNow)
        {
            return TimerEndsUtc == DateTime.MinValue
                ? TimerSeconds : Math.Max(0, (TimerEndsUtc - utcNow).TotalSeconds);
        }
    }

    internal class Storage
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(Settings));
        private readonly string file;

        public Storage(string folder)
        {
            file = Path.Combine(folder, "settings.xml");
        }

        public Settings Load()
        {
            foreach (string candidate in new[] { file, file + ".bak" })
            {
                if (!File.Exists(candidate))
                    continue;
                try
                {
                    using (var stream = File.OpenRead(candidate))
                        return (Settings)Serializer.Deserialize(stream);
                }
                catch (IOException) { }
                catch (InvalidOperationException) { }
            }
            return new Settings();
        }

        public void Save(Settings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            string pending = file + ".tmp";
            using (var stream = File.Create(pending))
            {
                Serializer.Serialize(stream, settings);
                stream.Flush(true);
            }
            if (File.Exists(file))
                File.Replace(pending, file, file + ".bak");
            else
                File.Move(pending, file);
        }
    }

    internal static class ClockMath
    {
        public static bool TryTime(string value, out TimeSpan time)
        {
            return TimeSpan.TryParseExact(value.Trim(), new[] { @"h\:mm", @"hh\:mm" },
                CultureInfo.InvariantCulture, out time);
        }

        public static string Readout(TimeSpan value, bool hundredths)
        {
            string digits = string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}:{2:00}",
                (int)value.TotalHours, value.Minutes, value.Seconds);
            return hundredths ? digits + string.Format(CultureInfo.InvariantCulture, ".{0:00}", value.Milliseconds / 10) : digits;
        }
    }
}
