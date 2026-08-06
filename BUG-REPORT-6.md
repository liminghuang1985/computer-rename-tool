# BUG-REPORT-6:EXE 启动时未请求管理员权限(2026-08-05 用户根因诊断)

## 用户根因(完全正确)
用户跑 `Rename-Computer` 用的是**普通 PowerShell**(提示符 `PS C:\WINDOWS\system32>` 没 `(管理员)` 后缀),所以 PowerShell 进程是 **Medium Integrity Level**,改名 API 需要 **High Integrity** → "拒绝访问"。

**EXE 同理**:双击 `ComputerRenameTool.exe` 默认是 Medium Integrity,即使账号是本地 admin,进程没提权,改名 API 全失败。

## 解决方案(用户提的,2 选 1 都行)

### 方案 A:app.manifest 强制 requireAdministrator
最简单。用户双击 EXE → Windows 自动弹 UAC → 用户点是 → 进程获得 High Integrity。

**改动**:
- 加/改 `src/ComputerRenameTool/app.manifest`:
  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
    <assemblyIdentity version="1.0.0.0" name="ComputerRenameTool.app"/>
    <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
      <security>
        <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
          <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
        </requestedPrivileges>
      </security>
    </trustInfo>
    <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
      <application>
        <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}"/>
      </application>
    </compatibility>
  </assembly>
  ```
- `.csproj` 加 `<ApplicationManifest>app.manifest</ApplicationManifest>`

**优点**:用户双击就提权,无歧义,体验最好
**缺点**:如果用户双击 EXE 不想用(只是看看信息),也要走一次 UAC(但 UAC 接受后可以"以后不再询问"勾选)

### 方案 B:运行时检测 + 自动重启提权
更灵活。EXE 启动时检测,如果不是 admin,弹"是否提权"对话框,用户点是则重启自己提权版。

**改动**:
- `App.xaml.cs` OnStartup 钩子:
  ```csharp
  if (!IsRunAsAdmin())
  {
      // 弹"是否以管理员身份启动?"对话框
      var result = MessageBox.Show(
          "修改机器名需要管理员权限,是否以管理员身份重新启动?",
          "需要管理员权限",
          MessageBoxButton.YesNo, MessageBoxImage.Question);
      if (result == MessageBoxResult.Yes)
      {
          RestartAsAdmin();
          Shutdown(0);  // 当前进程退出
          return;
      }
    // No → 继续启动,但改名按钮 disabled
  }
  ```
- `IsRunAsAdmin()`:
  ```csharp
  using System.Security.Principal;
  static bool IsRunAsAdmin()
  {
      using var identity = WindowsIdentity.GetCurrent();
      var principal = new WindowsPrincipal(identity);
      return principal.IsInRole(WindowsBuiltInRole.Administrator);
  }
  ```
- `RestartAsAdmin()`:
  ```csharp
  static void RestartAsAdmin()
  {
      var psi = new ProcessStartInfo
      {
          UseShellExecute = true,
          Verb = "runas",  // 触发 UAC
          FileName = Process.GetCurrentProcess().MainModule.FileName
      };
      Process.Start(psi);
  }
  ```

**优点**:只读使用不用提权(用户可以查电脑信息不弹 UAC)
**缺点**:用户体验复杂(要看对话框,有时候弹有时候不弹)

### 方案 C(推荐):A + B 组合
- manifest 用 `requireAdministrator`(总是要 UAC,但首次双击走一次)
- 改名按钮 **默认 enabled**(因为进程提权了)
- 如果用户真的不想 UAC(看信息即可),他可以右键属性"以普通身份运行" — 但 UAC 弹窗默认是"允许",体验不差

**我推荐方案 A(单纯)**:
- 最简单,1 个 manifest 文件 + .csproj 1 行
- 不需要 OnStartup 逻辑
- UAC 只弹一次,Windows 记住信任
- 用户体验最直接

## 你的任务

### 必做
1. 选方案(我推荐 **方案 A**)
2. 创建/修改 `src/ComputerRenameTool/app.manifest`:
   ```xml
   <requestedExecutionLevel level="requireAdministrator" uiAccess="false" />
   ```
3. 修改 `src/ComputerRenameTool/ComputerRenameTool.csproj`:
   - 加 `<ApplicationManifest>app.manifest</ApplicationManifest>`
4. **验证 build**(确保 manifest 被打入 EXE)
5. **验证 EXE 双击会弹 UAC**(用户报告确认)
6. commit + push(commit msg: `feat(security): requireAdministrator manifest for UAC elevation`)
7. 等 build 通过
8. gh release upload v1.0.0 <新 EXE> --clobber
9. 报告:改了哪些文件、新 EXE md5、Release URL

### 同步做的(如果方案 A 不够,加这些)
- 如果用户想"看信息不弹 UAC",加 **方案 B** 的运行时检测 + 普通模式启动参数(比如 `--readonly`)
- 默认双击走方案 A(提权),用户加 `--readonly` 走方案 B(不提权,只读)

## 验收(用户验收)
- [ ] 双击 EXE → Windows 弹 UAC → 用户点"是" → 进程以管理员身份运行
- [ ] 改名功能**真正生效**(改名后重启验证)
- [ ] log 里能看到 `启动程序: Admin` 不是 `User`

## 不要忘
- manifest 必须在 .csproj 里 `<ApplicationManifest>` 引用,否则 EXE 不带 manifest
- build 用 windows-latest runner 验证
- 跟之前 BUG-REPORT-1/2/3/4/5 修复叠加生效:
  - manifest 提权
  - WMI Win32_ComputerSystem.Rename
  - UI DataContext 绑
  - 按钮可见
  - SE_RESTORE_NAME 提权(虽然 WMI 不需要,但保留作 fallback)

## commit + push + build + release
跟之前一样流程,hermes 不动手。完成后报告新 EXE md5 + Release URL。
