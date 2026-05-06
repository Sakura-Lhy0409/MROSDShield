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
    static class AppInfo
    {
        public const string Version = "1.0.5";
        public const string PackageName = "MROSDShield-v1.0.5.zip";
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);

        [DllImport("user32.dll")]
        static extern bool DestroyIcon(IntPtr hIcon);

        [STAThread]
        static void Main(string[] args)
        {
            try
            {
                try { SetProcessDPIAware(); } catch { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (s, e) => LogCrash(e.Exception);
                AppDomain.CurrentDomain.UnhandledException += (s, e) => LogCrash(e.ExceptionObject as Exception);
                Log.Info("Application starting. Version=" + AppInfo.Version + ", Args=" + string.Join(" ", args) + ", Admin=" + IsAdmin());
                new App().Run(args.Length > 0 && args[0] == "--minimized");
            }
            catch (Exception ex)
            {
                LogCrash(ex);
                MessageBox.Show(ex.ToString(), "MR OSD Shield Crash", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        static void LogCrash(Exception ex)
        {
            try
            {
                string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + (ex == null ? "Unknown exception" : ex.ToString()) + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }

        public static void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (var p = Process.GetCurrentProcess())
                    EmptyWorkingSet(p.Handle);
            }
            catch { }
        }

        public static bool IsAdmin()
        {
            try
            {
                using (var id = WindowsIdentity.GetCurrent())
                    return new WindowsPrincipal(id).IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }

        public static void RestartAsAdmin(bool minimized)
        {
            try
            {
                var psi = new ProcessStartInfo(Application.ExecutablePath);
                psi.Verb = "runas";
                psi.UseShellExecute = true;
                if (minimized) psi.Arguments = "--minimized";
                Process.Start(psi);
                Log.Info("Restarting as administrator.");
                Application.Exit();
            }
            catch (Exception ex)
            {
                Log.Error("Restart as administrator failed.", ex);
            }
        }

        public static Icon CreateOwnedIcon(Bitmap bmp)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = bmp.GetHicon();
                using (var ico = Icon.FromHandle(h))
                    return (Icon)ico.Clone();
            }
            finally
            {
                if (h != IntPtr.Zero) DestroyIcon(h);
            }
        }
    }

    static class Log
    {
        static readonly object _lock = new object();
        static string Dir { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs"); } }
        public static string PathName { get { return Path.Combine(Dir, "mr_osd_shield.log"); } }

        public static void Info(string msg) { Write("INFO", msg, null); }
        public static void Error(string msg, Exception ex) { Write("ERROR", msg, ex); }

        static void Write(string level, string msg, Exception ex)
        {
            try
            {
                lock (_lock)
                {
                    Directory.CreateDirectory(Dir);
                    RotateIfNeeded();
                    var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + msg;
                    if (ex != null) line += Environment.NewLine + ex;
                    File.AppendAllText(PathName, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch { }
        }

        static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(PathName)) return;
                if (new FileInfo(PathName).Length < 512 * 1024) return;
                string old = Path.Combine(Dir, "mr_osd_shield.old.log");
                if (File.Exists(old)) File.Delete(old);
                File.Move(PathName, old);
            }
            catch { }
        }
    }

    static class L
    {
        static bool? _forceZh;
        public static bool Zh { get { return _forceZh.HasValue ? _forceZh.Value : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"; } }
        public static void Toggle() { _forceZh = !Zh; }
        static string S(string zh, string en) { return Zh ? zh : en; }
        public static string T { get { return "MR OSD Shield"; } }
        public static string TV { get { return "MR OSD Shield v" + AppInfo.Version; } }
        public static string Sub { get { return S("机械革命 GPU 控制防护", "MECHREVO GPU Control Shield"); } }
        public static string Home { get { return S("首页", "Home"); } }
        public static string Stat { get { return S("统计", "Stats"); } }
        public static string Set { get { return S("设置", "Settings"); } }
        public static string Det { get { return S("正在检测控制中心", "Detecting control center"); } }
        public static string Act { get { return S("防护已启用", "Protection Active"); } }
        public static string Warn { get { return S("需要注意", "Attention Needed"); } }
        public static string ActSub { get { return S("仅锁定 GPU 超频配置，控制中心功耗控制保持可用", "Only GPU OC config is locked; power controls stay available"); } }
        public static string WaitSub { get { return S("等待 GCUBridge 服务稳定后开始防护", "Waiting for GCUBridge to stabilize"); } }
        public static string WarnSub { get { return S("GPU 超频配置被改动，已修正并重应用小飞机配置", "GPU OC config changed; fixed and reapplied AB profile"); } }
        public static string Svc { get { return S("GCUBridge 服务", "GCUBridge Service"); } }
        public static string Gpu { get { return S("GPU 控制进程", "GPU Control Processes"); } }
        public static string Ses { get { return S("本次修复", "Session Fixes"); } }
        public static string Tot { get { return S("进程拦截", "Process Kills"); } }
        public static string Up { get { return S("运行时间", "Uptime"); } }
        public static string AS { get { return S("开机自启", "Start with Windows"); } }
        public static string BM { get { return S("开机自动最小化到托盘", "Start minimized to tray"); } }
        public static string WT { get { return S("控制中心稳定等待时间", "Control center wait time"); } }
        public static string Sec { get { return S("秒", "s"); } }
        public static string MT { get { return S("关闭窗口时最小化到托盘", "Minimize to tray on close"); } }
        public static string Btn { get { return S("最小化到托盘", "Minimize to Tray"); } }
        public static string Run { get { return S("运行中", "Running"); } }
        public static string Blk { get { return S("已屏蔽", "Blocked"); } }
        public static string Allow { get { return S("已允许", "Allowed"); } }
        public static string Stp { get { return S("已停止", "Stopped"); } }
        public static string NF { get { return S("未找到", "Not Found"); } }
        public static string AR { get { return S("程序已在运行。", "MR OSD Shield is already running."); } }
        public static string TP { get { return S("MR OSD Shield - 已防护", "MR OSD Shield - Protected"); } }
        public static string TW { get { return S("MR OSD Shield - 警告", "MR OSD Shield - Warning"); } }
        public static string TD { get { return S("MR OSD Shield - 检测中", "MR OSD Shield - Detecting"); } }
        public static string TT { get { return S("GPU 防护正在后台运行", "GPU shield is running in the background"); } }
        public static string AB { get { return S("MSI Afterburner 路径", "MSI Afterburner Path"); } }
        public static string CCP { get { return S("控制中心路径", "Control Center Path"); } }
        public static string Kills { get { return S("GPU 超频修复次数", "GPU OC Fixes"); } }
        public static string Resets { get { return S("进程拦截次数", "Process Kills"); } }
        public static string Avg { get { return S("平均修复 / 小时", "Avg Fixes / Hour"); } }
        public static string Log { get { return S("日志路径", "Log Path"); } }
        public static string Open { get { return S("打开", "Open"); } }
        public static string Exit { get { return S("退出", "Exit"); } }
        public static string Lang { get { return Zh ? "中文" : "EN"; } }
        public static string Live { get { return S("实时防护", "Live Shield"); } }
        public static string Core { get { return S("核心状态", "Core Status"); } }
        public static string Quick { get { return S("快捷设置", "Quick Settings"); } }
        public static string Author { get { return S("作者 Sakura", "by Sakura"); } }
        public static string Admin { get { return S("管理员权限", "Administrator"); } }
        public static string AdminOK { get { return S("已启用", "Enabled"); } }
        public static string AdminNO { get { return S("未启用，部分功能可能失败", "Not enabled, some features may fail"); } }
        public static string RestartAdmin { get { return S("以管理员身份重启", "Restart as Admin"); } }
        public static string OpenLogDir { get { return S("打开日志目录", "Open Logs"); } }
        public static string OpenAppDir { get { return S("打开程序目录", "Open Folder"); } }
        public static string ApplyAB { get { return S("重应用小飞机配置", "Apply AB Profile"); } }
        public static string RepairNow { get { return S("立即执行防护修复", "Repair Now"); } }
        public static string ABProfile { get { return S("小飞机 Profile", "AB Profile"); } }
        public static string KillProc { get { return S("兼容旧版强制拦截模式", "Legacy force-block mode"); } }
        public static string KillProcHint { get { return S("不建议开启；开启后可能导致功耗选项不可调", "Not recommended; may disable power controls"); } }
        public static string Choose { get { return S("选择", "Choose"); } }
    }

    static class Co
    {
        public static readonly Color BG = Color.FromArgb(7, 10, 18);
        public static readonly Color BG2 = Color.FromArgb(12, 18, 34);
        public static readonly Color Side = Color.FromArgb(11, 16, 30);
        public static readonly Color Card = Color.FromArgb(20, 29, 48);
        public static readonly Color Card2 = Color.FromArgb(27, 39, 64);
        public static readonly Color Border = Color.FromArgb(50, 70, 104);
        public static readonly Color Txt = Color.FromArgb(240, 246, 255);
        public static readonly Color Dim = Color.FromArgb(142, 157, 184);
        public static readonly Color Faint = Color.FromArgb(76, 88, 112);
        public static readonly Color Green = Color.FromArgb(36, 245, 164);
        public static readonly Color Blue = Color.FromArgb(70, 158, 255);
        public static readonly Color Purple = Color.FromArgb(164, 116, 255);
        public static readonly Color Red = Color.FromArgb(255, 82, 102);
        public static readonly Color Amber = Color.FromArgb(255, 185, 75);
        public static readonly Color Toggle = Color.FromArgb(68, 78, 98);
    }

    class Engine
    {
        static readonly string[] PROCS = { "GCUService", "GCUUtil" };
        string _abPath, _ccBase, _hwoc, _mainopt;
        System.Threading.Timer _tmr, _powerTmr;
        volatile bool _run, _done, _ready, _ccFS;
        int _quietTicks;
        readonly object _statusLock = new object();
        StatusInfo _lastStatus = new StatusInfo();
        int _ticking;
        DateTime _ccSince, _lastFileCheck, _hwocWrite, _mainoptWrite;

        public int Blocked, TotalBlocked, SessionKills, FileResets;
        public DateTime StartTime;
        public bool Active { get { return _done; } }
        public string ABPath { get { return _abPath ?? ""; } }
        public string CCPath { get { return _ccBase ?? ""; } }
        public int StableRem
        {
            get
            {
                if (!_ccFS || _done) return 0;
                return Math.Max(0, Pref.StableSeconds - (int)(DateTime.Now - _ccSince).TotalSeconds);
            }
        }

        public void StartEngine()
        {
            _run = true; _done = false; _ready = false; _ccFS = false;
            StartTime = DateTime.Now;
            _ccBase = FindCC();
            if (_ccBase != null)
            {
                _hwoc = Path.Combine(_ccBase, "AiStoneService", "MyControlCenter", "LiquidHWOC", "LiquidHWOC.json");
                _mainopt = Path.Combine(_ccBase, "AiStoneService", "MyControlCenter", "UserPofiles", "MainOption.json");
            }
            _abPath = FindAB();
            Log.Info("Engine started. CCPath=" + (_ccBase ?? "NotFound") + ", ABPath=" + (_abPath ?? "NotFound") + ", StableSeconds=" + Pref.StableSeconds);
            _lastFileCheck = DateTime.MinValue;
            _hwocWrite = GetWriteTime(_hwoc);
            _mainoptWrite = GetWriteTime(_mainopt);
            UpdateStatusCache();
            _tmr = new System.Threading.Timer(Tick, null, 500, 650);
        }

        public void Stop()
        {
            _run = false;
            if (_tmr != null) { _tmr.Dispose(); _tmr = null; }
            if (_powerTmr != null) { _powerTmr.Dispose(); _powerTmr = null; }
        }

        public void OnPowerModeChanged(PowerModes mode)
        {
            try
            {
                Log.Info("Power mode changed: " + mode + ". Scheduling MSI Afterburner profile reapply and repair.");
                if (_powerTmr != null) _powerTmr.Dispose();
                _powerTmr = new System.Threading.Timer(s =>
                {
                    try
                    {
                        ReapplyAB();
                        RepairNow();
                    }
                    catch (Exception ex) { Log.Error("Power mode repair failed.", ex); }
                }, null, 2500, Timeout.Infinite);
            }
            catch (Exception ex) { Log.Error("Handle power mode change failed.", ex); }
        }

        public void SetAfterburnerPath(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    Pref.AfterburnerPath = path;
                    _abPath = path;
                    Log.Info("Custom MSI Afterburner path set: " + path);
                }
            }
            catch (Exception ex) { Log.Error("Set MSI Afterburner path failed.", ex); }
        }

        public void SetControlCenterPath(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Pref.ControlCenterPath = path;
                    _ccBase = path;
                    _hwoc = Path.Combine(_ccBase, "AiStoneService", "MyControlCenter", "LiquidHWOC", "LiquidHWOC.json");
                    _mainopt = Path.Combine(_ccBase, "AiStoneService", "MyControlCenter", "UserPofiles", "MainOption.json");
                    Log.Info("Custom control center path set: " + path);
                }
            }
            catch (Exception ex) { Log.Error("Set control center path failed.", ex); }
        }

        bool CCUp()
        {
            try { using (var sc = new ServiceController("GCUBridge")) return sc.Status == ServiceControllerStatus.Running; }
            catch { return false; }
        }

        void Tick(object s)
        {
            if (!_run) return;
            if (Interlocked.Exchange(ref _ticking, 1) == 1) return;
            try
            {
                if (!_ready)
                {
                    if (CCUp())
                    {
                        if (!_ccFS) { _ccFS = true; _ccSince = DateTime.Now; }
                        if ((DateTime.Now - _ccSince).TotalSeconds >= Pref.StableSeconds)
                        {
                            _ready = true;
                            Log.Info("GCUBridge stabilized. Protection is active.");
                            ReapplyAB();
                            _done = true;
                        }
                    }
                    else _ccFS = false;
                    UpdateStatusCache();
                    return;
                }

                bool fixedOc = false;
                if ((DateTime.Now - _lastFileCheck).TotalMilliseconds >= 1200)
                {
                    _lastFileCheck = DateTime.Now;
                    bool touched = false;
                    if (ConfigTouched(_hwoc, ref _hwocWrite)) touched = true;
                    if (ConfigTouched(_mainopt, ref _mainoptWrite)) touched = true;
                    if (_hwoc != null && Zero(_hwoc, new[] { "CoreFreqOffset", "MemFreqOffset" })) fixedOc = true;
                    if (_mainopt != null && Zero(_mainopt, new[] { "TurboGPUOCOffset", "TurboSilentGPUOCOffset" })) fixedOc = true;
                    if (fixedOc)
                    {
                        FileResets++;
                        _hwocWrite = GetWriteTime(_hwoc);
                        _mainoptWrite = GetWriteTime(_mainopt);
                        ReapplyAB();
                        Log.Info("GPU OC config fixed without blocking control center power services.");
                    }
                    else if (touched)
                    {
                        ReapplyAB();
                        Log.Info("Control center GPU config touched; reapplied MSI Afterburner profile without blocking power controls.");
                    }
                }

                int k = 0;
                if (Pref.KillGpuProcesses) k = Kill();
                if (k > 0)
                {
                    _quietTicks = 0;
                    Blocked += k; TotalBlocked += k; SessionKills += k; Log.Info("Blocked GPU control processes. Count=" + k);
                    ChangeInterval(650);
                }
                else
                {
                    _quietTicks++;
                    if (_quietTicks > 460) ChangeInterval(2500);
                    else if (_quietTicks > 90) ChangeInterval(1500);
                }
                UpdateStatusCache();
            }
            finally { Interlocked.Exchange(ref _ticking, 0); }
        }

        void ChangeInterval(int ms)
        {
            try { if (_tmr != null) _tmr.Change(ms, ms); } catch { }
        }

        int Kill()
        {
            int n = 0;
            foreach (var name in PROCS)
                foreach (var p in Process.GetProcessesByName(name))
                {
                    try { p.Kill(); n++; }
                    catch (Exception ex) { Log.Error("Kill process failed: " + name, ex); }
                    finally { p.Dispose(); }
                }
            return n;
        }

        DateTime GetWriteTime(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path)) return File.GetLastWriteTimeUtc(path);
            }
            catch { }
            return DateTime.MinValue;
        }

        bool ConfigTouched(string path, ref DateTime lastWrite)
        {
            DateTime now = GetWriteTime(path);
            if (now == DateTime.MinValue) return false;
            if (lastWrite == DateTime.MinValue)
            {
                lastWrite = now;
                return false;
            }
            if (now != lastWrite)
            {
                lastWrite = now;
                return true;
            }
            return false;
        }

        bool Zero(string path, string[] keys)
        {
            return ZeroFile(path, keys);
        }

        public void RepairNow()
        {
            try
            {
                int k = Pref.KillGpuProcesses ? Kill() : 0;
                if (k > 0) { Blocked += k; TotalBlocked += k; SessionKills += k; }
                bool fixedOc = false;
                if (_hwoc != null && ZeroFile(_hwoc, new[] { "CoreFreqOffset", "MemFreqOffset" })) fixedOc = true;
                if (_mainopt != null && ZeroFile(_mainopt, new[] { "TurboGPUOCOffset", "TurboSilentGPUOCOffset" })) fixedOc = true;
                if (fixedOc)
                {
                    FileResets++;
                    ReapplyAB();
                }
                UpdateStatusCache();
                Log.Info("Manual repair executed. Kills=" + k + ", FixedOC=" + fixedOc + ", KillGpuProcesses=" + Pref.KillGpuProcesses);
            }
            catch (Exception ex) { Log.Error("Manual repair failed.", ex); }
        }

        public void ApplyAfterburnerProfile()
        {
            ReapplyAB();
        }

        bool ZeroFile(string path, string[] keys)
        {
            try
            {
                if (!File.Exists(path)) return false;
                string j = File.ReadAllText(path, Encoding.UTF8);
                bool ch = false;
                foreach (var k in keys)
                {
                    if (j.Contains("\"" + k + "\": 0") || j.Contains("\"" + k + "\": 0.0") || j.Contains("\"" + k + "\": false")) continue;
                    int i = j.IndexOf("\"" + k + "\""); if (i < 0) continue;
                    int c = j.IndexOf(':', i); if (c < 0) continue;
                    int vs = c + 1; while (vs < j.Length && char.IsWhiteSpace(j[vs])) vs++;
                    int ve = vs; while (ve < j.Length && j[ve] != ',' && j[ve] != '\n' && j[ve] != '}') ve++;
                    string ov = j.Substring(vs, ve - vs).Trim();
                    bool isBool = ov.Equals("true", StringComparison.OrdinalIgnoreCase) || ov.Equals("false", StringComparison.OrdinalIgnoreCase);
                    bool isNum;
                    double dummy;
                    isNum = double.TryParse(ov, System.Globalization.NumberStyles.Float, CultureInfo.InvariantCulture, out dummy);
                    if (!isBool && !isNum)
                    {
                        Log.Info("Skip non numeric/bool config value. Path=" + path + ", Key=" + k + ", Value=" + ov);
                        continue;
                    }
                    string nv = isBool ? "false" : "0";
                    if (ov == nv) continue;
                    j = j.Substring(0, vs) + nv + j.Substring(ve);
                    ch = true;
                }
                if (ch)
                {
                    BackupConfig(path);
                    File.WriteAllText(path, j, Encoding.UTF8);
                    Log.Info("Config reset: " + path);
                }
                return ch;
            }
            catch (Exception ex) { Log.Error("Config reset failed: " + path, ex); return false; }
        }

        void BackupConfig(string path)
        {
            try
            {
                string bak = path + ".mrosd.bak";
                if (!File.Exists(bak))
                {
                    File.Copy(path, bak, false);
                    Log.Info("Backup created: " + bak);
                }
            }
            catch (Exception ex) { Log.Error("Backup config failed: " + path, ex); }
        }

        void ReapplyAB()
        {
            try
            {
                if (_abPath == null || !File.Exists(_abPath)) _abPath = FindAB();
                if (_abPath != null)
                {
                    string profileArg = "-profile" + Pref.AfterburnerProfile;
                    Log.Info("Reapply MSI Afterburner " + profileArg + ": " + _abPath);
                    var p = Process.Start(new ProcessStartInfo(_abPath, profileArg) { CreateNoWindow = true, UseShellExecute = false });
                    if (p != null) p.WaitForExit(1200);
                }
            }
            catch (Exception ex) { Log.Error("Reapply MSI Afterburner profile failed.", ex); }
        }

        string FindCC()
        {
            if (Pref.ControlCenterPath.Length > 0 && Directory.Exists(Pref.ControlCenterPath)) return Pref.ControlCenterPath;
            string[] sp = { @"C:\Program Files\OEM\机械革命控制中心", @"D:\Program Files\OEM\机械革命控制中心", @"C:\Program Files (x86)\OEM\机械革命控制中心" };
            foreach (var p in sp) if (Directory.Exists(p)) return p;
            try
            {
                foreach (var d in DriveInfo.GetDrives())
                {
                    if (!d.IsReady) continue;
                    var p = Path.Combine(d.RootDirectory.FullName, "Program Files", "OEM", "机械革命控制中心");
                    if (Directory.Exists(p)) return p;
                }
            }
            catch { }
            return null;
        }

        string FindAB()
        {
            if (Pref.AfterburnerPath.Length > 0 && File.Exists(Pref.AfterburnerPath)) return Pref.AfterburnerPath;
            string[] sp = { @"C:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe", @"C:\Program Files\MSI Afterburner\MSIAfterburner.exe", @"D:\Program Files (x86)\MSI Afterburner\MSIAfterburner.exe" };
            foreach (var p in sp) if (File.Exists(p)) return p;
            try
            {
                foreach (var p in Process.GetProcessesByName("MSIAfterburner"))
                {
                    try { var f = p.MainModule.FileName; if (File.Exists(f)) return f; }
                    catch { }
                    finally { p.Dispose(); }
                }
            }
            catch { }
            return null;
        }

        void UpdateStatusCache()
        {
            var s = new StatusInfo();
            try { using (var sc = new ServiceController("GCUBridge")) { s.SvcRunning = sc.Status == ServiceControllerStatus.Running; s.SvcFound = true; } } catch { }
            foreach (var n in PROCS)
            {
                var ps = Process.GetProcessesByName(n);
                s.Procs[n + ".exe"] = ps.Length > 0;
                foreach (var p in ps) p.Dispose();
            }
            s.Blocked = Blocked; s.Total = TotalBlocked; s.SessionKills = SessionKills; s.FileResets = FileResets;
            s.Uptime = DateTime.Now - StartTime;
            s.AllOK = s.SvcRunning;
            if (Pref.KillGpuProcesses)
                foreach (var kv in s.Procs) if (kv.Value) s.AllOK = false;
            lock (_statusLock) _lastStatus = s;
        }

        public StatusInfo GetStatus()
        {
            lock (_statusLock) return _lastStatus.Clone();
        }
    }

    class StatusInfo
    {
        public bool SvcFound, SvcRunning, AllOK;
        public System.Collections.Generic.Dictionary<string, bool> Procs = new System.Collections.Generic.Dictionary<string, bool>();
        public int Blocked, Total, SessionKills, FileResets;
        public TimeSpan Uptime;

        public StatusInfo Clone()
        {
            var s = (StatusInfo)MemberwiseClone();
            s.Procs = new System.Collections.Generic.Dictionary<string, bool>(Procs);
            return s;
        }
    }

    static class Pref
    {
        static string PathName { get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.ini"); } }

        static string ReadValue(string key, string def)
        {
            try
            {
                string p = PathName;
                if (!File.Exists(p)) return def;
                foreach (var line in File.ReadAllLines(p, Encoding.UTF8))
                {
                    int i = line.IndexOf('=');
                    if (i <= 0) continue;
                    if (line.Substring(0, i).Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                        return line.Substring(i + 1).Trim();
                }
            }
            catch { }
            return def;
        }

        static void WriteValue(string key, string value)
        {
            try
            {
                var map = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string p = PathName;
                if (File.Exists(p))
                {
                    foreach (var line in File.ReadAllLines(p, Encoding.UTF8))
                    {
                        int i = line.IndexOf('=');
                        if (i <= 0) continue;
                        map[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
                }
                map[key] = value;
                var sb = new StringBuilder();
                foreach (var kv in map) sb.AppendLine(kv.Key + "=" + kv.Value);
                File.WriteAllText(p, sb.ToString(), Encoding.UTF8);
            }
            catch { }
        }

        public static bool BootMin
        {
            get { return !ReadValue("BootMin", "true").Equals("false", StringComparison.OrdinalIgnoreCase); }
            set { WriteValue("BootMin", value ? "true" : "false"); }
        }

        public static bool MinToTray
        {
            get { return !ReadValue("MinToTray", "true").Equals("false", StringComparison.OrdinalIgnoreCase); }
            set { WriteValue("MinToTray", value ? "true" : "false"); }
        }

        public static string AfterburnerPath
        {
            get { return ReadValue("AfterburnerPath", ""); }
            set { WriteValue("AfterburnerPath", value ?? ""); }
        }

        public static string ControlCenterPath
        {
            get { return ReadValue("ControlCenterPath", ""); }
            set { WriteValue("ControlCenterPath", value ?? ""); }
        }

        public static int AfterburnerProfile
        {
            get
            {
                int v;
                if (!int.TryParse(ReadValue("AfterburnerProfile", "1"), out v)) v = 1;
                return Math.Max(1, Math.Min(5, v));
            }
            set { WriteValue("AfterburnerProfile", Math.Max(1, Math.Min(5, value)).ToString()); }
        }

        public static int StableSeconds
        {
            get
            {
                int v;
                if (!int.TryParse(ReadValue("StableSeconds", "15"), out v)) v = 15;
                return Math.Max(5, Math.Min(60, v));
            }
            set { WriteValue("StableSeconds", Math.Max(5, Math.Min(60, value)).ToString()); }
        }

        public static bool KillGpuProcesses
        {
            get { return ReadValue("KillGpuProcesses", "false").Equals("true", StringComparison.OrdinalIgnoreCase); }
            set { WriteValue("KillGpuProcesses", value ? "true" : "false"); }
        }
    }

    static class AS
    {
        const string TN = "MR_OSD_Shield";
        static string RunProcess(string fileName, string args)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(fileName, args) { CreateNoWindow = true, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true });
                if (p == null) return "";
                if (!p.WaitForExit(8000))
                {
                    try { p.Kill(); } catch { }
                    return "TIMEOUT";
                }
                return p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd();
            }
            catch { return ""; }
        }

        static string R(string a)
        {
            return RunProcess("schtasks.exe", a);
        }

        static string PS(string script)
        {
            string tmp = "";
            try
            {
                tmp = Path.Combine(Path.GetTempPath(), "mr_osd_ps_" + Guid.NewGuid().ToString("N") + ".ps1");
                File.WriteAllText(tmp, script, Encoding.UTF8);
                return RunProcess("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -File " + Q(tmp));
            }
            catch { return ""; }
            finally
            {
                try { if (tmp.Length > 0 && File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        static string Q(string s) { return "\"" + s.Replace("\"", "\"\"") + "\""; }

        static string CurrentUser()
        {
            try { return WindowsIdentity.GetCurrent().Name; }
            catch { return Environment.UserName; }
        }

        public static bool On() { return R("/query /tn " + TN).Contains(TN); }

        public static void Enable()
        {
            string args = Pref.BootMin ? " --minimized" : "";
            string user = CurrentUser();
            string output = R("/create /tn " + TN + " /tr " + Q(Application.ExecutablePath + args) + " /sc onlogon /rl highest /ru " + Q(user) + " /f");
            Log.Info("Enable autostart. User=" + user + ", BootMin=" + Pref.BootMin + ", Output=" + output.Replace(Environment.NewLine, " "));
            FixPowerConditions(TN);
            FixAfterburnerPowerConditions();
        }

        public static void Disable()
        {
            string output = R("/delete /tn " + TN + " /f");
            Log.Info("Disable autostart. Output=" + output.Replace(Environment.NewLine, " "));
        }

        public static void FixPowerConditions(string taskName)
        {
            try
            {
                string tmp = Path.Combine(Path.GetTempPath(), "mr_osd_task_" + Guid.NewGuid().ToString("N") + ".xml");
                try
                {
                    string xml = R("/query /tn " + Q(taskName) + " /xml");
                    int i = xml.IndexOf("<?xml", StringComparison.OrdinalIgnoreCase);
                    if (i > 0) xml = xml.Substring(i);
                    if (xml.IndexOf("<Task", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        xml = SetXmlBool(xml, "DisallowStartIfOnBatteries", false);
                        xml = SetXmlBool(xml, "StopIfGoingOnBatteries", false);
                        xml = SetXmlBool(xml, "AllowHardTerminate", false);
                        xml = SetXmlBool(xml, "StartWhenAvailable", true);
                        File.WriteAllText(tmp, xml, Encoding.Unicode);
                        string output = R("/create /tn " + Q(taskName) + " /xml " + Q(tmp) + " /ru " + Q(CurrentUser()) + " /f");
                        Log.Info("Fixed task power conditions by XML: " + taskName + ". Output=" + output.Replace(Environment.NewLine, " "));
                    }
                }
                finally
                {
                    try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                }

                string psOutput = FixPowerConditionsByCom(taskName);
                Log.Info("Fixed task power conditions by COM: " + taskName + ". Output=" + psOutput.Replace(Environment.NewLine, " "));
            }
            catch (Exception ex) { Log.Error("Fix task power conditions failed: " + taskName, ex); }
        }

        static string FixPowerConditionsByCom(string taskName)
        {
            string safeTaskName = taskName.Replace("'", "''");
            string script =
@"$ErrorActionPreference = 'Stop'
$svc = New-Object -ComObject Schedule.Service
$svc.Connect()
$name = '" + safeTaskName + @"'
$path = '\'
if ($name.Contains('\')) {
    $idx = $name.LastIndexOf('\')
    $path = $name.Substring(0, $idx + 1)
    $name = $name.Substring($idx + 1)
    if ([string]::IsNullOrWhiteSpace($path)) { $path = '\' }
}
$folder = $svc.GetFolder($path)
$task = $folder.GetTask($name)
$def = $task.Definition
$def.Settings.DisallowStartIfOnBatteries = $false
$def.Settings.StopIfGoingOnBatteries = $false
$def.Settings.StartWhenAvailable = $true
$def.Settings.AllowHardTerminate = $false
$folder.RegisterTaskDefinition($name, $def, 6, $null, $null, 3) | Out-Null
'OK'";
            return PS(script);
        }

        public static void FixSelfAndAfterburnerPowerConditions()
        {
            try
            {
                if (On()) FixPowerConditions(TN);
                FixAfterburnerPowerConditions();
            }
            catch (Exception ex) { Log.Error("Fix startup task power conditions failed.", ex); }
        }

        public static void FixAfterburnerPowerConditions()
        {
            try
            {
                string list = R("/query /fo LIST /v");
                using (var sr = new StringReader(list))
                {
                    string line, task = "", run = "";
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith("TaskName:", StringComparison.OrdinalIgnoreCase))
                            task = line.Substring(line.IndexOf(':') + 1).Trim();
                        else if (line.StartsWith("Task To Run:", StringComparison.OrdinalIgnoreCase))
                            run = line.Substring(line.IndexOf(':') + 1).Trim();
                        else if (line.Trim().Length == 0)
                        {
                            if (task.Length > 0 && task.IndexOf("\\Microsoft\\", StringComparison.OrdinalIgnoreCase) < 0 && run.IndexOf("MSIAfterburner", StringComparison.OrdinalIgnoreCase) >= 0)
                                FixPowerConditions(task);
                            task = ""; run = "";
                        }
                    }
                    if (task.Length > 0 && task.IndexOf("\\Microsoft\\", StringComparison.OrdinalIgnoreCase) < 0 && run.IndexOf("MSIAfterburner", StringComparison.OrdinalIgnoreCase) >= 0)
                        FixPowerConditions(task);
                }

                string psOutput = FixAfterburnerPowerConditionsByCom();
                Log.Info("Fixed MSI Afterburner task power conditions by COM enumeration. Output=" + psOutput.Replace(Environment.NewLine, " "));
            }
            catch (Exception ex) { Log.Error("Fix MSI Afterburner task power conditions failed.", ex); }
        }

        static string FixAfterburnerPowerConditionsByCom()
        {
            string script =
@"$ErrorActionPreference = 'Stop'
$svc = New-Object -ComObject Schedule.Service
$svc.Connect()

function Fix-Folder($folder) {
    foreach ($task in $folder.GetTasks(0)) {
        $hit = $false
        foreach ($action in $task.Definition.Actions) {
            try {
                $p = [string]$action.Path
                $a = [string]$action.Arguments
                if (($p + ' ' + $a).IndexOf('MSIAfterburner', [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                    $hit = $true
                }
            } catch {}
        }

        if ($hit) {
            $def = $task.Definition
            $def.Settings.DisallowStartIfOnBatteries = $false
            $def.Settings.StopIfGoingOnBatteries = $false
            $def.Settings.StartWhenAvailable = $true
            $def.Settings.AllowHardTerminate = $false
            $folder.RegisterTaskDefinition($task.Name, $def, 6, $null, $null, 3) | Out-Null
            Write-Output ('Fixed: ' + $task.Path)
        }
    }

    foreach ($sub in $folder.GetFolders(0)) {
        Fix-Folder $sub
    }
}

Fix-Folder $svc.GetFolder('\')";
            return PS(script);
        }

        static string SetXmlBool(string xml, string name, bool value)
        {
            string v = value ? "true" : "false";
            string open = "<" + name + ">";
            string close = "</" + name + ">";
            int s = xml.IndexOf(open, StringComparison.OrdinalIgnoreCase);
            if (s >= 0)
            {
                int e = xml.IndexOf(close, s, StringComparison.OrdinalIgnoreCase);
                if (e > s) return xml.Substring(0, s + open.Length) + v + xml.Substring(e);
            }
            int p = xml.IndexOf("</Settings>", StringComparison.OrdinalIgnoreCase);
            if (p >= 0) return xml.Substring(0, p) + "    " + open + v + close + Environment.NewLine + xml.Substring(p);
            return xml;
        }
    }

    class ToggleSwitch : Control
    {
        bool _on;
        int _kx;
        System.Windows.Forms.Timer _a;
        public event Action<bool> Clicked;
        public ToggleSwitch(bool v)
        {
            _on = v; _kx = v ? 23 : 5;
            Size = new Size(44, 22);
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _a = new System.Windows.Forms.Timer { Interval = 12 };
            _a.Tick += (s, e) =>
            {
                int t = _on ? 23 : 5;
                if (Math.Abs(_kx - t) > 2) _kx += _kx < t ? 3 : -3;
                else { _kx = t; _a.Stop(); }
                Invalidate();
            };
        }
        protected override void OnClick(EventArgs e) { _on = !_on; _a.Start(); if (Clicked != null) Clicked(_on); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var p = Round(ClientRectangle, 11))
            using (var b = new LinearGradientBrush(ClientRectangle, _on ? Co.Green : Co.Toggle, _on ? Co.Blue : Color.FromArgb(48, 55, 70), 0f))
                e.Graphics.FillPath(b, p);
            using (var b = new SolidBrush(Color.White))
                e.Graphics.FillEllipse(b, _kx, 4, 14, 14);
        }
        protected override void OnPaintBackground(PaintEventArgs e) { }
        static GraphicsPath Round(Rectangle r, int d)
        {
            var p = new GraphicsPath(); int dd = d * 2;
            p.AddArc(r.X, r.Y, dd, dd, 180, 90); p.AddArc(r.Right - dd, r.Y, dd, dd, 270, 90);
            p.AddArc(r.Right - dd, r.Bottom - dd, dd, dd, 0, 90); p.AddArc(r.X, r.Bottom - dd, dd, dd, 90, 90);
            p.CloseFigure(); return p;
        }
    }

    class GlowCard : Panel
    {
        public string Title = "";
        public Color Accent = Co.Blue;
        public int Radius = 18;
        public GlowCard()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var p = Round(r, Radius))
            {
                using (var b = new SolidBrush(Co.Card))
                    e.Graphics.FillPath(b, p);
                using (var pen = new Pen(Co.Border, 1))
                    e.Graphics.DrawPath(pen, p);
            }
            using (var b = new SolidBrush(Accent))
                e.Graphics.FillRectangle(b, 0, 18, 3, Math.Max(20, Height - 36));
            if (Title.Length > 0)
            {
                using (var f = new Font("Segoe UI", 9f, FontStyle.Bold))
                    TextRenderer.DrawText(e.Graphics, Title, f, new Point(18, 14), Co.Dim);
            }
        }

        public static Color Blend(Color a, Color b, int pct)
        {
            pct = Math.Max(0, Math.Min(100, pct));
            return Color.FromArgb(a.R + (b.R - a.R) * pct / 100, a.G + (b.G - a.G) * pct / 100, a.B + (b.B - a.B) * pct / 100);
        }

        public static GraphicsPath Round(Rectangle r, int d)
        {
            var p = new GraphicsPath(); int dd = d * 2;
            p.AddArc(r.X, r.Y, dd, dd, 180, 90); p.AddArc(r.Right - dd, r.Y, dd, dd, 270, 90);
            p.AddArc(r.Right - dd, r.Bottom - dd, dd, dd, 0, 90); p.AddArc(r.X, r.Bottom - dd, dd, dd, 90, 90);
            p.CloseFigure(); return p;
        }
    }

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

    class App
    {
        Engine _e;
        Mutex _m;
        EventWaitHandle _activateEvent;
        Thread _activateThread;
        MainForm _form;
        volatile bool _running, _pendingActivate;

        public void Run(bool min)
        {
            bool c;
            string instanceName = "MR_OSD_Shield_" + InstanceId();
            _m = new Mutex(true, instanceName, out c);
            if (!c)
            {
                SignalExistingInstance(instanceName);
                Log.Info("Another instance is already running. Activation signal sent.");
                return;
            }

            Log.Info("App run. StartMinimized=" + min + ", Instance=" + instanceName);
            if (!Program.IsAdmin()) Log.Info("Application is not running as administrator.");

            _running = true;
            _activateEvent = new EventWaitHandle(false, EventResetMode.AutoReset, instanceName + "_Activate");
            _activateThread = new Thread(() =>
            {
                while (_running)
                {
                    try
                    {
                        _activateEvent.WaitOne();
                        if (!_running) break;
                        var f = _form;
                        if (f != null && !f.IsDisposed)
                            f.BeginInvoke(new Action(() => f.ActivateFromExternalInstance()));
                        else
                            _pendingActivate = true;
                    }
                    catch { }
                }
            });
            _activateThread.IsBackground = true;
            _activateThread.Start();

            _e = new Engine();
            _e.StartEngine();
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            _form = new MainForm(_e, min);
            if (_pendingActivate)
                _form.BeginInvoke(new Action(() => _form.ActivateFromExternalInstance()));

            var taskFixThread = new Thread(() =>
            {
                try { AS.FixSelfAndAfterburnerPowerConditions(); }
                catch (Exception ex) { Log.Error("Background task power condition fix failed.", ex); }
            });
            taskFixThread.IsBackground = true;
            taskFixThread.Start();

            Application.Run(_form);
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            _running = false;
            try { if (_activateEvent != null) _activateEvent.Set(); } catch { }
            if (_e != null) _e.Stop();
            if (_activateEvent != null) { _activateEvent.Dispose(); _activateEvent = null; }
            Log.Info("Application exited.");
            if (_m != null) _m.ReleaseMutex();
        }

        static void SignalExistingInstance(string instanceName)
        {
            try
            {
                using (var ev = EventWaitHandle.OpenExisting(instanceName + "_Activate"))
                    ev.Set();
            }
            catch { }
        }

        static string InstanceId()
        {
            try
            {
                string path = Application.ExecutablePath.ToLowerInvariant();
                using (var md5 = MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(path));
                    var sb = new StringBuilder();
                    for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
                    return sb.ToString();
                }
            }
            catch { return "Default"; }
        }

        void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (_e != null) _e.OnPowerModeChanged(e.Mode);
        }
    }
}