using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace MROSDShield
{
    class MainForm : Form
    {
        readonly Engine _e;
        readonly bool _startMinimized;
        readonly WebView2 _web;
        NotifyIcon _tray;
        readonly System.Windows.Forms.Timer _ui;
        bool _minToTray = Pref.MinToTray;
        bool _exiting;
        bool _webReady;
        string _lang = "zh";

        public MainForm(Engine e, bool startMinimized)
        {
            _e = e;
            _startMinimized = startMinimized;

            Text = L.TV;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new System.Drawing.Size(980, 700);
            Size = new System.Drawing.Size(1200, 800);
            BackColor = System.Drawing.Color.FromArgb(7, 10, 18);
            ShowInTaskbar = true;
            DoubleBuffered = true;
            MaximizeBox = true;
            MinimizeBox = true;

            _web = new WebView2
            {
                Dock = DockStyle.Fill,
                DefaultBackgroundColor = System.Drawing.Color.FromArgb(7, 10, 18)
            };
            Controls.Add(_web);

            _web.CoreWebView2InitializationCompleted += OnWebInitCompleted;
            _web.NavigationCompleted += OnWebNavigationCompleted;
            InitializeWebViewAsync();

            var menu = new ContextMenuStrip();
            menu.Items.Add(L.Open, null, (s, ev) => RestoreFromTray());
            menu.Items.Add("-");
            menu.Items.Add(L.Exit, null, (s, ev) => ExitApp());

            var appIcon = LoadIcon("app.ico");
            var trayIcon = LoadIcon("tray.ico", appIcon);

            Icon = trayIcon;

            _tray = new NotifyIcon
            {
                Icon = trayIcon,
                Text = L.TP,
                ContextMenuStrip = menu,
                Visible = true
            };
            _tray.DoubleClick += (s, ev) => RestoreFromTray();

            _ui = new System.Windows.Forms.Timer { Interval = 1000 };
            _ui.Tick += (s, ev) =>
            {
                if (_exiting) return;
                RefreshWebState();
            };
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

                Activate();
                BringToFront();
            };
        }

        System.Drawing.Icon LoadIcon(string fileName, System.Drawing.Icon fallback = null)
        {
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
                if (File.Exists(iconPath))
                {
                    return new System.Drawing.Icon(iconPath);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Load icon failed: " + fileName, ex);
            }

            return fallback ?? System.Drawing.SystemIcons.Application;
        }

        async void InitializeWebViewAsync()
        {
            try
            {
                await _web.EnsureCoreWebView2Async();
            }
            catch (Exception ex)
            {
                Log.Error("Initialize WebView2 failed.", ex);
                MessageBox.Show(ex.ToString(), "MR OSD Shield", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OnWebInitCompleted(object sender, CoreWebView2InitializationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _web.CoreWebView2 == null)
            {
                Log.Error("WebView2 init failed.", e.InitializationException);
                return;
            }

            _web.CoreWebView2.WebMessageReceived += OnWebMessageReceived;
            _web.CoreWebView2.DocumentTitleChanged += (s, ev) =>
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_web.CoreWebView2.DocumentTitle))
                        Text = _web.CoreWebView2.DocumentTitle;
                }
                catch { }
            };

            try
            {
                _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            }
            catch { }

            var index = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "frontend", "index.html");
            if (File.Exists(index))
            {
                _web.Source = new Uri(index);
            }
            else
            {
                Log.Error("frontend/index.html not found.", null);
                MessageBox.Show("frontend/index.html not found.", "MR OSD Shield", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        void OnWebNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            try
            {
                if (!e.IsSuccess) return;
                _webReady = true;
                SendState();
                SendNotify("frontend loaded");
            }
            catch (Exception ex)
            {
                Log.Error("Navigation completed handling failed.", ex);
            }
        }

        void OnWebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.WebMessageAsJson;
                using (var doc = JsonDocument.Parse(json))
                {
                    var root = doc.RootElement;
                    var type = root.TryGetProperty("type", out var t) ? t.GetString() : "";
                    switch (type)
                    {
                        case "ready":
                            _webReady = true;
                            SendState();
                            break;
                        case "getStatus":
                            SendState();
                            break;
                        case "pageChanged":
                            SendState();
                            break;
                        case "minimizeToTray":
                            ToTray();
                            break;
                        case "minimizeWindow":
                            WindowState = FormWindowState.Minimized;
                            break;
                        case "maximizeRestore":
                            ToggleMaximizeRestore();
                            break;
                        case "closeApp":
                            CloseOrTray();
                            break;
                        case "repairNow":
                            _e.RepairNow();
                            SendState();
                            break;
                        case "applyAfterburner":
                            _e.ApplyAfterburnerProfile();
                            SendState();
                            break;
                        case "chooseAfterburner":
                            ChooseAfterburnerPath();
                            SendState();
                            break;
                        case "chooseControlCenter":
                            ChooseControlCenterPath();
                            SendState();
                            break;
                        case "openLogDir":
                            OpenFolder(Path.Combine(Path.GetDirectoryName(Application.ExecutablePath), "logs"));
                            break;
                        case "openAppDir":
                            OpenFolder(Path.GetDirectoryName(Application.ExecutablePath));
                            break;
                        case "stepSetting":
                            HandleStepSetting(root);
                            SendState();
                            break;
                        case "toggleSetting":
                            HandleToggleSetting(root);
                            SendState();
                            break;
                        case "setPowerConfig":
                            HandleSetPowerConfig(root);
                            SendState();
                            break;
                        case "refreshPowerPlans":
                            _e.RefreshPowerPlans();
                            SendState();
                            break;
                        case "toggleLanguage":
                            HandleToggleLanguage(root);
                            SendState();
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Web message handling failed.", ex);
            }
        }

        void HandleToggleLanguage(JsonElement root)
        {
            try
            {
                var lang = root.TryGetProperty("lang", out var l) ? l.GetString() : "zh";
                _lang = string.IsNullOrWhiteSpace(lang) ? "zh" : lang;
            }
            catch { }
        }

        void HandleToggleSetting(JsonElement root)
        {
            try
            {
                var setting = root.TryGetProperty("setting", out var s) ? s.GetString() : "";
                var value = root.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.True;
                switch (setting)
                {
                    case "autoStart":
                        if (value) AS.Enable(); else AS.Disable();
                        break;
                    case "bootMin":
                        Pref.BootMin = value;
                        if (AS.On()) AS.Enable();
                        break;
                    case "minToTray":
                        _minToTray = value;
                        Pref.MinToTray = value;
                        break;
                    case "killGpuProcesses":
                        Pref.KillGpuProcesses = value;
                        break;
                    case "powerAutoSwitch":
                        Pref.PowerAutoSwitch = value;
                        break;
                    case "lockBestPerformanceMode":
                        Pref.LockBestPerformanceMode = value;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Toggle setting failed.", ex);
            }
        }

        void HandleSetPowerConfig(JsonElement root)
        {
            try
            {
                var targetProcess = root.TryGetProperty("targetProcess", out var tp) ? tp.GetString() : "";
                var foundGuid = root.TryGetProperty("whenFoundGuid", out var fg) ? fg.GetString() : "";
                var missingGuid = root.TryGetProperty("whenMissingGuid", out var mg) ? mg.GetString() : "";
                _e.SetPowerConfig(targetProcess, foundGuid, missingGuid);
            }
            catch (Exception ex)
            {
                Log.Error("Set power config message failed.", ex);
            }
        }

        void HandleStepSetting(JsonElement root)
        {
            try
            {
                var setting = root.TryGetProperty("setting", out var s) ? s.GetString() : "";
                var delta = root.TryGetProperty("delta", out var d) ? d.GetInt32() : 0;
                switch (setting)
                {
                    case "profile":
                        Pref.AfterburnerProfile = Pref.AfterburnerProfile + delta;
                        break;
                    case "stableSeconds":
                        Pref.StableSeconds = Pref.StableSeconds + delta;
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error("Step setting failed.", ex);
            }
        }

        void RefreshWebState()
        {
            if (!_webReady || _web.CoreWebView2 == null) return;

            var st = _e.GetStatus();
            var active = _e.Active;
            var warn = active && !st.AllOK;
            var payload = new
            {
                active,
                ready = _webReady,
                warn,
                svcFound = st.SvcFound,
                svcRunning = st.SvcRunning,
                gcuService = st.Procs.ContainsKey("GCUService.exe") && st.Procs["GCUService.exe"],
                gcuUtil = st.Procs.ContainsKey("GCUUtil.exe") && st.Procs["GCUUtil.exe"],
                admin = Program.IsAdmin(),
                processRows = st.ProcessRows.ConvertAll(p => new
                {
                    name = p.Name,
                    running = p.Running,
                    pid = p.Pid > 0 ? p.Pid.ToString() : "-",
                    cpuPercent = p.CpuPercent,
                    memoryBytes = p.MemoryBytes,
                    resourceText = p.ResourceText ?? "",
                    detail = p.Detail ?? ""
                }),
                cpuUsage = st.CpuUsage,
                memoryUsage = st.MemoryUsage,
                diskUsage = st.DiskUsage,
                diskText = st.DiskText,
                lastCheckTime = st.LastCheck == DateTime.MinValue ? "" : st.LastCheck.ToString("yyyy-MM-dd HH:mm:ss"),
                fileResets = st.FileResets,
                totalKills = st.Total,
                uptime = TimeText(st.Uptime),
                uptimeHours = st.Uptime.TotalHours,
                stableRem = _e.StableRem,
                bootMin = Pref.BootMin,
                minToTray = _minToTray,
                autoStart = AS.On(),
                killGpuProcesses = Pref.KillGpuProcesses,
                afterburnerProfile = Pref.AfterburnerProfile,
                stableSeconds = Pref.StableSeconds,
                afterburnerPath = _e.ABPath,
                controlCenterPath = _e.CCPath,
                powerAutoSwitch = st.PowerAutoSwitch,
                powerTargetProcess = st.PowerTargetProcess,
                powerPlanWhenFound = st.PowerPlanWhenFound,
                powerPlanWhenMissing = st.PowerPlanWhenMissing,
                powerLastApplied = Pref.PowerLastApplied,
                powerTargetRunning = st.PowerTargetRunning,
                powerProcessLassoDetected = st.PowerProcessLassoDetected,
                powerSwitchSkipped = st.PowerSwitchSkipped,
                powerActivePlanGuid = st.ActivePowerPlanGuid,
                powerActivePlanName = st.ActivePowerPlanName,
                powerDesiredPlanGuid = st.DesiredPowerPlanGuid,
                powerDesiredPlanName = st.DesiredPowerPlanName,
                powerPlans = st.PowerPlans.ConvertAll(p => new
                {
                    guid = p.Guid,
                    name = p.Name
                }),
                lockBestPerformanceMode = st.LockBestPerformanceMode,
                lockBestPerformanceActive = st.LockBestPerformanceActive,
                bestPerformancePlanGuid = st.BestPerformancePlanGuid
            };

            SendJson("state", payload);
        }

        void SendState()
        {
            if (!_webReady || _web.CoreWebView2 == null) return;
            RefreshWebState();
        }

        void SendNotify(string message)
        {
            try
            {
                if (!_webReady || _web.CoreWebView2 == null) return;
                SendJson("notify", new { message });
            }
            catch { }
        }

        void SendJson(string type, object data)
        {
            try
            {
                if (_web.CoreWebView2 == null) return;
                var json = JsonSerializer.Serialize(new { type, data });
                _web.CoreWebView2.PostWebMessageAsJson(json);
            }
            catch (Exception ex)
            {
                Log.Error("Send json failed.", ex);
            }
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
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Choose MSI Afterburner path failed.", ex);
            }
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
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error("Choose control center path failed.", ex);
            }
        }

        void OpenFolder(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return;
                Directory.CreateDirectory(path);
                Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log.Error("Open folder failed: " + path, ex);
            }
        }

        void ToggleMaximizeRestore()
        {
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
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

        void ToTray() { ToTray(true); }

        void ToTray(bool tip)
        {
            if (_exiting) return;
            ShowInTaskbar = false;
            Hide();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            if (tip && _tray != null) _tray.ShowBalloonTip(1600, L.T, L.TT, ToolTipIcon.Info);
            Program.TrimMemory();
        }

        void RestoreFromTray()
        {
            if (_exiting) return;
            ShowInTaskbar = true;
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        public void ActivateFromExternalInstance()
        {
            if (_exiting) return;
            RestoreFromTray();
            BringToFront();
            TopMost = true;
            TopMost = false;
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SYSCOMMAND = 0x0112;
            const int SC_MINIMIZE = 0xF020;

            if (!_exiting && m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_MINIMIZE)
            {
                ToTray();
                return;
            }

            base.WndProc(ref m);
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
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
                _tray = null;
            }
            base.OnFormClosing(e);
        }

        protected override void OnResize(EventArgs e)
        {
            if (!_exiting && WindowState == FormWindowState.Minimized) ToTray();
            base.OnResize(e);
        }

        string TimeText(TimeSpan t)
        {
            return string.Format("{0:D2}:{1:D2}:{2:D2}", (int)t.TotalHours, t.Minutes, t.Seconds);
        }
    }
}