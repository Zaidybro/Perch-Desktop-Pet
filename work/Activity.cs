using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace Perch
{
    internal partial class DeskPet
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct CursorPoint { public int X, Y; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out CursorPoint point);
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);

        private DispatcherTimer activity;
        private DateTime lastTyping = DateTime.MinValue;
        private static readonly int[] WatchedKeys = MakeWatchedKeys();

        private static int[] MakeWatchedKeys()
        {
            var keys = new System.Collections.Generic.List<int> { 8, 9, 13, 32 };
            for (int key = 0x30; key <= 0x5A; key++) keys.Add(key);
            for (int key = 0xBA; key <= 0xDE; key++) keys.Add(key);
            return keys.ToArray();
        }

        private void StartActivity()
        {
            if (activity != null) return;
            activity = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(65) };
            activity.Tick += delegate
            {
                if (avatar == null || pet == null) return;
                CursorPoint cursor;
                if (GetCursorPos(out cursor))
                {
                    Point center = avatar.PointToScreen(new Point(avatar.ActualWidth / 2, avatar.ActualHeight / 2));
                    double dx = cursor.X - center.X, dy = cursor.Y - center.Y;
                    if (dx * dx + dy * dy < 430 * 430)
                        avatar.LookAt(avatar.PointFromScreen(new Point(cursor.X, cursor.Y)));
                    else avatar.LookAway();
                }
                bool typed = false;
                foreach (int key in WatchedKeys)
                    if ((GetAsyncKeyState(key) & 1) != 0) typed = true;
                if (typed && (DateTime.UtcNow - lastTyping).TotalMilliseconds > 180)
                {
                    lastTyping = DateTime.UtcNow;
                    avatar.Typing();
                }
                if ((GetAsyncKeyState(1) & 1) != 0 || (GetAsyncKeyState(2) & 1) != 0)
                    avatar.Clicking();
            };
            activity.Start();
        }
    }
}
