using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using Microsoft.Win32;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MROSDShield
{
    class MainForm : Form
    {
        Engine _e;
        NotifyIcon _tray;
        System.Windows.Forms.Timer _ui;
        bool _minToTray = Pref.MinToTray, _exiting, _startMinimized;
        DateTime _lastTrim = DateTime.MinValue;
        int _page, _trayState = -1;
        Icon _icoGreen, _icoBlue, _icoRed;

        Panel _content;
        Label _navHome, _navStat, _navSet, _heroTitle, _heroSub, _statusChip;
        GlowCard _hero;
        Label _svcVal, _gcuVal, _gcuuVal, _sesVal, _totVal, _upVal, _waitVal, _adminVal, _profileVal;
        Label _statKills, _statResets, _statUp, _statAvg;

        public MainForm(Engine e, bool startMinimized)
        {
            _e = e;
            _startMinimized = startMinimized;
            Text = L.TV;
            Size = new Size(980, 700);
            MinimumSize = MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Co.BG;
            DoubleBuffered = true;
            ShowInTaskbar = true;
            try
            {
                _icoGreen = MkIco(Co.Green);
                _icoBlue = MkIco(Co.Blue);
                _icoRed = MkIco(Co.Red);
                Icon = _icoGreen;
            }
            catch { }

            BuildShell();
            SetupTray();

            _ui = new System.Windows.Forms.Timer { Interval = 1000 };
            _ui.Tick += (s, ev) => RefreshUI();
            _ui.Start();

            Shown += (s, ev) =>
            {
                if (_startMinimized)
                {
                    BeginInvoke(new Action(() =>
                    {
                        ToTray(false);
                        Program.TrimMemory();
                    }));
                    return;
                }

                Show();
                WindowState = FormWindowState.Normal;
                Activate();
                BringToFront();
                TopMost = true;
                TopMost = false;
            };

            ShowPage(0);
            RefreshUI();
        }

        const int WM_NCHITTEST = 0x84;
        const int HT_CAPTION = 0x2;
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m);
                if ((int)m.Result == 1) m.Result = (IntPtr)HT_CAPTION;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var b = new SolidBrush(Co.BG))
                e.Graphics.FillRectangle(b, ClientRectangle);

            using (var b = new SolidBrush(Color.FromArgb(220, 10, 15, 26)))
                e.Graphics.FillRectangle(b, 0, 0, Width, 48);
            using (var pen = new Pen(Color.FromArgb(48, 72, 96)))
                e.Graphics.DrawLine(pen, 0, 48, Width, 48);

            using (var b = new SolidBrush(Color.FromArgb(210, Co.Side)))
                e.Graphics.FillRectangle(b, 0, 48, 72, Height - 48);
            using (var pen = new Pen(Color.FromArgb(50, 68, 92)))
                e.Graphics.DrawLine(pen, 72, 48, 72, Height);
        }

        void BuildShell()
        {
            var topLogo = new Label { Text = "G", Font = new Font("Segoe UI", 12f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(28, 28), Location = new Point(24, 10) };
            Controls.Add(topLogo);
            Controls.Add(new Label { Text = L.TV, Font = new Font("Segoe UI", 12.5f), ForeColor = Co.Txt, BackColor = Color.Transparent, AutoSize = true, Location = new Point(70, 13) });
            _statusChip = new Label { Text = L.TD, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Co.Blue, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, AutoEllipsis = true, Size = new Size(210, 26), Location = new Point(610, 11) };
            _statusChip.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Color c = _trayState == 2 ? Co.Red : _trayState == 1 ? Co.Green : Co.Blue;
                using (var p = GlowCard.Round(new Rectangle(0, 0, _statusChip.Width - 1, _statusChip.Height - 1), 12))
                using (var b = new SolidBrush(Color.FromArgb(30, c)))
                using (var pen = new Pen(Color.FromArgb(90, c)))
                {
                    e.Graphics.FillPath(b, p);
                    e.Graphics.DrawPath(pen, p);
                }
            };
            Controls.Add(_statusChip);

            var minTop = SmallButton("—", 7, Co.Dim); minTop.Location = new Point(840, 7); minTop.Click += (s, e) => ToTray();
            var closeTop = SmallButton("×", 7, Co.Red); closeTop.Location = new Point(918, 7); closeTop.Click += (s, e) => CloseOrTray();

            var logo = new Label { Text = "G", Font = new Font("Segoe UI", 20f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(58, 58), Location = new Point(7, 58) };
            logo.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = GlowCard.Round(new Rectangle(4, 4, 50, 50), 12))
                using (var b = new LinearGradientBrush(new Rectangle(4, 4, 50, 50), Color.FromArgb(70, Co.Green), Color.FromArgb(22, Co.Green), 90f))
                    e.Graphics.FillPath(b, p);
            };
            Controls.Add(logo);

            _navHome = Nav("⌂", 136, 0);
            _navStat = Nav("▥", 190, 1);
            _navSet = Nav("⚙", 244, 2);

            Controls.Add(new Label { Text = "Sakura", Font = new Font("Segoe UI", 8.5f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(72, 20), Location = new Point(0, 568) });
            var lang = new Label { Text = L.Lang, Font = new Font("Segoe UI", 8f, FontStyle.Bold), ForeColor = Co.Blue, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(72, 24), Location = new Point(0, 612), Cursor = Cursors.Hand };
            lang.Click += (s, e) => { L.Toggle(); RebuildShell(); };
            Controls.Add(lang);

            _content = new Panel { BackColor = Co.BG, Location = new Point(110, 72), Size = new Size(820, 590) };
            _content.Paint += (s, e) => { };
            Controls.Add(_content);
        }

        Label Nav(string text, int y, int page)
        {
            var l = new Label { Text = text, Font = new Font("Segoe UI", 18f, FontStyle.Bold), ForeColor = Co.Dim, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(54, 46), Location = new Point(9, y), Cursor = Cursors.Hand };
            l.Paint += (s, e) =>
            {
                if (_page == page)
                {
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var p = GlowCard.Round(new Rectangle(0, 0, l.Width - 1, l.Height - 1), 12))
                    using (var b = new SolidBrush(Color.FromArgb(34, Co.Green)))
                        e.Graphics.FillPath(b, p);
                    using (var b = new SolidBrush(Co.Green)) e.Graphics.FillRectangle(b, 0, 10, 3, l.Height - 20);
                }
            };
            l.Click += (s, e) => ShowPage(page);
            Controls.Add(l);
            return l;
        }

        Label SmallButton(string text, int y, Color color)
        {
            var l = new Label { Text = text, Font = new Font("Segoe UI", 14f, FontStyle.Bold), ForeColor = color, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(42, 34), Location = new Point(76, y), Cursor = Cursors.Hand };
            l.Paint += (s, e) => { e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; using (var p = GlowCard.Round(new Rectangle(1, 1, l.Width - 2, l.Height - 2), 14)) using (var pen = new Pen(Color.FromArgb(55, color))) e.Graphics.DrawPath(pen, p); };
            Controls.Add(l);
            return l;
        }

        void RebuildShell()
        {
            Controls.Clear();
            BuildShell();
            ShowPage(_page);
        }

        void ShowPage(int p)
        {
            _page = p;
            _navHome.ForeColor = p == 0 ? Co.Txt : Co.Dim;
            _navStat.ForeColor = p == 1 ? Co.Txt : Co.Dim;
            _navSet.ForeColor = p == 2 ? Co.Txt : Co.Dim;
            _navHome.Invalidate(); _navStat.Invalidate(); _navSet.Invalidate();

            _content.Controls.Clear();
            if (p == 0) BuildHome();
            else if (p == 1) BuildStats();
            else BuildSettings();
            RefreshUI();
        }

        void BuildHome()
        {
            _content.Controls.Add(new Label { Text = L.T, Font = new Font("Segoe UI", 21f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Co.BG, AutoSize = true, Location = new Point(0, 0) });
            _content.Controls.Add(new Label { Text = L.Sub, Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Co.Dim, BackColor = Co.BG, AutoEllipsis = true, Size = new Size(560, 24), Location = new Point(2, 44) });

            _hero = new GlowCard { Title = "", Accent = Co.Green, Size = new Size(820, 92), Location = new Point(0, 84) };
            _content.Controls.Add(_hero);
            _heroTitle = new Label { Text = L.Det, Font = new Font("Microsoft YaHei UI", 15f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Color.Transparent, AutoEllipsis = true, Size = new Size(700, 32), Location = new Point(68, 18), Parent = _hero };
            _heroSub = new Label { Text = L.WaitSub, Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Co.Dim, BackColor = Color.Transparent, AutoEllipsis = true, Size = new Size(700, 24), Location = new Point(70, 54), Parent = _hero };
            _hero.Controls.Add(new Label { Text = "S", Font = new Font("Segoe UI", 17f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(42, 42), Location = new Point(18, 25) });

            var core = new GlowCard { Title = L.Core, Accent = Co.Blue, Size = new Size(820, 182), Location = new Point(0, 194) };
            _content.Controls.Add(core);
            _svcVal = Row(core, L.Svc, 48);
            _gcuVal = Row(core, "GCUService.exe", 84);
            _gcuuVal = Row(core, "GCUUtil.exe", 118);
            _adminVal = Row(core, L.Admin, 152);
            if (!Program.IsAdmin())
            {
                var adminBtn = Pill(L.RestartAdmin, new Point(620, 344), new Size(200, 36), Co.Red);
                adminBtn.Click += (s, e) => Program.RestartAsAdmin(!Visible || WindowState == FormWindowState.Minimized);
                _content.Controls.Add(adminBtn);
            }

            var stats = new GlowCard { Title = L.Stat, Accent = Co.Purple, Size = new Size(820, 102), Location = new Point(0, 392) };
            _content.Controls.Add(stats);
            _sesVal = Metric(stats, L.Ses, 54, 40);
            _totVal = Metric(stats, L.Tot, 286, 40);
            _upVal = Metric(stats, L.Up, 540, 40);

            var quick = new GlowCard { Title = L.Quick, Accent = Co.Amber, Size = new Size(600, 88), Location = new Point(0, 498) };
            _content.Controls.Add(quick);
            SettingRow(quick, L.AS, 34, AS.On(), (v) => { if (v) AS.Enable(); else AS.Disable(); });
            SettingRow(quick, L.MT, 66, _minToTray, (v) => { _minToTray = v; Pref.MinToTray = v; });

            var btn = Pill(L.Btn, new Point(620, 530), new Size(200, 44), Co.Blue);
            btn.Click += (s, e) => ToTray();
            _content.Controls.Add(btn);
        }

        Label Row(Control parent, string name, int y)
        {
            parent.Controls.Add(new Label { Text = name, Font = new Font("Segoe UI", 9f), ForeColor = Co.Dim, BackColor = Color.Transparent, AutoSize = true, Location = new Point(20, y) });
            var v = new Label { Text = "--", Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleRight, AutoEllipsis = true, Size = new Size(190, 24), Location = new Point(parent.Width - 220, y - 2) };
            parent.Controls.Add(v);
            return v;
        }

        Label Metric(Control parent, string title, int x, int y)
        {
            parent.Controls.Add(new Label { Text = title, Font = new Font("Segoe UI", 8f), ForeColor = Co.Dim, BackColor = Color.Transparent, AutoSize = true, Location = new Point(x, y) });
            var v = new Label { Text = "0", Font = new Font("Consolas", 17f, FontStyle.Bold), ForeColor = Co.Green, BackColor = Color.Transparent, AutoSize = true, Location = new Point(x, y + 22) };
            parent.Controls.Add(v);
            return v;
        }

        void BuildStats()
        {
            Header(L.Stat, L.Sub);
            _statKills = InfoCard(L.Kills, 0, 76, Co.Green);
            _statResets = InfoCard(L.Resets, 282, 76, Co.Blue);
            _statUp = InfoCard(L.Up, 0, 218, Co.Purple);
            _statAvg = InfoCard(L.Avg, 282, 218, Co.Amber);
        }

        void BuildSettings()
        {
            Header(L.Set, L.Sub);
            PathCard(L.AB, _e.ABPath, 76, Co.Green);
            var abChoose = Pill(L.Choose, new Point(570, 92), new Size(120, 32), Co.Green);
            abChoose.Click += (s, e) => ChooseAfterburnerPath();
            _content.Controls.Add(abChoose);
            ProfileRow(new Point(570, 128));
            PathCard(L.CCP, _e.CCPath, 158, Co.Blue);
            var ccChoose = Pill(L.Choose, new Point(570, 174), new Size(120, 32), Co.Blue);
            ccChoose.Click += (s, e) => ChooseControlCenterPath();
            _content.Controls.Add(ccChoose);
            var c = new GlowCard { Title = L.Quick, Accent = Co.Purple, Size = new Size(548, 246), Location = new Point(0, 222) };
            _content.Controls.Add(c);
            SettingRow(c, L.AS, 42, AS.On(), (v) => { if (v) AS.Enable(); else AS.Disable(); });
            SettingRow(c, L.BM, 78, Pref.BootMin, (v) => { Pref.BootMin = v; if (AS.On()) AS.Enable(); });
            WaitRow(c, 114);
            SettingRow(c, L.MT, 154, _minToTray, (v) => { _minToTray = v; Pref.MinToTray = v; });
            SettingRow(c, L.KillProc, 186, Pref.KillGpuProcesses, (v) => { Pref.KillGpuProcesses = v; });
            c.Controls.Add(new Label { Text = L.KillProcHint, Font = new Font("Microsoft YaHei UI", 7.5f), ForeColor = Co.Dim, BackColor = Color.Transparent, AutoEllipsis = true, Size = new Size(c.Width - 44, 18), Location = new Point(22, 216) });
            PathCard(L.Log, Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "logs", "mr_osd_shield.log"), 482, Co.Amber);
            var b1 = Pill(L.OpenLogDir, new Point(570, 482), new Size(120, 32), Co.Amber);
            b1.Click += (s, e) => OpenFolder(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "logs"));
            _content.Controls.Add(b1);
            var b2 = Pill(L.OpenAppDir, new Point(700, 482), new Size(120, 32), Co.Blue);
            b2.Click += (s, e) => OpenFolder(Path.GetDirectoryName(Application.ExecutablePath));
            _content.Controls.Add(b2);
            var b3 = Pill(L.ApplyAB, new Point(570, 522), new Size(120, 32), Co.Green);
            b3.Click += (s, e) => _e.ApplyAfterburnerProfile();
            _content.Controls.Add(b3);
            var b4 = Pill(L.RepairNow, new Point(700, 522), new Size(120, 32), Co.Red);
            b4.Click += (s, e) => _e.RepairNow();
            _content.Controls.Add(b4);
        }

        void Header(string title, string sub)
        {
            _content.Controls.Add(new Label { Text = title, Font = new Font("Microsoft YaHei UI", 20f, FontStyle.Bold), ForeColor = Co.Txt, BackColor = Co.BG, AutoEllipsis = true, Size = new Size(520, 40), Location = new Point(0, 0) });
            _content.Controls.Add(new Label { Text = sub, Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Co.Dim, BackColor = Co.BG, AutoEllipsis = true, Size = new Size(620, 24), Location = new Point(4, 44) });
        }

        Label InfoCard(string title, int x, int y, Color accent)
        {
            var c = new GlowCard { Title = title, Accent = accent, Size = new Size(266, 118), Location = new Point(x, y) };
            _content.Controls.Add(c);
            var v = new Label { Text = "0", Font = new Font("Consolas", 22f, FontStyle.Bold), ForeColor = accent, BackColor = Color.Transparent, AutoSize = true, Location = new Point(22, 54), Parent = c };
            return v;
        }

        void PathCard(string title, string path, int y, Color accent)
        {
            var c = new GlowCard { Title = title, Accent = accent, Size = new Size(548, 66), Location = new Point(0, y) };
            _content.Controls.Add(c);
            c.Controls.Add(new Label { Text = path.Length > 0 ? path : L.NF, Font = new Font("Consolas", 8.5f), ForeColor = path.Length > 0 ? Co.Txt : Co.Red, BackColor = Color.Transparent, AutoEllipsis = true, Size = new Size(500, 22), Location = new Point(22, 34) });
        }

        void OpenFolder(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                Process.Start("explorer.exe", "\"" + path + "\"");
            }
            catch (Exception ex) { Log.Error("Open folder failed: " + path, ex); }
        }

        void ProfileRow(Point loc)
        {
            _content.Controls.Add(new Label { Text = L.ABProfile, Font = new Font("Microsoft YaHei UI", 8.5f), ForeColor = Co.Dim, BackColor = Co.BG, AutoEllipsis = true, Size = new Size(120, 20), Location = loc });
            var minus = MiniButton("-", new Point(loc.X + 126, loc.Y - 2));
            minus.Click += (s, e) => { Pref.AfterburnerProfile = Pref.AfterburnerProfile - 1; UpdateProfileText(); };
            _content.Controls.Add(minus);
            _profileVal = new Label { Text = "", Font = new Font("Consolas", 10f, FontStyle.Bold), ForeColor = Co.Green, BackColor = Co.BG, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(34, 24), Location = new Point(loc.X + 158, loc.Y - 2) };
            _content.Controls.Add(_profileVal);
            var plus = MiniButton("+", new Point(loc.X + 194, loc.Y - 2));
            plus.Click += (s, e) => { Pref.AfterburnerProfile = Pref.AfterburnerProfile + 1; UpdateProfileText(); };
            _content.Controls.Add(plus);
            UpdateProfileText();
        }

        void UpdateProfileText()
        {
            if (_profileVal != null) _profileVal.Text = Pref.AfterburnerProfile.ToString();
        }

        void ChooseAfterburnerPath()
        {
            try
            {
                using (var d = new OpenFileDialog())
                {
                    d.Title = L.AB;
                    d.Filter = "MSIAfterburner.exe|MSIAfterburner.exe|Executable files (*.exe)|*.exe|All files (*.*)|*.*";
                    d.FileName = "MSIAfterburner.exe";
                    if (_e.ABPath.Length > 0 && File.Exists(_e.ABPath)) d.InitialDirectory = Path.GetDirectoryName(_e.ABPath);
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        _e.SetAfterburnerPath(d.FileName);
                        ShowPage(2);
                    }
                }
            }
            catch (Exception ex) { Log.Error("Choose MSI Afterburner path failed.", ex); }
        }

        void ChooseControlCenterPath()
        {
            try
            {
                using (var d = new FolderBrowserDialog())
                {
                    d.Description = L.CCP;
                    if (_e.CCPath.Length > 0 && Directory.Exists(_e.CCPath)) d.SelectedPath = _e.CCPath;
                    if (d.ShowDialog(this) == DialogResult.OK)
                    {
                        _e.SetControlCenterPath(d.SelectedPath);
                        ShowPage(2);
                    }
                }
            }
            catch (Exception ex) { Log.Error("Choose control center path failed.", ex); }
        }

        void SettingRow(Control parent, string text, int y, bool on, Action<bool> changed)
        {
            parent.Controls.Add(new Label { Text = text, Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Co.Txt, BackColor = Color.Transparent, AutoEllipsis = true, Size = new Size(parent.Width - 120, 24), Location = new Point(22, y) });
            var t = new ToggleSwitch(on) { Location = new Point(parent.Width - 66, y) };
            t.Clicked += changed;
            parent.Controls.Add(t);
        }

        void WaitRow(Control parent, int y)
        {
            parent.Controls.Add(new Label { Text = L.WT, Font = new Font("Microsoft YaHei UI", 9f), ForeColor = Co.Txt, BackColor = Color.Transparent, AutoEllipsis = true, Size = new Size(parent.Width - 190, 24), Location = new Point(22, y) });
            var minus = MiniButton("-", new Point(parent.Width - 154, y - 1));
            minus.Click += (s, e) => { Pref.StableSeconds = Pref.StableSeconds - 1; UpdateWaitText(); };
            parent.Controls.Add(minus);
            _waitVal = new Label { Text = "", Font = new Font("Consolas", 10f, FontStyle.Bold), ForeColor = Co.Green, BackColor = Color.Transparent, TextAlign = ContentAlignment.MiddleCenter, Size = new Size(56, 24), Location = new Point(parent.Width - 118, y) };
            parent.Controls.Add(_waitVal);
            var plus = MiniButton("+", new Point(parent.Width - 56, y - 1));
            plus.Click += (s, e) => { Pref.StableSeconds = Pref.StableSeconds + 1; UpdateWaitText(); };
            parent.Controls.Add(plus);
            UpdateWaitText();
        }

        void UpdateWaitText()
        {
            if (_waitVal != null) _waitVal.Text = Pref.StableSeconds + L.Sec;
        }

        Label MiniButton(string text, Point loc)
        {
            var l = new Label { Text = text, Font = new Font("Segoe UI", 10f, FontStyle.Bold), ForeColor = Co.Txt, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, Location = loc, Size = new Size(28, 24), Cursor = Cursors.Hand };
            l.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = GlowCard.Round(new Rectangle(0, 0, l.Width - 1, l.Height - 1), 8))
                using (var b = new SolidBrush(Color.FromArgb(34, Co.Blue)))
                using (var pen = new Pen(Color.FromArgb(105, Co.Blue)))
                {
                    e.Graphics.FillPath(b, p);
                    e.Graphics.DrawPath(pen, p);
                }
            };
            return l;
        }

        Label Pill(string text, Point loc, Size size, Color accent)
        {
            var l = new Label { Text = text, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), ForeColor = Co.Txt, TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent, Location = loc, Size = size, Cursor = Cursors.Hand };
            l.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = GlowCard.Round(new Rectangle(0, 0, l.Width - 1, l.Height - 1), 18))
                using (var b = new SolidBrush(Color.FromArgb(76, accent)))
                using (var pen = new Pen(Color.FromArgb(135, accent)))
                {
                    e.Graphics.FillPath(b, p);
                    e.Graphics.DrawPath(pen, p);
                }
            };
            return l;
        }

        void SetupTray()
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add(L.Open, null, (s, e) => RestoreFromTray());
            menu.Items.Add("-");
            menu.Items.Add(L.Exit, null, (s, e) => ExitApp());
            _tray = new NotifyIcon { Icon = _icoGreen, Text = L.TP, ContextMenuStrip = menu, Visible = true };
            _tray.DoubleClick += (s, e) => RestoreFromTray();
        }

        void RefreshUI()
        {
            if (_exiting) return;
            try
            {
                var st = _e.GetStatus();

                if (!Visible && _ui.Interval != 5000) _ui.Interval = 5000;
                else if (Visible && _ui.Interval != 1000) _ui.Interval = 1000;
                if (!Visible)
                {
                    RefreshTray(st);
                    MaybeTrim();
                    return;
                }
                if (_page == 0)
                {
                    bool active = _e.Active;
                    bool warn = active && !st.AllOK;
                    if (_hero != null) _hero.Accent = warn ? Co.Red : active ? Co.Green : Co.Blue;
                    if (_heroTitle != null) _heroTitle.Text = active ? (warn ? L.Warn : L.Act) : (_e.StableRem > 0 ? L.Det + " · " + _e.StableRem + "s" : L.Det);
                    if (_heroSub != null) _heroSub.Text = active ? (warn ? L.WarnSub : L.ActSub) : L.WaitSub;

                    if (_svcVal != null) { _svcVal.Text = st.SvcFound ? (st.SvcRunning ? L.Run : L.Stp) : L.NF; _svcVal.ForeColor = st.SvcRunning ? Co.Green : Co.Red; }
                    SetProc(_gcuVal, st, "GCUService.exe");
                    SetProc(_gcuuVal, st, "GCUUtil.exe");
                    if (_adminVal != null) { _adminVal.Text = Program.IsAdmin() ? L.AdminOK : L.AdminNO; _adminVal.ForeColor = Program.IsAdmin() ? Co.Green : Co.Amber; }
                    if (_sesVal != null) _sesVal.Text = st.FileResets.ToString();
                    if (_totVal != null) _totVal.Text = st.Total.ToString();
                    if (_upVal != null) _upVal.Text = TimeText(st.Uptime);
                }
                if (_statKills != null)
                {
                    _statKills.Text = st.FileResets.ToString();
                    _statResets.Text = st.Total.ToString();
                    _statUp.Text = TimeText(st.Uptime);
                    double hrs = st.Uptime.TotalHours;
                    _statAvg.Text = hrs > 0 ? (st.FileResets / hrs).ToString("F1") : "0.0";
                }

                MaybeTrim();
                RefreshTray(st);
            }
            catch { }
        }

        void RefreshTray(StatusInfo st)
        {
            if (_tray == null) return;
            int ns = !_e.Active ? 0 : st.AllOK ? 1 : 2;
            string txt = ns == 0 ? L.TD : ns == 1 ? L.TP : L.TW;
            if (_trayState != ns)
            {
                _trayState = ns;
                _tray.Icon = ns == 0 ? _icoBlue : ns == 1 ? _icoGreen : _icoRed;
                if (_statusChip != null)
                {
                    _statusChip.Text = txt;
                    _statusChip.ForeColor = ns == 0 ? Co.Blue : ns == 1 ? Co.Green : Co.Red;
                    _statusChip.Invalidate();
                }
            }
            _tray.Text = txt;
        }

        void MaybeTrim()
        {
            if ((DateTime.Now - _lastTrim).TotalSeconds >= 180)
            {
                _lastTrim = DateTime.Now;
                if (!Visible || WindowState == FormWindowState.Minimized) Program.TrimMemory();
            }
        }

        void SetProc(Label l, StatusInfo st, string p)
        {
            if (l == null) return;
            bool r = st.Procs.ContainsKey(p) && st.Procs[p];
            if (!Pref.KillGpuProcesses)
            {
                l.Text = r ? L.Run : L.Allow;
                l.ForeColor = r ? Co.Green : Co.Dim;
                return;
            }
            l.Text = r ? L.Run : L.Blk;
            l.ForeColor = r ? Co.Red : Co.Green;
        }

        string TimeText(TimeSpan t) { return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds); }

        void ToTray() { ToTray(true); }

        void ToTray(bool tip)
        {
            if (_exiting) return;
            Hide();
            WindowState = FormWindowState.Normal;
            ShowInTaskbar = false;
            if (tip && _tray != null) _tray.ShowBalloonTip(1600, L.T, L.TT, ToolTipIcon.Info);
            _lastTrim = DateTime.MinValue;
            Program.TrimMemory();
        }

        public void ActivateFromExternalInstance()
        {
            if (_exiting) return;
            RestoreFromTray();
            BringToFront();
            TopMost = true;
            TopMost = false;
        }

        void RestoreFromTray()
        {
            if (_exiting) return;
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        void CloseOrTray()
        {
            if (_minToTray) ToTray();
            else ExitApp();
        }

        void ExitApp()
        {
            _exiting = true;
            if (_ui != null) _ui.Stop();
            if (_tray != null) _tray.Visible = false;
            _e.Stop();
            Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting && _minToTray)
            {
                e.Cancel = true;
                ToTray();
                return;
            }
            _exiting = true;
            if (_ui != null) _ui.Stop();
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; }
            if (_icoGreen != null) { _icoGreen.Dispose(); _icoGreen = null; }
            if (_icoBlue != null) { _icoBlue.Dispose(); _icoBlue = null; }
            if (_icoRed != null) { _icoRed.Dispose(); _icoRed = null; }
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e) { if (!_exiting && WindowState == FormWindowState.Minimized) ToTray(); }

        Icon MkIco(Color c)
        {
            using (var bmp = new Bitmap(32, 32))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);
                    using (var p = GlowCard.Round(new Rectangle(3, 3, 26, 26), 7))
                    using (var b = new SolidBrush(Color.FromArgb(18, 26, 42)))
                    using (var pen = new Pen(c, 3))
                    {
                        g.FillPath(b, p);
                        g.DrawPath(pen, p);
                    }
                    using (var b = new SolidBrush(c))
                        g.FillRectangle(b, 9, 23, 14, 3);
                }
                return Program.CreateOwnedIcon(bmp);
            }
        }
    }

}
