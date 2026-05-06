# MR OSD Shield

<div align="center">

# MR OSD Shield v1.0.4

**机械革命 GPU 控制防护工具**  
**MECHREVO GPU Control Shield**

让 MSI Afterburner 的 GPU 超频配置保持稳定，同时尽量保留机械革命控制中心的功耗调节能力。

**Author: Sakura**

[![Version](https://img.shields.io/badge/version-v1.0.4-4ade80)](#更新日志)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%2F%2011-60a5fa)](#系统要求)
[![Framework](https://img.shields.io/badge/.NET%20Framework-4.x-8b5cf6)](#编译方法)
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
v1.0.4
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

MR OSD Shield 用于在后台持续监控并修复这些覆盖行为。

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

其中 `N` 由配置项 `AfterburnerProfile` 决定，默认是 `1`。

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
| `KillGpuProcesses` | `false` | 是否启用兼容旧版强制拦截模式；开启后可能导致功耗选项不可调 |

---

## 系统要求

- Windows 10 / 11
- .NET Framework 4.x
- 机械革命控制中心
- MSI Afterburner，可选但推荐
- 管理员权限，推荐

说明：

- 没有安装 MSI Afterburner 时，程序仍可运行。
- 未找到 MSI Afterburner 时，会跳过自动重应用 Profile。
- GPU 配置修复逻辑仍然生效。
- 部分操作如创建任务计划、结束进程、写入控制中心配置可能需要管理员权限。

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
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Shield.cs
```

---

## 发布打包

运行：

```batch
build-release.bat
```

会自动：

1. 编译 `Shield.cs`
2. 创建临时发布目录
3. 复制程序和源码文件
4. 使用 PowerShell `Compress-Archive` 打包
5. 在项目上级目录生成发布包

输出示例：

```text
MROSDShield-v1.0.4.zip
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
├── Shield.cs                  源代码
├── compile.bat                编译脚本
├── build-release.bat          发布打包脚本
├── README.md                  项目说明
├── LICENSE                    开源协议
├── settings.ini               用户设置，运行后自动生成
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
- WinForms 深色界面
- 托盘常驻和开机自启