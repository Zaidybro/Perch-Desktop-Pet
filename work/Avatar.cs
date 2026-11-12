using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Perch
{
    internal sealed class Avatar : FrameworkElement
    {
        private enum Motion { Idle, Blink, Drag, Land, Wave, Hop, Shuffle, Twist, Pet, Type, Click, Note, ClickPaw, ClickEar, ClickBoop, AmbientEar, AmbientLook, AmbientStretch, Rub }
        internal struct Pose
        {
            public double X, Y, Angle, Stretch, HeadY, HeadAngle, LeftEar, RightEar;
            public double LeftPaw, RightPaw, Tail;
            public bool Smile, Sleepy, Paper, Wink;
        }

        private readonly Stopwatch time;
        private readonly DispatcherTimer frames;
        private readonly Random random = new Random();
        private static readonly Motion[] clickMoves = { Motion.Click, Motion.ClickPaw, Motion.ClickEar, Motion.ClickBoop };
        private static readonly Motion[] ambientMoves = { Motion.AmbientEar, Motion.AmbientLook, Motion.AmbientStretch };
        private Motion motion;
        private int dance;
        private int lastClick = -1, lastAmbient = -1;
        private DateTime nextAmbientUtc;
        private double tilt, rubDirection, gazeX, gazeY;
        public string Fur { get; set; }
        public string Accessory { get; set; }
        public bool FocusActive { get; set; }

        public Avatar(string kind, bool animated)
        {
            RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.NearestNeighbor);
            if (!animated) return;
            time = new Stopwatch();
            frames = new DispatcherTimer(DispatcherPriority.Background);
            frames.Tick += delegate
            {
                if (motion == Motion.Idle && DateTime.UtcNow >= nextAmbientUtc) PlayAmbient();
                else if (motion == Motion.Idle && time.Elapsed.TotalSeconds >= 4.1) Play(Motion.Blink);
                else if (motion != Motion.Idle && motion != Motion.Drag && time.Elapsed.TotalSeconds >= Duration()) Play(Motion.Idle);
                else InvalidateVisual();
            };
            Loaded += delegate { nextAmbientUtc = DateTime.UtcNow.AddSeconds(40); Play(Motion.Idle); };
            Unloaded += delegate { frames.Stop(); };
        }

        public void Wave() { if (motion == Motion.Idle || motion == Motion.Blink) Play(Motion.Wave); }
        public void Carry(double horizontal)
        {
            tilt = Clamp(horizontal * 2, -24, 24);
            if (motion != Motion.Drag) Play(Motion.Drag);
            else InvalidateVisual();
        }
        public void Land() { Play(Motion.Land); }
        public void Rub(double horizontal)
        {
            rubDirection = Clamp(horizontal * 2, -12, 12);
            if (motion != Motion.Rub) Play(Motion.Rub);
            else InvalidateVisual();
        }
        public void Dance() { Play((Motion)((int)Motion.Hop + dance++ % 3)); }
        public void Pet() { Play(Motion.Pet); }
        public void NoteSaved() { Play(Motion.Note); }
        public void Typing() { if (motion == Motion.Idle || motion == Motion.Blink || motion == Motion.Type) Play(Motion.Type); }
        public void Clicking()
        {
            if (motion != Motion.Idle && motion != Motion.Blink) return;
            int choice = random.Next(clickMoves.Length - (lastClick < 0 ? 0 : 1));
            if (lastClick >= 0 && choice >= lastClick) choice++;
            lastClick = choice;
            Play(clickMoves[choice]);
        }

        private void PlayAmbient()
        {
            nextAmbientUtc = DateTime.UtcNow.AddSeconds(40);
            int choice = random.Next(ambientMoves.Length - (lastAmbient < 0 ? 0 : 1));
            if (lastAmbient >= 0 && choice >= lastAmbient) choice++;
            lastAmbient = choice;
            Play(ambientMoves[choice]);
        }

        public void LookAt(Point point)
        {
            double x = Clamp((point.X - ActualWidth / 2) / 85, -4, 4);
            double y = Clamp((point.Y - ActualHeight / 2) / 110, -2.5, 2.5);
            if (Math.Abs(x - gazeX) < .12 && Math.Abs(y - gazeY) < .12) return;
            gazeX = x; gazeY = y; InvalidateVisual();
        }
        public void LookAway()
        {
            if (gazeX == 0 && gazeY == 0) return;
            gazeX = gazeY = 0; InvalidateVisual();
        }

        private void Play(Motion next)
        {
            motion = next;
            time.Restart();
            frames.Interval = TimeSpan.FromMilliseconds(next == Motion.Idle ? 85 : next == Motion.Blink ? 140 : 33);
            frames.Start();
            InvalidateVisual();
        }
        private double Duration()
        {
            switch (motion)
            {
                case Motion.Blink: return .14;
                case Motion.Rub: return double.PositiveInfinity;
                case Motion.Land: return .43;
                case Motion.Type: return .4;
                case Motion.Click: return .48;
                case Motion.ClickPaw: return .55;
                case Motion.ClickEar: return .6;
                case Motion.ClickBoop: return .55;
                case Motion.AmbientEar: return .75;
                case Motion.AmbientLook: return 1.15;
                case Motion.AmbientStretch: return 1.1;
                case Motion.Wave: return .82;
                case Motion.Pet: return 1.05;
                case Motion.Note: return 1.35;
                default: return 1.72;
            }
        }

        private static double Clamp(double value, double low, double high) { return Math.Max(low, Math.Min(high, value)); }
        private static double Pulse(double t, double center, double width)
        {
            double distance = Math.Abs(t - center) / width;
            return distance >= 1 ? 0 : Math.Sin((1 - distance) * Math.PI / 2);
        }

        private Pose Animate(double t)
        {
            Pose p = new Pose { Stretch = 1, Tail = Math.Sin(t * 5) * 8 };
            switch (motion)
            {
                case Motion.Idle:
                    p.Y = Math.Sin(t * 2.5) * .9;
                    p.HeadAngle = Math.Sin(t * 1.8) * 1.6;
                    p.LeftEar = Math.Sin(t * 2) * 2;
                    p.RightEar = -Math.Sin(t * 2) * 3;
                    break;
                case Motion.Blink:
                    p.Sleepy = true;
                    break;
                case Motion.Drag:
                    p.Y = -10 + Math.Sin(t * 11) * 2;
                    p.Angle = tilt * .55 + Math.Sin(t * 14) * 5;
                    p.Stretch = 1.12 + Math.Sin(t * 16) * .025;
                    p.HeadAngle = -p.Angle * .45;
                    p.LeftEar = -13 + Math.Sin(t * 13) * 7;
                    p.RightEar = 12 - Math.Sin(t * 13) * 7;
                    p.LeftPaw = 8 + Math.Sin(t * 17) * 4;
                    p.RightPaw = 8 - Math.Sin(t * 17) * 4;
                    p.Tail = Math.Sin(t * 19) * 25;
                    break;
                case Motion.Land:
                    double impact = Pulse(t, .15, .16);
                    p.Stretch = 1 - impact * .22;
                    p.Y = impact * 5 - Pulse(t, .33, .12) * 4;
                    p.LeftEar = -impact * 15;
                    p.RightEar = impact * 15;
                    p.HeadY = impact * 5;
                    break;
                case Motion.Wave:
                    p.RightPaw = -13 - Math.Abs(Math.Sin(t * 15)) * 4;
                    p.HeadAngle = -8;
                    p.Tail = Math.Sin(t * 20) * 19;
                    p.Smile = true;
                    p.Wink = true;
                    break;
                case Motion.Hop:
                    double jump = Pulse(t, .51, .34) + Pulse(t, 1.22, .32);
                    p.Y = -27 * jump;
                    p.Stretch = 1 + jump * .09 - Pulse(t, .12, .11) * .09;
                    p.LeftPaw = -jump * 8;
                    p.RightPaw = -jump * 8;
                    p.LeftEar = -jump * 10;
                    p.RightEar = jump * 10;
                    p.Tail = Math.Sin(t * 23) * 20;
                    p.Smile = true;
                    break;
                case Motion.Shuffle:
                    p.X = Math.Sin(t * 12) * 12;
                    p.Y = -Math.Abs(Math.Sin(t * 12)) * 5;
                    p.Angle = -Math.Sin(t * 12) * 11;
                    p.HeadAngle = -p.Angle * .7;
                    p.LeftPaw = Math.Max(0, Math.Sin(t * 12)) * -9;
                    p.RightPaw = Math.Max(0, -Math.Sin(t * 12)) * -9;
                    p.Tail = Math.Sin(t * 24) * 25;
                    p.Smile = true;
                    break;
                case Motion.Twist:
                    p.Angle = Math.Sin(t * 10) * 26;
                    p.HeadAngle = -Math.Sin(t * 10) * 13;
                    p.Y = -Math.Abs(Math.Sin(t * 10)) * 8;
                    p.LeftEar = Math.Sin(t * 10) * 13;
                    p.RightEar = -Math.Sin(t * 10) * 13;
                    p.LeftPaw = -Math.Max(0, Math.Sin(t * 10)) * 7;
                    p.RightPaw = -Math.Max(0, -Math.Sin(t * 10)) * 7;
                    p.Tail = Math.Sin(t * 25) * 25;
                    p.Smile = true;
                    break;
                case Motion.Pet:
                    double joy = Pulse(t, .32, .3);
                    p.Y = -5 * joy;
                    p.HeadY = 4 - 5 * joy + Math.Sin(t * 12) * 1.5;
                    p.HeadAngle = Math.Sin(t * 9) * 6;
                    p.LeftEar = -8 - 7 * joy;
                    p.RightEar = 9 + 7 * joy;
                    p.Tail = Math.Sin(t * 24) * 22;
                    p.Sleepy = true;
                    p.Smile = true;
                    break;
                case Motion.Rub:
                    p.HeadY = 4 + Math.Sin(t * 11) * 1.5;
                    p.HeadAngle = rubDirection * .65 + Math.Sin(t * 10) * 2.5;
                    p.LeftEar = -8 + Math.Sin(t * 8) * 3;
                    p.RightEar = 8 - Math.Sin(t * 8) * 3;
                    p.Tail = Math.Sin(t * 23) * 18;
                    p.Sleepy = true;
                    p.Smile = true;
                    break;
                case Motion.Type:
                    p.LeftPaw = -Math.Max(0, Math.Sin(t * 33)) * 7;
                    p.RightPaw = -Math.Max(0, -Math.Sin(t * 33)) * 7;
                    p.HeadY = Math.Sin(t * 17) * 1.5;
                    p.Tail = Math.Sin(t * 16) * 12;
                    break;
                case Motion.Click:
                    p.HeadAngle = -12 * Pulse(t, .2, .22);
                    p.LeftEar = -12 * Pulse(t, .2, .22);
                    p.RightEar = 10 * Pulse(t, .2, .22);
                    p.RightPaw = -7 * Pulse(t, .2, .22);
                    p.Wink = t < .3;
                    break;
                case Motion.ClickPaw:
                    p.LeftPaw = -11 * Pulse(t, .22, .2);
                    p.HeadAngle = 5 * Pulse(t, .27, .23);
                    p.Tail = Math.Sin(t * 20) * 11;
                    p.Smile = true;
                    break;
                case Motion.ClickEar:
                    p.LeftEar = -19 * Pulse(t, .25, .23);
                    p.RightEar = 16 * Pulse(t, .25, .23);
                    p.HeadY = -2 * Pulse(t, .25, .23);
                    p.Wink = t > .18 && t < .38;
                    break;
                case Motion.ClickBoop:
                    p.Stretch = 1 - .065 * Pulse(t, .2, .19);
                    p.HeadY = 4 * Pulse(t, .2, .19);
                    p.Sleepy = t > .12 && t < .28;
                    p.Tail = Math.Sin(t * 18) * 10;
                    break;
                case Motion.AmbientEar:
                    p.LeftEar = -12 * Pulse(t, .22, .2);
                    p.RightEar = 7 * Pulse(t, .45, .2);
                    break;
                case Motion.AmbientLook:
                    p.HeadAngle = 6 * Pulse(t, .55, .48);
                    p.RightEar = -5 * Pulse(t, .55, .4);
                    break;
                case Motion.AmbientStretch:
                    double stretch = Pulse(t, .55, .5);
                    p.Stretch = 1 + .055 * stretch;
                    p.Y = -3 * stretch;
                    p.LeftPaw = -3 * stretch;
                    p.RightPaw = -3 * stretch;
                    p.Tail = Math.Sin(t * 10) * 11;
                    break;
                case Motion.Note:
                    p.HeadAngle = 8 * Pulse(t, .65, .62);
                    p.LeftPaw = -13 * Pulse(t, .7, .55);
                    p.Paper = t > .15 && t < 1.23;
                    p.Tail = Math.Sin(t * 15) * 12;
                    p.Smile = t > .7;
                    p.Wink = t > .8;
                    break;
            }
            return p;
        }

        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);
            if (ActualWidth <= 0 || ActualHeight <= 0) return;
            double t = time == null ? 0 : time.Elapsed.TotalSeconds;
            Pose p = Animate(t);
            if (FocusActive && (motion == Motion.AmbientEar || motion == Motion.AmbientLook || motion == Motion.AmbientStretch))
            {
                p.HeadAngle *= .55;
                p.LeftEar *= .55;
                p.RightEar *= .55;
                p.Y *= .55;
                p.Stretch = 1 + (p.Stretch - 1) * .55;
            }
            if (FocusActive && motion == Motion.Idle)
            {
                p.HeadAngle = Math.Sin(t * 1.2) * .7;
                p.Tail *= .35;
            }
            p.X += p.Angle * .18;
            var frame = PixelMochi.Frame(p, Fur, Accessory, FocusActive, gazeX, gazeY,
                motion == Motion.Hop || motion == Motion.Shuffle || motion == Motion.Twist,
                motion == Motion.Pet, t);
            double size = Math.Min(ActualWidth, ActualHeight);
            dc.DrawImage(frame, new Rect((ActualWidth - size) / 2, (ActualHeight - size) / 2, size, size));
        }
    }
}
