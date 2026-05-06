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

        public static bool PowerAutoSwitch
        {
            get { return ReadValue("PowerAutoSwitch", "false").Equals("true", StringComparison.OrdinalIgnoreCase); }
            set { WriteValue("PowerAutoSwitch", value ? "true" : "false"); }
        }

        public static string PowerTargetProcess
        {
            get { return ReadValue("PowerTargetProcess", ""); }
            set { WriteValue("PowerTargetProcess", value ?? ""); }
        }

        public static string PowerPlanWhenFound
        {
            get { return ReadValue("PowerPlanWhenFound", ""); }
            set { WriteValue("PowerPlanWhenFound", value ?? ""); }
        }

        public static string PowerPlanWhenMissing
        {
            get { return ReadValue("PowerPlanWhenMissing", ""); }
            set { WriteValue("PowerPlanWhenMissing", value ?? ""); }
        }

        public static string PowerLastApplied
        {
            get { return ReadValue("PowerLastApplied", ""); }
            set { WriteValue("PowerLastApplied", value ?? ""); }
        }

        public static bool LockBestPerformanceMode
        {
            get { return ReadValue("LockBestPerformanceMode", "false").Equals("true", StringComparison.OrdinalIgnoreCase); }
            set { WriteValue("LockBestPerformanceMode", value ? "true" : "false"); }
        }
    }

}