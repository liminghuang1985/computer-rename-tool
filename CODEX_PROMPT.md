# Computer Rename Tool — Codex 委派 Prompt

> **目标**:实现一个 Windows 机器名修改工具(单文件 WPF EXE)
> **PRD**:`docs/PRD-V1.0.md`(必读)
> **Design Spec**:`DESIGN.md`(本目录顶层,17 节,严格按它实现)

---

## 你的任务

按 DESIGN.md §2 项目布局,创建 22 个文件:
- 1 .sln
- 1 .csproj
- 18 .cs/.xaml 实现文件
- 1 README.md
- 1 LICENSE

**不要做的事**(DESIGN.md §15):
- 不引入任何 NuGet 包
- 不实现远程管理 / AD 域 / 历史回溯
- 不做 .NET Framework 兼容
- 不做 Win 7/8 兼容
- 不写多语言资源(仅 zh-CN)
- 不写自动更新
- 不写安装包

---

## 关键约束(从 DESIGN.md)

### 命名规范
- 命名空间:`ComputerRenameTool` (e.g. `ComputerRenameTool.ViewModels`)
- 类名:PascalCase
- 私有字段:`_camelCase`
- 接口:`IPascalCase`
- XAML 控件:`x:Name="PascalCase"`

### .csproj 关键配置
```xml
<PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <LangVersion>latest</LangVersion>
    <AssemblyName>ComputerRenameTool</AssemblyName>
    <RootNamespace>ComputerRenameTool</RootNamespace>
</PropertyGroup>

<PropertyGroup>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PublishReadyToRun>true</PublishReadyToRun>
    <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```

### 5 个核心 Service 接口
1. `ISystemInfoService` — `GetComputerInfo() / GetHardwareInfo()`
2. `IComputerRenameService` — `Rename(string) → RenameResult`
3. `IAdminPrivilegeService` — `IsRunAsAdmin() / RestartAsAdmin()`
4. `IRebootService` — `InitiateReboot(60) / CancelReboot()`
5. `ILogger` — `Info / Warn / Error`

### P/Invoke
- `kernel32.dll!SetComputerNameExW(ComputerNameFormat, string)`
- `ComputerNameFormat.ComputerNamePhysicalDnsHostname = 6`

### Validator
- 正则 `^[A-Za-z0-9\-]+$`
- 最大 15 字符
- 不允许中文/空格/下划线/斜杠/点

### MVVM
- 用 `CommunityToolkit.Mvvm` 的 `ObservableObject` 和 `RelayCommand`?

**不,DESIGN.md §11 说"不引入任何 NuGet 包"**。手写 `ObservableObject` 和 `RelayCommand`:
- `ObservableObject`:实现 `INotifyPropertyChanged`,有 `SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)`
- `RelayCommand`:实现 `ICommand`,接受 `Action execute` 和可选 `Func<bool> canExecute`

### 状态机(RenameViewModel)
5 个 ValidationState:Empty / Invalid / TooLong / SameAsCurrent / Valid

### 异常映射
| HRESULT | 提示 |
|---|---|
| 0x80070005 | 修改失败,请确认具有管理员权限 |
| 0x8007007B | 机器名格式错误 |
| 0x8007089A | 当前网络环境不允许修改机器名 |
| 0x80070015 | 启动重启失败,请手动重启 |

---

## 实施顺序(必须按这个)

1. **.csproj + .sln + App.xaml + MainWindow.xaml 占位**(可编译)
2. **Models**(4 个)
3. **Services 接口 + 实现**(5 个)
4. **Helpers**(Validator + ClipboardHelper)
5. **ViewModels**(4 个)
6. **Views: UserControls**(3 个)
7. **Views: MainWindow 数据绑定**
8. **UAC 提权 + 重启倒计时**(UI + 后台线程)
9. **单元测试**(Validator 必须,ViewModel 可选)
10. **README**

每步完成后**必须**跑:
```bash
cd /Users/cf/Developer/computer-rename-tool
dotnet build src/ComputerRenameTool 2>&1 | tail -20
```

**如果 build 失败,立即修复再继续**。

---

## 验收(必须自检,详见 DESIGN.md §14)

完成前必须确认:
- [ ] `dotnet build` 0 error
- [ ] `dotnet publish -c Release -r win-x64` 产出单文件 EXE < 30 MB
- [ ] 单元测试全部通过(如有)
- [ ] 22 个文件齐全
- [ ] README 含 build/publish/使用说明
- [ ] 0 NuGet 包
- [ ] 命名规范符合 DESIGN.md §3
- [ ] P/Invoke 用 `[DllImport]` 声明
- [ ] 关键方法有 XML doc 注释

---

## 输出

完成后在 `src/ComputerRenameTool/` 下创建完成报告:
- `IMPLEMENTATION_REPORT.md`:列出所有创建的文件 + 行数 + 关键决策

---

## 工作目录
`/Users/cf/Developer/computer-rename-tool`

## 重要工作流
- 不要问问题,直接实现
- 遇到 PRD/DESIGN 不明确的地方,选择最符合"IT 工具"常识的方案
- 每个 .cs/.xaml 写完后**立即** `dotnet build` 验证
- 完成所有文件后跑 `dotnet publish` 验证单文件 EXE

**开始干活。**
