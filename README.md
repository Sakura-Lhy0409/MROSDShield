# MR OSD Shield

<div align="center">

# MR OSD Shield v1.1

**机械革命 GPU 控制防护工具**  
**MECHREVO GPU Control Shield**

让 MSI Afterburner 的 GPU 超频配置保持稳定，同时尽量保留机械革命控制中心的功耗调节能力。  
`v1.1` 是一次完整的大版本更新：在保留原有 GPU 防护能力的基础上，正式补齐了**控制中心性能模式联动**、**多进程电源计划绑定**、**中文电源计划识别修复**、**GCUBridge 服务状态修复**以及**界面交互稳定性修复**，使整套联动逻辑更适合日常长期后台使用。

**Author: Sakura**

[![Version](https://img.shields.io/badge/version-v1.1-4ade80)](#更新日志)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-60a5fa)](#系统要求)
[![Framework](https://img.shields.io/badge/.NET-8.0-8b5cf6)](#编译方法)
[![License](https://img.shields.io/badge/license-MIT-f59e0b)](./LICENSE)

---

## 支持作者 / Support

如果这个工具帮到了你，欢迎通过爱发电支持后续维护：

# [https://afdian.com/a/LHY0409](https://afdian.com/a/LHY0409)

</div>

---

## 目录

- [项目简介](#项目简介)
- [为什么需要它](#为什么需要它)
- [核心设计目标](#核心设计目标)
- [核心功能](#核心功能)
- [v1.0.4 防护策略说明](#v104-防护策略说明)
- [界面说明](#界面说明)
- [工作流程](#工作流程)
- [配置文件](#配置文件)
- [系统要求](#系统要求)
- [使用方法](#使用方法)
- [编译方法](#编译方法)
- [发布打包](#发布打包)
- [常见问题](#常见问题)
- [文件结构](#文件结构)
- [开源声明](#开源声明)
- [开源协议](#开源协议)
- [更新日志](#更新日志)

---

## 项目简介

MR OSD Shield 是一款面向机械革命笔记本用户的轻量级 Windows 桌面工具。

它的主要目标是：**阻止机械革命控制中心覆盖 MSI Afterburner 的 GPU 频率 / 显存频率 / 超频 Profile 配置**，让小飞机参数保持稳定。

从 `v1.0.5` 开始，MR OSD Shield 进一步强化了笔记本拔电和电池供电场景的防护：当系统从交流电切换到电池、从电池切回交流电，或从挂起状态恢复时，程序会自动重新应用 MSI Afterburner Profile，并执行一次防护修复，避免小飞机参数在断电切换后失效。

同时，MR OSD Shield 从 `v1.0.3` 开始默认采用“功耗兼容模式”，尽量不影响控制中心的其他功能，例如：

- 风扇控制
- 键盘灯效
- 性能模式切换
- Tj Max
- SPL
- sPPT
- fPPT
- Dynamic Boost
- 控制中心其他非 GPU 超频功能

当前版本：

```text
v1.1
```

---

## 为什么需要它

部分机械革命控制中心版本会在启动、切换模式、打开自定义设置或刷新配置时重新写入 GPU 相关参数。

这可能导致：

- MSI Afterburner 的 GPU 核心频率偏移被覆盖
- MSI Afterburner 的显存频率偏移被覆盖
- 小飞机 Profile 已应用，但一段时间后又被控制中心改回
- 开机后需要手动重新点一次小飞机 Profile
- 调整控制中心功耗选项时，GPU 超频配置被顺带覆盖
- 笔记本拔掉电源切换到电池后，小飞机 GPU 参数失效
- Windows 任务计划因为电源条件限制，导致防护程序或 MSI Afterburner 在电池供电时停止 / 不启动

MR OSD Shield 用于在后台持续监控并修复这些覆盖行为。  
`v1.0.5` 针对断电切换问题增加了电源状态监听、任务计划电源条件修复和自动重应用小飞机配置逻辑，确保拔电后防护仍然有效。

---

## 核心设计目标

MR OSD Shield 的设计原则是：

1. **优先保护 MSI Afterburner 的 GPU 超频参数**
2. **默认不强制结束控制中心进程**
3. **默认保留控制中心功耗调节**
4. **只处理必要的 GPU 超频字段**
5. **尽量低占用后台常驻**
6. **必要时自动重新应用小飞机 Profile**
7. **提供旧版强制拦截模式，但不推荐日常开启**

---

## 核心功能

### 实时 GPU 超频配置防护

默认只锁定 GPU 频率 / 显存频率偏移字段：

```text
LiquidHWOC.json:
- CoreFreqOffset
- MemFreqOffset

MainOption.json:
- TurboGPUOCOffset
- TurboSilentGPUOCOffset
```

程序会自动检查这些字段是否被控制中心改写。

如果发现被改写，会：

1. 自动备份配置文件
2. 将相关 GPU 超频偏移字段归零
3. 重新应用 MSI Afterburner Profile
4. 写入运行日志

---

### 功耗兼容模式

默认配置：

```ini
KillGpuProcesses=false
```

默认不会强制结束：

```text
GCUService.exe
GCUUtil.exe
```

这样可以尽量保留控制中心的功耗调节功能，例如：

- Tj Max
- SPL
- sPPT
- fPPT
- Dynamic Boost

---

### 控制中心配置触碰检测

从 `v1.0.4` 开始，即使控制中心触碰 GPU 配置文件但偏移字段仍显示为 `0`，MR OSD Shield 也会重新应用 MSI Afterburner Profile。

这样可以处理一种特殊情况：

```text
控制中心没有明显修改 JSON 偏移字段
但仍然直接下发了 GPU 超频参数
导致小飞机参数被覆盖
```

程序会在检测到配置文件被触碰后自动执行：

```text
MSIAfterburner.exe -profileN
```

其中 `N` 默认由配置项 `AfterburnerProfile` 决定，默认是 `1`。如果开启了 `BatteryProfileSwitch` 且当前处于离电 / 电池供电状态，则会改用 `BatteryAfterburnerProfile`。

---

### 控制中心稳定等待

程序启动后不会立刻进入强防护状态，而是会：

1. 检测 `GCUBridge` 服务
2. 等待控制中心稳定
3. 默认等待 `15 秒`
4. 再重新应用小飞机 Profile
5. 然后进入实时防护

等待时间可调：

```text
5 秒 ~ 60 秒
```

---

### 断电 / 电池供电防护

从 `v1.0.5` 开始，程序会监听 Windows 电源状态变化。

当发生以下情况时：

```text
交流电 → 电池
电池 → 交流电
系统挂起 / 恢复
```

MR OSD Shield 会自动：

1. 延迟重新应用 MSI Afterburner Profile
2. 执行一次 GPU 配置防护修复
3. 修复任务计划中的电池电源限制
4. 尽量避免小飞机 GPU 参数在拔电后掉失

从 `v1.0.9` 开始，设置页新增：

```text
离电自动切换 Profile
离电 Profile
当前供电
当前生效 Profile
```

开启“离电自动切换 Profile”后，当系统检测到笔记本处于电池供电 / 离电状态时，会自动使用独立的 `BatteryAfterburnerProfile` 配置；接入交流电源时则恢复使用普通的 `AfterburnerProfile` 配置。

示例：

```text
小飞机 Profile = 1
离电自动切换 Profile = 开启
离电 Profile = 3
```

此时：

```text
接入电源 → 自动应用 MSIAfterburner.exe -profile1
拔掉电源 → 自动应用 MSIAfterburner.exe -profile3
```

同时程序会自动修复 `MR_OSD_Shield` 和 MSI Afterburner 相关任务计划，关闭：

```text
只在计算机使用交流电源时才启动此任务
如果计算机改用电池电源，则停止
```

这样可以避免笔记本拔掉电源后，Windows 因任务计划电源条件导致防护或小飞机失效。

---

### MSI Afterburner Profile 重应用

控制中心稳定后，程序会自动执行：

```text
MSIAfterburner.exe -profile1
```

如果在设置里调整了 Profile 编号，例如 Profile 2，则会执行：

```text
MSIAfterburner.exe -profile2
```

支持范围：

```text
Profile 1 ~ Profile 5
```

---

### 系统托盘常驻

程序支持托盘后台运行：

- 关闭窗口时可最小化到托盘
- 双击托盘图标恢复窗口
- 右键托盘图标打开菜单
- 托盘隐藏时降低 UI 刷新频率
- 托盘隐藏时定期修剪内存工作集

---

### 开机自启

程序通过 Windows 任务计划程序实现开机自启。

任务名：

```text
MR_OSD_Shield
```

特点：

- 当前登录用户运行
- 最高权限运行
- 避免 SYSTEM 会话导致托盘不可见
- 自动关闭电池电源限制
- 支持开机自动最小化到托盘

---

### 兼容旧版强制拦截模式

设置页中有一个高级开关：

```text
兼容旧版强制拦截模式
```

对应配置：

```ini
KillGpuProcesses=true
```

开启后会强制结束：

```text
GCUService.exe
GCUUtil.exe
```

注意：

> 不建议日常开启。开启后可能导致控制中心功耗选项不可调。

推荐保持默认关闭：

```ini
KillGpuProcesses=false
```

---

## v1.0.4 防护策略说明

`v1.0.4` 的重点是同时解决两个问题：

1. **GPU 超频参数不能被控制中心覆盖**
2. **控制中心功耗选项仍然可以调节**

因此当前策略是：

```text
不修改 HWOCEnable
不默认结束 GCUService.exe / GCUUtil.exe
只修正 GPU 频率 / 显存频率偏移字段
配置文件被触碰时也会重新应用小飞机 Profile
```

### 为什么不再修改 HWOCEnable

旧逻辑中如果把：

```text
HWOCEnable=false
```

可能导致控制中心自定义功耗页面被置灰，表现为：

- SPL 不可调
- sPPT 不可调
- fPPT 不可调
- Dynamic Boost 不可调

因此 `v1.0.4` 不再修改 `HWOCEnable`。

---

## 界面说明

软件包含三个主要页面。

### 首页

用于查看实时防护状态。

包含：

- 当前防护状态
- `GCUBridge` 服务状态
- `GCUService.exe` 状态
- `GCUUtil.exe` 状态
- 管理员权限状态
- 本次 GPU 超频修复次数
- 进程拦截次数
- 运行时间
- 快捷设置
  - 开机自启
  - 关闭窗口时最小化到托盘
- 最小化到托盘按钮

---

### 统计

用于查看运行统计。

包含：

- GPU 超频修复次数
- 进程拦截次数
- 运行时间
- 平均修复 / 小时

---

### 设置

用于查看路径和配置高级选项。

包含：

- MSI Afterburner 路径
- 控制中心路径
- 小飞机 Profile 编号
- 开机自启
- 开机自动最小化到托盘
- 控制中心稳定等待时间
- 关闭窗口时最小化到托盘
- 兼容旧版强制拦截模式
- 日志路径
- 打开日志目录
- 打开程序目录
- 重应用小飞机配置
- 立即执行防护修复

---

## 工作流程

```text
启动 MR OSD Shield
  │
  ▼
读取 settings.ini
  │
  ▼
查找机械革命控制中心路径
  │
  ▼
查找 MSI Afterburner 路径
  │
  ▼
检测 GCUBridge 服务
  │
  ▼
等待控制中心稳定
  │
  └── 默认 15 秒，可调 5~60 秒
  │
  ▼
重新应用 MSI Afterburner Profile
  │
  ▼
进入实时防护
  │
  ├── 监控 LiquidHWOC.json
  ├── 监控 MainOption.json
  ├── 修正 GPU 频率 / 显存频率偏移字段
  ├── 配置被触碰时重应用 MSI Afterburner Profile
  ├── 拔电 / 电池供电切换时自动重应用小飞机 Profile
  ├── 自动修复任务计划电源条件
  └── 可选启用旧版强制拦截模式
```

---

## 配置文件

程序会在自身目录下读取 / 写入：

```text
settings.ini
```

示例：

```ini
BootMin=true
StableSeconds=15
MinToTray=true
AfterburnerProfile=1
AfterburnerPath=
ControlCenterPath=
BatteryProfileSwitch=false
BatteryAfterburnerProfile=1
ControlModeLinkEnabled=false
ControlMode1PowerPlan=
ControlMode2PowerPlan=
ControlMode3PowerPlan=
ControlMode4PowerPlan=
ControlMode1AfterburnerProfile=1
ControlMode2AfterburnerProfile=1
ControlMode3AfterburnerProfile=1
ControlMode4AfterburnerProfile=1
KillGpuProcesses=false
```

字段说明：

| 字段 | 默认值 | 说明 |
|---|---:|---|
| `BootMin` | `true` | 开机自启时是否自动最小化到托盘 |
| `StableSeconds` | `15` | 控制中心稳定等待时间，范围 `5~60` 秒 |
| `MinToTray` | `true` | 关闭窗口时是否最小化到托盘 |
| `AfterburnerProfile` | `1` | MSI Afterburner Profile 编号，范围 `1~5` |
| `AfterburnerPath` | 空 | 可选，自定义 MSI Afterburner 路径 |
| `ControlCenterPath` | 空 | 可选，自定义机械革命控制中心路径 |
| `BatteryProfileSwitch` | `false` | 是否开启离电自动切换 MSI Afterburner Profile |
| `BatteryAfterburnerProfile` | `1` | 离电 / 电池供电时使用的 MSI Afterburner Profile 编号，范围 `1~5` |
| `ControlModeLinkEnabled` | `false` | 是否启用控制中心性能模式联动 |
| `ControlMode1PowerPlan` ~ `ControlMode4PowerPlan` | 空 | 控制中心 `OperatingMode 1 ~ 4` 对应模式（办公、均衡、狂暴、自定义）分别绑定的 Windows 电源计划 GUID，留空表示该模式不联动电源计划 |
| `ControlMode1AfterburnerProfile` ~ `ControlMode4AfterburnerProfile` | `1` | 控制中心 `OperatingMode 1 ~ 4` 对应模式（办公、均衡、狂暴、自定义）分别绑定的 MSI Afterburner Profile 编号，范围 `1~5` |
| `KillGpuProcesses` | `false` | 是否启用兼容旧版强制拦截模式；开启后可能导致功耗选项不可调 |

---

## 系统要求

### 最低配置

| 项目 | 要求 |
|------|------|
| **操作系统** | Windows 10 / Windows 11 |
| **.NET 运行时** | .NET 8.0 Runtime（或 .NET Desktop Runtime 8.0） |
| **WebView2** | Microsoft Edge WebView2 Runtime |
| **权限** | 管理员权限（推荐） |
| **硬盘空间** | 约 50 MB |
| **内存** | 约 50-100 MB（运行时） |

### 软件依赖

| 软件 | 必需性 | 说明 |
|------|--------|------|
| **机械革命控制中心** | 必需 | 程序监控的目标软件 |
| **MSI Afterburner** | 推荐 | 不安装也可运行，但无法重应用 Profile |
| **.NET 8.0 Runtime** | 必需 | [下载地址](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **WebView2 Runtime** | 必需 | 用于渲染程序界面，Windows 11 自带，Windows 10 需手动安装 |

### 硬件要求

- **笔记本品牌**：机械革命（其他品牌未测试）
- **显卡**：NVIDIA 独立显卡（支持 MSI Afterburner）
- **处理器**：无特殊要求

### 技术架构说明

- **界面技术**：WinForms 窗体 + Microsoft Edge WebView2 控件 + HTML/CSS/JavaScript 前端
- **后端逻辑**：.NET 8.0 C# 实现核心防护引擎
- **前后端通信**：通过 WebView2 的 `PostWebMessageAsJson` 和 `WebMessageReceived` 进行双向消息传递
- 没有安装 MSI Afterburner 时，程序仍可运行，但会跳过自动重应用 Profile
- GPU 配置修复逻辑不依赖 MSI Afterburner，仍然生效
- 部分操作如创建任务计划、结束进程、写入控制中心配置需要管理员权限
- WebView2 Runtime 是 Microsoft Edge 浏览器内核，Windows 11 系统自带，Windows 10 需手动安装

---

## 使用方法

### 1. 启动程序

双击运行：

```text
MR_OSD_Shield.exe
```

建议以管理员身份运行。

---

### 2. 设置小飞机 Profile

默认使用：

```text
Profile 1
```

如果你的小飞机超频配置保存在其他 Profile，可以在设置页调整：

```text
小飞机 Profile
```

支持：

```text
1 ~ 5
```

如果你希望笔记本拔掉电源后自动切换到另一套小飞机配置，可以在设置页打开：

```text
离电自动切换 Profile
```

然后调整：

```text
离电 Profile
```

程序会根据当前供电状态自动选择生效配置：

```text
接入电源：使用“小飞机 Profile”
离电 / 电池供电：使用“离电 Profile”
```

---

### 3. 最小化到托盘

点击首页：

```text
最小化到托盘
```

或开启：

```text
关闭窗口时最小化到托盘
```

之后点击关闭按钮时，程序只会隐藏到托盘，防护仍会继续运行。

---

### 4. 开启开机自启

进入首页或设置页，打开：

```text
开机自启
```

程序会创建 Windows 任务计划：

```text
MR_OSD_Shield
```

---

### 5. 开机自动最小化到托盘

进入设置页，打开：

```text
开机自动最小化到托盘
```

开启后，任务计划会使用：

```text
MR_OSD_Shield.exe --minimized
```

关闭后，任务计划会使用：

```text
MR_OSD_Shield.exe
```

---

### 6. 调整控制中心稳定等待时间

进入设置页，找到：

```text
控制中心稳定等待时间
```

使用 `-` / `+` 调节。

推荐：

```text
15 秒
```

如果控制中心启动较慢，可调到：

```text
20 ~ 30 秒
```

---

## 编译方法

项目使用 .NET Framework C# 编译器，不依赖 Visual Studio 工程文件。

直接运行：

```batch
compile.bat
```

无暂停编译：

```batch
compile.bat --no-pause
```

手动编译命令：

```batch
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll @src_files.txt
```

说明：

- `compile.bat` 会自动扫描 `src/` 下所有 `.cs` 文件并生成临时编译列表
- 当前源码已经拆分为后端逻辑、前端 UI 和基础设施三个部分

---

## 发布打包

运行：

```batch
build-release.bat
```

会自动：

1. 编译 `src/` 下所有源码
2. 创建临时发布目录
3. 复制程序、README、LICENSE、编译脚本、`src/` 和 `tools/`
4. 使用 PowerShell `Compress-Archive` 打包
5. 在项目上级目录生成发布包

输出示例：

```text
MROSDShield-v1.1.zip
```

发布包不包含本机运行日志。

---

## 常见问题

### Q: 为什么需要等待控制中心稳定？

控制中心刚启动时可能会反复写入 GPU 相关配置。  
如果防护过早介入，可能导致状态反复变化。

因此程序会先等待 `GCUBridge` 服务稳定，再开始防护。

---

### Q: 功耗调节和 GPU 超频防护会冲突吗？

`v1.0.4` 默认策略就是为了避免冲突。

默认情况下：

```ini
KillGpuProcesses=false
```

并且程序不会修改：

```text
HWOCEnable
```

所以控制中心功耗调节通常可以正常使用。

---

### Q: 兼容旧版强制拦截模式要不要打开？

一般不建议打开。

只有在新防护策略对你的设备无效时，才考虑开启：

```ini
KillGpuProcesses=true
```

开启后可能导致功耗选项不可调。

---

### Q: 关闭窗口后程序是不是退出了？

如果开启：

```text
关闭窗口时最小化到托盘
```

关闭窗口只是隐藏到托盘，防护仍在运行。

如果需要真正退出，请右键托盘图标选择：

```text
退出
```

---

### Q: 没有 MSI Afterburner 可以用吗？

可以。

程序会跳过 Afterburner Profile 重应用步骤，但仍会进行 GPU 控制配置防护。

---

### Q: 日志在哪里？

运行日志：

```text
logs/mr_osd_shield.log
```

日志超过约 `512 KB` 后会自动轮转为：

```text
logs/mr_osd_shield.old.log
```

崩溃日志：

```text
logs/crash.log
```

---

### Q: 如何卸载？

1. 打开程序
2. 关闭“开机自启”
3. 退出程序
4. 删除整个 `MROSDShield` 文件夹

---

## 文件结构

```text
MROSDShield/
├── MR_OSD_Shield.exe          主程序
├── README.md                  项目说明
├── LICENSE                    开源协议
├── compile.bat                编译脚本
├── build-release.bat          发布打包脚本
├── Shield.cs                  迁移说明，旧单文件入口已废弃
├── settings.ini               用户设置，运行后自动生成
├── src/                       当前源码目录
│   ├── App.cs
│   ├── AppInfo.cs
│   ├── Program.cs
│   ├── Backend/
│   │   ├── AutoStart.cs
│   │   ├── Engine.cs
│   │   └── Preferences.cs
│   ├── Frontend/
│   │   ├── MainForm.cs
│   │   └── Controls/
│   │       ├── GlowCard.cs
│   │       └── ToggleSwitch.cs
│   └── Infrastructure/
│       ├── Localization.cs
│       ├── Log.cs
│       └── ThemeColors.cs
├── tools/
│   ├── split_source.ps1
│   └── split_source.py
└── logs/                      日志目录，运行后自动生成
    ├── mr_osd_shield.log      运行日志
    ├── mr_osd_shield.old.log  轮转日志
    └── crash.log              崩溃日志
```

---

## 开源声明

MR OSD Shield 是一个开源项目，主要面向机械革命笔记本用户，用于减少控制中心对 MSI Afterburner GPU 超频配置的干扰。

你可以在遵守开源协议的前提下：

- 查看源码
- 学习实现方式
- 修改源码
- 自行编译
- 基于本项目进行二次开发
- 分享改进建议或修复方案

请注意：

- 本项目为个人工具，不隶属于机械革命、MECHREVO、MSI 或 MSI Afterburner。
- 本项目不包含机械革命控制中心或 MSI Afterburner 的任何代码。
- 使用本工具可能会修改控制中心配置文件，请自行承担使用风险。
- 超频和功耗调节可能带来稳定性、温度、功耗和硬件风险，请根据设备情况谨慎使用。

---

## 开源协议

本项目采用 **MIT License** 开源。

完整协议见：

```text
LICENSE
```

简要说明：

- 允许商业和非商业使用
- 允许复制、修改、分发
- 允许二次开发
- 需要保留原始版权声明和协议文本
- 作者不对使用本项目造成的任何问题承担责任

---

## 支持作者

如果这个项目对你有帮助，欢迎通过爱发电支持作者继续维护：

# [爱发电：Sakura / LHY0409](https://afdian.com/a/LHY0409)

链接：

```text
https://afdian.com/a/LHY0409
```

你的支持可以帮助这个小工具继续更新和适配更多控制中心版本。

---

## 更新日志

### v1.1

- **发布版本升级**
  - 正式发布版本号升级为 `v1.1`
  - 程序内部版本、前端显示版本、打包文件名、README 文档版本号全部统一更新为 `1.1`

- **修复控制中心性能模式联动电源计划不生效**
  - 修复检测到 `Process Lasso` 后错误拦截“控制中心性能模式联动”电源计划切换的问题
  - 现在 `Process Lasso` 仅暂停“按目标进程自动切换电源计划”，不会再阻断“控制中心性能模式联动”
  - 小飞机 Profile 联动与电源计划联动优先级逻辑重新校正，控制中心模式绑定行为更加符合预期

- **修复电源计划中文乱码**
  - 不再单纯依赖 `powercfg /list` 文本解析电源计划名称
  - 改为优先通过 `PowerShell + CIM (Win32_PowerPlan)` 读取电源计划 GUID 与名称
  - 修复中文系统中“平衡”“高性能”“自定义电源计划”等名称乱码问题

- **修复 GCUBridge 服务误报“未找到”**
  - 修复软件内部对 `GCUBridge` 服务状态检测失真，导致前端明明服务正在运行却显示“未找到”的问题
  - 改进 GCUBridge 服务状态采集链路，使首页、状态页和防护引擎判断保持一致

- **修复软件无法启动**
  - 补齐 `WebView2Loader.dll` 运行依赖
  - 修复重新部署后程序因 WebView2 Loader 缺失导致无法打开的问题

- **电源计划自动切换交互大幅优化**
  - 新增“选择 EXE 文件”“打开文件夹批量选择”“清空列表”
  - 现在可直接从程序目录选择一个或多个 `.exe`
  - 多进程绑定策略改为：**命中任意一个进程即触发切换**
  - 自动去重并规范化目标进程名，减少手动输入错误

- **修复保存前被轮询状态覆盖**
  - 修复“电源计划自动切换”界面中，修改设置后尚未点击保存就被 3 秒状态刷新覆盖的问题
  - 修复“控制中心性能模式联动”界面中，小飞机 Profile / 电源计划 / 总开关还没保存就被后端状态回填覆盖的问题
  - 新增本地草稿与待保存状态，只有点击保存后才真正写入后端配置

- **文档更新**
  - README 软件介绍、版本信息、更新日志、发布打包示例已同步更新
  - 当前文档内容与 `v1.1` 发布版本保持一致

---

### v1.0.10

- **新增控制中心性能模式联动**
  - 新增“控制中心性能模式联动”总开关，默认关闭，由用户手动决定是否启用
  - 自动检测机械革命控制中心 `MainOption.json` 中的 `OperatingMode`，并对 4 个实际模式分别建立联动规则
  - 当前已按用户机器上的控制中心实际选项映射为：`1=办公`、`2=均衡`、`3=狂暴`、`4=自定义`
  - 每个模式可单独绑定一个 Windows 电源计划，也可单独绑定一个 MSI Afterburner Profile
  - 未配置绑定项时会自动回退到原有逻辑，不影响现有使用方式

- **联动优先级与兼容逻辑完善**
  - 当启用性能模式联动且当前模式存在绑定时，对应模式绑定的电源计划会优先于原有按进程自动切换电源计划逻辑
  - 对应模式绑定的 MSI Afterburner Profile 会优先于普通 Profile 与离电 Profile 逻辑
  - “锁定最佳性能模式”仍保持最高优先级，避免与已有强制锁定行为冲突
  - 与现有离电自动切换 Profile、电源计划自动切换、防护修复逻辑保持兼容

- **界面与状态展示同步更新**
  - 设置页新增控制中心性能模式联动配置区域，可直接为办公、均衡、狂暴、自定义 4 个模式分别设置电源计划和小飞机 Profile
  - 新增当前控制中心模式与联动状态显示，便于确认实时生效情况
  - 所有新增 UI 均复用现有卡片、下拉框、开关、配色和全局样式，保持与当前界面风格一致

- **配置项更新**
  - 新增 `ControlModeLinkEnabled=false`
  - 新增 `ControlMode1PowerPlan` ~ `ControlMode4PowerPlan`（分别对应办公、均衡、狂暴、自定义）
  - 新增 `ControlMode1AfterburnerProfile=1` ~ `ControlMode4AfterburnerProfile=1`（分别对应办公、均衡、狂暴、自定义）

- 更新版本号为 `v1.0.10`

---

### v1.0.9

- **新增离电自动切换 MSI Afterburner Profile**
  - 新增“离电自动切换 Profile”开关，可在笔记本拔掉电源后自动启用独立的小飞机 Profile
  - 新增“离电 Profile”数值设置，支持 `Profile 1 ~ Profile 5`
  - 接入交流电源时继续使用原有“小飞机 Profile”
  - 离电 / 电池供电时自动切换为 `BatteryAfterburnerProfile`
  - 电源状态切换、系统挂起 / 恢复后会自动重新计算当前生效 Profile 并重应用

- **新增供电状态与生效 Profile 显示**
  - 设置页新增“当前供电”，实时显示“接入电源 / 离电 / 未知”
  - 设置页新增“当前生效 Profile”，方便确认当前实际应用的小飞机配置
  - 首页进程状态中的 MSI Afterburner 行同步显示当前生效 Profile 和供电状态

- **保持现有 UI 风格一致**
  - 新功能 UI 放置在现有“数值设置”卡片内
  - 复用项目已有的开关、步进器、信息行、卡片和全局配色样式
  - 未引入割裂的新控件样式，保持与当前 WebView2 深色界面统一

- **配置项更新**
  - 新增 `BatteryProfileSwitch=false`
  - 新增 `BatteryAfterburnerProfile=1`
  - 保持原有 `AfterburnerProfile` 配置兼容

- 更新版本号为 `v1.0.9`

---

### v1.0.8

- **修复电源计划无法显示的关键 Bug**
  - 修复 `ParsePowerPlanLine` 方法中的括号解析错误：将 `IndexOf` 改为 `LastIndexOf` 查找右括号
  - 解决电源计划名称格式如 "(ayo) *" 时解析失败的问题
  - 修复 `UpdateStatusCache` 方法未将 `_powerPlans` 复制到 `_lastStatus.PowerPlans` 的问题
  - 确保前端能够正确接收并显示电源计划列表
  
- **电源计划数据传输修复**
  - 在 `UpdateStatusCache()` 中添加电源计划列表复制逻辑
  - 使用 `foreach` 循环将 `_powerPlans` 中的每个计划复制到 `_lastStatus.PowerPlans`
  - 确保 StatusInfo 对象包含完整的电源计划数据供前端使用
  
- **问题根源分析**
  - 电源计划 GUID 和名称解析正常，但未传递给前端
  - `_powerPlans` 列表已正确填充，但 `_lastStatus.PowerPlans` 为空
  - 前端下拉框因接收到空列表而显示"未获取电源计划"
  
- 更新版本号为 `v1.0.8`

---

### v1.0.7

- **深度内存优化（重大更新）**
  - 修复内存泄漏问题：从 20+MB 持续增长降低到约 10MB 稳定运行
  - 优化 MainForm 状态刷新：添加 1.5 秒节流和 JSON 缓存对比，避免重复发送相同状态
  - 优化 Engine 状态缓存：改为直接更新对象而非每次创建新对象，减少 80% 对象分配
  - 修复 Process 对象泄漏：确保所有 Process 对象在使用后立即 Dispose
  - 添加定期 GC 回收：每 60 秒执行一次 Gen2 优化回收
  - 优化 UI 刷新间隔：从 1000ms 增加到 2000ms，减少前端对象创建
  - 窗口隐藏时自动调用内存整理，释放未使用内存
  
- **后台性能优化**
  - 优化定时器轮询间隔：初始 800ms，安静状态下自动延长至 2000ms/3000ms
  - 实现状态更新节流：根据轮询间隔动态调整状态缓存更新频率
  - 新增 CPU 采样清理机制：每 30 秒自动清理已退出进程的 CPU 采样数据
  - 优化 Process Lasso 检测：增加 10 秒缓存，减少重复检测开销
  - 优化文件检测间隔：从 1200ms 延长至 1500ms，减少磁盘 I/O
  
- **代码质量改进**
  - 重构 UpdateStatusCache() 方法，复用对象而非每次创建
  - 重构 CollectProcessRows() 方法，确保 Process 对象及时释放
  - 重构 Kill() 方法，优化进程枚举和释放逻辑
  - 添加 finally 块确保资源正确释放
  
- 更新版本号为 `v1.0.7`

---

### v1.0.6

- **全新现代化界面**
  - 采用 WebView2 + HTML/CSS/JavaScript 技术栈重构界面
  - 精致优雅的深色主题设计，告别旧版简陋的原生 WinForms 界面
  - 流畅的动画效果和现代化交互体验
  - 响应式布局，自适应窗口大小
  - 自定义发光卡片控件和开关控件
  
- **新增电源计划管理功能**（v1.0.5 及之前版本不具备）
  - 电源计划自动切换：根据指定进程运行状态自动切换电源计划
  - 锁定最佳性能模式：强制锁定 Windows 电源计划为最佳性能，防止控制中心切换
  - 智能识别最佳性能计划：自动查找"卓越性能"、"高性能"等电源计划
  - Process Lasso 检测：检测到 Process Lasso 时自动跳过电源计划切换，避免冲突
  - 电源计划下拉选择：可视化选择"检测到进程时"和"未检测到进程时"的电源计划
  - 实时状态显示：显示当前激活的电源计划和期望的电源计划
  
- **修复电源计划读取问题**
  - 修复 `powercfg` 命令输出编码导致电源计划无法读取的问题
  - 实现编码容错机制：优先使用 GBK (936)，失败时回退到系统默认编码
  - 解决中文电源计划名称显示乱码的问题
  - 增强日志记录，显示实际使用的编码信息
  
- **项目结构优化**
  - 完全重构项目组织结构，为开源发布做好准备
  - 创建规范的 `.gitignore` 文件，排除编译输出和临时文件
  - 新增 `PROJECT_STRUCTURE.md` 详细说明项目架构和模块职责
  - 清理项目根目录，移除所有编译产物
  - 优化文件组织，源码按后端、前端、基础设施分层
  - 规范化文档说明和编译发布流程
  
- 更新版本号为 `v1.0.6`

---

### v1.0.5

- 完美修复拔掉电源 / 切换到电池供电后防护失效的问题
- 新增 Windows 电源状态变化监听：
  - 插电切换到电池
  - 电池切换到插电
  - 系统挂起 / 恢复
- 检测到电源状态变化后，会自动延迟重应用 MSI Afterburner Profile，并执行一次防护修复
- 强化任务计划电源条件修复：
  - 自动关闭“只在计算机使用交流电源时才启动此任务”
  - 自动关闭“如果计算机改用电池电源，则停止”
  - 同时修复 `MR_OSD_Shield` 自启任务和 MSI Afterburner 相关任务
- 新增 Task Scheduler COM 枚举修复逻辑，解决中文系统下 `schtasks` 输出字段不一致导致小飞机任务识别失败的问题
- 修复任务计划修复逻辑阻塞主界面的问题：
  - 主窗口优先显示
  - 任务计划修复改为后台线程执行
  - `schtasks` / PowerShell 调用增加超时保护
- 优化单实例逻辑：
  - 实例锁从固定名称改为基于程序路径生成，避免不同目录版本互相误判
  - 重复启动时自动唤醒已有窗口，而不是只提示“程序已在运行”
  - 修复托盘隐藏或窗口尚未创建时重复启动无法恢复窗口的问题
- 更新版本号为 `v1.0.5`

### v1.0.4

- 修复功耗调节兼容问题：不再把 `LiquidHWOC.json` 中的 `HWOCEnable` 强制改为 `false`
- 防护范围收窄为 GPU 超频偏移字段：
  - `LiquidHWOC.json`：`CoreFreqOffset`、`MemFreqOffset`
  - `MainOption.json`：`TurboGPUOCOffset`、`TurboSilentGPUOCOffset`
- 默认继续保持 `KillGpuProcesses=false`，不强制结束 `GCUService.exe` / `GCUUtil.exe`
- 目标行为调整为：拦截控制中心覆盖小飞机 GPU 频率参数，同时保留控制中心功耗选项可调节
- 增强防护触发逻辑：当控制中心触碰 GPU 配置文件但偏移字段仍显示为 `0` 时，也会重新应用 MSI Afterburner Profile，避免控制中心直接下发 GPU 超频参数覆盖小飞机设置
- 优化设置页文案：
  - `开机自动最小化状态栏` 改为 `开机自动最小化到托盘`
  - `强制结束 GPU 控制进程` 改为 `兼容旧版强制拦截模式`
  - 增加不建议开启旧版强制拦截模式的风险提示
- 更新版本号为 `v1.0.4`

---

### v1.0.3

- 改进防护策略：默认不再强制结束 `GCUService.exe` / `GCUUtil.exe`
- 默认切换为“功耗兼容模式”，保留控制中心 Tj Max、SPL、sPPT、fPPT、Dynamic Boost 等功耗调节能力
- 默认仅修正 GPU 超频相关配置字段：
  - `LiquidHWOC.json`：`HWOCEnable`、`CoreFreqOffset`、`MemFreqOffset`
  - `MainOption.json`：`TurboGPUOCOffset`、`TurboSilentGPUOCOffset`
- 当检测到 GPU 超频配置被控制中心改动时，自动修正并重应用 MSI Afterburner Profile
- 新增 `KillGpuProcesses` 配置，允许手动恢复旧版强制拦截进程模式
- 设置页新增“兼容旧版强制拦截模式”开关，默认关闭
- 调整首页统计：
  - “本次拦截”改为“本次修复”
  - “总拦截”改为“进程拦截”
- 调整统计页显示逻辑：
  - 优先显示 `GPU 超频修复次数`
  - 单独显示 `进程拦截次数`
  - “平均拦截 / 小时”改为“平均修复 / 小时”
  - 避免默认功耗兼容模式下进程拦截次数一直为 0 造成误解
- 修复设置页右侧维护按钮间距过小导致 UI 重叠 / 畸形的问题
- 更新版本号为 `v1.0.3`

---

### v1.0.2

- 新增版本号常量，窗口标题和日志显示当前版本
- 新增配置文件写入前自动备份 `.mrosd.bak`
- 加强 JSON 修正边界，只处理数字 / 布尔类型字段
- 新增 Engine 自适应轮询，长时间安静时降低检测频率
- 托盘隐藏状态下只刷新托盘状态，减少 UI 控件刷新
- 降低周期性强制内存修剪频率，减少额外 GC 开销
- 新增设置页“打开日志目录”
- 新增设置页“打开程序目录”
- 新增设置页“重应用小飞机配置”
- 新增设置页“立即执行防护修复”
- 新增 `AfterburnerProfile` 配置，支持 Profile 1~5
- 新增 `AfterburnerPath` 和 `ControlCenterPath` 自定义路径配置
- 改进 `compile.bat`，支持 `--no-pause` 和错误码
- 新增 `build-release.bat`，自动编译并生成发布 zip
- 发布包规范化，不打包本机运行日志

---

### v1.0.1

- 修复开机自启后 `--minimized` 被窗口显示逻辑覆盖的问题
- 修复点击右上角关闭按钮后直接退出的问题
- 新增 `MinToTray` 配置，关闭窗口最小化到托盘设置可持久化
- 新增任务计划电源条件自动修复，避免拔电源后任务被停止
- 新增 MSI Afterburner 任务电源条件自动修复尝试
- 新增运行日志 `logs/mr_osd_shield.log`
- 新增管理员权限检测和“以管理员身份重启”
- 优化任务计划创建逻辑，保持当前登录用户运行，避免 SYSTEM 会话托盘不可见
- 优化托盘隐藏时的 UI 刷新频率
- 优化托盘状态内存占用，增加工作集修剪
- 修复托盘图标 `Icon.FromHandle` 潜在 GDI 句柄泄漏

---

### v1.0.0

- 初始版本
- GPU 控制进程防护
- 控制中心配置修正
- MSI Afterburner Profile 1 重应用
- 原生 WinForms 深色界面
- 托盘常驻和开机自启

**注**：v1.0.0 使用原生 WinForms 控件绘制界面。后续版本已升级为 WebView2 + HTML/CSS/JS 现代化界面，提供更好的视觉效果和交互体验。如需使用新界面，请重新编译源码或下载最新发布版本。