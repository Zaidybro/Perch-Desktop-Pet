using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Perch
{
    internal static class Program
    {
        [STAThread]
        public static void Main()
        {
            bool first;
            using (var instance = new Mutex(true, @"Local\Perch.DesktopPet", out first))
            {
                if (!first)
                {
                    MessageBox.Show("Perch is already running. Click your pet to open its tools.", "Perch");
                    return;
                }
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Perch");
                var storage = new Storage(folder);
                new DeskPet(storage, () => Environment.Exit(0)).Run();
                app.Run();
            }
        }
    }

    internal partial class DeskPet : Theme
    {
        private static readonly string[] Kinds = { "Corgi" };
        private static readonly string[] DefaultNames = { "Mochi" };
        private static readonly string[] PetColors = { "#FAE9D7" };

        private readonly Storage storage;
        private readonly Action exitProcess;
        private readonly Settings settings;
        private readonly DispatcherTimer alerts = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer readouts = new DispatcherTimer(DispatcherPriority.Background);
        private readonly DispatcherTimer saveSoon;
        private readonly Stopwatch stopwatch = new Stopwatch();
        private readonly List<TimeSpan> laps = new List<TimeSpan>();
        private readonly List<string> notifications = new List<string>();
        private Window pet;
        private Window onboarding;
        private Grid surface;
        private Popup flyout;
        private Border flyoutCard;
        private Avatar avatar;
        private Border badge;
        private StackPanel banner;
        private bool exiting;
        private bool dirty;
        private bool saveErrorShown;
        private string clockPage = "Timer";
        private string currentPage = "Menu";
        private Action refreshReadout;

        public DeskPet(Storage storage, Action exitProcess = null)
        {
            this.storage = storage;
            this.exitProcess = exitProcess;
            settings = storage.Load();
            if (settings.FocusEndsUtc != DateTime.MinValue && settings.FocusPhase == "Idle")
                settings.FocusPhase = "Focus";
            alerts.Tick += delegate { CheckAlerts(); };
            focusTicker.Interval = TimeSpan.FromSeconds(1);
            focusTicker.Tick += delegate { UpdateFocusIndicator(); };
            readouts.Tick += delegate { if (refreshReadout != null) refreshReadout(); };
            saveSoon = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(320) };
            saveSoon.Tick += delegate { saveSoon.Stop(); Save(); };
        }

        public void Run()
        {
            if (!Kinds.Contains(settings.Pet) && !string.IsNullOrWhiteSpace(settings.Name))
            {
                settings.Pet = "Corgi";
                Changed();
            }
            if (!Kinds.Contains(settings.Pet))
            {
                onboarding = new Window
                {
                    Title = "Perch", Width = 562, Height = 620, MinWidth = 480,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Background = Brush(Paper), FontFamily = new FontFamily("Segoe UI"),
                    Content = BuildPetPicker(true)
                };
                onboarding.Closed += delegate
                {
                    onboarding = null;
                    if (pet == null && !exiting)
                        Quit();
                };
                onboarding.Show();
            }
            else
            {
                ShowPet();
                CheckAlerts();
            }
        }

        private void SetReadout(Action update, int milliseconds = 0)
        {
            readouts.Stop();
            refreshReadout = update;
            if (update != null)
                update();
            if (milliseconds > 0)
            {
                readouts.Interval = TimeSpan.FromMilliseconds(milliseconds);
                readouts.Start();
            }
        }

        private void Changed(bool debounce = false)
        {
            dirty = true;
            if (debounce)
            {
                saveSoon.Stop();
                saveSoon.Start();
            }
            else
                Save();
        }

        private void Save()
        {
            if (!dirty)
                return;
            try
            {
                storage.Save(settings);
                dirty = false;
                saveErrorShown = false;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException) && !(ex is UnauthorizedAccessException))
                    throw;
                if (!saveErrorShown && !exiting)
                {
                    saveErrorShown = true;
                    Notify("Perch couldn't save to your local profile. Your changes are still in memory.");
                }
            }
        }

        private void Quit()
        {
            exiting = true;
            settings.FocusPhase = "Idle";
            settings.FocusEndsUtc = DateTime.MinValue;
            settings.FocusRemainingSeconds = 0;
            dirty = true;
            alerts.Stop();
            readouts.Stop();
            focusTicker.Stop();
            if (activity != null) activity.Stop();
            if (distractionCheck != null) distractionCheck.Stop();
            if (nudgeTip != null) nudgeTip.IsOpen = false;
            saveSoon.Stop();
            Save();
            if (flyout != null)
                flyout.IsOpen = false;
            Application.Current.Shutdown();
            if (exitProcess != null)
                exitProcess();
        }

        private Button Emergency()
        {
            var button = Button("Emergency quit", Quit, Pink, Red);
            button.ToolTip = "Close Perch immediately";
            return button;
        }

        private FrameworkElement BuildPetPicker(bool firstRun)
        {
            var form = new StackPanel { Margin = firstRun ? new Thickness(26, 20, 26, 16) : new Thickness(4) };
            if (firstRun)
            {
                Add(form, Text("P E R C H   /   G E T   S T A R T E D", 11, Green, true), 12);
                Add(form, Text("Meet your lock-in buddy", 31, Ink, true), 5);
                Add(form, Text("Mochi helps you study or work one block at a time.", 15, Muted), 17);
            }
            var grid = new UniformGrid { Columns = 1 };
            Avatar livePreview = null;
            var cards = new List<Button>();
            int selected = firstRun ? 0 : Math.Max(0, Array.IndexOf(Kinds, settings.Pet));
            var name = Input(firstRun ? DefaultNames[selected] : settings.Name, "Pet name");
            name.MaxLength = 28;
            for (int i = 0; i < Kinds.Length; i++)
            {
                int index = i;
                double size = firstRun ? 101 : 57;
                var body = new StackPanel { Width = size };
                var preview = new Avatar(Kinds[i], false) { Height = size, Width = size, Fur = settings.Fur, Accessory = settings.Accessory };
                livePreview = preview;
                body.Children.Add(preview);
                var title = Text(DefaultNames[i], firstRun ? 15 : 12, Ink, true);
                title.TextAlignment = TextAlignment.Center;
                body.Children.Add(title);
                if (firstRun)
                {
                    var species = Text("Your tiny desktop corgi", 11, Muted);
                    species.TextAlignment = TextAlignment.Center;
                    body.Children.Add(species);
                }
                var card = Button(body, delegate
                {
                    string previousDefault = DefaultNames[selected];
                    selected = index;
                    if (string.IsNullOrWhiteSpace(name.Text) || name.Text == previousDefault)
                        name.Text = DefaultNames[selected];
                    for (int k = 0; k < cards.Count; k++)
                        cards[k].BorderBrush = Brush(k == selected ? Green : BorderColor);
                }, PetColors[i], Ink);
                AutomationProperties.SetName(card, DefaultNames[i]);
                if (!firstRun)
                    card.Padding = new Thickness(3);
                card.BorderThickness = new Thickness(2);
                card.BorderBrush = Brush(i == selected ? Green : BorderColor);
                cards.Add(card);
                grid.Children.Add(card);
            }
            Add(form, grid, 15);
            Add(form, CustomizationControls(livePreview), 12);
            Add(form, Text("Pet name", 13, Ink, true), 5);
            Add(form, name, 13);
            if (firstRun)
                Add(form, Card(Text("Click Mochi to set a goal and lock in. Drag to move. Emergency quit closes Perch in one click.", 13, Muted), Soft), 16);
            var start = Button(firstRun ? "Bring my pet to the desktop" : "Save pet", delegate
            {
                settings.Pet = Kinds[selected];
                settings.Name = string.IsNullOrWhiteSpace(name.Text) ? DefaultNames[selected] : name.Text.Trim();
                Changed();
                ShowPet();
                if (firstRun)
                {
                    onboarding.Close();
                    CheckAlerts();
                }
                else
                    ShowPage("Menu");
            }, Green, White);
            start.IsDefault = true;
            Add(form, firstRun ? Row(start, Emergency()) : Row(start), 0);
            return firstRun ? Scroll(form) : (FrameworkElement)form;
        }

        private void ShowPet()
        {
            bool create = pet == null;
            if (create)
            {
                pet = new Window
                {
                    Title = "Perch pet", Width = 142, Height = 137, AllowsTransparency = true,
                    WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize,
                    Background = System.Windows.Media.Brushes.Transparent, Topmost = true, ShowInTaskbar = false,
                    ShowActivated = false, WindowStartupLocation = WindowStartupLocation.Manual
                };
                Rect area = SystemParameters.WorkArea;
                pet.Left = OnDesktop(settings.X, SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenWidth)
                    ? settings.X : area.Right - pet.Width - 30;
                pet.Top = OnDesktop(settings.Y, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenHeight)
                    ? settings.Y : area.Bottom - pet.Height - 18;
                pet.Closed += delegate { if (!exiting) Quit(); };
            }
            if (create)
                surface = new Grid { Background = Brush("#01000000"), Cursor = Cursors.Hand };
            surface.Children.Clear();
            AutomationProperties.SetName(surface, settings.Name + ", desktop pet");
            var name = Text(settings.Name, 11, White, true);
            focusLabel = name;
            name.TextTrimming = TextTrimming.CharacterEllipsis;
            name.TextWrapping = TextWrapping.NoWrap;
            var tag = new Border
            {
                Child = name, Background = Brush("#E7255C47"), CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 2, 8, 2), MaxWidth = 130, HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0), IsHitTestVisible = false
            };
            avatar = new Avatar(settings.Pet, true) { Fur = settings.Fur, Accessory = settings.Accessory };
            surface.Children.Add(avatar);
            surface.Children.Add(tag);
            badge = new Border
            {
                Background = Brush(Red), CornerRadius = new CornerRadius(10), Width = 20, Height = 20,
                HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 21, 10, 0), IsHitTestVisible = false,
                Child = new TextBlock { Text = "!", Foreground = Brush(White), HorizontalAlignment = HorizontalAlignment.Center, FontWeight = FontWeights.Bold },
                Visibility = notifications.Count > 0 ? Visibility.Visible : Visibility.Collapsed
            };
            surface.Children.Add(badge);
            if (create)
            {
                MakeFlyout(surface);
                var open = new MenuItem { Header = "Open pet menu" };
                open.Click += delegate { pet.Dispatcher.BeginInvoke(new Action(OpenFlyout)); };
                var quit = new MenuItem { Header = "Emergency quit" };
                quit.Click += delegate { Quit(); };
                surface.ContextMenu = new ContextMenu();
                surface.ContextMenu.Items.Add(open);
                surface.ContextMenu.Items.Add(quit);
                surface.ToolTip = "Click for tools. Gently rub to pet. Drag farther to move. Right-click to quit.";
                SetUpPetDrag(surface);
                pet.Content = surface;
                pet.Show();
                StartActivity();
                StartDistractionChecks();
            }
            avatar.Wave();
            UpdateFocusIndicator();
        }

        private static bool OnDesktop(double value, double start, double extent)
        {
            return value >= start && value <= start + extent - 42;
        }

        private void SetUpPetDrag(Grid surface)
        {
            Point start = new Point(), last = new Point();
            double left = 0, top = 0;
            bool moved = false, rubbing = false;
            surface.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs args)
            {
                start = last = surface.PointToScreen(args.GetPosition(surface));
                left = pet.Left;
                top = pet.Top;
                moved = false;
                rubbing = false;
                surface.CaptureMouse();
            };
            surface.MouseMove += delegate(object sender, MouseEventArgs args)
            {
                if (!surface.IsMouseCaptured || args.LeftButton != MouseButtonState.Pressed)
                    return;
                Point current = surface.PointToScreen(args.GetPosition(surface));
                Vector delta = PresentationSource.FromVisual(surface).CompositionTarget.TransformFromDevice.Transform(current - start);
                if (!moved && delta.Length < 7)
                    return;
                if (!moved && delta.Length < 26)
                {
                    rubbing = true;
                    flyout.IsOpen = false;
                    avatar.Rub(current.X - last.X);
                    last = current;
                    return;
                }
                moved = true;
                rubbing = false;
                flyout.IsOpen = false;
                avatar.Carry(current.X - last.X);
                last = current;
                pet.Left = left + delta.X;
                pet.Top = top + delta.Y;
            };
            surface.MouseLeftButtonUp += delegate(object sender, MouseButtonEventArgs args)
            {
                if (!surface.IsMouseCaptured)
                    return;
                bool wasDragging = moved;
                bool wasRubbing = rubbing;
                rubbing = false;
                surface.ReleaseMouseCapture();
                if (wasRubbing && !wasDragging)
                {
                    avatar.Pet();
                    return;
                }
                if (!wasDragging)
                {
                    if (args.ClickCount >= 2)
                    {
                        flyout.IsOpen = false;
                        avatar.Pet();
                    }
                    else if (flyout.IsOpen)
                        flyout.IsOpen = false;
                    else
                    {
                        avatar.Wave();
                        OpenFlyout();
                    }
                }
            };
            surface.LostMouseCapture += delegate
            {
                if (rubbing)
                {
                    rubbing = false;
                    avatar.Pet();
                }
                if (!moved)
                    return;
                moved = false;
                avatar.Land();
                settings.X = pet.Left;
                settings.Y = pet.Top;
                Changed();
            };
        }

        private void MakeFlyout(Grid anchor)
        {
            flyout = new Popup
            {
                PlacementTarget = anchor,
                PlacementRectangle = new Rect(23, 23, 96, 100),
                Placement = PlacementMode.Custom,
                AllowsTransparency = true,
                StaysOpen = false,
                CustomPopupPlacementCallback = (size, target, offset) => new[]
                {
                    new CustomPopupPlacement(new Point(-size.Width - 5, target.Height - size.Height), PopupPrimaryAxis.Vertical),
                    new CustomPopupPlacement(new Point(target.Width + 5, target.Height - size.Height), PopupPrimaryAxis.Vertical),
                    new CustomPopupPlacement(new Point((target.Width - size.Width) / 2, -size.Height - 5), PopupPrimaryAxis.Horizontal)
                }
            };
            flyout.Closed += delegate
            {
                SetReadout(null);
                scheduleList = null;
                scheduleEdit = null;
                banner = null;
                flyout.Child = null;
                flyoutCard = null;
                Save();
            };
        }

        private void OpenFlyout()
        {
            if (exiting || flyout.IsOpen)
                return;
            flyoutCard = new Border
            {
                Background = Brush("#252B29"), BorderBrush = Brush("#464F4B"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(16), Padding = new Thickness(9), Focusable = true
            };
            flyoutCard.PreviewKeyDown += delegate(object sender, KeyEventArgs args)
            {
                if (args.Key != Key.Escape)
                    return;
                flyout.IsOpen = false;
                args.Handled = true;
            };
            flyout.Child = flyoutCard;
            ShowPage("Menu");
            flyout.IsOpen = true;
            flyoutCard.Focus();
        }

        private void ShowPage(string page, bool addNote = false)
        {
            currentPage = page;
            SetReadout(null);
            scheduleList = null;
            scheduleEdit = null;
            bool menu = page == "Menu";
            flyoutCard.Width = menu ? 237 : 343;
            var root = new StackPanel();
            var header = new DockPanel();
            var close = FlyoutButton("Close", "\uE711", () => flyout.IsOpen = false, true);
            DockPanel.SetDock(close, Dock.Right);
            header.Children.Add(close);
            if (!menu)
            {
                var back = FlyoutButton("Back", "\uE72B", () => ShowPage("Menu"), true);
                DockPanel.SetDock(back, Dock.Left);
                header.Children.Add(back);
            }
            var title = Text(menu ? settings.Name : page, 15, White, true);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(7, 0, 0, 0);
            title.TextWrapping = TextWrapping.NoWrap;
            title.TextTrimming = TextTrimming.CharacterEllipsis;
            header.Children.Add(title);
            Add(root, header, 7);
            banner = new StackPanel();
            root.Children.Add(banner);
            UpdateBanner();
            if (menu)
            {
                root.Children.Add(FlyoutButton("Lock in", "\uE823", () => ShowPage("Focus")));
                root.Children.Add(FlyoutButton("Capture thought", "\uE70F", () => ShowPage("Notes", true)));
                root.Children.Add(FlyoutButton("Saved notes", "\uE8A5", () => ShowPage("Notes")));
                root.Children.Add(FlyoutButton("Clock", "\uE823", () => ShowPage("Clock")));
                root.Children.Add(FlyoutButton("Reminders", "\uE7ED", () => ShowPage("Reminders")));
                root.Children.Add(FlyoutButton("Pet Mochi", "\uE8DC", delegate { flyout.IsOpen = false; avatar.Pet(); }));
                root.Children.Add(FlyoutButton("Do a dance", "♫", delegate { flyout.IsOpen = false; avatar.Dance(); }));
                root.Children.Add(FlyoutButton("Customize", "\uE77B", () => ShowPage("Customize")));
            }
            else
            {
                FrameworkElement body;
                switch (page)
                {
                    case "Notes": body = NotesPage(addNote); break;
                    case "Clock": body = ClockPage(); break;
                    case "Reminders": body = SchedulePane(false); break;
                    case "Focus": body = FocusPage(); break;
                    default: body = BuildPetPicker(false); break;
                }
                var scroll = Scroll(body);
                scroll.MaxHeight = Math.Min(page == "Focus" ? 460 : 370, SystemParameters.WorkArea.Height * .65);
                Add(root, Card(scroll, Paper, 8), 6);
            }
            Add(root, new Border { Height = 1, Background = Brush("#464F4B"), Margin = new Thickness(3, 5, 3, 0) }, 4);
            root.Children.Add(FlyoutButton("Emergency quit", "\uE8BB", Quit, false, "#FFC2B6"));
            flyoutCard.Child = root;
            if (flyout.IsOpen)
                flyoutCard.Focus();
        }

        private static Button FlyoutButton(string label, string icon, Action action, bool iconOnly = false, string color = White)
        {
            var glyph = Text(icon, 14, color);
            glyph.FontFamily = new FontFamily(icon == "♫" ? "Segoe UI Symbol" : "Segoe MDL2 Assets");
            glyph.Width = 24;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            var button = Button(iconOnly ? (UIElement)glyph : Row(glyph, Text(label, 13, color)), action, "#252B29", color);
            button.HorizontalContentAlignment = HorizontalAlignment.Left;
            button.Padding = new Thickness(iconOnly ? 6 : 9, 4, iconOnly ? 0 : 9, 4);
            button.MinHeight = 30;
            button.Margin = new Thickness(0);
            button.ToolTip = iconOnly ? label : null;
            AutomationProperties.SetName(button, label);
            return button;
        }

        private void ScheduleAlerts()
        {
            alerts.Stop();
            double seconds = double.PositiveInfinity;
            DateTime now = DateTime.Now;
            foreach (Alert alert in settings.Alerts)
                if (alert.Enabled)
                    seconds = Math.Min(seconds, (alert.Due - now).TotalSeconds);
            if (settings.TimerEndsUtc != DateTime.MinValue)
                seconds = Math.Min(seconds, settings.TimerLeft(DateTime.UtcNow));
            if (settings.FocusEndsUtc != DateTime.MinValue)
                seconds = Math.Min(seconds, (settings.FocusEndsUtc - DateTime.UtcNow).TotalSeconds);
            if (double.IsPositiveInfinity(seconds))
                return;
            alerts.Interval = TimeSpan.FromSeconds(Math.Max(.025, Math.Min(60, seconds)));
            alerts.Start();
        }

        private void CheckAlerts()
        {
            DateTime utcNow = DateTime.UtcNow;
            DateTime now = DateTime.Now;
            var due = new List<string>();
            if (settings.TimerEndsUtc != DateTime.MinValue && settings.TimerEndsUtc <= utcNow)
            {
                settings.TimerEndsUtc = DateTime.MinValue;
                settings.TimerSeconds = 0;
                due.Add("Timer finished");
            }
            if (settings.FocusEndsUtc != DateTime.MinValue && settings.FocusEndsUtc <= utcNow)
                FinishFocusPhase(utcNow, due);
            foreach (Alert alert in settings.Alerts.Where(a => a.Enabled && a.Due <= now))
            {
                alert.Fire(now);
                due.Add((alert.IsAlarm ? "Alarm: " : "Reminder: ") + alert.Label);
            }
            if (due.Count > 0)
            {
                Changed();
                foreach (string message in due)
                    Notify(message);
                if (scheduleList != null)
                    RefreshScheduleList();
                if (flyout != null && flyout.IsOpen && currentPage == "Focus")
                    ShowPage("Focus");
            }
            ScheduleAlerts();
            if (refreshReadout != null)
                refreshReadout();
        }

        private void Notify(string message)
        {
            notifications.Add(message);
            if (badge != null)
                badge.Visibility = Visibility.Visible;
            if (avatar != null)
                avatar.Wave();
            System.Media.SystemSounds.Exclamation.Play();
            UpdateBanner();
        }

        private void UpdateBanner()
        {
            if (banner == null)
                return;
            banner.Children.Clear();
            if (notifications.Count == 0)
                return;
            var row = new DockPanel();
            var dismiss = Button("Dismiss", delegate
            {
                notifications.Clear();
                if (badge != null)
                    badge.Visibility = Visibility.Collapsed;
                UpdateBanner();
            });
            DockPanel.SetDock(dismiss, Dock.Right);
            row.Children.Add(dismiss);
            string summary = string.Join("\n", notifications.Skip(Math.Max(0, notifications.Count - 3)));
            if (notifications.Count > 3)
                summary = (notifications.Count - 3) + " earlier alerts\n" + summary;
            var message = Text(summary, 12, Red, true);
            message.MaxHeight = 66;
            row.Children.Add(message);
            Add(banner, Card(row, Pink, 9), 8);
        }
    }

}
