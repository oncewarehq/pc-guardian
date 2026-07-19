// PC Guardian - free PC scanner & maintenance tool (GUI shell)
// The real work is done by PCGuardian.ps1 in the same folder;
// this app just runs it and shows the output in a friendly window.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PCGuardian
{
    public class MainForm : Form
    {
        static readonly Color BG     = Color.FromArgb(24, 26, 34);
        static readonly Color PANEL  = Color.FromArgb(33, 36, 48);
        static readonly Color CONSBG = Color.FromArgb(16, 18, 24);
        static readonly Color ACCENT = Color.FromArgb(0, 210, 160);
        static readonly Color TEXT   = Color.FromArgb(222, 226, 235);
        static readonly Color MUTED  = Color.FromArgb(150, 156, 172);
        static readonly Color OKCOL  = Color.FromArgb(90, 220, 130);
        static readonly Color WARN   = Color.FromArgb(255, 200, 80);
        static readonly Color HEAD   = Color.FromArgb(90, 200, 250);

        RichTextBox console;
        Label statusLabel;
        Panel statusDot;
        Button stopButton;
        Button[] taskButtons;
        Process current;
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
            ClientSize = new Size(S(1000), S(640));
            MinimumSize = new Size(S(860), S(540));
            BackColor = BG;
            ForeColor = TEXT;
            Font = new Font("Segoe UI", 9.75f);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            // ----- header -----
            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = S(74);
            header.BackColor = PANEL;

            Label title = new Label();
            title.Text = "🛡  PC GUARDIAN";
            title.Font = new Font("Segoe UI", 17f, FontStyle.Bold);
            title.ForeColor = ACCENT;
            title.AutoSize = true;
            title.Location = new Point(S(20), S(12));

            Label subtitle = new Label();
            subtitle.UseMnemonic = false;
            subtitle.Text = "Free scans, cleanup & safe driver updates — powered by Windows, built with Claude";
            subtitle.Font = new Font("Segoe UI", 9f);
            subtitle.ForeColor = MUTED;
            subtitle.AutoSize = true;
            subtitle.Location = new Point(S(24), S(47));

            header.Controls.Add(title);
            header.Controls.Add(subtitle);

            // ----- status bar -----
            Panel status = new Panel();
            status.Dock = DockStyle.Bottom;
            status.Height = S(34);
            status.BackColor = PANEL;

            statusDot = new Panel();
            statusDot.Size = new Size(S(10), S(10));
            statusDot.Location = new Point(S(16), S(12));
            statusDot.BackColor = OKCOL;

            statusLabel = new Label();
            statusLabel.Text = "Ready.";
            statusLabel.ForeColor = MUTED;
            statusLabel.AutoSize = true;
            statusLabel.Location = new Point(S(34), S(8));

            status.Controls.Add(statusDot);
            status.Controls.Add(statusLabel);

            // ----- sidebar -----
            Panel sidebar = new Panel();
            sidebar.Dock = DockStyle.Left;
            sidebar.Width = S(240);
            sidebar.BackColor = PANEL;
            sidebar.Padding = new Padding(S(12));

            string[,] tasks = new string[,] {
                { "Quick Scan",          "quick",         "Common malware spots  ·  ~5 min" },
                { "Full Scan",           "full",          "Every file on disk  ·  30–90 min" },
                { "Deep Audit",          "audit",         "Keyloggers · botware · hijacks" },
                { "Clean Junk",          "clean",         "Temp files only, never documents" },
                { "Check Drivers",       "drivers",       "See what Windows Update offers" },
                { "Install Drivers",     "driverinstall", "Restore point first, then install" },
                { "Update Definitions",  "definitions",   "Refresh Defender's virus list" },
                { "Run Everything",      "all",           "Scan + audit + clean + drivers" }
            };

            int count = tasks.GetLength(0);
            taskButtons = new Button[count];
            // build bottom-up because DockStyle.Top stacks in reverse order
            stopButton = MakeButton("■  Stop current task", "", Color.FromArgb(60, 42, 48));
            stopButton.Dock = DockStyle.Bottom;
            stopButton.Enabled = false;
            stopButton.Click += delegate { StopTask(); };
            sidebar.Controls.Add(stopButton);

            for (int i = count - 1; i >= 0; i--)
            {
                string label = tasks[i, 0], task = tasks[i, 1], hint = tasks[i, 2];
                Button b = MakeButton(label, hint, PANEL);
                b.Click += delegate { RunTask(task, label); };
                taskButtons[i] = b;
                sidebar.Controls.Add(b);
            }

            // ----- console -----
            console = new RichTextBox();
            console.Dock = DockStyle.Fill;
            console.ReadOnly = true;
            console.BackColor = CONSBG;
            console.ForeColor = TEXT;
            console.BorderStyle = BorderStyle.None;
            console.Font = new Font("Consolas", 9.75f);
            console.DetectUrls = false;

            Panel consoleWrap = new Panel();
            consoleWrap.Dock = DockStyle.Fill;
            consoleWrap.Padding = new Padding(S(14));
            consoleWrap.BackColor = CONSBG;
            consoleWrap.Controls.Add(console);

            Controls.Add(consoleWrap);
            Controls.Add(sidebar);
            Controls.Add(status);
            Controls.Add(header);

            AppendLine("Welcome to PC Guardian.", MUTED);
            AppendLine("Pick a task on the left. Results appear here.", MUTED);
            AppendLine("Rule of thumb for the Deep Audit: every line should be software", MUTED);
            AppendLine("you recognize. Anything unfamiliar? Ask Claude before deleting.", MUTED);

            FormClosing += delegate { try { if (current != null && !current.HasExited) current.Kill(); } catch { } };

            if (!File.Exists(scriptPath))
                AppendLine("\r\nWARNING: PCGuardian.ps1 not found next to the app - keep them in the same folder.", WARN);
        }

        Button MakeButton(string label, string hint, Color back)
        {
            Button b = new Button();
            b.Text = hint.Length > 0 ? label + "\r\n" + hint : label;
            b.TextAlign = ContentAlignment.MiddleLeft;
            b.Dock = DockStyle.Top;
            b.Height = hint.Length > 0 ? S(52) : S(44);
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(45, 50, 66);
            b.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 90, 70);
            b.BackColor = back;
            b.ForeColor = TEXT;
            b.Font = new Font("Segoe UI", 9f);
            b.Margin = new Padding(0, 0, 0, 6);
            b.Cursor = Cursors.Hand;
            return b;
        }

        void RunTask(string task, string label)
        {
            if (current != null && !current.HasExited) return;

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

            AppendLine("", TEXT);
            SetBusy(true, label);

            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "powershell.exe";
            psi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File \"" + scriptPath + "\" -Task " + task + " -AutoYes";
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            current = new Process();
            current.StartInfo = psi;
            current.EnableRaisingEvents = true;
            current.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data != null) BeginInvoke((Action)delegate { AppendColored(e.Data); });
            };
            current.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data != null && e.Data.Trim().Length > 0)
                    BeginInvoke((Action)delegate { AppendLine(e.Data, WARN); });
            };
            current.Exited += delegate
            {
                BeginInvoke((Action)delegate
                {
                    AppendLine("─── " + label + " finished ───", ACCENT);
                    SetBusy(false, null);
                });
            };

            try
            {
                current.Start();
                current.BeginOutputReadLine();
                current.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                AppendLine("Could not start task: " + ex.Message, WARN);
                SetBusy(false, null);
            }
        }

        void StopTask()
        {
            try { if (current != null && !current.HasExited) current.Kill(); } catch { }
            AppendLine("Task stopped by user.", WARN);
            SetBusy(false, null);
        }

        void SetBusy(bool busy, string label)
        {
            foreach (Button b in taskButtons) b.Enabled = !busy;
            stopButton.Enabled = busy;
            statusDot.BackColor = busy ? WARN : OKCOL;
            statusLabel.Text = busy ? "Running: " + label + "…" : "Ready.";
        }

        void AppendColored(string line)
        {
            string t = line.TrimStart();
            if (t.StartsWith("====") || (t.StartsWith("==") && t.EndsWith("==")))      AppendLine(line, HEAD);
            else if (t.StartsWith("---"))                                              AppendLine(line, Color.White);
            else if (line.Contains("[OK]"))                                            AppendLine(line, OKCOL);
            else if (line.Contains("[CHECK]") || line.Contains("WARNING"))             AppendLine(line, WARN);
            else if (t.Length > 1 && t == new string('=', t.Length))                   AppendLine(line, HEAD);
            else                                                                       AppendLine(line, TEXT);
        }

        void AppendLine(string line, Color color)
        {
            console.SelectionStart = console.TextLength;
            console.SelectionColor = color;
            console.AppendText(line + "\r\n");
            console.SelectionStart = console.TextLength;
            console.ScrollToCaret();
        }
    }
}
