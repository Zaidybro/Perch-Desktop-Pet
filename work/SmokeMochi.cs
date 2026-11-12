using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Perch
{
    internal static class SmokeMochi
    {
        [STAThread]
        public static int Main()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "work", "smoke", "qa");
            Directory.CreateDirectory(dir);
            var store = new Storage(Path.Combine(dir, "profile-" + Guid.NewGuid().ToString("N")));
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var desk = new DeskPet(store);
            Check(BrowserNudge.SiteFromAddress("https://www.youtube.com/watch?v=abc") == "youtube", "YouTube address detected");
            Check(BrowserNudge.SiteFromAddress("m.tiktok.com") == "tiktok", "TikTok address detected");
            Check(BrowserNudge.SiteFromAddress("https://www.roblox.com/home") == "roblox", "Roblox address detected");
            Check(BrowserNudge.SiteFromAddress("https://notyoutube.com") == null, "similar domains ignored");
            Exception failure = null;
            app.Dispatcher.BeginInvoke(new Action(delegate
            {
                try
                {
                    desk.Run();
                    var onboarding = app.Windows.Cast<Window>().Single();
                    Shot((FrameworkElement)onboarding.Content, Path.Combine(dir, "onboarding.png"));
                    Find<TextBox>(onboarding, t => AutomationProperties.GetName(t) == "Pet name").Text = "Mochi";
                    Click(onboarding, "Bring my pet to the desktop");
                    var pet = app.Windows.Cast<Window>().Single(w => w.IsVisible);
                    Pump(900);
                    Shot((FrameworkElement)pet.Content, Path.Combine(dir, "pet.png"));
                    var menu = ((FrameworkElement)pet.Content).ContextMenu;
                    ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                    Pump(150);
                    var popup = (Popup)typeof(DeskPet).GetField("flyout", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(desk);
                    Check(popup.IsOpen, "anchored popup opens");
                    Shot((FrameworkElement)popup.Child, Path.Combine(dir, "menu.png"));
                    Click((FrameworkElement)popup.Child, "Capture thought");
                    var noteAvatar = (Avatar)typeof(DeskPet).GetField("avatar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(desk);
                    Check(typeof(Avatar).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(noteAvatar).ToString() == "Note", "new note starts paper animation");
                    Click((FrameworkElement)popup.Child, "Back");
                    Click((FrameworkElement)popup.Child, "Lock in");
                    Shot((FrameworkElement)popup.Child, Path.Combine(dir, "focus.png"));
                    Find<TextBox>((FrameworkElement)popup.Child, t => AutomationProperties.GetName(t) == "Focus goal").Text = "Study biology";
                    Click((FrameworkElement)popup.Child, "Start lock-in");
                    Check(store.Load().FocusEndsUtc > DateTime.UtcNow, "focus persisted");
                    var settings = (Settings)typeof(DeskPet).GetField("settings", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(desk);
                    Check(settings.FocusPhase == "Focus" && settings.FocusGoal == "Study biology", "goal and lock-in active");
                    var focusLabel = (TextBlock)typeof(DeskPet).GetField("focusLabel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(desk);
                    Check(focusLabel.Text.StartsWith("FOCUS "), "desktop pet shows focus countdown");
                    Click((FrameworkElement)popup.Child, "Pause");
                    Check(settings.FocusPhase == "PausedFocus" && settings.FocusRemainingSeconds > 0, "focus paused");
                    Click((FrameworkElement)popup.Child, "Resume");
                    Check(settings.FocusPhase == "Focus", "focus resumed");
                    var checkAlerts = typeof(DeskPet).GetMethod("CheckAlerts", BindingFlags.NonPublic | BindingFlags.Instance);
                    settings.FocusEndsUtc = DateTime.UtcNow.AddSeconds(-1);
                    checkAlerts.Invoke(desk, null);
                    Check(store.Load().FocusSessions == 1 && settings.FocusPhase == "Break" && settings.TodayMinutes == settings.FocusMinutes, "completion starts break and counts time");
                    Check(focusLabel.Text.StartsWith("BREAK "), "desktop pet shows break countdown");
                    Click((FrameworkElement)popup.Child, "Skip break");
                    Check(settings.FocusPhase == "Idle", "break can be skipped");
                    Click((FrameworkElement)popup.Child, "Start lock-in");
                    settings.FocusEndsUtc = DateTime.UtcNow.AddSeconds(-1);
                    checkAlerts.Invoke(desk, null);
                    Check(store.Load().FocusSessions == 2, "second focus counted");
                    settings.FocusEndsUtc = DateTime.UtcNow.AddSeconds(-1);
                    checkAlerts.Invoke(desk, null);
                    Check(settings.FocusPhase == "Idle" && settings.FocusSessions == 2, "break completes without counting as focus");
                    Click((FrameworkElement)popup.Child, "Back");
                    Click((FrameworkElement)popup.Child, "Customize");
                    ((FrameworkElement)popup.Child).UpdateLayout();
                    Pump(100);
                    var combos = Descendants<ComboBox>((FrameworkElement)popup.Child).ToArray();
                    Check(combos.Length >= 2, "customization controls visible");
                    Check(combos[1].Items.Contains("Bow"), "bow unlocked after focus");
                    combos[0].SelectedItem = "Cocoa";
                    combos[1].SelectedItem = "Bandana";
                    Check(store.Load().Fur == "Cocoa" && store.Load().Accessory == "Bandana", "customization persisted");
                    Shot((FrameworkElement)pet.Content, Path.Combine(dir, "customized.png"));
                    Check(combos[0].Items.Contains("Strawberry") && combos[0].Items.Contains("Midnight") &&
                        combos[0].Items.Contains("Rainy day"), "pixel themes available");
                    foreach (string theme in new[] { "Honey", "Cream", "Cocoa", "Strawberry", "Midnight", "Rainy day" })
                    {
                        combos[0].SelectedItem = theme;
                        var sample = new Avatar("Corgi", false) { Width = 136, Height = 136, Fur = theme };
                        sample.Measure(new Size(136, 136));
                        sample.Arrange(new Rect(0, 0, 136, 136));
                        Shot(sample, Path.Combine(dir, "theme-" + theme.Replace(' ', '-') + ".png"));
                    }
                    Check(store.Load().Fur == "Rainy day", "pixel theme persisted");
                    combos[0].SelectedItem = "Cocoa";
                    var avatar = (Avatar)typeof(DeskPet).GetField("avatar", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(desk);
                    Pump(1800);
                    avatar.Clicking();
                    string firstClick = typeof(Avatar).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(avatar).ToString();
                    Pump(220); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "click-1.png"));
                    Pump(450);
                    avatar.Clicking();
                    string secondClick = typeof(Avatar).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(avatar).ToString();
                    Check(firstClick != secondClick, "click reactions do not immediately repeat");
                    Pump(220); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "click-2.png"));
                    Pump(500);
                    typeof(Avatar).GetField("nextAmbientUtc", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(avatar, DateTime.UtcNow.AddSeconds(-1));
                    Pump(120);
                    Check(typeof(Avatar).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(avatar).ToString().StartsWith("Ambient"), "quiet ambient action starts after interval");
                    Shot((FrameworkElement)pet.Content, Path.Combine(dir, "ambient.png"));
                    avatar.Rub(5); Pump(220);
                    Check(typeof(Avatar).GetField("motion", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(avatar).ToString() == "Rub", "short held movement plays rub animation");
                    Shot((FrameworkElement)pet.Content, Path.Combine(dir, "rub.png"));
                    avatar.Pet(); Pump(180); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "pet-after-rub.png"));
                    avatar.Carry(8); Pump(200); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "drag.png"));
                    avatar.Land(); Pump(160); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "land.png"));
                    avatar.Dance(); Pump(450); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "hop.png"));
                    avatar.Dance(); Pump(320); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "shuffle.png"));
                    avatar.Dance(); Pump(320); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "twist.png"));
                    avatar.NoteSaved(); Pump(500); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "note.png"));
                    Pump(1000); avatar.Typing(); Pump(180); Shot((FrameworkElement)pet.Content, Path.Combine(dir, "typing.png"));
                    Click((FrameworkElement)popup.Child, "Emergency quit");
                    Console.WriteLine("OK emergency quit requested");
                }
                catch (Exception ex) { failure = ex; app.Shutdown(1); }
            }));
            int result = app.Run();
            if (failure != null) Console.Error.WriteLine(failure);
            else if (result == 0) Console.WriteLine("PASS emergency shutdown completed");
            return result;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Console.WriteLine("OK " + message);
        }
        private static void Pump(int ms)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ms) };
            timer.Tick += delegate { timer.Stop(); frame.Continue = false; };
            timer.Start(); Dispatcher.PushFrame(frame);
        }
        private static void Click(DependencyObject root, string label)
        {
            var element = root as FrameworkElement;
            if (element != null) element.UpdateLayout();
            var button = Find<Button>(root, b => (b.Content as string) == label || AutomationProperties.GetName(b) == label);
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
        private static T Find<T>(DependencyObject root, Func<T, bool> match) where T : DependencyObject
        {
            return Descendants<T>(root).First(match);
        }
        private static System.Collections.Generic.IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
        {
            T self = root as T;
            if (self != null) yield return self;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
                foreach (T value in Descendants<T>(VisualTreeHelper.GetChild(root, i))) yield return value;
        }
        private static void Shot(FrameworkElement element, string path)
        {
            element.UpdateLayout();
            int width = Math.Max(1, (int)Math.Ceiling(element.ActualWidth));
            int height = Math.Max(1, (int)Math.Ceiling(element.ActualHeight));
            var image = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            image.Render(element);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
