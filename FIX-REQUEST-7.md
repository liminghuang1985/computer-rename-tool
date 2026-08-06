# FIX-REQUEST-7:ComputerRenameTool 扩展为硬件巡检工具(2026-08-05)

## 用户决策
- ✅ 阶段 1 + 2 全做(内存/硬盘 + CPU/GPU/BIOS/主板/网络/操作系统)
- ✅ 版本:v1.0.0 → **v1.1.0**(minor bump,新增功能)

## 目标
把现在的 ComputerRenameTool(只显示基础硬件)扩展为**改名 + 硬件巡检**二合一工具,UI 类似 `windows_inspection_tool` 的暗色深蓝风格但保持主功能(改名)为最高优先级。

## 数据采集源(全部走 WMI CIM)

| 类别 | WMI Class | 关键字段 |
|---|---|---|
| **机器名 / Win / 用户** | `Win32_OperatingSystem` / `Win32_ComputerSystem` | Caption / Version / UserName |
| **CPU** | `Win32_Processor` | Name / NumberOfCores / NumberOfLogicalProcessors / MaxClockSpeed / LoadPercentage |
| **内存(汇总)** | `Win32_OperatingSystem` | TotalVisibleMemorySize / FreePhysicalMemory |
| **内存(插槽)** | `Win32_PhysicalMemory` | Manufacturer / Capacity / Speed / PartNumber / DeviceLocator / FormFactor |
| **物理盘** | `Win32_DiskDrive` | Model / Size / InterfaceType / Status / SerialNumber |
| **逻辑盘** | `Win32_LogicalDisk -Filter 'DriveType=3'` | DeviceID / VolumeName / Size / FreeSpace |
| **GPU** | `Win32_VideoController` | Name / DriverVersion / AdapterRAM / VideoProcessor |
| **BIOS** | `Win32_BIOS` | Manufacturer / SMBIOSBIOSVersion / ReleaseDate |
| **主板** | `Win32_BaseBoard` | Manufacturer / Product / SerialNumber |
| **网络(物理网卡)** | `Win32_NetworkAdapter WHERE NetEnabled=TRUE` | Name / MACAddress / NetConnectionID / Speed |
| **操作系统** | `Win32_OperatingSystem` | InstallDate / LastBootUpTime / SerialNumber / BuildNumber |

## UI 重新设计

**窗口**:`600×450` → **`720×720`**

**布局**(垂直):
```
┌────────────────────────────────────┐
│ [渐变条 cyan→purple]                 │
├────────────────────────────────────┤
│ 【修改机器名】(主功能,位置不动)       │
│   当前机器名:CM-ZH-A0660 [复制]      │
│   Windows:Windows 11 25H2            │
│   当前用户:hlm                       │
│   [机器名输入框] [修改机器名按钮]     │
├────────────────────────────────────┤
│ 【电脑配置】(摘要,始终显示)           │
│   CPU: Intel Core i7-14700           │
│   内存: 32 GB (2 × 16GB DDR5)       │
│   显卡: NVIDIA RTX 4060              │
│   硬盘: 1 TB SSD + 512 GB SSD       │
│   BIOS: Dell 1.5.0 (2024-01-15)     │
│   主板: Dell Inc. Latitude 5540     │
│   操作系统: Windows 11 Pro 25H2     │
│   启动时间: 2026-08-04 09:00:12     │
│   IP: 10.12.138.38 (WiFi)            │
├────────────────────────────────────┤
│ ▶ 展开详细信息(可折叠)                │
│   ┌─ 内存详情 ──────────────┐         │
│   │ 插槽 2 / 已插 2         │         │
│   │ 制造商  容量    频率   型号│         │
│   │ Samsung  16GB  4800  M...│         │
│   │ Hynix    16GB  4800  H...│         │
│   └────────────────────────┘         │
│   ┌─ 物理盘详情 ────────────┐         │
│   │ 物理盘 1                  │         │
│   │ 型号  容量   接口   健康  │         │
│   │ Sam..  1TB    NVMe  OK   │         │
│   └────────────────────────┘         │
│   ┌─ 逻辑盘详情 ────────────┐         │
│   │ C: System 100/200GB      │         │
│   │ D: Data   500/800GB      │         │
│   └────────────────────────┘         │
│   ┌─ CPU 详情 ───────────────┐         │
│   │ 核心 20 / 线程 28         │         │
│   │ 最大频率 5.4 GHz          │         │
│   │ 当前负载 23%              │         │
│   └────────────────────────┘         │
│   ┌─ GPU 详情 ───────────────┐         │
│   │ 名称 NVIDIA RTX 4060      │         │
│   │ 显存 8 GB                 │         │
│   │ 驱动 31.0.15.4601         │         │
│   └────────────────────────┘         │
│   ┌─ 网络详情 ───────────────┐         │
│   │ WiFi: 10.12.138.38      │         │
│   │   MAC: aa:bb:cc:dd:ee    │         │
│   │   1 Gbps                 │         │
│   │ Ethernet: 未连接         │         │
│   └────────────────────────┘         │
│   ┌─ BIOS / 主板 / 系统 ─────┐         │
│   │ BIOS: Dell 1.5.0         │         │
│   │ 主板: Dell Latitude 5540 │         │
│   │ 序列号: ABC123            │         │
│   │ 启动时间: ...             │         │
│   │ 安装日期: ...             │         │
│   └────────────────────────┘         │
└────────────────────────────────────┘
```

**【展开详细信息】** 默认折叠,只显示摘要行。点开后展开各分类表格。

## ViewModel 结构调整

### 新增 Models
```
src/ComputerRenameTool/Models/
├── ComputerInfo.cs          (已有,扩展)
├── MemoryChip.cs             新增
├── PhysicalDisk.cs           新增
├── LogicalDisk.cs            新增
├── CpuInfo.cs                 新增
├── GpuInfo.cs                 新增
├── NetworkAdapter.cs         新增
├── BiosInfo.cs                新增
├── MotherboardInfo.cs        新增
├── OperatingSystemInfo.cs    新增
└── HardwareReport.cs         新增(汇总所有)
```

### 新增 Services
```
src/ComputerRenameTool/Services/
├── ISystemInfoService.cs      扩展 + 8 个新方法
├── SystemInfoService.cs       真实 WMI 实现
└── IHardwareReportService.cs  新增(汇总调用入口,后台线程)
```

### ViewModel
```
src/ComputerRenameTool/ViewModels/
├── MainViewModel.cs           扩展
├── HardwareReportViewModel.cs 新增(详情折叠展开)
└── HardwareDetailViewModel.cs 新增(各子表格)
```

### Views
```
src/ComputerRenameTool/Views/UserControls/
├── HardwareSummarySection.xaml   已有,扩展(摘要行加 CPU/内存/硬盘汇总)
├── HardwareDetailSection.xaml    新增(展开区,各分类 Expander)
├── MemoryDetailTable.xaml        新增
├── PhysicalDiskDetailTable.xaml  新增
├── LogicalDiskDetailTable.xaml   新增
├── CpuDetailTable.xaml           新增
├── GpuDetailTable.xaml           新增
├── NetworkDetailTable.xaml       新增
└── BiosDetailTable.xaml          新增
```

## 关键实现要求

### 1. WMI 调用必须真实(不能 mock fallback)
- ❌ 错:读不到就 return "未知 (驱动未安装)"
- ✅ 对:读不到 → 异常 → ViewModel 显示空 + log 记录,UI 诚实显示"数据不可读"

### 2. 数据采集在后台线程
```csharp
public async Task<HardwareReport> CollectAsync()
{
    return await Task.Run(() =>
    {
        var report = new HardwareReport();
        report.Computer = GetComputerInfo();
        report.Cpu = GetCpuInfo();
        report.MemoryChips = GetMemoryChips();
        // ... 串行采集(避免 WMI 并发死锁)
        return report;
    });
}
```

### 3. 启动时显示"加载中",完成后刷新
```csharp
public MainViewModel()
{
    IsLoading = true;
    _ = Task.Run(async () =>
    {
        var report = await _hardwareService.CollectAsync();
        // marshall back to UI thread
        Application.Current.Dispatcher.Invoke(() =>
        {
            Hardware = new HardwareReportViewModel(report);
            IsLoading = false;
        });
    });
}
```

### 4. 改名后也重新采集(显示新机器名 + 其他不变)
```csharp
private async void OnRenameCompleted()
{
    var report = await _hardwareService.CollectAsync();  // 重新读机器名
    Computer.CurrentName = report.Computer.MachineName;
    // ... 刷新 UI
}
```

### 5. UI 样式继续深色科技风(BUG-REPORT-4 已定)
- 表格:深色背景 + 1px 边框 + cyan 表头
- 摘要行:12px 副文本 + 14px 主文本
- 展开按钮:cyan accent
- 加载中:3 个 dot 跳动

## 单元测试

- ✅ `SystemInfoServiceTests` 现有保留(mock WMI)
- ✅ 新增 `MemoryChipTests` / `PhysicalDiskTests` 等 model 序列化测试
- ❌ 不要测 WMI 调用本身(CI 跑不了,本地 Windows 跑)
- ✅ 测 ViewModel:用 mock `IHardwareReportService` 返回固定 HardwareReport,验证 UI 状态

## 改 csproj 改 1 行(版本号)
```xml
<Version>1.1.0</Version>
<FileVersion>1.1.0.0</FileVersion>
<AssemblyVersion>1.1.0.0</AssemblyVersion>
```

## 不做
- ❌ 不加声音 / 通知 / 定时刷新
- ❌ 不做导出报告(只显示在 UI)
- ❌ 不做趋势图 / 历史
- ❌ 不加 remote 网络检测
- ❌ 不改后端 / 不动其他项目

## 验收(用户验收)
- [ ] 启动 EXE → 2-3 秒后看到完整硬件信息
- [ ] 展开按钮 → 各分类表格清晰
- [ ] 内存:插槽 2 + 2 行(Samsung/Hynix)
- [ ] 物理盘:NVMe 1TB / SATA 512GB 等
- [ ] 逻辑盘:C:/D: 盘符 + 标签 + 容量 + 百分比
- [ ] CPU:核心/线程/频率
- [ ] GPU:名称/显存/驱动
- [ ] 网络:WiFi/Ethernet + IP/MAC
- [ ] BIOS/主板/启动时间/安装日期
- [ ] 改名后顶部立即刷新新名
- [ ] 窗口大小 720×720
- [ ] 整个 EXE 体积 < 80 MB

## commit + push + build + release
1. 改代码 + 测试
2. commit: `feat(hardware): expand system info to full hardware report (v1.1.0)`
3. push 到 main
4. 等 build 通过
5. 这次是**新版本** release:
   ```bash
   git tag -a v1.1.0 -m "v1.1.0: full hardware report"
   git push origin v1.1.0
   gh release create v1.1.0 <exe> \
     --title "Computer Rename Tool v1.1.0" \
     --notes "新增完整硬件巡检..." \
     --generate-notes
   ```
6. 报告:改了哪些文件、新 EXE md5、v1.1.0 release URL

记住:所有代码改动、build、release 全归你,hermes 不再改源码。
