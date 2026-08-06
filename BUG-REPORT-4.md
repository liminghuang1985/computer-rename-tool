# BUG-REPORT-4:PowerShell Rename-Computer 也失败(用户实证,2026-08-05)

## 关键发现
**用户用 admin 权限的 PowerShell 跑 `Rename-Computer` 也报"拒绝访问"**:
```
PS C:\WINDOWS\system32> Rename-Computer -NewName 'CM-ZH-A0AAA' -Force -Restart
Rename-Computer : 由于发生以下异常，无法将计算机"CM-ZH-A0660"重命名为"CM-ZH-A0AAA": 拒绝访问。。

PS C:\WINDOWS\system32>
```

## 排除的原因
- ❌ 不是 EXE bug(WMI 路径已经对了)
- ❌ 不是 C# 代码问题(微软自己的 PowerShell cmdlet 也拒绝)
- ❌ 不是 admin token 不够(PowerShell 已用 admin 跑)

## 真正的可能原因(用户 Windows 环境层面的问题)

### 假设 1:加入 AD 域(Active Directory)
**最可能**。如果这台机器加入了公司域(看 machine name 前缀 `CM-ZH-` 像是公司规范命名),改机器名需要:
- 域管理员权限(不是本地 admin)
- 或者在域控制器上手动改名
- 或者先退域再改

**诊断**:
```powershell
# 看机器是否在域里
(Get-WmiObject -Class Win32_ComputerSystem).Domain
# 如果返回 company.local / corp.com 等 → 在域里
# 如果返回 WORKGROUP → 不在域里(可以本地改)
```

**如果是在域里**:
- 联系域管理员(IT 部门)改机器名
- 或者用域账号登录(PowerShell 用域账号的 token 跑 Rename-Computer)
- 或者先 `Unjoin-Workgroup` 再改,改完再 `Join-Domain`

### 假设 2:域账户(不是本地账户)有特殊限制
即使 `whoami` 出来是 admin,**这个 admin 可能是域 admin 不是本地 admin**。域 token 在本地改机器名时受限。

**诊断**:
```powershell
whoami /groups | findstr "S-1-5-32-544"
# 如果没 S-1-5-32-544 → 不是本地 Administrators 组成员
# 即使看到 admin 字样

whoami /priv
# 看是否有 SeRestorePrivilege 之类
```

### 假设 3:Windows Installer 或其他服务占用了 HKLM 注册表
**罕见但可能**:某些 Windows 服务(Windows Update、Defender、MDM)打开了机器名相关注册表键的写句柄,其他进程无法写。

**诊断**:
```powershell
# 重启到安全模式再试
# 或: 检查是否被 MDM 策略锁定
gpresult /h gpresult.html
# 看 Computer Configuration → Administrative Templates → System → Computer Name
```

### 假设 4:UAC 限制(虽然跑 PowerShell 是 admin)
某些企业版 Windows 启用了 **UAC 限制本地账户**(RunAsPPL),即使 admin token 也不能做某些事。

### 假设 5:这台机器是 Azure AD joined
Windows 10/11 加入 Azure AD(AAD joined)后,机器名由 AAD 管理,**本地改不了**。

**诊断**:
```powershell
dsregcmd /status
# 看 Device State → AzureAdJoined: YES
# 如果 YES → 必须用 Intune / AAD 改
```

## 你的 EXE 应该如何应对

### 方案 A:检测错误来源 + 友好提示
EXE 改名失败时,**不只是显示 HRESULT**,还要:
- 检测是否在 AD 域(`Environment.UserDomainName` 看是不是机器名)
- 检测 Azure AD joined(`dsregcmd` 间接查注册表)
- 给出对应解决方案
  - "请用域管理员账号登录 PowerShell 跑 Rename-Computer"
  - "请联系域管理员(IT 部门)改名"
  - "如 Azure AD 加入,联系 IT 用 Intune 改名"

### 方案 B:直接调 netdom renamecomputer(域工具)
如果机器在域,本地 admin 不能改,但域工具可以(用域账号)。

```csharp
// 走 netdom.exe(Windows RSAT 工具,需要 IT 装)
var psi = new ProcessStartInfo
{
    FileName = "netdom.exe",
    Arguments = $"renamecomputer {Environment.MachineName} /newname:{newName} /userd:domain\\admin /passwordd:*",
    UseShellExecute = false,
    CreateNoWindow = true,
    RedirectStandardOutput = true
};
```

但需要域账号 + 密码,不实用。

### 方案 C:用 PowerShell 显式用域账号(用户输入)
EXE 弹窗让用户输入域账号 + 密码,然后用这个账号调 PowerShell Rename-Computer。

**但**:用户已经试过 admin PowerShell 了,说明就是域限制或者本机根本改不了。

### 方案 D(推荐):UI 提示用户去 IT 部门改,EXE 退化成"显示当前机器名 + 复制"
既然改不了,EXE 应该:
1. 检测是否能改(改名前弹"该机器可能受域策略限制"提示)
2. 给用户两个选择:
   - "我知道我能改(我有域账号)" — 弹域账号密码输入框
   - "请 IT 同事帮我改" — EXE 直接退化为只读工具

## 你的任务

### 1. 排查(用户给反馈后)
**问用户**:
- `whoami`(确认登录身份)
- `(Get-WmiObject -Class Win32_ComputerSystem).Domain` 或 `dsregcmd /status`(看是否在域/AAD)
- 之前改名是不是找 IT 改的?如果找 IT 改,那 EXE 改不了是正常的

**重要**:如果用户**之前**就是找 IT 改的机器名,那 EXE 的"改名"功能对这个用户**永远不适用**。EXE 应该**只显示 + 复制**机器名 + **建议"改名请联系 IT"**。

### 2. 改 EXE(根据排查结果)

**情况 A:在域 → EXE 退化为只读 + 提示"改名请联系 IT"**
- 检测:`Environment.UserDomainName` 跟 `Environment.MachineName` 不一致 → 在域
- 改 `MainViewModel.cs` 加一个 `IsInDomain` 属性
- 改名按钮显示但**点击时弹提示框**"本机在域中,请联系 IT 改名",不改按钮 disabled(可能用户有域账号)
- 也加"复制当前机器名"按钮已存在(满足只读需求)

**情况 B:在 AAD → 同样退化为只读**
- 检测:注册表 `HKLM\SYSTEM\CurrentControlSet\Control\CloudDomainJoin` 存在
- 提示:"本机 Azure AD joined,改名请联系 IT 用 Intune"

**情况 C:不在域 + admin 应该能改 → 但拒绝访问**
- 这种罕见,可能是 UAC 限制 / Windows 版本 bug
- 提示:"本地 admin 改不了,尝试用 SYSTEM 权限"
- EXE 加"以 SYSTEM 身份启动"按钮(`PsExec.exe -s -i ComputerRenameTool.exe`)

### 3. 改完后必跑
- [ ] 检测域/AAD 的逻辑正确
- [ ] UI 友好提示(不是冷冰冰的 HRESULT)
- [ ] "复制机器名"按钮始终可用(只读功能)
- [ ] 给用户明确的下一步建议

## 这次最重要的事

**承认 EXE 的局限**:如果机器在域 / AAD,EXE 永远改不了。EXE 应该**给用户清晰解释 + 退化为只读工具**,而不是反复修代码让改名"看起来成功"但实际不生效。

## commit + push + build + release
跟之前一样流程,hermes 不动手。完成后报告:
- 改了哪些文件
- 新 EXE md5
- Release URL

## 给用户最终解释模板
"本机 [在域/在 AAD/独立],改名需要 [IT 协助/Intune/退域后改]。本 EXE 已退化为只读工具,你可以用它查看机器名 + 复制到 IT 申请单。"
