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

}
