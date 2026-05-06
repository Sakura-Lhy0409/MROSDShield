# MR OSD Shield

**机械革命 GPU 控制防护工具 | MECHREVO GPU Control Shield**  
**Author: Sakura**

---

## 简介

MR OSD Shield 是一款面向机械革命笔记本用户的轻量级 GPU 控制防护工具。

当前版本：`v1.0.3`

它主要用于阻止机械革命控制中心覆盖 MSI Afterburner 的 GPU 频率 / 超频配置，让 Afterburner 配置保持稳定，同时尽量不影响控制中心的其他功能，例如风扇控制、键盘灯效、性能模式切换等。

软件采用 C# / WinForms 编写，界面为深色极简风格，支持系统托盘常驻、开机自启、开机自动最小化到托盘、可调等待时间、中英文界面切换、运行日志、管理员权限检测和低占用托盘常驻等功能。

---

## 核心功能

- **实时 GPU 超频配置防护**
  - 默认仅锁定 GPU 超频配置字段
  - 默认不再强制结束 `GCUService.exe` / `GCUUtil.exe`
  - 保留控制中心功耗控制、Tj Max、SPL、sPPT、fPPT、Dynamic Boost 等功能可用
  - 可在设置中手动开启“强制结束 GPU 控制进程”兼容旧防护方式
  - 防止控制中心持续覆盖 GPU 超频配置

- **控制中心稳定等待**
  - 自动检测 `GCUBridge` 服务
  - 等待控制中心稳定后再开始防护
  - 等待时间可在设置中调节
  - 默认等待时间为 `15 秒`
  - 可调范围为 `5 ~ 60 秒`

- **MSI Afterburner 配置重应用**
  - 控制中心稳定后自动执行：
    ```text
    MSIAfterburner.exe -profile1
    ```
  - 用于恢复 Afterburner 的 Profile 1 超频配置

- **配置文件保护**
  - 自动检查并归零控制中心 GPU 超频相关字段
  - 修改前自动生成 `.mrosd.bak` 备份
  - 仅修正数字 / 布尔类型字段，避免误改异常 JSON 值
  - 涉及配置：
    - `LiquidHWOC.json`
    - `MainOption.json`

- **系统托盘常驻**
  - 关闭窗口时可最小化到托盘
  - 双击托盘图标可恢复窗口
  - 右键托盘图标可打开或退出程序

- **开机自启**
  - 通过 Windows 任务计划程序实现
  - 支持一键开启 / 关闭
  - 自动关闭任务计划中的电池电源限制
  - 保持当前登录用户运行，避免 SYSTEM 会话导致托盘不可见

- **开机自动最小化到托盘**
  - 可独立配置
  - 开启后，开机自启时自动带 `--minimized` 参数
  - 关闭后，开机自启时显示主窗口

- **中英文界面切换**
  - 默认根据系统语言显示
  - 可点击左侧底部 `中文 / EN` 切换语言

- **管理员权限检测**
  - 首页显示当前是否以管理员权限运行
  - 非管理员运行时提供“以管理员身份重启”入口
  - 便于确保结束进程、修改配置、创建任务计划等功能稳定执行

- **运行日志**
  - 自动写入 `logs/mr_osd_shield.log`
  - 自动轮转为 `logs/mr_osd_shield.old.log`
  - 崩溃日志单独写入 `logs/crash.log`
  - 关键失败路径会记录错误信息，便于排查

- **手动维护工具**
  - 设置页支持打开日志目录
  - 设置页支持打开程序目录
  - 设置页支持手动重应用 MSI Afterburner Profile
  - 设置页支持立即执行一次防护修复

- **极简 UI**
  - 深色主题
  - 左侧导航
  - 状态卡片
  - 低动效、低资源占用设计
  - 托盘隐藏时仅刷新托盘状态
  - 防护引擎支持安静状态自适应降频轮询
  - 托盘隐藏时降低 UI 刷新频率并修剪内存工作集

---

## 界面说明

软件包含三个主要页面：

### 首页

用于查看实时防护状态。

包含：

- 当前防护状态
- `GCUBridge` 服务状态
- `GCUService.exe` 状态
- `GCUUtil.exe` 状态
- 管理员权限状态
- 本次拦截次数
- 总拦截次数
- 运行时间
- 快捷设置
  - 开机自启
  - 关闭窗口时最小化到托盘
- 最小化到托盘按钮

### 统计

用于查看运行统计。

包含：

- 进程拦截次数
- 配置重置次数
- 运行时间
- 平均拦截 / 小时

### 设置

用于查看路径和配置高级选项。

包含：

- MSI Afterburner 路径
- 控制中心路径
- 开机自启
- 开机自动最小化状态栏
- 控制中心稳定等待时间
- 关闭窗口时最小化到托盘
- 日志路径

---

## 工作流程

```text
启动 MR OSD Shield
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
  └── 默认 15 秒，可在设置中调节为 5~60 秒
  │
  ▼
重新应用 MSI Afterburner Profile 1
  │
  ▼
进入实时防护
  │
  ├── 修正 LiquidHWOC.json
  ├── 修正 MainOption.json
  ├── 配置被改动时重应用 MSI Afterburner Profile
  └── 可选强制拦截 GCUService.exe / GCUUtil.exe
```

---

## 不影响的功能

MR OSD Shield 只针对控制中心的 GPU 控制 / GPU 超频覆盖行为进行处理。

通常不影响：

- 风扇控制
- 键盘灯效
- 性能模式切换
- 显示器设置
- 控制中心其他非 GPU 超频功能

---

## 系统要求

- Windows 10 / 11
- .NET Framework 4.x
- 机械革命控制中心
- MSI Afterburner（可选，但推荐）
- 管理员权限

说明：

- 没有安装 MSI Afterburner 时，程序仍可运行。
- 未找到 Afterburner 时，会跳过自动重应用 Profile 1。
- GPU 防护逻辑仍然生效。

---

## 使用方法

### 1. 启动程序

双击运行：

```text
MR_OSD_Shield.exe
```

程序会启动防护引擎并显示主界面。

### 2. 最小化到托盘

点击首页的：

```text
最小化到托盘
```

或关闭窗口时，如果开启了“关闭窗口时最小化到托盘”，程序会隐藏到系统托盘。

### 3. 从托盘恢复

- 双击托盘图标：恢复主窗口
- 右键托盘图标：
  - 打开
  - 退出

### 4. 开启开机自启

进入首页或设置页，打开：

```text
开机自启
```

程序会创建 Windows 任务计划：

```text
MR_OSD_Shield
```

创建任务时程序会自动尝试关闭以下电源限制：

- 只有在计算机使用交流电源时才启动此任务
- 如果计算机改用电池电源，则停止

这样可以避免拔掉电源后，任务计划把 MR OSD Shield 停止。

### 5. 开机自动最小化到托盘

进入设置页，打开：

```text
开机自动最小化状态栏
```

开启后，开机自启任务会使用：

```text
MR_OSD_Shield.exe --minimized
```

关闭后，开机自启任务会使用：

```text
MR_OSD_Shield.exe
```

如果已经开启了开机自启，切换该选项时会自动刷新计划任务。

### 6. 调整等待时间

进入设置页，找到：

```text
控制中心稳定等待时间
```

使用 `-` / `+` 调节秒数。

范围：

```text
5 秒 ~ 60 秒
```

默认：

```text
15 秒
```

---

## 配置文件

程序会在自身目录下生成配置文件：

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

| 字段 | 说明 |
|---|---|
| `BootMin` | 开机自启时是否自动最小化到托盘 |
| `StableSeconds` | 控制中心稳定等待时间，范围 5~60 秒 |
| `MinToTray` | 关闭窗口时是否最小化到托盘 |
| `AfterburnerProfile` | MSI Afterburner Profile 编号，范围 1~5 |
| `AfterburnerPath` | 可选，自定义 MSI Afterburner 路径 |
| `ControlCenterPath` | 可选，自定义机械革命控制中心路径 |
| `KillGpuProcesses` | 是否强制结束 `GCUService.exe` / `GCUUtil.exe`，默认 `false`，关闭时保留控制中心功耗调节 |

---

## 文件结构

```text
MROSDShield/
├── MR_OSD_Shield.exe      主程序
├── Shield.cs              源代码
├── compile.bat            编译脚本
├── build-release.bat      发布打包脚本
├── README.md              项目说明
├── settings.ini           用户设置，运行后自动生成
└── logs/                  日志目录，自动生成
    ├── mr_osd_shield.log      运行日志
    ├── mr_osd_shield.old.log  轮转日志
    └── crash.log              崩溃日志
```

---

## 编译方法

项目使用 .NET Framework C# 编译器。

直接运行：

```batch
compile.bat
```

命令行无暂停编译：

```batch
compile.bat --no-pause
```

或手动执行：

```batch
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Shield.cs
```

发布打包：

```batch
build-release.bat
```

发布包会生成到项目上级目录，例如：

```text
MROSDShield-v1.0.3.zip
```

发布包不包含本机运行日志，日志目录会在程序运行时自动生成。

---

## 常见问题

### Q: 为什么需要等待控制中心稳定？

控制中心刚启动时可能会反复写入 GPU 相关配置。  
如果防护过早介入，可能导致状态反复变化。  
因此程序会先等待 `GCUBridge` 服务稳定，再开始防护。

### Q: 等待时间应该设置多少？

默认 `15 秒` 适合大多数情况。

如果你的控制中心启动很慢，可以调高到：

```text
20 ~ 30 秒
```

如果你的系统启动很快，可以调低到：

```text
5 ~ 10 秒
```

### Q: 开机自启和开机自动最小化有什么区别？

- **开机自启**：控制程序是否随 Windows 登录启动
- **开机自动最小化状态栏**：控制开机启动后是否直接进入托盘

两者是独立设置。

### Q: 关闭窗口后程序是不是退出了？

如果开启了“关闭窗口时最小化到托盘”，关闭窗口只是隐藏到托盘，防护仍在运行。

如果需要真正退出，请右键托盘图标选择：

```text
退出
```

如果程序没有以管理员权限运行，首页会显示管理员权限提醒。点击“以管理员身份重启”后，程序会请求 UAC 提权并重新启动。

### Q: 会影响风扇或键盘灯吗？

通常不会。  
程序主要处理 GPU 控制进程和 GPU 超频配置字段，不针对风扇、灯效或模式切换功能。

### Q: 没有 MSI Afterburner 可以用吗？

可以。  
程序会跳过 Afterburner Profile 重应用步骤，但仍会进行 GPU 控制防护。

### Q: 日志在哪里？

运行日志：

```text
logs/mr_osd_shield.log
```

日志超过约 512 KB 后会自动轮转为：

```text
logs/mr_osd_shield.old.log
```

崩溃日志：

```text
logs/crash.log
```

### Q: 如何卸载？

1. 打开程序
2. 关闭“开机自启”
3. 退出程序
4. 删除整个 `MROSDShield` 文件夹

---

## 更新日志

### v1.0.3

- 改进防护策略：默认不再强制结束 `GCUService.exe` / `GCUUtil.exe`
- 新增 `KillGpuProcesses` 配置，允许手动恢复旧版强制拦截进程模式
- 默认仅修正 GPU 超频相关配置字段，保留控制中心功耗控制功能
- 当检测到 GPU 超频配置被控制中心改动时，自动修正并重应用 MSI Afterburner Profile
- 设置页新增“强制结束 GPU 控制进程”开关
- 更新版本号为 `v1.0.3`

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

### v1.0.0

- 初始版本
- GPU 控制进程防护
- 控制中心配置修正
- MSI Afterburner Profile 1 重应用
- WinForms 深色界面
- 托盘常驻和开机自启

---

## 说明

MR OSD Shield 是一个轻量级个人工具，主要目标是减少机械革命控制中心对 GPU 超频配置的干扰。

请根据自己的设备情况调整等待时间和启动选项。