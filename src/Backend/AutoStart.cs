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

}
