// 开屏动画：一块 DSH 风格的接入面板，1.3 秒走完进度后淡出。
// 画面先画进 Bitmap 再当背景图，避开分层窗口下 OnPaint 不一定被调的问题。
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace CapriDesktop
{
    internal sealed class Splash : Form
    {
        private static readonly Color Back = Color.FromArgb(255, 7, 11, 20);
        private static readonly Color Cyan = Color.FromArgb(255, 0, 240, 255);
        private static readonly Color Rose = Color.FromArgb(255, 255, 42, 109);
        private static readonly Color Ink = Color.FromArgb(255, 232, 246, 255);
        private static readonly Color Dim = Color.FromArgb(255, 92, 110, 130);

        private static readonly string[] Steps =
        {
            "> uplink: deepseek harness",
            "> construct: capri",
            "> loading persona",
            "> ready"
        };

        private readonly Timer clock = new Timer { Interval = 30 };
        private readonly Random random = new Random();
        private int elapsed;
        private int jitter;
        private Bitmap art;

        private Splash()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(430, 132);
            BackColor = Back;
            DoubleBuffered = true;
            art = Render();
            BackgroundImage = art;
            clock.Tick += delegate
            {
                elapsed += clock.Interval;
                if (elapsed % 90 < clock.Interval) jitter = random.Next(-3, 4);
                if (elapsed > 1250) Opacity = Math.Max(0.0, 1.0 - (elapsed - 1250) / 260.0);
                if (elapsed > 1520) { Close(); return; }
                Bitmap next = Render();
                BackgroundImage = next;
                if (art != null) art.Dispose();
                art = next;
            };
            clock.Start();
        }

        internal static void Play(Point center)
        {
            Splash splash = new Splash();
            splash.Location = new Point(center.X - splash.Width / 2, center.Y - splash.Height / 2);
            splash.Show();
            splash.BringToFront();
        }

        private Bitmap Render()
        {
            Bitmap canvas = new Bitmap(Width <= 0 ? 430 : Width, Height <= 0 ? 132 : Height);
            using (Graphics g = Graphics.FromImage(canvas))
            using (SolidBrush fill = new SolidBrush(Back))
            using (Pen edge = new Pen(Cyan, 1f))
            using (Font title = new Font("Consolas", 15f, FontStyle.Bold))
            using (Font body = new Font("Consolas", 9.5f))
            using (SolidBrush ink = new SolidBrush(Ink))
            using (SolidBrush cyan = new SolidBrush(Cyan))
            using (SolidBrush rose = new SolidBrush(Rose))
            using (SolidBrush dim = new SolidBrush(Dim))
            using (SolidBrush track = new SolidBrush(Color.FromArgb(255, 22, 30, 44)))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.FillRectangle(fill, 0, 0, canvas.Width, canvas.Height);
                int cut = 14;
                Point[] shape =
                {
                    new Point(0, 0), new Point(canvas.Width - cut, 0), new Point(canvas.Width - 1, cut),
                    new Point(canvas.Width - 1, canvas.Height - 1), new Point(cut, canvas.Height - 1),
                    new Point(0, canvas.Height - cut)
                };
                g.DrawPolygon(edge, shape);
                g.DrawString("DEEPSEEK HARNESS", title, cyan, 16 + jitter, 10);
                g.DrawString("// CAPRI CONSTRUCT", body, rose, 286, 20);

                int step = Math.Min(Steps.Length - 1, Math.Max(0, elapsed / 300));
                g.DrawString(Steps[step], body, ink, 16, 50);

                double share = Math.Min(1.0, elapsed / 1250.0);
                int done = (int)((canvas.Width - 34) * share);
                g.FillRectangle(track, 16, 90, canvas.Width - 34, 8);
                g.FillRectangle(cyan, 16, 90, done, 8);
                g.DrawString((int)(share * 100) + "%", body, cyan, canvas.Width - 60, 104);
                g.DrawString("dsh 0.2.x · build " + Build(), body, dim, 16, 104);
            }
            return canvas;
        }

        private static string Build()
        {
            Version version = typeof(Splash).Assembly.GetName().Version;
            return version == null ? "local" : version.ToString(3);
        }

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            clock.Stop();
            clock.Dispose();
            if (art != null) art.Dispose();
            base.OnFormClosed(e);
        }
    }
}
