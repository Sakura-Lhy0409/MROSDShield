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
