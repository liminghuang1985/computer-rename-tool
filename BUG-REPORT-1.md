# Bug Report:改名后 UI 顶部不刷新(2026-08-05 用户验收)

## 现象
用户安装 v1.0.0 EXE,点【修改机器名】→ 提示"修改成功" → **没弹"立即重启 / 稍后重启"对话框** → 手动重启电脑 → 顶部"当前机器名"仍显示旧名

## 根因(可能)

### 假设 1:`SetComputerNameExW` 调用方式不对
WPF 调用 `kernel32.dll!SetComputerNameExW` 时可能:
- 用了错误的 `ComputerNameFormat` 枚举值(应该用 `ComputerNamePhysicalDnsHostname = 6` 或 `ComputerNamePhysicalNetBIOS = 5`)
- 缺 `SetLastError=true` 或 `Marshal.GetLastWin32Error()` 后没正确读
- 没在调用前提升进程权限(`SetComputerNameExW` 需要 SE_RESTORE_NAME 权限)
- 没在 `kernel32.dll!SetComputerNameExW` 前调用 `AdjustTokenPrivileges` 提权

### 假设 2:成功提示走错分支
"修改成功"弹了,但**没触发"立即/稍后重启"弹窗**(可能 ViewModel 状态机卡在 `Renaming` 没进 `RebootPrompt`)

### 假设 3:Service / ViewModel 没传 HRESULT 错误
- `Rename()` 失败时返回 `RenameResult.Failed(errCode, msg)`,但 UI 可能吞了错误直接显示"成功"
- 看 `ComputerRenameService.cs` 是否真调了 `SetComputerNameExW`,还是只 mock 了

## 调试步骤(Claude 执行)

### 1. 看 `src/ComputerRenameTool/Services/ComputerRenameService.cs`
确认:
- `SetComputerNameExW` P/Invoke 签名正确
- `ComputerNameFormat` 用了正确枚举
- 错误处理正确(Marshal.GetLastWin32Error())
- **进程权限提升**:如果当前进程不是从 admin token 启动,`SetComputerNameExW` 会 0x80070005 (Access Denied)

### 2. 看 `src/ComputerRenameTool/ViewModels/MainViewModel.cs` + `RenameViewModel.cs`
确认:
- 改名成功后 `RenameVM.IsSubmitSuccess = true` 触发 `MainWindow` 监听
- 弹"立即/稍后重启"对话框逻辑在 MainWindow.xaml.cs 哪里
- UI 顶部"当前机器名"是否用了 binding(`{Binding Computer.CurrentName}`)还是 hardcoded

### 3. 看 `src/ComputerRenameTool/Views/MainWindow.xaml` + `Views/UserControls/ComputerInfoSection.xaml`
- 顶部"当前机器名"是 `<TextBlock Text="{Binding ...}" />` 还是 `<TextBlock Text="..." />` 直接 hardcode?
- `RenameViewModel` 改名成功后,`MainViewModel.Computer.CurrentName` 是否更新?

## 关键检查点

### A. `ComputerRenameService.cs` 加 SE_RESTORE_NAME 提权
MSDN:`SetComputerNameExW` 需要调用进程持有 `SE_RESTORE_NAME` 权限。即使是 admin,进程 token 默认不开启。

参考代码:
```csharp
[DllImport("advapi32.dll", SetLastError = true)]
static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges,
    ref TOKEN_PRIVILEGES NewState, uint BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

[DllImport("advapi32.dll", SetLastError = true)]
static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, ref LUID lpLuid);

[DllImport("kernel32.dll")]
static extern IntPtr GetCurrentProcess();

[DllImport("advapi32.dll", SetLastError = true)]
static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

[StructLayout(LayoutKind.Sequential)]
struct TOKEN_PRIVILEGES { public uint PrivilegeCount; public LUID_AND_ATTRIBUTES Privileges; }

[StructLayout(LayoutKind.Sequential)]
struct LUID { public uint LowPart; public int HighPart; }

[StructLayout(LayoutKind.Sequential)]
struct LUID_AND_ATTRIBUTES { public LUID Luid; public uint Attributes; }

const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
const uint SE_PRIVILEGE_ENABLED = 0x00000002;
const string SE_RESTORE_NAME = "SeRestorePrivilege";

// 调用 Rename 之前先提权
static bool EnablePrivilege()
{
    IntPtr hToken;
    if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES, out hToken)) return false;
    LUID luid = new LUID();
    if (!LookupPrivilegeValue(null, SE_RESTORE_NAME, ref luid)) return false;
    TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES();
    tp.PrivilegeCount = 1;
    // ...
    return AdjustTokenPrivileges(hToken, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
}
```

### B. `ComputerInfoSection.xaml` 检查 binding
确认顶部机器名用了 `{Binding ...}`,并且 `RenameViewModel.SubmitSuccess` 后,`MainViewModel` 会刷新 `Computer.CurrentName`。

可能需要:
- `MainViewModel` 订阅 `RenameVM.SubmitSuccess` 事件
- 改名成功后,重新调 `SystemInfoService.GetComputerInfo()` 拿新机器名
- 触发 `Computer.CurrentName` PropertyChanged

### C. 修复"立即/稍后重启"弹窗
- 在 `MainWindow.xaml.cs` 监听 `RenameVM.IsSubmitSuccess` 或新加一个 `RenameVM.RequestReboot` 事件
- 改名成功后弹 `RebootPromptWindow` 让用户选

## 你的任务
1. 读 `src/ComputerRenameTool/Services/ComputerRenameService.cs` — 确认 P/Invoke 正确
2. 读 `src/ComputerRenameTool/Views/UserControls/ComputerInfoSection.xaml` — 确认机器名 binding
3. 读 `src/ComputerRenameTool/Views/MainWindow.xaml.cs` + `ViewModels/MainViewModel.cs` — 确认改名后状态流转
4. 修复:
   - `SetComputerNameExW` 之前 enable SE_RESTORE_NAME 权限
   - 改名成功后刷新 `MainViewModel.Computer.CurrentName`(重新调 `GetComputerInfo()`)
   - 弹"立即/稍后重启"对话框
5. 加单元测试:mock service 验证 ViewModel 状态流转
6. commit + push(commit msg: `fix(rename): SE_RESTORE_NAME privilege + UI refresh + reboot prompt`)
7. 等 build 通过
8. **直接用 gh CLI 重新下载新 EXE + 更新 v1.0.0 Release 的 EXE 文件**(`gh release upload v1.0.0 ComputerRenameTool.exe --clobber`)
9. 报告给黎明大哥:
   - 改了哪些文件
   - 新 EXE md5
   - Release 链接(同 URL,EXE 已替换)

## 参考之前怎么 update release EXE
hermes 的工作流(CLAUDE.md 规则):"以后这类构建/发布我直接接手"
- git push 触发 build(已配 workflow)
- gh run download 拿 EXE(用 API 路径,直接走 token)
- gh release upload v1.0.0 <exe> --clobber 替换附件

如果 build 又失败,继续调 workflow 直到通过,最后给 EXE 链接。

## 验收
- [ ] 改名后顶部立即显示新名(不等重启)
- [ ] 改名成功后弹"立即/稍后重启"对话框
- [ ] v1.0.0 Release 页面 EXE 已是新文件
- [ ] 报告新 EXE md5
