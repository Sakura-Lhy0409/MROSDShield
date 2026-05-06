# 项目结构说明

## 目录组织

```
MROSDShield/
├── .gitignore                 # Git 忽略规则
├── LICENSE                    # MIT 开源协议
├── README.md                  # 项目说明文档
├── PROJECT_STRUCTURE.md       # 本文件：项目结构说明
├── MROSDShield.csproj         # .NET 项目配置文件
├── compile.bat                # 快速编译脚本
├── build-release.bat          # 发布打包脚本
├── app.manifest               # 应用程序清单（管理员权限）
├── app.ico                    # 应用程序图标
├── tray.ico                   # 托盘图标
│
├── src/                       # 源代码目录
│   ├── Program.cs             # 程序入口
│   ├── App.cs                 # 应用程序主类
│   ├── AppInfo.cs             # 版本信息
│   │
│   ├── Backend/               # 后端逻辑
│   │   ├── Engine.cs          # 核心防护引擎
│   │   ├── Preferences.cs     # 配置管理
│   │   └── AutoStart.cs       # 开机自启管理
│   │
│   ├── Frontend/              # 前端界面
│   │   ├── MainForm.cs        # 主窗体
│   │   └── Controls/          # 自定义控件
│   │       ├── GlowCard.cs    # 发光卡片控件
│   │       └── ToggleSwitch.cs # 开关控件
│   │
│   └── Infrastructure/        # 基础设施
│       ├── Log.cs             # 日志系统
│       ├── Localization.cs    # 本地化支持
│       └── ThemeColors.cs     # 主题颜色
│
├── frontend/                  # Web 前端资源
│   ├── index.html             # 主界面 HTML
│   ├── styles.css             # 样式表
│   └── app.js                 # 前端逻辑
│
├── assets/                    # 资源文件
│   ├── app.ico                # 应用图标（源文件）
│   └── tray.ico               # 托盘图标（源文件）
│
├── tools/                     # 开发工具
│   ├── split_source.ps1       # PowerShell 源码拆分工具
│   └── split_source.py        # Python 源码拆分工具
│
├── runtimes/                  # 运行时依赖（NuGet 包）
│   ├── win/                   # Windows 通用
│   ├── win-x86/               # Windows x86
│   ├── win-x64/               # Windows x64
│   └── win-arm64/             # Windows ARM64
│
├── bin/                       # 编译输出（.gitignore）
├── obj/                       # 编译中间文件（.gitignore）
└── logs/                      # 运行日志（.gitignore）
```

## 核心模块说明

### 后端模块 (Backend)

**Engine.cs** - 核心防护引擎
- GPU 配置文件监控
- 控制中心进程管理
- MSI Afterburner Profile 重应用
- 电源状态监听
- 任务计划修复

**Preferences.cs** - 配置管理
- settings.ini 读写
- 配置项验证
- 默认值管理

**AutoStart.cs** - 开机自启
- Windows 任务计划创建/删除
- 电源条件修复
- 任务状态查询

### 前端模块 (Frontend)

**MainForm.cs** - 主窗体
- WebView2 集成
- 前后端消息通信
- 托盘功能
- 窗口状态管理

**Controls/** - 自定义控件
- GlowCard: 发光效果卡片
- ToggleSwitch: 现代化开关控件

### 基础设施 (Infrastructure)

**Log.cs** - 日志系统
- 文件日志记录
- 日志轮转
- 崩溃日志

**Localization.cs** - 本地化
- 多语言支持框架
- 当前仅中文

**ThemeColors.cs** - 主题
- 深色主题配色
- UI 颜色常量

## 编译流程

1. **compile.bat** 扫描 `src/` 目录下所有 `.cs` 文件
2. 使用 .NET 8.0 编译器编译为 `MR_OSD_Shield.exe`
3. 自动复制运行时依赖到项目根目录
4. 生成可直接运行的程序

## 发布流程

1. **build-release.bat** 执行完整编译
2. 创建临时发布目录
3. 复制必要文件：
   - 编译后的 exe 和 dll
   - README.md 和 LICENSE
   - 源代码目录 src/
   - 前端资源 frontend/
   - 资源文件 assets/
   - 编译脚本
   - 开发工具 tools/
4. 打包为 zip 文件
5. 输出到父目录

## 依赖说明

### NuGet 包
- Microsoft.Web.WebView2 (1.0.2792.45)
- System.ServiceProcess.ServiceController (9.0.0)
- System.Diagnostics.EventLog (9.0.0)

### 运行时要求
- .NET 8.0 Runtime
- Microsoft Edge WebView2 Runtime
- Windows 10/11

## 开发建议

### 添加新功能
1. 后端逻辑添加到 `src/Backend/`
2. 前端界面修改 `frontend/` 下的 HTML/CSS/JS
3. 通过 WebView2 消息机制通信

### 调试
- 使用 Visual Studio 或 VS Code
- 或直接运行 `compile.bat` 后测试

### 代码规范
- 使用 C# 标准命名规范
- 保持模块职责单一
- 添加必要的注释

## Git 工作流

### 提交前检查
```bash
# 确保 .gitignore 生效
git status

# 不应包含：
# - bin/ 和 obj/ 目录
# - *.exe, *.dll, *.pdb 等编译输出
# - settings.ini 用户配置
# - logs/ 日志目录
# - *.WebView2/ 缓存目录
```

### 推荐的 .gitignore
项目已包含完整的 `.gitignore` 文件，涵盖：
- 编译输出
- 用户配置
- 运行日志
- IDE 配置
- 临时文件

## 许可证

本项目采用 MIT License 开源。详见 [LICENSE](LICENSE) 文件。