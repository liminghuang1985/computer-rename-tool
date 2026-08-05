# Computer Rename Tool — Design Document

> **Spec for**:Claude Code 子代理实现
> **PRD**:`docs/PRD-V1.0.md`(必读)
> **目标**:产出 23 个文件 + 单文件 EXE 可发布

---

## 1. 范围(Scope)

**实现**:V1.0 全部功能 + V1.0 验收标准
**不实现**:远程管理 / AD 域 / 历史记录回溯 / 自动更新(见 PRD §十二)

---

## 2. 项目布局(Project Layout)

```
computer-rename-tool/
├── docs/
│   └── PRD-V1.0.md                      # 产品需求文档
├── src/                                  # 实现源码
│   ├── ComputerRenameTool.sln
│   ├── ComputerRenameTool/               # 主项目
│   │   ├── ComputerRenameTool.csproj     # .NET 8 WPF 单文件 publish
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── AssemblyInfo.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml
│   │   │   ├── MainWindow.xaml.cs
│   │   │   └── UserControls/
│   │   │       ├── ComputerInfoSection.xaml(.cs)
│   │   │       ├── HardwareInfoSection.xaml(.cs)
│   │   │       └── RenameSection.xaml(.cs)
│   │   ├── ViewModels/
│   │   │   ├── MainViewModel.cs
│   │   │   ├── ComputerInfoViewModel.cs
│   │   │   ├── HardwareInfoViewModel.cs
│   │   │   └── RenameViewModel.cs
│   │   ├── Models/
│   │   │   ├── ComputerInfo.cs
│   │   │   ├── HardwareInfo.cs
│   │   │   ├── RenameRequest.cs
│   │   │   └── RenameResult.cs
│   │   ├── Services/
│   │   │   ├── ISystemInfoService.cs
│   │   │   ├── SystemInfoService.cs       # WMI 实现
│   │   │   ├── IComputerRenameService.cs
│   │   │   ├── ComputerRenameService.cs   # Win32 SetComputerNameExW
│   │   │   ├── IAdminPrivilegeService.cs
│   │   │   ├── AdminPrivilegeService.cs   # UAC + runAs
│   │   │   ├── IRebootService.cs
│   │   │   ├── RebootService.cs           # ExitWindowsEx + 倒计时
│   │   │   ├── ILogger.cs
│   │   │   └── FileLogger.cs              # 写 Logs/rename-tool-YYYY-MM-DD.log
│   │   ├── Helpers/
│   │   │   ├── ComputerNameValidator.cs   # 实时校验规则
│   │   │   ├── ClipboardHelper.cs
│   │   │   └── ToastNotifier.cs           # 开机提醒(后续开机启动检查标记文件)
│   │   └── Resources/
│   │       └── app.ico
│   └── ComputerRenameTool.Tests/          # 单元测试(可选,V1.0 可省)
├── publish/                              # publish 输出(单文件 EXE)
├── README.md
├── LICENSE
└── .gitignore
```

**目标文件数**:18 个 .cs/.xaml + 1 .csproj + 1 .sln + 1 README + 1 LICENSE = **22 个文件**

---

## 3. 命名规范(Conventions)

| 类别 | 规范 | 示例 |
|---|---|---|
| 命名空间 | `ComputerRenameTool` | `ComputerRenameTool.ViewModels` |
| 类名 | PascalCase | `MainViewModel` |
| 方法名 | PascalCase | `GetCurrentNameAsync` |
| 私有字段 | `_camelCase` | `_systemInfo` |
| 属性 | PascalCase | `CurrentName` |
| 常量 | `UPPER_SNAKE` | `MAX_NAME_LENGTH` |
| 接口 | `IPascalCase` | `ISystemInfoService` |
| XAML 控件 | `x:Name="PascalCase"` | `x:Name="RenameButton"` |

---

## 4. 关键类接口(Interface Contracts)

### 4.1 `ISystemInfoService`

```csharp
public interface ISystemInfoService
{
    ComputerInfo GetComputerInfo();
    HardwareInfo GetHardwareInfo();
}
```

**实现要求**:
- WMI 查询用 `ManagementObjectSearcher`
- 显卡:取第一个 `AdapterCompatibility != "Advanced Micro Devices, Inc."` 或指定品牌过滤
- 硬盘:取 `Win32_DiskDrive` 中 `Index == 0`(系统盘)
- **任一字段获取失败抛 `HardwareReadException`,UI 层捕获并显示 "未知"**

### 4.2 `IComputerRenameService`

```csharp
public interface IComputerRenameService
{
    RenameResult Rename(string newName);
}
```

**实现要求**:
- 调用 `Kernel32.SetComputerNameExW` (P/Invoke)
- 入参校验(由调用方保证,这里再校验一次)
- 失败抛 `RenameException(Exception e)`,捕获 HRESULT 返回 `RenameResult.Failed(HRESULT, message)`
- **不重启**(重启是 RebootService 的事)

### 4.3 `IAdminPrivilegeService`

```csharp
public interface IAdminPrivilegeService
{
    bool IsRunAsAdmin();
    bool RestartAsAdmin();  // 返回 true=已提权重启
}
```

**实现要求**:
- `IsRunAsAdmin()`:检查 `WindowsIdentity.GetCurrent().Owner` 是否是 `S-1-5-32-544`
- `RestartAsAdmin()`:`Process.Start(new ProcessStartInfo { Verb = "runas", UseShellExecute = true })`,启动后原进程退出

### 4.4 `IRebootService`

```csharp
public interface IRebootService
{
    void InitiateReboot(int countdownSeconds = 60);
    void CancelReboot();
}
```

**实现要求**:
- `InitiateReboot`:启动 `Shutdown.exe -r -t 60`,UI 显示倒计时
- `CancelReboot`:`Shutdown.exe -a`
- 倒计时归零时自动执行

### 4.5 `ILogger`

```csharp
public interface ILogger
{
    void Info(string message);
    void Warn(string message, Exception? ex = null);
    void Error(string message, Exception? ex = null);
}
```

**实现要求**:
- 写 `Logs/rename-tool-YYYY-MM-DD.log`
- 单文件当天追加,跨天换文件
- 30 天自动清理

---

## 5. ViewModel 状态

### 5.1 `MainViewModel`

```csharp
public class MainViewModel : ObservableObject
{
    public ComputerInfoViewModel Computer { get; }
    public HardwareInfoViewModel Hardware { get; }
    public RenameViewModel Rename { get; }

    public bool IsAdmin { get; }   // 控制修改按钮 enabled
    public string StatusMessage { get; set; }   // 底部状态栏
}
```

### 5.2 `RenameViewModel`(最复杂)

```csharp
public class RenameViewModel : ObservableObject
{
    public string InputName { get; set; }  // 双向绑定
    public ValidationState State { get; set; }  // Valid/Invalid/Same/Unchanged
    public string ValidationMessage { get; set; }
    public bool CanSubmit { get; }  // State == Valid && InputName != CurrentName

    public IRelayCommand SubmitCommand { get; }
    public IRelayCommand UseSuggestedCommand { get; }
    public IRelayCommand CopyCurrentNameCommand { get; }
}
```

`ValidationState` enum:
```csharp
public enum ValidationState { Empty, Invalid, TooLong, SameAsCurrent, Valid }
```

### 5.3 状态机实现

```csharp
// RenameViewModel.OnInputNameChanged
private void OnInputNameChanged(string value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        State = ValidationState.Empty;
        ValidationMessage = "请输入新的机器名";
        CanSubmit = false;
    }
    else if (!ComputerNameValidator.IsValid(value, out var error))
    {
        State = ValidationState.Invalid;
        ValidationMessage = error;
        CanSubmit = false;
    }
    else if (value.Length > 15)
    {
        State = ValidationState.TooLong;
        ValidationMessage = "机器名长度不能超过15个字符";
        CanSubmit = false;
    }
    else if (value == _currentName)
    {
        State = ValidationState.SameAsCurrent;
        ValidationMessage = "机器名未发生变化";
        CanSubmit = false;
    }
    else
    {
        State = ValidationState.Valid;
        ValidationMessage = "";
        CanSubmit = true;
    }
}
```

---

## 6. 数据流(Data Flow)

### 6.1 启动流程

```
App.OnStartup
    ├── 检查管理员权限
    │   └── 无权限 → 弹"是否提权"对话框
    │       ├── 是 → RestartAsAdmin() → 退出当前进程
    │       └── 否 → 继续(只读模式)
    ├── MainViewModel 构造
    │   ├── ComputerInfoVM ← SystemInfoService.GetComputerInfo()
    │   ├── HardwareInfoVM ← SystemInfoService.GetHardwareInfo()
    │   │   └── 任一字段失败 → 显示"未知 (...)",不抛
    │   └── RenameVM.Init(currentName)
    └── MainWindow.Show
```

### 6.2 改名流程

```
用户输入 InputName
    └── RenameVM.OnInputNameChanged → 实时校验更新 State

用户点【修改机器名】
    └── SubmitCommand.CanExecute → 检查 CanSubmit
        └── false → 按钮置灰,不响应
        └── true → 弹确认框
            ├── 取消 → 关弹窗
            └── 确定 → RenameService.Rename(InputName)
                ├── 成功 → 弹"是否重启"对话框
                │   ├── 立即重启 → RebootService.InitiateReboot(60)
                │   └── 稍后重启 → 写"pending reboot"标记 → 工具退出
                └── 失败 → 弹错误对话框(含 HRESULT) → 工具保持
```

---

## 7. 命名规则校验(ComputerNameValidator)

```csharp
public static class ComputerNameValidator
{
    public const int MaxLength = 15;
    private static readonly Regex ValidPattern = new(@"^[A-Za-z0-9\-]+$", RegexOptions.Compiled);

    public static bool IsValid(string name, out string error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            error = "请输入新的机器名";
            return false;
        }
        if (name.Length > MaxLength)
        {
            error = "机器名长度不能超过15个字符";
            return false;
        }
        if (!ValidPattern.IsMatch(name))
        {
            error = "机器名只能包含字母、数字和 \"-\"";
            return false;
        }
        error = "";
        return true;
    }
}
```

**正则说明**:`[A-Za-z0-9\-]+` 不需要 `^$` 包裹,因为 `IsMatch` 默认全匹配

---

## 8. P/Invoke 声明(ComputerRenameService)

```csharp
[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
private static extern bool SetComputerNameExW(
    ComputerNameFormat NameType,
    string lpBuffer
);

[DllImport("kernel32.dll", SetLastError = true)]
private static extern ErrorCode GetLastError();

[Flags]
private enum ComputerNameFormat
{
    ComputerNamePhysicalNetBIOS = 5,
    ComputerNamePhysicalDnsHostname = 6
}
```

**使用**:
```csharp
public RenameResult Rename(string newName)
{
    try
    {
        bool ok = SetComputerNameExW(ComputerNameFormat.ComputerNamePhysicalDnsHostname, newName);
        if (!ok)
        {
            int err = Marshal.GetLastWin32Error();
            return RenameResult.Failed(err, $"HRESULT 0x{err:X8}");
        }
        return RenameResult.Success(newName);
    }
    catch (Exception ex)
    {
        return RenameResult.Failed(-1, ex.Message);
    }
}
```

---

## 9. 启动 EXE 后的 `csproj` 关键配置

```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <AssemblyName>ComputerRenameTool</AssemblyName>
    <RootNamespace>ComputerRenameTool</RootNamespace>
    <ApplicationIcon>Resources/app.ico</ApplicationIcon>
</PropertyGroup>

<PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>true</PublishReadyToRun>
    <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

**Publish 命令**:
```bash
dotnet publish src/ComputerRenameTool -c Release -r win-x64 -o publish/
```

**目标产物**:`publish/ComputerRenameTool.exe`(单文件,约 30 MB)

---

## 10. 测试范围(V1.0 必做)

### 10.1 单元测试(轻量,V1.0 至少覆盖)

`ComputerNameValidatorTests`:
- `IsValid("ABC")` → true
- `IsValid("ABC-123")` → true
- `IsValid("ABC DEF")` → false(空格)
- `IsValid("ABC_DEF")` → false(下划线)
- `IsValid("机器")` → false(中文)
- `IsValid("A.B")` → false(点)
- `IsValid("")` → false
- `IsValid(new string('A', 16))` → false(超长)
- `IsValid(new string('A', 15))` → true

`RenameViewModelTests`(可选):
- 输入合法名 → CanSubmit = true
- 输入空 → CanSubmit = false
- 输入当前名 → CanSubmit = false,Message = "机器名未发生变化"

### 10.2 集成测试(手工,V1.0 必做)

| 场景 | 期望 |
|---|---|
| Win 11 启动 EXE | 看到电脑信息 + 修改区域 |
| 输入 "DESKTOP-ABC123" | 提示"机器名未发生变化" |
| 输入 "valid-name" | 提示通过 |
| 输入 "invalid name" | 提示"机器名只能包含字母、数字和 \"-\"" |
| 点修改 + 确认 | 改名成功 + 弹重启选择 |
| 选立即重启 + 等 60 秒 | 系统重启 |
| 重启后查看 | 机器名已变更 |
| 选稍后重启 | 工具退出,下次开机弹提醒 |

---

## 11. 依赖清单(Dependencies)

**外部 NuGet 包**:**0 个**(自写,不引入任何第三方库)

**运行时**:.NET 8 Runtime(用户机器需装,或用 `SelfContained=true` 自带)

**系统 API 调用**(P/Invoke):
- `kernel32.dll!SetComputerNameExW`
- `kernel32.dll!GetLastError`
- `advapi32.dll!GetTokenInformation`(可选,用于管理员检查)
- `ShellExecute`(通过 `Process.Start` 的 `runAs` verb)

---

## 12. 文件大小预算

| 类别 | 大小 |
|---|---|
| .NET 8 Runtime (SelfContained) | ~28 MB |
| 应用代码 | ~200 KB |
| 资源 (icon) | ~50 KB |
| **总计** | **~28-30 MB** |

如超过 30 MB,关闭 `PublishReadyToRun`(可省 5 MB)。

---

## 13. 实施步骤(Implementation Steps)

1. **骨架**:建 .csproj + .sln + App.xaml + MainWindow.xaml 占位(可运行)
2. **Models**:ComputerInfo / HardwareInfo / RenameRequest / RenameResult
3. **Services 接口**:5 个 IService 接口
4. **Services 实现**:SystemInfo / ComputerRename / AdminPrivilege / Reboot / FileLogger
5. **Helpers**:ComputerNameValidator / ClipboardHelper
6. **ViewModels**:4 个 ObservableObject
7. **Views**:MainWindow + 3 个 UserControl
8. **绑定**:在 MainWindow.xaml 中连接 ViewModel + 双向绑定
9. **UAC 提权**:AdminPrivilegeService
10. **重启倒计时**:RebootService + UI 倒计时线程
11. **单元测试**:Validator + ViewModel
12. **publish**:dotnet publish 单文件 EXE

---

## 14. 验收清单(子代理自检)

完成实现后,Claude Code 子代理必须验证:

- [ ] `dotnet build` 0 error 0 warning
- [ ] `dotnet publish` 产出单文件 EXE < 30 MB
- [ ] 单元测试全部通过
- [ ] 22 个文件齐全
- [ ] README 包含 build / publish / 使用说明
- [ ] 没有外部 NuGet 依赖(0 包)
- [ ] 命名规范符合本文 §3
- [ ] 所有 P/Invoke 用 `[DllImport]` 声明
- [ ] 关键方法有 XML doc 注释

---

## 15. 不做的事(再次强调)

❌ 不引入任何 NuGet 包
❌ 不写远程管理 / AD 域 / 历史回溯代码
❌ 不做 .NET Framework 兼容(仅 .NET 8)
❌ 不做 Windows 7 / 8 兼容(PRD 明确 Win 10/11)
❌ 不写多语言资源(仅 zh-CN)
❌ 不写自动更新检查
❌ 不写 GUI 测试(Playwright/WinAppDriver)— V1.0 用手测
❌ 不写安装包(msi/inno setup)— V1.0 单文件 EXE 即可

---

## 16. 风险与回退

| 风险 | 概率 | 应对 |
|---|---|---|
| WMI 在某些精简版 Win 10 不可用 | 低 | 改用 `System.Environment` + `Microsoft.VisualBasic.Devices.ComputerInfo` |
| `SetComputerNameExW` 在域控机器需要更多权限 | 中 | 异常映射表已有 Network 错误码,UI 提示 |
| SelfContained EXE > 30 MB | 低 | 关闭 ReadyToRun |
| 改名后不重启,程序状态错乱 | 中 | 改名成功后强制退出,防止脏数据 |

---

## 17. 交付物(Deliverables)

| 文件 | 说明 |
|---|---|
| `src/ComputerRenameTool.sln` | Solution |
| `src/ComputerRenameTool/*.cs(xaml)` | 18 个实现文件 |
| `publish/ComputerRenameTool.exe` | 单文件可执行 |
| `README.md` | 使用 + build 文档 |
| `docs/PRD-V1.0.md` | 需求文档(已存在) |
| `DESIGN.md` | 本文件 |

**总代码量预算**:~1500-2000 行(简单工具,不要膨胀)

---

**Spec end. 开工.**
