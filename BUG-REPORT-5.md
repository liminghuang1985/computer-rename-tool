# BUG-REPORT-5:PowerShell Rename-Computer 失败,本地独立+本地 admin(2026-08-05)

## 用户诊断输出(已确认)
```
whoami                              → cm-zh-a0660\hlm
whoami /groups | S-1-5-32-544       → YES (本地 Administrators)
(Get-WmiObject Win32_ComputerSystem).Domain → WORKGROUP (不在域)
dsregcmd /status | joined           → NO/AZURE/ENTERPRISE/WORKPLACE 都是 NO
Rename-Computer -Force              → 拒绝访问 (Access Denied)
```

**结论**:本地独立 + 本地 admin,改名**仍失败**。这是异常情况,需要深查。

## 排除的可能
- ❌ 不在 AD 域
- ❌ 不在 Azure AD
- ❌ 不在 Workplace joined
- ❌ 登录身份有 admin
- ❌ PowerShell 用了 admin token(已验证)

## 真正的可能原因(按概率排序)

### 假设 1:某个进程持有注册表锁(最常见)
`HKLM\SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName` 注册表键的写句柄被其他进程持有。

**诊断命令**:
```cmd
:: 1. 列出 HKLM 注册表所有持有 ComputerName 写锁的进程
handle.exe -a "HKLM\SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName"

:: 2. 或用 PowerShell
Get-Process | ForEach-Object {
    $proc = $_
    $handles = (Get-Process -Id $proc.Id -ErrorAction SilentlyContinue).Handle
    # 复杂,handle.exe 更直接
}
```

**修法**:找到占用进程 → 关闭它 → 再改名。

### 假设 2:第三方安全/IT 管理软件锁了
- Symantec / McAfee / Trend Micro / Sophos / Carbon Black / CrowdStrike
- 国产 360 / 腾讯管家 / 火绒
- 企业 MDM:SCCM Client / Intune / Workspace ONE / Jamf / Kaseya

**诊断**:
```powershell
# 看启动项
Get-CimInstance Win32_StartupCommand | Select-Object Name, Command, Location
# 看服务
Get-Service | Where-Object {$_.Status -eq "Running"} | Select-Object Name, DisplayName
# 看可疑第三方
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" 
Get-ItemProperty "HKLM:\SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Run"
```

**修法**:临时禁用可疑服务,改名。

### 假设 3:Windows 11 24H2 / 25H2 的新 UAC 行为
Win11 24H2+ 启用了 "RunAsPPL" 默认,本地 admin 即使 UAC 提权,改 machine name 也可能受限。

**诊断**:
```cmd
:: 看 RunAsPPL 状态
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Lsa" /v RunAsPPL
:: 0 = 没启用, 1 = 启用
```

如果 = 1,这就是根因。

**修法**:
```cmd
:: 改 RunAsPPL = 0
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Lsa" /v RunAsPPL /t REG_DWORD /d 0 /f
:: 重启
```

(改完重启后 admin 就能改 machine name 了。改回去也是一行命令)

### 假设 4:之前 join 过域但残留 SID 锁注册表
机器**曾经**在域(看 machine name `CM-ZH-` 是公司规范命名,说明以前 IT 改过),退域后机器名注册表键的 ACL 可能残留原域 SID,本地 admin 写不进。

**诊断**:
```powershell
# 看 ComputerName 注册表键的 ACL
$key = [Microsoft.Win32.RegistryKey]::OpenRemoteBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine, $env:COMPUTERNAME)
$subKey = $key.OpenSubKey("SYSTEM\CurrentControlSet\Control\ComputerName\ComputerName")
$sddl = $subKey.GetAccessControl().Sddl
$sddl
```

看输出有没有 `O:BA` (Builtin Admin) 和 `S-1-5-32-544`。

**修法**:
```powershell
# 用 SYSTEM 身份跑 PowerShell(绕过 ACL)
# 或:subinacl /setowner Administrators "HKLM\SYSTEM\CurrentControlSet\Control\ComputerName"
```

### 假设 5:Windows Defender Credential Guard
**罕见但可能**:Credential Guard 锁了 LSA 进程,改名调用 LSA 时被拦截。

## 你的 EXE 改法(我建议的"全栈兜底")

### 1. 加详细诊断模式
在 `ComputerRenameService.Rename()` 前先跑检测,UI 显示"为什么改不了":

```csharp
public class DiagnosticResult
{
    public bool IsInDomain { get; set; }
    public bool IsAadJoined { get; set; }
    public bool IsRunAsPplEnabled { get; set; }
    public string ComputerNameKeyOwner { get; set; }
    public List<string> HoldingProcesses { get; set; }
    public string RootCause { get; set; }   // 给用户的简短解释
    public string SuggestedFix { get; set; }
}

public DiagnosticResult Diagnose()
{
    var result = new DiagnosticResult();
    
    // 1. 域检测
    result.IsInDomain = !string.Equals(
        Environment.UserDomainName, 
        Environment.MachineName, 
        StringComparison.OrdinalIgnoreCase);
    
    // 2. AAD joined 检测
    var aadKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\CloudDomainJoin");
    result.IsAadJoined = aadKey != null;
    
    // 3. RunAsPPL 检测
    var lsaKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Lsa");
    if (lsaKey != null)
    {
        var ppl = lsaKey.GetValue("RunAsPPL");
        result.IsRunAsPplEnabled = ppl != null && Convert.ToInt32(ppl) == 1;
    }
    
    // 4. 决定 root cause
    if (result.IsInDomain) result.RootCause = "机器在域中";
    else if (result.IsAadJoined) result.RootCause = "机器 Azure AD joined";
    else if (result.IsRunAsPplEnabled) result.RootCause = "Windows 启用 RunAsPPL,本地 admin 受限";
    else result.RootCause = "未知原因(可能进程锁注册表)";
    
    return result;
}
```

### 2. UI 加"诊断"按钮
在 "修改机器名" 区加一个灰色按钮:**[🔍 诊断改名失败原因]**

点了之后弹一个详情窗:
```
诊断结果:
  ✗ 机器在域: NO
  ✗ Azure AD joined: NO
  ✗ RunAsPPL: 0 (未启用)
  ✗ 注册表键持有进程: (待扫描)
  
  根因: 未知,可能是第三方安全软件占用了注册表
  建议:
    1. 临时关闭 360 / 火绒 / 腾讯管家 / 各类杀毒
    2. 重启后重试
    3. 如果还不行,用以下命令以 SYSTEM 身份运行:
       PsExec.exe -s -i powershell.exe -Command "Rename-Computer -NewName '...' -Force"
```

### 3. 加 PsExec SYSTEM 改名的 fallback
EXE 检测到本地改名失败,给个**"以 SYSTEM 身份重试"**按钮,实际就是 `PsExec.exe -s -i` 调用 Rename-Computer。

(但 PsExec 需要下载,IT 机器通常装了;先检测 PATH)

### 4. 改名失败时,弹"IT 协助"联系卡
UI 友好提示:
```
❌ 本机改名受限

最可能的原因:
  • 第三方安全软件(360 / 火绒 / Defender)占用注册表
  • Windows 11 24H2+ RunAsPPL 启用(需要域管理员)
  • 残留域 ACL(以前 join 过域)

推荐处理:
  1. 关闭所有杀毒软件后重启
  2. 联系 IT 部门(IT 工单 / 飞书找 @斌哥)
  3. 临时方案:用 PsExec 以 SYSTEM 身份改名
     PsExec.exe -s -i powershell -Command "Rename-Computer ..."
```

## 你的任务

### 必做
1. 写 `IDiagnosticService` + `DiagnosticService.cs` 实现上面 4 项检测
2. 改 `MainViewModel` / `MainWindow.xaml` 加"🔍 诊断"按钮 + 弹详情窗
3. 改名失败时**不要冷冰冰显示 HRESULT**,弹"IT 协助"友好卡
4. 加单元测试 `DiagnosticServiceTests`
5. commit + push
6. 等 build 通过
7. gh release upload v1.0.0 --clobber
8. 报告:改了哪些文件、新 EXE md5、Release URL

### 不做
- ❌ 死磕让 EXE 改名生效(本机就是改不了,跟 EXE 无关)
- ❌ 移除之前的 WMI 改名代码(保留作 fallback)

## 验收(用户验收)
- [ ] 启动后能看到电脑信息
- [ ] 改名失败时**不再弹"修改成功"假象**,改失败时给详细错误
- [ ] "🔍 诊断"按钮能点,弹的诊断窗显示具体原因
- [ ] 用户能根据诊断卡决定下一步(关杀毒 / 找 IT / 用 PsExec)

## 给用户的话(供 UI 显示)
"本机不在域也不是 AAD,但 PowerShell Rename-Computer 也拒绝访问。这通常是:
1. 第三方安全软件(360 / 火绒 / Defender)锁了注册表
2. Windows 11 24H2+ 默认启用了 RunAsPPL,本地 admin 受限
3. 之前 join 过域但残留 ACL 锁了注册表键

建议:先关所有杀毒软件重启再试,或联系 IT 协助。"
