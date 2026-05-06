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
        DateTime _lastPowerTick = DateTime.MinValue;
        readonly System.Collections.Generic.List<PowerPlanInfo> _powerPlans = new System.Collections.Generic.List<PowerPlanInfo>();
        string _activePowerPlanGuid = "";
        string _activePowerPlanName = "";
        string _desiredPowerPlanGuid = "";
        string _desiredPowerPlanName = "";
        bool _powerTargetRunning;
        bool _powerProcessLassoDetected;
        bool _powerSwitchSkipped;
        string _bestPerformancePlanGuid = "";
        bool _lockBestPerformanceActive;
        readonly object _statusLock = new object();
        StatusInfo _lastStatus = new StatusInfo();
        int _ticking;
        DateTime _ccSince, _lastFileCheck, _hwocWrite, _mainoptWrite;
        readonly System.Collections.Generic.Dictionary<int, ProcessCpuSample> _processCpuSamples = new System.Collections.Generic.Dictionary<int, ProcessCpuSample>();
        ulong _lastCpuIdle, _lastCpuKernel, _lastCpuUser;
        bool _hasCpuSample;

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
            RefreshPowerPlanCatalog();
            RefreshPowerPlanSnapshot();
            _powerProcessLassoDetected = IsProcessLassoDetected();
            _powerTargetRunning = IsTargetPowerProcessRunning(Pref.PowerTargetProcess);
            _bestPerformancePlanGuid = FindBestPerformancePlanGuid();
            _lockBestPerformanceActive = false;
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

        public void RefreshPowerPlans()
        {
            RefreshPowerPlanCatalog();
            RefreshPowerPlanSnapshot();
            UpdateStatusCache();
        }

        public void SetPowerConfig(string targetProcess, string whenFoundGuid, string whenMissingGuid)
        {
            try
            {
                Pref.PowerTargetProcess = targetProcess ?? "";
                Pref.PowerPlanWhenFound = NormalizeGuid(whenFoundGuid);
                Pref.PowerPlanWhenMissing = NormalizeGuid(whenMissingGuid);
                RefreshPowerPlanCatalog();
                _powerTargetRunning = IsTargetPowerProcessRunning(Pref.PowerTargetProcess);
                HandlePowerPlanSwitch(true);
                UpdateStatusCache();
                Log.Info("Power plan auto switch config updated. Targets=" + Pref.PowerTargetProcess + ", Found=" + Pref.PowerPlanWhenFound + ", Missing=" + Pref.PowerPlanWhenMissing);
            }
            catch (Exception ex) { Log.Error("Set power config failed.", ex); }
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
                    HandlePowerPlanSwitch(false);
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

                HandlePowerPlanSwitch(false);
                UpdateStatusCache();
            }
            finally { Interlocked.Exchange(ref _ticking, 0); }
        }

        void ChangeInterval(int ms)
        {
            try { if (_tmr != null) _tmr.Change(ms, ms); } catch { }
        }

        void HandlePowerPlanSwitch(bool force)
        {
            try
            {
                if (!force && (DateTime.Now - _lastPowerTick).TotalSeconds < 4)
                    return;

                _lastPowerTick = DateTime.Now;
                _powerProcessLassoDetected = IsProcessLassoDetected();
                _powerTargetRunning = IsTargetPowerProcessRunning(Pref.PowerTargetProcess);

                if (Pref.LockBestPerformanceMode)
                {
                    HandleLockBestPerformanceMode(force);
                    return;
                }

                _lockBestPerformanceActive = false;

                if (!Pref.PowerAutoSwitch)
                {
                    _powerSwitchSkipped = false;
                    _desiredPowerPlanGuid = "";
                    _desiredPowerPlanName = "";
                    return;
                }

                if (_powerPlans.Count == 0) RefreshPowerPlanCatalog();

                RefreshPowerPlanSnapshot();

                string desiredGuid = _powerTargetRunning ? Pref.PowerPlanWhenFound : Pref.PowerPlanWhenMissing;
                _desiredPowerPlanGuid = NormalizeGuid(desiredGuid);
                _desiredPowerPlanName = FindPowerPlanName(_desiredPowerPlanGuid);

                if (_powerProcessLassoDetected)
                {
                    _powerSwitchSkipped = true;
                    Log.Info("Power plan auto switch skipped because Process Lasso was detected.");
                    return;
                }

                _powerSwitchSkipped = false;

                if (string.IsNullOrWhiteSpace(_desiredPowerPlanGuid))
                    return;

                if (string.Equals(_desiredPowerPlanGuid, _activePowerPlanGuid, StringComparison.OrdinalIgnoreCase))
                    return;

                if (SetActivePowerScheme(_desiredPowerPlanGuid))
                {
                    Pref.PowerLastApplied = _desiredPowerPlanGuid;
                    _activePowerPlanGuid = _desiredPowerPlanGuid;
                    _activePowerPlanName = _desiredPowerPlanName;
                    Log.Info("Power plan switched to " + (_powerTargetRunning ? "target-found" : "target-missing") + " plan: " + _desiredPowerPlanName + " (" + _desiredPowerPlanGuid + ")");
                }
            }
            catch (Exception ex) { Log.Error("Power plan auto switch failed.", ex); }
        }

        void HandleLockBestPerformanceMode(bool force)
        {
            try
            {
                if (_powerPlans.Count == 0) RefreshPowerPlanCatalog();

                if (string.IsNullOrWhiteSpace(_bestPerformancePlanGuid))
                    _bestPerformancePlanGuid = FindBestPerformancePlanGuid();

                RefreshPowerPlanSnapshot();

                if (string.IsNullOrWhiteSpace(_bestPerformancePlanGuid))
                {
                    _lockBestPerformanceActive = false;
                    Log.Info("Lock best performance mode: cannot find best performance plan GUID.");
                    return;
                }

                if (string.Equals(_activePowerPlanGuid, _bestPerformancePlanGuid, StringComparison.OrdinalIgnoreCase))
                {
                    _lockBestPerformanceActive = true;
                    return;
                }

                if (SetActivePowerScheme(_bestPerformancePlanGuid))
                {
                    _activePowerPlanGuid = _bestPerformancePlanGuid;
                    _activePowerPlanName = FindPowerPlanName(_bestPerformancePlanGuid);
                    _lockBestPerformanceActive = true;
                    Log.Info("Locked to best performance mode: " + _activePowerPlanName + " (" + _bestPerformancePlanGuid + ")");
                }
            }
            catch (Exception ex) { Log.Error("Lock best performance mode failed.", ex); }
        }

        string FindBestPerformancePlanGuid()
        {
            try
            {
                string[] knownGuids = {
                    "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",
                    "ded574b5-45a0-4f42-8737-46345c09c238"
                };

                foreach (var guid in knownGuids)
                {
                    foreach (var plan in _powerPlans)
                    {
                        if (string.Equals(plan.Guid, guid, StringComparison.OrdinalIgnoreCase))
                            return plan.Guid;
                    }
                }

                foreach (var plan in _powerPlans)
                {
                    string lower = (plan.Name ?? "").ToLowerInvariant();
                    if (lower.Contains("ultimate") || lower.Contains("performance") || lower.Contains("高性能") || lower.Contains("最佳性能") || lower.Contains("卓越"))
                        return plan.Guid;
                }
            }
            catch { }
            return "";
        }

        void RefreshPowerPlanCatalog()
        {
            try
            {
                _powerPlans.Clear();
                var lines = RunPowerCfg("/list");
                foreach (var line in lines)
                {
                    var plan = ParsePowerPlanLine(line);
                    if (plan != null && !string.IsNullOrWhiteSpace(plan.Guid))
                        _powerPlans.Add(plan);
                }

                if (_powerPlans.Count == 0)
                {
                    Log.Info("No power plans parsed from powercfg /list output. RawLines=" + lines.Count);
                }
                else
                {
                    Log.Info("Power plan catalog refreshed. Count=" + _powerPlans.Count + ", Active=" + _activePowerPlanGuid + ", Desired=" + _desiredPowerPlanGuid);
                }
            }
            catch (Exception ex) { Log.Error("Refresh power plan catalog failed.", ex); }
        }

        void RefreshPowerPlanSnapshot()
        {
            try
            {
                var current = GetActivePowerScheme();
                _activePowerPlanGuid = current.Guid;
                _activePowerPlanName = current.Name;
            }
            catch (Exception ex) { Log.Error("Refresh power plan snapshot failed.", ex); }
        }

        bool IsTargetPowerProcessRunning(string rawName)
        {
            try
            {
                var targets = ParseProcessTargets(rawName);
                if (targets.Count == 0)
                    return false;

                Process[] ps = null;
                try
                {
                    ps = Process.GetProcesses();
                    foreach (var p in ps)
                    {
                        try
                        {
                            if (MatchesAnyTargetProcess(p, targets))
                                return true;
                        }
                        catch { }
                    }
                    return false;
                }
                finally
                {
                    if (ps != null) foreach (var p in ps) p.Dispose();
                }
            }
            catch { return false; }
        }

        System.Collections.Generic.List<string> ParseProcessTargets(string rawNames)
        {
            var list = new System.Collections.Generic.List<string>();
            try
            {
                if (string.IsNullOrWhiteSpace(rawNames))
                    return list;

                var parts = rawNames.Split(new[] { ',', ';', '\r', '\n', '|', '，', '；' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var name = NormalizeProcessName(part);
                    if (string.IsNullOrWhiteSpace(name))
                        continue;
                    if (!list.Exists(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
                        list.Add(name);
                }
            }
            catch { }
            return list;
        }

        bool MatchesAnyTargetProcess(Process p, System.Collections.Generic.IList<string> targets)
        {
            try
            {
                if (p == null || targets == null || targets.Count == 0)
                    return false;

                string procName = "";
                try { procName = p.ProcessName ?? ""; } catch { }
                string fileName = "";
                string filePath = "";
                try
                {
                    filePath = p.MainModule != null ? (p.MainModule.FileName ?? "") : "";
                    if (!string.IsNullOrWhiteSpace(filePath))
                        fileName = Path.GetFileNameWithoutExtension(filePath);
                }
                catch { }

                foreach (var target in targets)
                {
                    if (string.IsNullOrWhiteSpace(target))
                        continue;

                    if (string.Equals(procName, target, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (!string.IsNullOrWhiteSpace(fileName) && string.Equals(fileName, target, StringComparison.OrdinalIgnoreCase))
                        return true;

                    if (!string.IsNullOrWhiteSpace(filePath) && filePath.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            catch { }
            return false;
        }

        bool IsProcessLassoDetected()
        {
            try
            {
                string[] names =
                {
                    "ProcessLasso",
                    "ProcessLasso64",
                    "ProcessLassoStarter",
                    "ProcessGovernor",
                    "ProcessGovernor64",
                    "ProcessLassoUI",
                    "Bitsum",
                    "bitsumhighestperformance",
                    "bitsumhighestperformancegui"
                };

                Process[] ps = null;
                try
                {
                    ps = Process.GetProcesses();
                    foreach (var p in ps)
                    {
                        try
                        {
                            string procName = "";
                            try { procName = p.ProcessName ?? ""; } catch { }

                            foreach (var name in names)
                            {
                                if (string.Equals(procName, name, StringComparison.OrdinalIgnoreCase))
                                    return true;
                            }

                            if (!string.IsNullOrWhiteSpace(procName))
                            {
                                string lowerProcName = procName.ToLowerInvariant();
                                if (lowerProcName.Contains("process lasso") ||
                                    lowerProcName.Contains("processlasso") ||
                                    lowerProcName.Contains("process governor") ||
                                    lowerProcName.Contains("processgovernor") ||
                                    lowerProcName.Contains("bitsum"))
                                {
                                    return true;
                                }
                            }

                            string title = "";
                            try { title = p.MainWindowTitle ?? ""; } catch { }
                            if (!string.IsNullOrWhiteSpace(title))
                            {
                                string lowerTitle = title.ToLowerInvariant();
                                if (lowerTitle.Contains("process lasso") || lowerTitle.Contains("processlasso") || lowerTitle.Contains("bitsum"))
                                    return true;
                            }

                            string path = "";
                            try { path = p.MainModule != null ? (p.MainModule.FileName ?? "") : ""; } catch { }
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                string lower = path.ToLowerInvariant();
                                if (lower.Contains("process lasso") || lower.Contains("processlasso") || lower.Contains("bitsum"))
                                    return true;
                            }
                        }
                        catch { }
                    }
                }
                finally
                {
                    if (ps != null) foreach (var p in ps) p.Dispose();
                }

                try
                {
                    using (var sc = new ServiceController("ProcessLasso"))
                        if (sc.Status == ServiceControllerStatus.Running) return true;
                }
                catch { }
                try
                {
                    using (var sc = new ServiceController("ProcessGovernor"))
                        if (sc.Status == ServiceControllerStatus.Running) return true;
                }
                catch { }

                try
                {
                    foreach (var sc in ServiceController.GetServices())
                    {
                        try
                        {
                            if (sc.Status != ServiceControllerStatus.Running)
                                continue;

                            string serviceName = sc.ServiceName ?? "";
                            string displayName = sc.DisplayName ?? "";
                            if (serviceName.IndexOf("ProcessLasso", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                serviceName.IndexOf("ProcessGovernor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                serviceName.IndexOf("Bitsum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                displayName.IndexOf("Process Lasso", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                displayName.IndexOf("ProcessLasso", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                displayName.IndexOf("Bitsum", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return true;
                            }
                        }
                        catch { }
                        finally { sc.Dispose(); }
                    }
                }
                catch { }

                try
                {
                    var psi = new ProcessStartInfo("tasklist.exe", "/v /fo csv /nh")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.Default,
                        StandardErrorEncoding = Encoding.Default
                    };
                    using (var p = Process.Start(psi))
                    {
                        if (p != null)
                        {
                            string output = p.StandardOutput.ReadToEnd();
                            string err = p.StandardError.ReadToEnd();
                            p.WaitForExit(3000);
                            string text = (output ?? "") + "\n" + (err ?? "");
                            string lower = text.ToLowerInvariant();
                            if (lower.Contains("process lasso") || lower.Contains("processlasso") || lower.Contains("processgovernor") || lower.Contains("bitsum"))
                                return true;
                        }
                    }
                }
                catch { }

                string[] paths =
                {
                    @"C:\Program Files\Process Lasso",
                    @"C:\Program Files (x86)\Process Lasso",
                    @"C:\Program Files\Bitsum\Process Lasso",
                    @"C:\Program Files (x86)\Bitsum\Process Lasso",
                    @"C:\Program Files\Bitsum",
                    @"C:\Program Files (x86)\Bitsum"
                };
                foreach (var path in paths)
                    if (Directory.Exists(path))
                        return true;
            }
            catch { }
            return false;
        }

        string NormalizeProcessName(string rawName)
        {
            if (string.IsNullOrWhiteSpace(rawName)) return "";
            var name = rawName.Trim().Trim('"');
            try
            {
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = Path.GetFileNameWithoutExtension(name);
                else if (name.IndexOf('\\') >= 0 || name.IndexOf('/') >= 0)
                    name = Path.GetFileNameWithoutExtension(name);
            }
            catch { }
            return name;
        }

        string NormalizeGuid(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "";
            var text = value.Trim();
            var start = text.IndexOf('{');
            var end = text.IndexOf('}');
            if (start >= 0 && end > start) text = text.Substring(start + 1, end - start - 1);
            text = text.Trim();
            return text.Length == 36 ? text.ToUpperInvariant() : text.ToUpperInvariant();
        }

        PowerPlanInfo ParsePowerPlanLine(string line)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line)) return null;

                var idx = line.IndexOf(':');
                if (idx < 0) return null;

                var tail = line.Substring(idx + 1).Trim();
                if (string.IsNullOrWhiteSpace(tail)) return null;

                string guid = "";
                var match = System.Text.RegularExpressions.Regex.Match(tail, @"(?i)\b[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\b");
                if (match.Success)
                    guid = NormalizeGuid(match.Value);
                else
                {
                    foreach (var part in tail.Split(new[] { ' ', '\t', '【', '】', '[', ']', '(', ')', '*', '\u3000' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var g = NormalizeGuid(part);
                        if (g.Length == 36)
                        {
                            guid = g;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(guid))
                    return null;

                var name = tail;
                var open = tail.LastIndexOf('(');
                var close = tail.LastIndexOf(')');
                if (open >= 0 && close > open)
                {
                    name = tail.Substring(open + 1, close - open - 1).Trim();
                    if (string.IsNullOrWhiteSpace(name))
                        name = tail.Substring(0, open).Trim();
                }
                else
                {
                    name = tail.Replace(guid, "").Replace("*", "").Trim();
                }

                if (string.IsNullOrWhiteSpace(name))
                    name = guid;

                return new PowerPlanInfo { Guid = guid, Name = name };
            }
            catch { return null; }
        }

        System.Collections.Generic.List<string> RunPowerCfg(string args)
        {
            var lines = new System.Collections.Generic.List<string>();
            try
            {
                Encoding encoding = null;
                try
                {
                    encoding = Encoding.GetEncoding(936);
                }
                catch
                {
                    encoding = Encoding.Default;
                }

                var psi = new ProcessStartInfo("powercfg.exe", args)
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = encoding,
                    StandardErrorEncoding = encoding
                };
                using (var p = Process.Start(psi))
                {
                    if (p != null)
                    {
                        string output = p.StandardOutput.ReadToEnd();
                        string error = p.StandardError.ReadToEnd();
                        p.WaitForExit(3000);

                        if (string.IsNullOrWhiteSpace(output) && !string.IsNullOrWhiteSpace(error))
                            output = error;

                        using (var sr = new StringReader(output ?? ""))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                                lines.Add(line);
                        }

                        Log.Info("powercfg " + args + " exit=" + p.ExitCode + ", lines=" + lines.Count + ", encoding=" + encoding.EncodingName);
                    }
                }
            }
            catch (Exception ex) { Log.Error("powercfg execution failed: " + args, ex); }
            return lines;
        }

        PowerPlanInfo GetActivePowerScheme()
        {
            try
            {
                foreach (var line in RunPowerCfg("/getactivescheme"))
                {
                    var plan = ParsePowerPlanLine(line);
                    if (plan != null) return plan;
                }
            }
            catch (Exception ex) { Log.Error("Get active power scheme failed.", ex); }
            return new PowerPlanInfo { Guid = "", Name = "未知" };
        }

        string FindPowerPlanName(string guid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(guid)) return "";
                foreach (var p in _powerPlans)
                    if (string.Equals(p.Guid, guid, StringComparison.OrdinalIgnoreCase))
                        return p.Name ?? guid;
            }
            catch { }
            return string.IsNullOrWhiteSpace(guid) ? "" : guid;
        }

        bool SetActivePowerScheme(string guid)
        {
            try
            {
                guid = NormalizeGuid(guid);
                if (string.IsNullOrWhiteSpace(guid))
                    return false;
                using (var p = Process.Start(new ProcessStartInfo("powercfg.exe", "/setactive " + guid)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true
                }))
                {
                    if (p != null) p.WaitForExit(2000);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Set active power scheme failed: " + guid, ex);
                return false;
            }
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
            s.LastCheck = DateTime.Now;
            try
            {
                using (var sc = new ServiceController("GCUBridge"))
                {
                    s.SvcRunning = sc.Status == ServiceControllerStatus.Running;
                    s.SvcFound = true;
                    s.ProcessRows.Add(new ProcessRowInfo
                    {
                        Name = "GCUBridge 服务",
                        Running = s.SvcRunning,
                        Pid = 0,
                        CpuPercent = 0,
                        MemoryBytes = 0,
                        ResourceText = s.SvcRunning ? "服务运行" : "服务已停止",
                        Detail = s.SvcRunning ? "Windows 服务状态正常" : "Windows 服务未运行"
                    });
                }
            }
            catch
            {
                s.ProcessRows.Add(new ProcessRowInfo
                {
                    Name = "GCUBridge 服务",
                    Running = false,
                    Pid = 0,
                    CpuPercent = 0,
                    MemoryBytes = 0,
                    ResourceText = "未找到",
                    Detail = "未检测到 GCUBridge 服务"
                });
            }

            foreach (var n in PROCS)
            {
                string displayName = n + ".exe";
                var rows = CollectProcessRows(n, displayName);
                s.Procs[displayName] = rows.Count > 0;
                if (rows.Count == 0)
                {
                    s.ProcessRows.Add(new ProcessRowInfo
                    {
                        Name = displayName,
                        Running = false,
                        Pid = 0,
                        CpuPercent = 0,
                        MemoryBytes = 0,
                        ResourceText = "0% / 0 MB",
                        Detail = "未运行"
                    });
                }
                else
                {
                    foreach (var row in rows) s.ProcessRows.Add(row);
                }
            }

            var current = Process.GetCurrentProcess();
            try
            {
                s.ProcessRows.Add(new ProcessRowInfo
                {
                    Name = "管理员权限",
                    Running = Program.IsAdmin(),
                    Pid = current.Id,
                    CpuPercent = GetProcessCpuPercent(current),
                    MemoryBytes = current.WorkingSet64,
                    ResourceText = Program.IsAdmin() ? FormatResource(GetProcessCpuPercent(current), current.WorkingSet64) : "权限受限",
                    Detail = Program.IsAdmin() ? "当前程序已以管理员权限运行" : "当前程序未以管理员权限运行"
                });
            }
            catch { }
            finally { current.Dispose(); }

            s.Blocked = Blocked; s.Total = TotalBlocked; s.SessionKills = SessionKills; s.FileResets = FileResets;
            s.Uptime = DateTime.Now - StartTime;
            s.CpuUsage = GetSystemCpuUsage();
            s.MemoryUsage = GetMemoryUsage();
            s.DiskUsage = GetSystemDriveUsage();
            s.DiskText = s.DiskUsage >= 0 ? s.DiskUsage + "%" : "未知";
            s.AllOK = s.SvcRunning;
            if (Pref.KillGpuProcesses)
                foreach (var kv in s.Procs) if (kv.Value) s.AllOK = false;

            s.PowerAutoSwitch = Pref.PowerAutoSwitch;
            s.PowerTargetProcess = Pref.PowerTargetProcess;
            s.PowerPlanWhenFound = Pref.PowerPlanWhenFound;
            s.PowerPlanWhenMissing = Pref.PowerPlanWhenMissing;
            s.PowerTargetRunning = _powerTargetRunning;
            s.PowerProcessLassoDetected = _powerProcessLassoDetected;
            s.PowerSwitchSkipped = _powerSwitchSkipped;
            s.ActivePowerPlanGuid = _activePowerPlanGuid ?? "";
            s.ActivePowerPlanName = _activePowerPlanName ?? "";
            s.DesiredPowerPlanGuid = _desiredPowerPlanGuid ?? "";
            s.DesiredPowerPlanName = _desiredPowerPlanName ?? "";
            s.PowerPlans = new System.Collections.Generic.List<PowerPlanInfo>(_powerPlans);
            s.LockBestPerformanceMode = Pref.LockBestPerformanceMode;
            s.LockBestPerformanceActive = _lockBestPerformanceActive;
            s.BestPerformancePlanGuid = _bestPerformancePlanGuid ?? "";

            lock (_statusLock) _lastStatus = s;
        }

        System.Collections.Generic.List<ProcessRowInfo> CollectProcessRows(string processName, string displayName)
        {
            var rows = new System.Collections.Generic.List<ProcessRowInfo>();
            Process[] ps = null;
            try
            {
                ps = Process.GetProcessesByName(processName);
                foreach (var p in ps)
                {
                    try
                    {
                        double cpu = GetProcessCpuPercent(p);
                        long memory = p.WorkingSet64;
                        rows.Add(new ProcessRowInfo
                        {
                            Name = displayName,
                            Running = true,
                            Pid = p.Id,
                            CpuPercent = cpu,
                            MemoryBytes = memory,
                            ResourceText = FormatResource(cpu, memory),
                            Detail = "真实进程采样"
                        });
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                if (ps != null) foreach (var p in ps) p.Dispose();
            }
            return rows;
        }

        double GetProcessCpuPercent(Process p)
        {
            try
            {
                var now = DateTime.UtcNow;
                var total = p.TotalProcessorTime;
                ProcessCpuSample last;
                if (!_processCpuSamples.TryGetValue(p.Id, out last))
                {
                    _processCpuSamples[p.Id] = new ProcessCpuSample { Time = now, TotalProcessorTime = total };
                    return 0;
                }

                var seconds = (now - last.Time).TotalSeconds;
                if (seconds <= 0.05) return 0;
                var cpu = (total - last.TotalProcessorTime).TotalMilliseconds / (seconds * 1000.0 * Math.Max(1, Environment.ProcessorCount)) * 100.0;
                _processCpuSamples[p.Id] = new ProcessCpuSample { Time = now, TotalProcessorTime = total };
                if (double.IsNaN(cpu) || double.IsInfinity(cpu)) return 0;
                return Math.Max(0, Math.Min(100, Math.Round(cpu, 1)));
            }
            catch { return 0; }
        }

        string FormatResource(double cpu, long memoryBytes)
        {
            return cpu.ToString("0.0", CultureInfo.InvariantCulture) + "% / " + (memoryBytes / 1024d / 1024d).ToString("0", CultureInfo.InvariantCulture) + " MB";
        }

        int GetSystemCpuUsage()
        {
            try
            {
                FileTime idle, kernel, user;
                if (!GetSystemTimes(out idle, out kernel, out user)) return 0;
                ulong idleNow = ToUInt64(idle);
                ulong kernelNow = ToUInt64(kernel);
                ulong userNow = ToUInt64(user);

                if (!_hasCpuSample)
                {
                    _lastCpuIdle = idleNow; _lastCpuKernel = kernelNow; _lastCpuUser = userNow; _hasCpuSample = true;
                    return 0;
                }

                ulong idleDiff = idleNow - _lastCpuIdle;
                ulong kernelDiff = kernelNow - _lastCpuKernel;
                ulong userDiff = userNow - _lastCpuUser;
                ulong total = kernelDiff + userDiff;

                _lastCpuIdle = idleNow; _lastCpuKernel = kernelNow; _lastCpuUser = userNow;
                if (total == 0) return 0;
                var value = (int)Math.Round((total - idleDiff) * 100.0 / total);
                return Math.Max(0, Math.Min(100, value));
            }
            catch { return 0; }
        }

        int GetMemoryUsage()
        {
            try
            {
                var mem = new MemoryStatusEx();
                mem.dwLength = (uint)Marshal.SizeOf(typeof(MemoryStatusEx));
                if (!GlobalMemoryStatusEx(ref mem)) return 0;
                return (int)Math.Max(0, Math.Min(100, mem.dwMemoryLoad));
            }
            catch { return 0; }
        }

        int GetSystemDriveUsage()
        {
            try
            {
                var root = Path.GetPathRoot(Environment.SystemDirectory);
                if (string.IsNullOrEmpty(root)) return -1;
                var drive = new DriveInfo(root);
                if (!drive.IsReady || drive.TotalSize <= 0) return -1;
                return (int)Math.Round((drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize);
            }
            catch { return -1; }
        }

        static ulong ToUInt64(FileTime ft)
        {
            return ((ulong)ft.HighDateTime << 32) | ft.LowDateTime;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

        [StructLayout(LayoutKind.Sequential)]
        struct FileTime
        {
            public uint LowDateTime;
            public uint HighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MemoryStatusEx
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        public StatusInfo GetStatus()
        {
            lock (_statusLock) return _lastStatus.Clone();
        }
    }

    class ProcessCpuSample
    {
        public DateTime Time;
        public TimeSpan TotalProcessorTime;
    }

    class ProcessRowInfo
    {
        public string Name;
        public bool Running;
        public int Pid;
        public double CpuPercent;
        public long MemoryBytes;
        public string ResourceText;
        public string Detail;
    }

    class PowerPlanInfo
    {
        public string Guid;
        public string Name;
    }

    class StatusInfo
    {
        public bool SvcFound, SvcRunning, AllOK;
        public System.Collections.Generic.Dictionary<string, bool> Procs = new System.Collections.Generic.Dictionary<string, bool>();
        public System.Collections.Generic.List<ProcessRowInfo> ProcessRows = new System.Collections.Generic.List<ProcessRowInfo>();
        public int Blocked, Total, SessionKills, FileResets;
        public int CpuUsage, MemoryUsage, DiskUsage;
        public string DiskText = "未知";
        public DateTime LastCheck;
        public TimeSpan Uptime;
        public bool PowerAutoSwitch, PowerTargetRunning, PowerProcessLassoDetected, PowerSwitchSkipped;
        public string PowerTargetProcess = "";
        public string PowerPlanWhenFound = "";
        public string PowerPlanWhenMissing = "";
        public string ActivePowerPlanGuid = "";
        public string ActivePowerPlanName = "";
        public string DesiredPowerPlanGuid = "";
        public string DesiredPowerPlanName = "";
        public System.Collections.Generic.List<PowerPlanInfo> PowerPlans = new System.Collections.Generic.List<PowerPlanInfo>();
        public bool LockBestPerformanceMode;
        public bool LockBestPerformanceActive;
        public string BestPerformancePlanGuid = "";

        public StatusInfo Clone()
        {
            var s = (StatusInfo)MemberwiseClone();
            s.Procs = new System.Collections.Generic.Dictionary<string, bool>(Procs);
            s.ProcessRows = new System.Collections.Generic.List<ProcessRowInfo>(ProcessRows);
            s.PowerPlans = new System.Collections.Generic.List<PowerPlanInfo>(PowerPlans);
            return s;
        }
    }

}