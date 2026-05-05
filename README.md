# MR OSD Shield

**机械革命 GPU 控制防护工具 | MECHREVO GPU Control Shield**  
**Author: Sakura**

---

## 简介

MR OSD Shield 是一款面向机械革命笔记本用户的轻量级 GPU 控制防护工具。

它主要用于阻止机械革命控制中心覆盖 MSI Afterburner 的 GPU 频率 / 超频配置，让 Afterburner 配置保持稳定，同时尽量不影响控制中心的其他功能，例如风扇控制、键盘灯效、性能模式切换等。

软件采用 C# / WinForms 编写，界面为深色极简风格，支持系统托盘常驻、开机自启、开机自动最小化到托盘、可调等待时间、中英文界面切换等功能。

---

## 核心功能

- **实时 GPU 防护**
  - 自动拦截 `GCUService.exe`
  - 自动拦截 `GCUUtil.exe`
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

- **开机自动最小化到托盘**
  - 可独立配置
  - 开启后，开机自启时自动带 `--minimized` 参数
  - 关闭后，开机自启时显示主窗口

- **中英文界面切换**
  - 默认根据系统语言显示
  - 可点击左侧底部 `中文 / EN` 切换语言

- **极简 UI**
  - 深色主题
  - 左侧导航
  - 状态卡片
  - 低动效、低资源占用设计

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
  ├── 拦截 GCUService.exe
  ├── 拦截 GCUUtil.exe
  ├── 修正 LiquidHWOC.json
  └── 修正 MainOption.json
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
```

字段说明：

| 字段 | 说明 |
|---|---|
| `BootMin` | 开机自启时是否自动最小化到托盘 |
| `StableSeconds` | 控制中心稳定等待时间，范围 5~60 秒 |

---

## 文件结构

```text
MROSDShield/
├── MR_OSD_Shield.exe      主程序
├── Shield.cs              源代码
├── compile.bat            编译脚本
├── README.md              项目说明
├── settings.ini           用户设置，运行后自动生成
└── logs/                  日志目录，自动生成
    └── crash.log          崩溃日志
```

---

## 编译方法

项目使用 .NET Framework C# 编译器。

直接运行：

```batch
compile.bat
```

或手动执行：

```batch
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:MR_OSD_Shield.exe /platform:anycpu /optimize+ /nologo /reference:System.ServiceProcess.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Shield.cs
```

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

### Q: 会影响风扇或键盘灯吗？

通常不会。  
程序主要处理 GPU 控制进程和 GPU 超频配置字段，不针对风扇、灯效或模式切换功能。

### Q: 没有 MSI Afterburner 可以用吗？

可以。  
程序会跳过 Afterburner Profile 重应用步骤，但仍会进行 GPU 控制防护。

### Q: 如何卸载？

1. 打开程序
2. 关闭“开机自启”
3. 退出程序
4. 删除整个 `MROSDShield` 文件夹

---

## 说明

MR OSD Shield 是一个轻量级个人工具，主要目标是减少机械革命控制中心对 GPU 超频配置的干扰。

请根据自己的设备情况调整等待时间和启动选项。