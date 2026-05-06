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

}
