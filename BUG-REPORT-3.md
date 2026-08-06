# BUG-REPORT-3:改名实际没生效(用户复现,2026-08-05)

## 现象(用户最新反馈)
- 重启了 EXE 覆盖安装新版本(MD5 `4d1645af9cb7f0e49e5dc07861945cac`)
- 字段都正常显示了
- 输入新机器名 → 点【修改机器名】→ 弹"修改成功" → **没弹"立即/稍后重启"** → 重启设备 → **机器名没变**

## 关键问题:之前 2 个 fix 都只修 UI,没修改名的核心逻辑
- BUG-REPORT-1 修了:SE_RESTORE_NAME 提权、UI 刷新、重启弹窗 — **这些"看起来"在动但实际没改名**
- BUG-REPORT-2 修了:DataContext、按钮布局、单元测试 — **纯粹 UI bug**
- **真正改名的 P/Invoke 调用还是有问题**

## 你要重点排查的代码

### 1. `src/ComputerRenameTool/Services/ComputerRenameService.cs`

**检查清单**:
- [ ] `SetComputerNameExW` P/Invoke 签名是否正确
- [ ] 用的 `ComputerNameFormat` 枚举值(应该是 `ComputerNamePhysicalDnsHostname = 6` 或 `ComputerNamePhysicalNetBIOS = 5`)
- [ ] **SE_RESTORE_NAME 提权是否真的 enable 成功**(返回值要查)
- [ ] 调用返回 `false` 时是否真的 `Marshal.GetLastWin32Error()` 读错误码
- [ ] 调用是否在 Try / Catch 里(异常可能被吞)

**MSDN 真相**:
> `SetComputerNameExW` 不只是需要 admin,**必须有 `SE_RESTORE_NAME` privilege** 显式 enable。
> 但 WPF 进程 token 默认**不** enable 这个 privilege,即使你"以管理员身份运行"。
>
> 提权需要:
> 1. `OpenProcessToken` 拿当前进程 token
> 2. `LookupPrivilegeValue(null, "SeRestorePrivilege", ...)` 拿 LUID
> 3. `AdjustTokenPrivileges(token, false, {PrivilegeCount=1, {LUID, SE_PRIVILEGE_ENABLED}}, ...)`
> 4. 检查 `GetLastError()` 不是 `ERROR_NOT_ALL_ASSIGNED` (1300)

**之前 fix 的实现可能漏了第 4 步** — `AdjustTokenPrivileges` 返回 `true` 但 `GetLastError() == 1300` 表示**提权失败**(虽然返回 true),导致 `SetComputerNameExW` 仍然报 `ERROR_ACCESS_DENIED (5)`。

### 2. WPF 调用时是否真的进了 `Rename()` 方法
**检查**:
- [ ] `MainViewModel.cs` 改名时是否 **真**调了 `_renameService.Rename(newName)`,而不是只弹"成功"消息
- [ ] `_renameService.Rename()` 返回的 `RenameResult` 字段 `IsSuccess` / `ErrorCode` 是什么
- [ ] `RenameViewModel.OnSubmit()` 是否真 await 了 async,有没有 fire-and-forget 漏掉异常

### 3. 改名后是否触发了"立即/稍后重启"弹窗
**检查**:
- [ ] `MainWindow.xaml.cs` 监听 `RenameCompleted` 事件的代码是否真执行
- [ ] `_rebootPromptShown` flag 是否被提前 set 阻止弹窗
- [ ] `RebootPromptWindow.ShowDialog()` 是否被调用

**可能原因**:如果 `RenameCompleted` 事件没触发(因为 RenameService 抛异常没成功),弹窗当然不弹。

### 4. 进程权限
**问题根因猜测**:
- WPF .NET 8 self-contained EXE 是 `WinExe` 编译
- 即使 UAC 提权,**token 还是 admin user token 不是 SYSTEM token**
- 改机器名需要 `SE_RESTORE_NAME` + 改注册表(HKLM)权限
- **某些 Windows 版本需要 SYSTEM token 才能改机器名** — 这就是为什么 UAC 提权了还是改不了

**Workaround 选项**:
- A. 让 `RebootService.InitiateReboot()` 调 `shutdown.exe` 前先 `taskkill /im explorer.exe` + 重新启 explorer(这个不改机器名,但确认 explorer 没在锁)
- B. 改用 `SetComputerName` API(老的,可能比 `SetComputerNameEx` 兼容)— 实际不是,SetComputerName 调的就是 SetComputerNameEx
- C. **改用 WMI 改机器名**:`Win32_ComputerSystem.Rename("新名")` — 这个用 admin token 就能改
- D. **调用 `NetServerComputerNameDel/Add` API** — 用于 AD 集成场景
- E. **改成"重命名 + 立即重启"** — 用 shutdown.exe -r 触发重启,**重启过程**会 commit 改名(Windows 内部机制)

## 调试步骤(你必跑)

### 1. 加详细日志
在 `ComputerRenameService.Rename()` 加日志:
```csharp
public RenameResult Rename(string newName)
{
    try
    {
        // 1. 提权
        bool privOk = EnablePrivilege("SeRestorePrivilege");
        int privErr = Marshal.GetLastWin32Error();
        _logger.Info($"Privilege SeRestorePrivilege: enabled={privOk}, lastError={privErr} (0x{privErr:X})");
        
        if (privErr == 1300)  // ERROR_NOT_ALL_ASSIGNED
        {
            _logger.Error("提权失败,可能进程不是以 admin 启动,或 UAC 提权没真正成功");
        }
        
        // 2. 改名
        bool ok = SetComputerNameExW(ComputerNameFormat.ComputerNamePhysicalDnsHostname, newName);
        int winErr = Marshal.GetLastWin32Error();
        _logger.Info($"SetComputerNameExW: success={ok}, lastError={winErr} (0x{winErr:X})");
        
        if (!ok)
        {
            return RenameResult.Failed(winErr, $"HRESULT 0x{winErr:X8}");
        }
        return RenameResult.Success(newName);
    }
    catch (Exception ex)
    {
        _logger.Error("Rename exception", ex);
        return RenameResult.Failed(-1, ex.Message);
    }
}
```

### 2. 跑 EXE 后看 `Logs/rename-tool-YYYY-MM-DD.log`
应该看到类似:
```
2026-08-05 15:30:01
启动程序: Admin
启动用户: Administrator

2026-08-05 15:30:10
修改机器名:
  DESKTOP-ABC123
  → BJ-IT-001
Privilege SeRestorePrivilege: enabled=True, lastError=0
SetComputerNameExW: success=False, lastError=5 (0x5)  ← 0x5 = ERROR_ACCESS_DENIED
```

如果看到 `success=False, lastError=5` — **就是 UAC 提权没真正拿到 SE_RESTORE_NAME**。

### 3. 真正的修法(改用 WMI 路径)

**`SetComputerNameExW` 在 Win10/11 上需要 SYSTEM token,普通 admin 不够**。改用 WMI:

```csharp
// 需要 NuGet? 不,可以用 System.Management
// 但需要 .csproj 加 <UseWPF>true</UseWPF> + Reference Include="System.Management"
// 或者在 WindowsBase 里直接 using System.Management;

using System.Management;

public RenameResult RenameViaWmi(string newName)
{
    try
    {
        // WMI 路径:Win32_ComputerSystem.Rename 需要 admin,但不需要 SE_RESTORE_NAME
        var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_ComputerSystem");
        foreach (ManagementObject obj in searcher.Get())
        {
            var result = obj.InvokeMethod("Rename", new object[] { newName });
            int retCode = Convert.ToInt32(result["ReturnValue"]);
            if (retCode == 0)
            {
                _logger.Info("WMI Rename 成功");
                return RenameResult.Success(newName);
            }
            else
            {
                _logger.Error($"WMI Rename 失败, ReturnValue={retCode}");
                return RenameResult.Failed(retCode, $"WMI ReturnValue={retCode}");
            }
        }
        return RenameResult.Failed(-1, "找不到 Win32_ComputerSystem");
    }
    catch (Exception ex)
    {
        _logger.Error("WMI Rename 异常", ex);
        return RenameResult.Failed(-1, ex.Message);
    }
}
```

**.csproj 要加**:
```xml
<ItemGroup>
    <Reference Include="System.Management" />
</ItemGroup>
```

(System.Management 是 .NET Framework 内置,net8.0-windows TFM 默认应该可用,但需要 explicit reference)

### 4. 备选:用 `rename computer` 命令

如果 WMI 也不行(老 Windows),用 `netdom renamecomputer`(需要 AD 工具) 或 `Rename-Computer` PowerShell:

```csharp
// PowerShell 方式
public RenameResult RenameViaPowerShell(string newName)
{
    var psi = new ProcessStartInfo
    {
        FileName = "powershell.exe",
        Arguments = $"-Command \"Rename-Computer -NewName '{newName}' -Force -Restart\"",
        UseShellExecute = false,  // 必须 false 才能 RedirectStandardOutput
        CreateNoWindow = true,
        Verb = "runas"  // 触发 UAC
    };
    using var proc = Process.Start(psi);
    proc.WaitForExit();
    return proc.ExitCode == 0 
        ? RenameResult.Success(newName) 
        : RenameResult.Failed(proc.ExitCode, $"PowerShell exit code {proc.ExitCode}");
}
```

**优势**:
- `Rename-Computer` 内置 `SE_RESTORE_NAME` 提权(用 `Restart-Computer` 流程)
- `-Force` 跳过确认
- `-Restart` 立即重启
- 是微软**官方**改名方式

**但有坑**:
- 需要 PowerShell 进程权限足够(进程本身要 admin)
- 输出捕获不容易

## 你的任务

**最简路径**(我推荐):
1. **改用 WMI 路径**(`Win32_ComputerSystem.Rename`)— **不用改 SE_RESTORE_NAME 提权代码**
2. 如果 WMI 也不行,**改用 PowerShell 路径**(`Rename-Computer` cmdlet)
3. 删掉失败的 P/Invoke 路径(或保留作 fallback)
4. 加详细日志(`Logs/rename-tool-YYYY-MM-DD.log`)
5. **加单元测试** 用 `Win32_ComputerSystem` mock 验证 WMI 路径被调用
6. commit + push
7. 等 build 通过
8. gh release upload v1.0.0 <新 EXE> --clobber
9. 报告新 EXE md5

## 验收
- [ ] 启动 EXE → 改名 → 弹"立即/稍后重启"(或 PowerShell 直接重启)
- [ ] 重启后机器名真的变了(`hostname` 命令 或 系统设置查看)
- [ ] log 文件记录每次改名的 attempted + result

## 不要忘
- 改完后**用 GitHub Actions windows runner 跑测试**(`dotnet test` — 但 WMI 需要 Windows 真实环境,可能 skip)
- **重点保证 真实 Windows 环境下改名生效**
- 之前 fix 都只是 UI 看起来对了,这次必须确认实际改名真的写入 Windows
