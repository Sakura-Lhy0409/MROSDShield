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

}
