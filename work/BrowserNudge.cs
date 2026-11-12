using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Perch
{
    internal static class BrowserNudge
    {
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        public static string SiteFromAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address)) return null;
            Uri uri;
            if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
                Uri.TryCreate("https://" + address, UriKind.Absolute, out uri);
            if (uri == null || (uri.Scheme != "http" && uri.Scheme != "https")) return null;
            string host = uri.Host.ToLowerInvariant();
            foreach (string site in new[] { "youtube.com", "tiktok.com", "roblox.com" })
                if (host == site || host.EndsWith("." + site, StringComparison.Ordinal))
                    return site.Split('.')[0];
            return null;
        }

        public static string ActiveSite()
        {
            try
            {
                IntPtr window = GetForegroundWindow();
                if (window == IntPtr.Zero) return null;
                uint processId;
                GetWindowThreadProcessId(window, out processId);
                if (processId == 0) return null;
                using (var process = Process.GetProcessById((int)processId))
                {
                    string name = process.ProcessName.ToLowerInvariant();
                    if (name != "chrome" && name != "msedge" && name != "firefox" &&
                        name != "brave" && name != "opera" && name != "vivaldi") return null;
                }
                AutomationElement root = AutomationElement.FromHandle(window);
                if (root == null) return null;
                var edits = root.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
                double windowTop = root.Current.BoundingRectangle.Top;
                foreach (AutomationElement edit in edits)
                {
                    string label = (edit.Current.Name + " " + edit.Current.AutomationId).ToLowerInvariant();
                    double top = edit.Current.BoundingRectangle.Top - windowTop;
                    if (!label.Contains("address") && !label.Contains("adress") &&
                        !label.Contains("urlbar") && !label.Contains("location") &&
                        (top < 0 || top > 160)) continue;
                    object value;
                    if (!edit.TryGetCurrentPattern(ValuePattern.Pattern, out value)) continue;
                    string site = SiteFromAddress(((ValuePattern)value).Current.Value);
                    if (site != null) return site;
                }
            }
            catch (Exception) { /* Browsers may hide their address bar from accessibility. */ }
            return null;
        }
    }

    internal partial class DeskPet
    {
        private DispatcherTimer distractionCheck;
        private ToolTip nudgeTip;
        private DateTime lastNudgeUtc = DateTime.MinValue;
        private int checkingDistraction;

        private void StartDistractionChecks()
        {
            if (distractionCheck != null) return;
            distractionCheck = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(4) };
            distractionCheck.Tick += delegate
            {
                if (settings.FocusPhase != "Focus" || !settings.DistractionNudges || exiting ||
                    DateTime.UtcNow - lastNudgeUtc < TimeSpan.FromMinutes(3) ||
                    Interlocked.Exchange(ref checkingDistraction, 1) != 0) return;
                ThreadPool.QueueUserWorkItem(delegate
                {
                    string site = BrowserNudge.ActiveSite();
                    Interlocked.Exchange(ref checkingDistraction, 0);
                    if (site == null) return;
                    var app = Application.Current;
                    if (app == null || app.Dispatcher.HasShutdownStarted) return;
                    app.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (exiting || settings.FocusPhase != "Focus" || !settings.DistractionNudges ||
                            DateTime.UtcNow - lastNudgeUtc < TimeSpan.FromMinutes(3)) return;
                        lastNudgeUtc = DateTime.UtcNow;
                        ShowNudge(site);
                    }));
                });
            };
            distractionCheck.Start();
        }

        private void ShowNudge(string site)
        {
            if (pet == null || surface == null) return;
            if (nudgeTip != null) nudgeTip.IsOpen = false;
            nudgeTip = new ToolTip
            {
                Content = new TextBlock
                {
                    Text = "Mochi spotted " + char.ToUpperInvariant(site[0]) + site.Substring(1) + ". Back to your goal?",
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 210
                },
                PlacementTarget = surface,
                Placement = PlacementMode.Top,
                Background = new SolidColorBrush(Color.FromRgb(37, 43, 41)),
                Foreground = Brushes.White,
                Padding = new Thickness(12, 8, 12, 8),
                MaxWidth = 240,
                IsOpen = true
            };
            avatar.Wave();
            var close = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
            ToolTip shown = nudgeTip;
            close.Tick += delegate { close.Stop(); shown.IsOpen = false; };
            close.Start();
        }
    }
}
