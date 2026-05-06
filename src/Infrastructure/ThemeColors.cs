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

}
