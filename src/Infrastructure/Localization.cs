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
    static class L
    {
        static bool? _forceZh;
        public static bool Zh { get { return _forceZh.HasValue ? _forceZh.Value : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "zh"; } }
        public static void Toggle() { _forceZh = !Zh; }
        static string S(string zh, string en) { return Zh ? zh : en; }
        public static string T { get { return "MR OSD Shield"; } }
        public static string TV { get { return "MR OSD Shield v" + AppInfo.Version; } }
        public static string Sub { get { return S("机械革命 GPU 控制防护", "MECHREVO GPU Control Shield"); } }
        public static string Home { get { return S("首页", "Home"); } }
        public static string Stat { get { return S("统计", "Stats"); } }
        public static string Set { get { return S("设置", "Settings"); } }
        public static string Det { get { return S("正在检测控制中心", "Detecting control center"); } }
        public static string Act { get { return S("防护已启用", "Protection Active"); } }
        public static string Warn { get { return S("需要注意", "Attention Needed"); } }
        public static string ActSub { get { return S("仅锁定 GPU 超频配置，控制中心功耗控制保持可用", "Only GPU OC config is locked; power controls stay available"); } }
        public static string WaitSub { get { return S("等待 GCUBridge 服务稳定后开始防护", "Waiting for GCUBridge to stabilize"); } }
        public static string WarnSub { get { return S("GPU 超频配置被改动，已修正并重应用小飞机配置", "GPU OC config changed; fixed and reapplied AB profile"); } }
        public static string Svc { get { return S("GCUBridge 服务", "GCUBridge Service"); } }
        public static string Gpu { get { return S("GPU 控制进程", "GPU Control Processes"); } }
        public static string Ses { get { return S("本次修复", "Session Fixes"); } }
        public static string Tot { get { return S("进程拦截", "Process Kills"); } }
        public static string Up { get { return S("运行时间", "Uptime"); } }
        public static string AS { get { return S("开机自启", "Start with Windows"); } }
        public static string BM { get { return S("开机自动最小化到托盘", "Start minimized to tray"); } }
        public static string WT { get { return S("控制中心稳定等待时间", "Control center wait time"); } }
        public static string Sec { get { return S("秒", "s"); } }
        public static string MT { get { return S("关闭窗口时最小化到托盘", "Minimize to tray on close"); } }
        public static string Btn { get { return S("最小化到托盘", "Minimize to Tray"); } }
        public static string Run { get { return S("运行中", "Running"); } }
        public static string Blk { get { return S("已屏蔽", "Blocked"); } }
        public static string Allow { get { return S("已允许", "Allowed"); } }
        public static string Stp { get { return S("已停止", "Stopped"); } }
        public static string NF { get { return S("未找到", "Not Found"); } }
        public static string AR { get { return S("程序已在运行。", "MR OSD Shield is already running."); } }
        public static string TP { get { return S("MR OSD Shield - 已防护", "MR OSD Shield - Protected"); } }
        public static string TW { get { return S("MR OSD Shield - 警告", "MR OSD Shield - Warning"); } }
        public static string TD { get { return S("MR OSD Shield - 检测中", "MR OSD Shield - Detecting"); } }
        public static string TT { get { return S("GPU 防护正在后台运行", "GPU shield is running in the background"); } }
        public static string AB { get { return S("MSI Afterburner 路径", "MSI Afterburner Path"); } }
        public static string CCP { get { return S("控制中心路径", "Control Center Path"); } }
        public static string Kills { get { return S("GPU 超频修复次数", "GPU OC Fixes"); } }
        public static string Resets { get { return S("进程拦截次数", "Process Kills"); } }
        public static string Avg { get { return S("平均修复 / 小时", "Avg Fixes / Hour"); } }
        public static string Log { get { return S("日志路径", "Log Path"); } }
        public static string Open { get { return S("打开", "Open"); } }
        public static string Exit { get { return S("退出", "Exit"); } }
        public static string Lang { get { return Zh ? "中文" : "EN"; } }
        public static string Live { get { return S("实时防护", "Live Shield"); } }
        public static string Core { get { return S("核心状态", "Core Status"); } }
        public static string Quick { get { return S("快捷设置", "Quick Settings"); } }
        public static string Author { get { return S("作者 Sakura", "by Sakura"); } }
        public static string Admin { get { return S("管理员权限", "Administrator"); } }
        public static string AdminOK { get { return S("已启用", "Enabled"); } }
        public static string AdminNO { get { return S("未启用，部分功能可能失败", "Not enabled, some features may fail"); } }
        public static string RestartAdmin { get { return S("以管理员身份重启", "Restart as Admin"); } }
        public static string OpenLogDir { get { return S("打开日志目录", "Open Logs"); } }
        public static string OpenAppDir { get { return S("打开程序目录", "Open Folder"); } }
        public static string ApplyAB { get { return S("重应用小飞机配置", "Apply AB Profile"); } }
        public static string RepairNow { get { return S("立即执行防护修复", "Repair Now"); } }
        public static string ABProfile { get { return S("小飞机 Profile", "AB Profile"); } }
        public static string KillProc { get { return S("兼容旧版强制拦截模式", "Legacy force-block mode"); } }
        public static string KillProcHint { get { return S("不建议开启；开启后可能导致功耗选项不可调", "Not recommended; may disable power controls"); } }
        public static string Choose { get { return S("选择", "Choose"); } }
    }

}
