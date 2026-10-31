// Capri 立绘桌宠 —— 只做立绘：三套素材（清晰 / 像素 / 动漫）、换装、大小、随处走动、拖拽。
// 素材来自 X1a0heZzzz/capri-desktop-pet（角色素材不随本程序分发，见 packs/README.txt）。
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CapriPet
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new PetForm());
        }
    }

    internal sealed class PetForm : Form
    {
        private const int FW = 192, FH = 208, COLS = 8, ROWS = 9;

        private static readonly string Root = Path.Combine(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "capri-sprite-pet"));
        private static readonly string PrefsFile = Path.Combine(Root, "prefs.json");
        private static readonly string PackDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "packs");

        private static readonly string[] PackIds = { "capri-clear", "capri-pixel", "capri-anime" };
        private static readonly string[] PackNames = { "清晰", "像素", "动漫" };

        private sealed class Clip
        {
            internal int Row, Frames;
            internal int[] Delay;
            internal bool Walk;
        }

        private static readonly Dictionary<string, Clip> Clips = new Dictionary<string, Clip>
        {
            { "idle",    new Clip { Row = 0, Frames = 6, Delay = new[] { 280, 110, 110, 140, 140, 320 } } },
            { "right",   new Clip { Row = 1, Frames = 8, Delay = Fill(8, 120), Walk = true } },
            { "left",    new Clip { Row = 2, Frames = 8, Delay = Fill(8, 120), Walk = true } },
            { "wave",    new Clip { Row = 3, Frames = 4, Delay = new[] { 140, 140, 140, 280 } } },
            { "jump",    new Clip { Row = 4, Frames = 5, Delay = new[] { 140, 140, 140, 140, 280 } } },
            { "tired",   new Clip { Row = 5, Frames = 8, Delay = Fill(8, 140) } },
            { "waiting", new Clip { Row = 6, Frames = 6, Delay = Fill(6, 150) } },
            { "busy",    new Clip { Row = 7, Frames = 6, Delay = Fill(6, 120) } },
            { "review",  new Clip { Row = 8, Frames = 6, Delay = Fill(6, 150) } }
        };

        private static int[] Fill(int n, int ms)
        {
            int[] d = new int[n];
            for (int i = 0; i < n; i++) d[i] = (i == n - 1) ? ms * 2 : ms;
            return d;
        }

        private readonly Timer clock = new Timer { Interval = 30 };
        private readonly Dictionary<string, Bitmap[]> frames = new Dictionary<string, Bitmap[]>();
        private readonly Random random = new Random();
        private NotifyIcon tray;

        private string pack = "capri-clear";
        private float scale = 1f;
        private string clipName = "idle";
        private int frame, elapsed;
        private double walkLeft;
        private int walkDir;
        private double untilWander = 6;
        private bool dragging;
        private Point grab;
        private bool topMost = true;

        public PetForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            Text = "Capri";

            LoadPrefs();
            LoadPack(pack);
            ApplySize();

            clock.Tick += OnTick;
            clock.Start();
            BuildTray();
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00080000; // WS_EX_LAYERED
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        // ---------- 素材 ----------

        private void LoadPack(string id)
        {
            string file = Path.Combine(Path.Combine(PackDir, id), "spritesheet.png");
            if (!File.Exists(file))
            {
                foreach (string other in PackIds)
                {
                    string fallback = Path.Combine(Path.Combine(PackDir, other), "spritesheet.png");
                    if (File.Exists(fallback)) { id = other; file = fallback; break; }
                }
            }
            if (!File.Exists(file)) throw new FileNotFoundException("找不到立绘素材：" + file);

            pack = id;
            frames.Clear();
            using (Bitmap atlas = new Bitmap(file))
            {
                for (int row = 0; row < ROWS; row++)
                {
                    Bitmap[] rowFrames = new Bitmap[COLS];
                    for (int col = 0; col < COLS; col++)
                    {
                        Rectangle cut = new Rectangle(col * FW, row * FH, FW, FH);
                        if (cut.Right > atlas.Width || cut.Bottom > atlas.Height) { rowFrames[col] = null; continue; }
                        Bitmap copy = new Bitmap(FW, FH, PixelFormat.Format32bppPArgb);
                        using (Graphics g = Graphics.FromImage(copy))
                        {
                            g.CompositingMode = CompositingMode.SourceCopy;
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            g.DrawImage(atlas, new Rectangle(0, 0, FW, FH), cut, GraphicsUnit.Pixel);
                        }
                        rowFrames[col] = copy;
                    }
                    frames["row" + row] = rowFrames;
                }
            }
            frame = 0;
            elapsed = 0;
        }

        private Bitmap Current
        {
            get
            {
                Clip clip = Clips[clipName];
                Bitmap[] row = frames["row" + clip.Row];
                int index = frame % clip.Frames;
                if (index >= row.Length || row[index] == null) index = 0;
                return row[index];
            }
        }

        // ---------- 行为 ----------

        private void SetClip(string name, bool restart = true)
        {
            if (clipName == name && !restart) return;
            clipName = name;
            frame = 0;
            elapsed = 0;
            Paint();
        }

        private void OnTick(object sender, EventArgs e)
        {
            Clip clip = Clips[clipName];
            elapsed += clock.Interval;
            int delay = clip.Delay[frame % clip.Delay.Length];
            if (elapsed >= delay)
            {
                elapsed = 0;
                frame++;
                if (frame >= clip.Frames)
                {
                    if (clipName == "wave" || clipName == "jump") SetClip("idle");
                    else frame = 0;
                }
                Paint();
            }

            if (clipName == "right" || clipName == "left")
            {
                int step = (int)Math.Max(1, 2 * scale);
                double dx = clipName == "right" ? step : -step;
                Rectangle screen = Screen.FromPoint(new Point(Left + Width / 2, Top + Height / 2)).Bounds;
                int next = (int)Math.Round(Left + dx);
                if (next < screen.Left + 8 || next + Width > screen.Right - 8)
                {
                    next = Math.Max(screen.Left + 8, Math.Min(next, screen.Right - Width - 8));
                    SetClip(clipName == "right" ? "left" : "right");
                }
                Left = next;
                walkLeft -= Math.Abs(dx);
                if (walkLeft <= 0) SetClip("idle");
                Paint();
                return;
            }

            untilWander -= clock.Interval / 1000.0;
            if (untilWander <= 0 && !dragging && clipName == "idle")
            {
                untilWander = 6 + random.NextDouble() * 8;
                if (random.NextDouble() < 0.55)
                {
                    walkDir = random.Next(2) == 0 ? 1 : -1;
                    walkLeft = 80 + random.NextDouble() * 240;
                    SetClip(walkDir > 0 ? "right" : "left");
                }
                else
                {
                    string[] idleShows = { "wave", "jump", "waiting", "review" };
                    SetClip(idleShows[random.Next(idleShows.Length)]);
                }
            }
        }

        // ---------- 绘制（分层窗口，按像素透明）----------

        private new void Paint()
        {
            Bitmap art = Current;
            if (art == null) return;
            int w = Math.Max(1, (int)Math.Round(FW * scale));
            int h = Math.Max(1, (int)Math.Round(FH * scale));
            using (Bitmap canvas = new Bitmap(w, h, PixelFormat.Format32bppPArgb))
            {
                using (Graphics g = Graphics.FromImage(canvas))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(art, new Rectangle(0, 0, w, h));
                }
                Blend(canvas, Left, Top);
            }
            if (Width != w || Height != h) Size = new Size(w, h);
        }

        private void Blend(Bitmap canvas, int x, int y)
        {
            IntPtr screen = GetDC(IntPtr.Zero);
            IntPtr memory = CreateCompatibleDC(screen);
            IntPtr hBitmap = canvas.GetHbitmap(Color.FromArgb(0));
            IntPtr previous = SelectObject(memory, hBitmap);
            try
            {
                Native.POINT src = new Native.POINT(0, 0);
                Native.SIZE size = new Native.SIZE(canvas.Width, canvas.Height);
                Native.POINT dst = new Native.POINT(x, y);
                Native.BLENDFUNCTION blend = new Native.BLENDFUNCTION
                {
                    BlendOp = 0,
                    SourceConstantAlpha = 255,
                    AlphaFormat = 1
                };
                UpdateLayeredWindow(Handle, screen, ref dst, ref size, memory, ref src, 0, ref blend, 2);
            }
            finally
            {
                SelectObject(memory, previous);
                DeleteObject(hBitmap);
                DeleteDC(memory);
                ReleaseDC(IntPtr.Zero, screen);
            }
        }

        // ---------- 交互 ----------

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            grab = new Point(Cursor.Position.X - Left, Cursor.Position.Y - Top);
            if (clipName != "idle") SetClip("idle");
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            Left = Cursor.Position.X - grab.X;
            Top = Cursor.Position.Y - grab.Y;
            Paint();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            SavePrefs();
        }

        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            SetClip("wave");
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Right) return;
            ShowMenu(PointToClient(Cursor.Position));
        }

        private void ShowMenu(Point at)
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Font = new Font("Microsoft YaHei UI", 9f);
            foreach (string id in PackIds)
            {
                string copy = id;
                ToolStripMenuItem item = new ToolStripMenuItem("换装：" + PackNames[Array.IndexOf(PackIds, id)]);
                item.Checked = id == pack;
                item.Click += delegate
                {
                    LoadPack(copy);
                    ApplySize();
                    SavePrefs();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            float[] sizes = { 0.75f, 1f, 1.25f, 1.5f, 2f };
            foreach (float size in sizes)
            {
                float copy = size;
                ToolStripMenuItem item = new ToolStripMenuItem("大小 " + Math.Round(size * 100) + "%");
                item.Checked = Math.Abs(size - scale) < 0.01f;
                item.Click += delegate
                {
                    scale = copy;
                    ApplySize();
                    SavePrefs();
                };
                menu.Items.Add(item);
            }
            menu.Items.Add(new ToolStripSeparator());
            ToolStripMenuItem stay = new ToolStripMenuItem("总在最前");
            stay.Checked = topMost;
            stay.Click += delegate
            {
                topMost = !topMost;
                TopMost = topMost;
                SavePrefs();
            };
            menu.Items.Add(stay);
            menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { Close(); }));
            menu.Show(this, at);
        }

        private void ApplySize()
        {
            Size = new Size(Math.Max(1, (int)Math.Round(FW * scale)), Math.Max(1, (int)Math.Round(FH * scale)));
            Paint();
        }

        // ---------- 托盘 ----------

        private void BuildTray()
        {
            tray = new NotifyIcon();
            Bitmap art = frames["row0"][0];
            tray.Icon = Icon.FromHandle(art.GetHicon());
            tray.Text = "Capri 立绘桌宠";
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add(new ToolStripMenuItem("呼到这里", null, delegate
            {
                Rectangle screen = Screen.PrimaryScreen.WorkingArea;
                Left = screen.Right - Width - 60;
                Top = screen.Bottom - Height - 60;
                Paint();
                SavePrefs();
            }));
            menu.Items.Add(new ToolStripMenuItem("退出", null, delegate { Close(); }));
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { Show(); BringToFront(); };
            tray.Visible = true;
        }

        // ---------- 预置 ----------

        private void LoadPrefs()
        {
            try
            {
                if (!File.Exists(PrefsFile)) { PlaceDefault(); return; }
                string json = File.ReadAllText(PrefsFile, Encoding.UTF8);
                pack = Read(json, "pack") ?? pack;
                scale = ParseFloat(Read(json, "scale"), 1f);
                Left = (int)ParseFloat(Read(json, "x"), -1);
                Top = (int)ParseFloat(Read(json, "y"), -1);
                topMost = (Read(json, "top") ?? "true") != "false";
                TopMost = topMost;
                if (Left < -4000 || Top < -4000) PlaceDefault();
            }
            catch (Exception) { PlaceDefault(); }
        }

        private void PlaceDefault()
        {
            Rectangle screen = Screen.PrimaryScreen.WorkingArea;
            Left = screen.Right - Math.Max(1, (int)(FW * scale)) - 80;
            Top = screen.Bottom - Math.Max(1, (int)(FH * scale)) - 60;
        }

        private static string Read(string json, string key)
        {
            int at = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (at < 0) return null;
            int colon = json.IndexOf(':', at);
            if (colon < 0) return null;
            int end = json.IndexOfAny(new[] { ',', '}' }, colon + 1);
            if (end < 0) end = json.Length;
            string value = json.Substring(colon + 1, end - colon - 1).Trim().Trim('"');
            return value.Length == 0 ? null : value;
        }

        private static float ParseFloat(string text, float fallback)
        {
            float value;
            return float.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private void SavePrefs()
        {
            try
            {
                Directory.CreateDirectory(Root);
                string json = "{\"pack\":\"" + pack + "\",\"scale\":"
                    + scale.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",\"x\":" + Left
                    + ",\"y\":" + Top + ",\"top\":" + (topMost ? "true" : "false") + "}";
                File.WriteAllText(PrefsFile, json, Encoding.UTF8);
            }
            catch (Exception) { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SavePrefs();
            clock.Stop();
            if (tray != null) { tray.Visible = false; tray.Dispose(); }
            base.OnFormClosing(e);
        }

        // ---------- Win32 ----------

        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);
        [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, ref Native.POINT pptDst,
            ref Native.SIZE psize, IntPtr hdcSrc, ref Native.POINT pptSrc, int crKey, ref Native.BLENDFUNCTION pblend, int dwFlags);
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; public POINT(int x, int y) { X = x; Y = y; } }
        [StructLayout(LayoutKind.Sequential)] internal struct SIZE { public int CX, CY; public SIZE(int cx, int cy) { CX = cx; CY = cy; } }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }
    }
}
