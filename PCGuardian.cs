// PC Guardian - free PC scanner & maintenance tool (GUI shell)
// The real work is done by PCGuardian.ps1 in the same folder;
// this app just runs it and shows the output in a friendly window.
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PCGuardian
{
    // Design tokens shared by the window and its custom-drawn controls.
    static class Theme
    {
        public static readonly Color BG       = Color.FromArgb(0x18, 0x1a, 0x22);
        public static readonly Color SIDEBAR  = Color.FromArgb(0x1b, 0x1e, 0x28);
        public static readonly Color SURFACE  = Color.FromArgb(0x25, 0x29, 0x36);
        public static readonly Color ACTIVE   = Color.FromArgb(0x28, 0x2c, 0x3a);
        public static readonly Color BORDER   = Color.FromArgb(0x32, 0x37, 0x4a);
        public static readonly Color CONSBG   = Color.FromArgb(0x13, 0x15, 0x1c);
        public static readonly Color ACCENT   = Color.FromArgb(0x00, 0xd2, 0xa0);
        public static readonly Color ACCENTDK = Color.FromArgb(0x00, 0xb0, 0x86);
        public static readonly Color ONACCENT = Color.FromArgb(0x0d, 0x1f, 0x1a);
        public static readonly Color TEXT     = Color.FromArgb(0xf2, 0xf4, 0xf8);
        public static readonly Color TEXT2    = Color.FromArgb(0xd1, 0xd5, 0xdb);
        public static readonly Color MUTED    = Color.FromArgb(0x8d, 0x94, 0xa5);
        public static readonly Color DIM      = Color.FromArgb(0x5c, 0x62, 0x73);
        public static readonly Color WARN     = Color.FromArgb(0xff, 0xb8, 0x6c);
        public static readonly Color ERR      = Color.FromArgb(0xff, 0x6e, 0x6e);

        // Windows 11 ships Segoe Fluent Icons; Windows 10 has the same glyphs in Segoe MDL2 Assets.
        public static readonly string IconFont = HasFont("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        public static readonly string MonoFont = HasFont("Cascadia Mono") ? "Cascadia Mono" : "Consolas";

        static bool HasFont(string name)
        {
            using (InstalledFontCollection fonts = new InstalledFontCollection())
                foreach (FontFamily f in fonts.Families) if (f.Name == name) return true;
            return false;
        }
    }

    // Sidebar task button: icon, title and hint, with hover / active / disabled / primary states.
    // Still a real Button underneath, so keyboard, accessibility and BM_CLICK keep working.
    class TaskButton : Button
    {
        public string Glyph, Title, Hint;
        public bool Primary;
        bool active, hover;
        readonly float dpi;

        public bool Active { get { return active; } set { active = value; Invalidate(); } }

        public TaskButton(string glyph, string title, string hint, float dpi)
        {
            Glyph = glyph; Title = title; Hint = hint; this.dpi = dpi;
            Text = hint.Length > 0 ? title + "\r\n" + hint : title;   // read by screen readers
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Dock = DockStyle.Top;
            Height = (int)((hint.Length > 0 ? 54 : 44) * dpi);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(Parent != null ? Parent.BackColor : Theme.SIDEBAR);

            // the card, inset so stacked buttons get a small gap between them
            Rectangle card = new Rectangle(0, 0, Width, Height - (int)(6 * dpi));
            Color back = Primary ? (hover && Enabled ? Theme.ACCENTDK : Theme.ACCENT)
                       : active ? Theme.ACTIVE
                       : hover && Enabled ? Theme.SURFACE
                       : Theme.SIDEBAR;
            if (Primary && !Enabled) back = Theme.SURFACE;
            using (GraphicsPath path = Rounded(card, (int)(6 * dpi)))
            using (SolidBrush b = new SolidBrush(back))
                g.FillPath(b, path);

            if (active && !Primary)
                using (SolidBrush b = new SolidBrush(Theme.ACCENT))
                    g.FillRectangle(b, 0, card.Top + (int)(10 * dpi), (int)(3 * dpi), card.Height - (int)(20 * dpi));

            Color iconCol  = !Enabled ? Theme.DIM : Primary ? Theme.ONACCENT : (hover || active) ? Theme.ACCENT : Theme.MUTED;
            Color titleCol = !Enabled ? Theme.DIM : Primary ? Theme.ONACCENT : Theme.TEXT;
            Color hintCol  = !Enabled ? Theme.DIM : Primary ? Theme.ONACCENT : Theme.MUTED;

            int left = (int)(14 * dpi);
            using (Font iconFont = new Font(Theme.IconFont, 13f))
            {
                Rectangle iconBox = new Rectangle(left, card.Top, (int)(24 * dpi), card.Height);
                TextRenderer.DrawText(g, Glyph, iconFont, iconBox, iconCol,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            int textLeft = left + (int)(36 * dpi);
            Rectangle textBox = new Rectangle(textLeft, card.Top, card.Width - textLeft - (int)(8 * dpi), card.Height);
            using (Font titleFont = new Font("Segoe UI Semibold", 9.75f))
            using (Font hintFont = new Font("Segoe UI", 8.25f))
            {
                if (Hint.Length == 0)
                {
                    TextRenderer.DrawText(g, Title, titleFont, textBox, titleCol,
                        TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    int th = TextRenderer.MeasureText(Title, titleFont).Height;
                    int hh = TextRenderer.MeasureText(Hint, hintFont).Height;
                    int top = card.Top + (card.Height - th - hh) / 2;
                    TextRenderer.DrawText(g, Title, titleFont, new Rectangle(textLeft, top, textBox.Width, th), titleCol,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    TextRenderer.DrawText(g, Hint, hintFont, new Rectangle(textLeft, top + th, textBox.Width, hh), hintCol,
                        TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
            }

            if (Focused && ShowFocusCues)
                using (Pen p = new Pen(Theme.ACCENT, Math.Max(1f, dpi)))
                using (GraphicsPath path = Rounded(new Rectangle(1, 1, card.Width - 3, card.Height - 3), (int)(6 * dpi)))
                    g.DrawPath(p, path);
        }

        public static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    // Thin indeterminate progress line: a teal segment sweeping while a task runs.
    class ProgressLine : Control
    {
        readonly Timer timer = new Timer();
        float pos;
        public ProgressLine()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            timer.Interval = 16;
            timer.Tick += delegate { pos += 0.012f; if (pos > 1.3f) pos = -0.3f; Invalidate(); };
        }
        public bool Running
        {
            get { return timer.Enabled; }
            set { timer.Enabled = value; pos = -0.3f; Invalidate(); }
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.BORDER);
            if (!Running) return;
            int w = Width / 4;
            using (SolidBrush b = new SolidBrush(Theme.ACCENT))
                e.Graphics.FillRectangle(b, (int)(pos * Width), 0, w, Height);
        }
        protected override void Dispose(bool disposing) { if (disposing) timer.Dispose(); base.Dispose(disposing); }
    }

    // Shown in the console area until the first task runs.
    class EmptyState : Control
    {
        readonly float dpi;
        public EmptyState(float dpi)
        {
            this.dpi = dpi;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.CONSBG;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.Clear(BackColor);
            string[] lines = {
                "Pick a task on the left to get started.",
                "Not sure? \"Run Everything\" does a quick scan, deep audit, cleanup and driver check.",
                "Deep Audit tip: every line should be software you recognize.",
                "Anything unfamiliar? Look it up before deleting it."
            };
            using (Font icon = new Font(Theme.IconFont, 40f))
            using (Font title = new Font("Segoe UI Semibold", 13f))
            using (Font body = new Font("Segoe UI", 9.5f))
            {
                int iconH = TextRenderer.MeasureText("", icon).Height;
                int titleH = TextRenderer.MeasureText(lines[0], title).Height;
                int bodyH = TextRenderer.MeasureText("X", body).Height;
                int total = iconH + (int)(12 * dpi) + titleH + (int)(10 * dpi) + bodyH * 3 + (int)(8 * dpi);
                int y = Math.Max((int)(16 * dpi), (Height - total) / 2);
                TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.WordBreak;
                TextRenderer.DrawText(g, "", icon, new Rectangle(0, y, Width, iconH), Theme.ACCENT, center);
                y += iconH + (int)(12 * dpi);
                TextRenderer.DrawText(g, lines[0], title, new Rectangle(0, y, Width, titleH), Theme.TEXT, center);
                y += titleH + (int)(10 * dpi);
                TextRenderer.DrawText(g, lines[1], body, new Rectangle((int)(24 * dpi), y, Width - (int)(48 * dpi), bodyH), Theme.MUTED, center);
                y += bodyH + (int)(8 * dpi);
                TextRenderer.DrawText(g, lines[2], body, new Rectangle(0, y, Width, bodyH), Theme.MUTED, center);
                y += bodyH;
                TextRenderer.DrawText(g, lines[3], body, new Rectangle(0, y, Width, bodyH), Theme.MUTED, center);
            }
        }
    }

    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);

        RichTextBox console;
        EmptyState emptyState;
        Label statusLabel;
        ProgressLine progress;
        Button stopButton;
        TaskButton[] taskButtons;
        Process current;
        string currentTask;
        bool closing;
        bool afterRule;   // the previous console line was a ===== rule (so this one may be a section title)
        bool titleOpen;   // printed a section title; the next rule closes the header box
        Font boldFont;
        string scriptPath;
        float dpi = 1f;

        // fonts follow the display scale automatically; layout pixels don't, so we scale them ourselves
        int S(int v) { return (int)(v * dpi); }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }

        public MainForm()
        {
            scriptPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PCGuardian.ps1");

            Text = "PC Guardian";
            StartPosition = FormStartPosition.CenterScreen;
            using (Graphics gr = CreateGraphics()) dpi = gr.DpiX / 96f;
            ClientSize = new Size(S(1080), S(700));
            MinimumSize = new Size(S(940), S(680));
            BackColor = Theme.BG;
            ForeColor = Theme.TEXT;
            Font = new Font("Segoe UI", 9.75f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ----- header -----
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = S(76);
            header.BackColor = Theme.BG;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.BORDER))
                    e.Graphics.DrawLine(p, 0, header.Height - 1, header.Width, header.Height - 1);
            };

            Label logo = new Label();
            logo.Text = "";
            logo.Font = new Font(Theme.IconFont, 22f);
            logo.ForeColor = Theme.ACCENT;
            logo.AutoSize = true;
            logo.Location = new Point(S(22), S(18));

            Label title = new Label();
            title.Text = "PC Guardian";
            title.Font = new Font("Segoe UI Semibold", 16f);
            title.ForeColor = Theme.TEXT;
            title.AutoSize = true;
            title.Location = new Point(S(66), S(10));

            Label subtitle = new Label();
            subtitle.UseMnemonic = false;
            subtitle.Text = "Honest PC maintenance, powered by Windows' own tools.";
            subtitle.Font = new Font("Segoe UI", 9f);
            subtitle.ForeColor = Theme.MUTED;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(S(68), S(44));

            Label badge = new Label();
            badge.Text = "Free  ·  Open source  ·  No subscriptions";
            badge.Font = new Font("Segoe UI", 8.25f);
            badge.ForeColor = Theme.MUTED;
            badge.AutoSize = true;
            badge.Anchor = AnchorStyles.Top | AnchorStyles.Right;

            header.Controls.Add(logo);
            header.Controls.Add(title);
            header.Controls.Add(subtitle);
            header.Controls.Add(badge);
            header.Layout += delegate { badge.Location = new Point(header.Width - badge.Width - S(22), S(30)); };

            // ----- status bar -----
            Panel status = new Panel();
            status.Dock = DockStyle.Bottom;
            status.Height = S(42);
            status.BackColor = Theme.SIDEBAR;

            progress = new ProgressLine();
            progress.Dock = DockStyle.Top;
            progress.Height = Math.Max(2, S(2));

            statusLabel = new Label();
            statusLabel.Text = "Ready";
            statusLabel.ForeColor = Theme.MUTED;
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(S(20), S(12));

            stopButton = new Button();
            stopButton.Text = "✕  Stop task";
            stopButton.FlatStyle = FlatStyle.Flat;
            stopButton.FlatAppearance.BorderColor = Theme.BORDER;
            stopButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(0x4a, 0x2a, 0x30);
            stopButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(0x5a, 0x30, 0x36);
            stopButton.BackColor = Theme.SIDEBAR;
            stopButton.ForeColor = Theme.TEXT2;
            stopButton.Font = new Font("Segoe UI", 9f);
            stopButton.Size = new Size(S(120), S(30));
            stopButton.Cursor = Cursors.Hand;
            stopButton.Enabled = false;
            stopButton.Visible = false;
            stopButton.Click += delegate { StopTask(); };
            status.Layout += delegate { stopButton.Location = new Point(status.Width - stopButton.Width - S(14), S(7)); };

            status.Controls.Add(statusLabel);
            status.Controls.Add(stopButton);
            status.Controls.Add(progress);

            // ----- sidebar -----
            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = S(292);
            sidebar.BackColor = Theme.SIDEBAR;
            sidebar.Padding = new Padding(S(12), S(14), S(12), S(10));
            sidebar.AutoScroll = false;

            // { glyph, label, task, hint } - group headers have an empty task
            string[,] tasks = new string[,] {
                { "", "Run Everything",     "all",           "Scan, audit, clean & check drivers" },
                { "",       "SCANS",              "",              "" },
                { "", "Quick Scan",         "quick",         "Common malware spots  ·  ~5 min" },
                { "", "Full Scan",          "full",          "Every file on disk  ·  30–90 min" },
                { "", "Deep Audit",         "audit",         "Startup, hijacks, extensions" },
                { "",       "MAINTENANCE",        "",              "" },
                { "", "Clean Junk",         "clean",         "Temp files only, never documents" },
                { "", "Update Definitions", "definitions",   "Refresh Defender's virus list" },
                { "",       "DRIVERS",            "",              "" },
                { "", "Check Drivers",      "drivers",       "See what Windows Update offers" },
                { "", "Install Drivers",    "driverinstall", "Restore point first, then install" }
            };

            int count = tasks.GetLength(0);
            System.Collections.Generic.List<TaskButton> buttons = new System.Collections.Generic.List<TaskButton>();
            // build bottom-up because DockStyle.Top stacks in reverse order
            for (int i = count - 1; i >= 0; i--)
            {
                string glyph = tasks[i, 0], label = tasks[i, 1], task = tasks[i, 2], hint = tasks[i, 3];
                if (task.Length == 0)
                {
                    Label group = new Label();
                    group.Text = label;
                    group.Font = new Font("Segoe UI Semibold", 7.5f);
                    group.ForeColor = Theme.DIM;
                    group.Dock = DockStyle.Top;
                    group.Height = S(28);
                    group.TextAlign = ContentAlignment.BottomLeft;
                    group.Padding = new Padding(S(6), 0, 0, S(6));
                    sidebar.Controls.Add(group);
                    continue;
                }
                TaskButton b = new TaskButton(glyph, label, hint, dpi);
                b.Primary = task == "all";
                if (b.Primary) b.Height = S(60);
                b.Click += delegate
                {
                    foreach (TaskButton other in taskButtons) other.Active = false;
                    b.Active = true;
                    RunTask(task, label);
                };
                buttons.Insert(0, b);
                sidebar.Controls.Add(b);
            }
            taskButtons = buttons.ToArray();

            // ----- console -----
            console = new RichTextBox();
            console.Dock = DockStyle.Fill;
            console.ReadOnly = true;
            console.BackColor = Theme.CONSBG;
            console.ForeColor = Theme.TEXT2;
            console.BorderStyle = BorderStyle.None;
            console.Font = new Font(Theme.MonoFont, 9.75f);
            console.DetectUrls = false;
            console.Visible = false;
            // dark scrollbar instead of the default white one (Windows 10 1809+ / 11)
            console.HandleCreated += delegate { try { SetWindowTheme(console.Handle, "DarkMode_Explorer", null); } catch { } };

            emptyState = new EmptyState(dpi);
            emptyState.Dock = DockStyle.Fill;

            Panel consoleWrap = new Panel();
            consoleWrap.Dock = DockStyle.Fill;
            consoleWrap.Padding = new Padding(S(22), S(18), S(14), S(12));
            consoleWrap.BackColor = Theme.CONSBG;
            consoleWrap.Controls.Add(console);
            consoleWrap.Controls.Add(emptyState);

            Controls.Add(consoleWrap);
            Controls.Add(sidebar);
            Controls.Add(status);
            Controls.Add(header);

            FormClosing += delegate(object s, FormClosingEventArgs e)
            {
                if (IsRunning() && currentTask == "driverinstall" &&
                    MessageBox.Show("Drivers are still installing. Closing now could leave one half-installed.\r\n\r\nClose anyway?",
                        "Install in progress", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                closing = true;
                if (IsScanTask(currentTask) && IsRunning()) CancelDefenderScan();
                try { if (IsRunning()) current.Kill(); } catch { }
            };

            if (!File.Exists(scriptPath))
            {
                ShowConsole();
                AppendLine("WARNING: PCGuardian.ps1 not found next to the app - keep them in the same folder.", Theme.WARN);
            }
        }

        // Dark title bar to match the app (Windows 10 2004+ / Windows 11; ignored elsewhere).
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4);
                int caption = Theme.BG.R | (Theme.BG.G << 8) | (Theme.BG.B << 16);   // COLORREF is 0x00BBGGRR
                DwmSetWindowAttribute(Handle, 35, ref caption, 4);                    // Windows 11 only
            }
            catch { }
        }

        void ShowConsole()
        {
            if (console.Visible) return;
            emptyState.Visible = false;
            console.Visible = true;
        }

        bool IsRunning() { return current != null && !current.HasExited; }

        // Tasks whose real work is a Windows Defender scan running inside Defender's own service.
        static bool IsScanTask(string task) { return task == "quick" || task == "full" || task == "all"; }

        // Process events arrive on worker threads and can land after the window is gone.
        void SafeInvoke(Action a)
        {
            if (closing || IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); }
            catch (InvalidOperationException) { }   // includes ObjectDisposedException
        }

        // Killing our PowerShell doesn't stop a scan - Defender's service keeps running it.
        // Defender's own command-line tool can cancel it (needs admin, which this app has).
        bool CancelDefenderScan()
        {
            string tool = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                                       @"Windows Defender\MpCmdRun.exe");
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo(tool, "-Scan -Cancel");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                using (Process p = Process.Start(psi))
                {
                    if (!p.WaitForExit(15000)) return false;
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }
        }

        void RunTask(string task, string label)
        {
            if (IsRunning()) return;

            if (task == "driverinstall")
            {
                DialogResult ok = MessageBox.Show(
                    "This will create a System Restore point (your undo button), then download and install " +
                    "every pending driver from Windows Update.\r\n\r\nOnly Microsoft-tested, signed drivers are used. Continue?",
                    "Install drivers safely", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ok != DialogResult.Yes) return;
            }
            if (task == "full")
            {
                DialogResult ok = MessageBox.Show(
                    "The full scan reads every file on the disk and usually takes 30–90 minutes.\r\n" +
                    "The PC stays usable the whole time. Start it?",
                    "Full-disk scan", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (ok != DialogResult.Yes) return;
            }

            ShowConsole();
            if (console.TextLength > 0) AppendLine("", Theme.TEXT2);
            currentTask = task;
            SetBusy(true, label);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" -Task " + task + " -AutoYes";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            Process p = new Process();
            p.StartInfo = psi;
            p.EnableRaisingEvents = true;
            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data != null) SafeInvoke(delegate { AppendColored(e.Data); });
            };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data != null && e.Data.Trim().Length > 0)
                    SafeInvoke(delegate { AppendLine("[ERROR] " + e.Data, Theme.ERR); });
            };
            p.Exited += delegate
            {
                SafeInvoke(delegate
                {
                    if (p != current) return;   // a task the user already stopped
                    AppendLine("✓  " + label + " finished", Theme.ACCENT, true);
                    SetBusy(false, null);
                });
            };
            current = p;

            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AppendLine("Could not start task: " + ex.Message, Theme.WARN);
                SetBusy(false, null);
            }
        }

        void StopTask()
        {
            if (!IsRunning()) return;
            Process p = current;
            current = null;   // its Exited event is now ignored
            if (IsScanTask(currentTask))
            {
                if (CancelDefenderScan()) AppendLine("Windows Defender cancelled the scan.", Theme.WARN);
                else AppendLine("Couldn't cancel Defender's scan - it may keep running in the background " +
                                "(see Windows Security > Virus & threat protection).", Theme.WARN);
            }
            try { if (!p.HasExited) p.Kill(); } catch { }
            AppendLine("Task stopped by user.", Theme.WARN);
            SetBusy(false, null);
        }

        void SetBusy(bool busy, string label)
        {
            foreach (Button b in taskButtons) b.Enabled = !busy;
            // A driver install can't be safely interrupted halfway, so it can't be stopped.
            stopButton.Enabled = busy && currentTask != "driverinstall";
            stopButton.Visible = busy;
            progress.Running = busy;
            statusLabel.ForeColor = busy ? Theme.TEXT2 : Theme.MUTED;
            statusLabel.Text = !busy ? "Ready"
                             : currentTask == "driverinstall" ? "Running: " + label + "…  (can't be stopped midway)"
                             : "Running: " + label + "…";
        }

        void AppendColored(string line)
        {
            string t = line.TrimStart();
            bool rule = t.Length > 1 && t == new string('=', t.Length);
            if (rule)                                                   { AppendLine(line, Theme.BORDER); afterRule = !titleOpen; titleOpen = false; return; }
            if (afterRule && t.Length > 0)                              { AppendLine(line, Theme.TEXT, true); afterRule = false; titleOpen = true; return; }
            afterRule = false;
            if (t.StartsWith("---"))                                    AppendLine(line, Theme.TEXT, true);
            else if (line.Contains("[OK]"))                             AppendLine(line, Theme.ACCENT);
            else if (line.Contains("[CHECK]") || line.Contains("WARNING")) AppendLine(line, Theme.WARN);
            else                                                        AppendLine(line, Theme.TEXT2);
        }

        void AppendLine(string line, Color color) { AppendLine(line, color, false); }

        void AppendLine(string line, Color color, bool bold)
        {
            console.SelectionStart = console.TextLength;
            console.SelectionColor = color;
            if (boldFont == null) boldFont = new Font(console.Font, FontStyle.Bold);
            console.SelectionFont = bold ? boldFont : console.Font;
            console.AppendText(line + "\r\n");
            console.SelectionStart = console.TextLength;
            console.ScrollToCaret();
        }
    }
}
