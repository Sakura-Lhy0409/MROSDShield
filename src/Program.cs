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

}
