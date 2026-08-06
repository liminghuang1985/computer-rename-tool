# BUG-REPORT-2:UI 信息空白 + 按钮被遮挡(2026-08-05)

## 截图证据
用户截图显示运行 v1.0.0 release EXE(`MD5: f1621e33fb94fea4d762555f7dd26d72`),有 2 个严重问题:

### Bug A:所有电脑信息字段空白
**现象**:
- "当前机器名:" 后面空白
- "Windows:" 后面空白
- "当前用户:" 后面空白
- "CPU:" / "内存:" / "显卡:" / "硬盘:" 后面空白

**不是 placeholder**(placeholder 是"请输入新的机器名"在输入框,跟显示字段无关)

**根因(几个可能)**:
1. `SystemInfoService.cs` 用 P/Invoke + 注册表读机器名/硬件,但**字段映射错了**(`Win32_Processor` 名字读不到 / WMI 已废弃,Claude 改用 P/Invoke 但读字段路径错)
2. 读取**抛异常但被吞了**(Service 层 catch 后没设到 ViewModel)
3. `MainViewModel.cs` 构造时**没等异步加载就 return**,UI 立即 binding 空字符串
4. binding 路径错(`{Binding Computer.CurrentName}` vs `{Binding CurrentName}`)

### Bug B:【修改机器名】按钮被遮挡
**现象**:
- 输入框 "CM-ZH-A0660" 在画面中部
- 输入框下方**没有看到【修改机器名】按钮**
- 用户输入新名后,找不到点击确认的地方

**根因(几个可能)**:
1. 按钮被键盘焦点 + 蓝色发光边框覆盖(WPF Template 重叠错)
2. 输入框高度撑爆,按钮被推到屏幕外
3. 布局容器写死 `Height` 没让按钮可见
4. UI 重新设计时只重写了输入框样式,**忘了保留按钮可见**

## 你的任务

### 1. 排查 Bug A(信息空白)
**优先看**:
- `src/ComputerRenameTool/Services/SystemInfoService.cs` — 读机器名/Win/用户/CPU/内存/显卡/硬盘的 P/Invoke 或 WMI 代码
- 跑一次本地测试(你 Mac 没装 dotnet,但可以看代码逻辑)
- 看每个 ReadXxx 方法:
  ```csharp
  // 错误示范(字段名可能错)
  var name = regKey.GetValue("ComputerName")?.ToString();
  // 应该是:
  var name = regKey.GetValue("ActiveComputerName")?.ToString();
  ```

**Windows 机器名读法(正确路径)**:
- `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion\WinLogo` → `ValueName` 字段
- 或者 `Environment.MachineName`(纯 .NET API,最稳)

**Windows 版本读法**:
- `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` → `ProductName` + `DisplayVersion` + `CurrentBuild`

**当前用户**:
- `Environment.UserName`(纯 .NET API,最稳)
- 或 `WindowsIdentity.GetCurrent().Name`

**CPU 读法**:
- 注册表 `HKLM\HARDWARE\DESCRIPTION\System\CentralProcessor\0` → `ProcessorNameString`
- 或 `Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER")`(粗略)

**内存读法**:
- `Microsoft.VisualBasic.Devices.ComputerInfo` → `TotalPhysicalMemory` / `AvailablePhysicalMemory`
- 或 P/Invoke `GlobalMemoryStatusEx`

**显卡读法**:
- 注册表 `HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\0000` → `DriverDesc`
- 遍历 subkey `0000` `0001` ... 取第一个有 `HardwareInformation.MemorySize` 的

**硬盘读法(系统盘)**:
- 注册表 `HKLM\SOFTWARE\Microsoft\Windows NT\CurrentVersion` → `SystemRoot` → 取盘符
- 遍历 `HKLM\SYSTEM\CurrentControlSet\Services\Disk\Enum` → 取第一个 disk
- 再查 `HKLM\SYSTEM\CurrentControlSet\Services\<disk>\Enum` → 取设备描述

**改法建议**:
1. **优先用纯 .NET API**(Environment.MachineName, Environment.UserName, Microsoft.VisualBasic.Devices.ComputerInfo)— 不依赖注册表路径,稳定
2. 注册表读不到时**回退到 WMI**(`System.Management.ManagementObjectSearcher` 查 `Win32_Processor` / `Win32_PhysicalMemory` / `Win32_VideoController` / `Win32_DiskDrive`)
3. 任意读失败 → catch 异常 → ViewModel 字段设 "未知 (驱动未安装)" 而非空字符串

**MainViewModel 加载顺序**:
- 构造时**同步阻塞**等 SystemInfoService.GetComputerInfo() + GetHardwareInfo() 完成
- 不要用 async/await fire-and-forget,UI 起来就该看到数据
- 加载过程中显示 "加载中..." 标签(可选)

### 2. 排查 Bug B(按钮被遮挡)
**优先看**:
- `src/ComputerRenameTool/Views/UserControls/RenameSection.xaml` — 看按钮位置 + 容器布局
- `src/ComputerRenameTool/Resources/Styles.xaml` — 看 PrimaryButton 模板的 Height/Margin
- `MainWindow.xaml` 整体 layout — 看 UserControl 是否给了固定 Height

**修法**:
1. RenameSection 顶层容器改成 `<StackPanel>`(Vertical) 或 `<Grid>` with auto row
2. 输入框给固定 `Height="40"` 不让它撑爆
3. 按钮放最下面,`Margin="0,16,0,0"`,`Height="40"`,`HorizontalAlignment="Stretch"`(撑满宽度)
4. **按钮文字 + 图标 + 颜色** = 清晰可见,不藏不挡
5. 测试**焦点状态**(focus 边框不能盖按钮)— 给输入框 focus 边框 max `BlurRadius=8` 不溢出

### 3. 单元测试必加
- `SystemInfoServiceTests`: 跑 `GetComputerInfo()` / `GetHardwareInfo()` 不抛异常(在 Windows 跑 / 或 mock)
- `MainViewModelTests`: 验证 `Computer.CurrentName` 初始值非空
- `RenameSectionViewModelTests`: 输入合法名时 `CanSubmit = true` + 命令可执行

## 你的任务清单

1. 排查 Bug A(读字段路径 + 异常处理)
2. 排查 Bug B(布局 / 按钮可见性)
3. 修代码(纯 .NET API 优先,注册表/WMI 兜底)
4. `dotnet build` 验证(本地无 dotnet → 等 CI)
5. 加单元测试
6. commit + push
7. 等 build 通过
8. gh release upload v1.0.0 <新 EXE> --clobber
9. 报告:
   - 改了哪些文件
   - 新 EXE md5
   - Release URL

## 验收(用户验收点)
- [ ] 启动后 0.5 秒内看到机器名 / Win 版本 / 用户 / CPU / 内存 / 显卡 / 硬盘
- [ ] **【修改机器名】按钮清晰可见**,在输入框正下方,不被遮挡
- [ ] 输入合法名 → 按钮变 cyan 高亮 → 可点击
- [ ] 改名后顶部立刻显示新名
- [ ] 改名成功后弹"立即/稍后重启"对话框

## 提醒
- 这次**不能再忘加注释**导致字段路径错 — 写完代码对着 PRD V1.0 §十一 验收标准过一遍
- 修完后跑 SPEC.md §3 项目布局确认所有文件都在
- 业务代码改的话, ViewModel/Service/Converter 都看,别只改 View
