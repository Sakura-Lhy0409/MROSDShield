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

}
