# FIX-REQUEST-8:ComputerRenameTool v1.1.1 — Tab 切换 + Bug 修复(2026-08-06)

## 用户反馈的 3 个问题

### Bug 1:内存摘要显示错误
- 用户实际:2 根 32GB 内存条(共 64GB,2 槽)
- 摘要显示:"64GB"
- **期望**:摘要显示 "2 × 32GB DDR5 (2/4 槽)" 或 "32GB × 2" 让人一眼看出插槽数

### Bug 2:IP 显示"以太网 3"不是 IP 地址
- 摘要显示:网络 = "以太网 3"
- 实际这是 **NetConnectionID**(网卡名),不是 IP
- **期望**:摘要显示 IPv4 IP 地址(如 10.12.138.38),详情表格里看完整 IP/MAC/适配器名

### Bug 3:信息太多,窗口装不下,不支持滚动
- 当前 720×720 固定窗口,展开后所有详情挤在一起
- **用户建议**:首页改名 + 第二页电脑配置(Tab 切换)

## 用户决定的设计
**Tab 切换**(两个 Tab):
- **Tab 1: 重命名**(主功能,简洁 600×500,不要太大)
- **Tab 2: 电脑配置**(完整信息,Tab 内部可滚动 `ScrollViewer`)

## 你的任务

### 1. UI 重构为 TabControl

`src/ComputerRenameTool/Views/MainWindow.xaml`:
- 当前是 `Grid` 多 section,改为 `TabControl`
- 2 个 TabItem:
  - **Tab 1 (重命名)**:
    - 当前机器名 + 复制按钮
    - Windows 版本 + 当前用户
    - 输入框 + 校验
    - 修改按钮
  - **Tab 2 (电脑配置)**:
    - 顶部摘要卡片(CPU/内存/硬盘/系统 — 各一行,4-5 个)
    - `ScrollViewer` 包住所有详情(各分类 `Expander` 折叠)
    - 各 Expander 内容:
      - **内存**:插槽数 + 已插数 + 表格(制造商/容量/频率/型号/FormFactor)
      - **物理盘**:表格(型号/容量/接口/健康/序列号)
      - **逻辑盘**:表格(盘符/标签/总量/空闲/已用/百分比)
      - **CPU**:核心/线程/最大频率/当前负载
      - **GPU**:名称/显存/驱动
      - **网络**:表格(适配器名/IPv4/子网掩码/网关/MAC/链路速度)
      - **BIOS / 主板 / 系统**:厂商/版本/日期/序列号

窗口大小:`600×500`(单 Tab,比现在小,因为 Tab 内部可滚)

### 2. 修内存摘要 bug

**之前逻辑**(猜的):
```csharp
MemorySummary = $"{totalGB} GB"  // 只算总容量
```

**新逻辑**:
```csharp
// 收集 chip 信息(每根内存)
var chips = GetMemoryChips();  // List<MemoryChip>
var totalGB = chips.Sum(c => c.CapacityGB);
var usedCount = chips.Count;
var slotCount = ??? // 需要从 SMBIOS 或 Win32_PhysicalMemoryArray 拿
var perChipGB = chips.GroupBy(c => c.CapacityGB)
                     .Select(g => $"{g.Count}×{g.Key}GB")
                     .Join(" + ");

// 摘要
MemorySummary = $"{totalGB} GB ({perChipGB}, {usedCount} 根)"  // e.g. "64 GB (2×32GB, 2 根)"
```

**拿插槽总数**:
```csharp
var searcher = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
foreach (ManagementObject mo in searcher.Get())
{
    var slots = Convert.ToInt32(mo["MemoryDevices"]);  // 总槽位数
    break;
}
```

### 3. 修 IP 显示 bug

**之前逻辑**(猜):
```csharp
NetworkSummary = adapter.NetConnectionID;  // 错误!这是"以太网 3"
```

**新逻辑**(取第一个有 IP 的物理网卡):
```csharp
var searcher = new ManagementObjectSearcher(
    "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True");
foreach (ManagementObject mo in searcher.Get())
{
    var ips = (string[])mo["IPAddress"];
    var ipv4 = ips?.FirstOrDefault(ip => !ip.Contains(":"));  // 排除 IPv6
    if (ipv4 != null)
    {
        NetworkSummary = ipv4;  // "10.12.138.38"
        break;
    }
}
```

详情表格里**保留 NetConnectionID** 作为"适配器名称"列,让用户能看出"哪个 IP 对应哪个网卡"。

### 4. 摘要卡片重新设计

把现在 8-10 行的"电脑配置"摘要,精简为 4-5 张卡片(更醒目):

| 卡片 | 内容 |
|---|---|
| **CPU** | Intel Core i7-14700 |
| **内存** | 64 GB (2×32GB, 2 根) |
| **硬盘** | 1 TB NVMe + 512 GB SSD |
| **系统** | Windows 11 Pro 25H2 |
| **IP** | 10.12.138.38(从网络摘要拿,别用 NetConnectionID) |

放在 Tab 2 顶部,4-5 张 1 行 grid 卡片。

### 5. Models 调整

`MemoryChip` 加字段:
- `SlotNumber`(Win32_MemoryDevice 的 Tag 字段,槽位编号)
- `FormFactor`(DIMM/SODIMM)

`NetworkAdapter` 加字段:
- `IPv4Address`(主 IP)
- `SubnetMask`
- `DefaultGateway`

### 6. 单元测试更新

- 摘要生成测试:喂 2 根 32GB 内存,验证摘要字符串是 "64 GB (2×32GB, 2 根)" 而不是 "64 GB"
- 摘要生成测试:喂 1 个有 IP 的网卡 + 1 个无 IP,验证摘要用有 IP 的那个

### 7. 不做

- ❌ 不改 WMI 路径
- ❌ 不改 version(1.1.0 → 1.1.1 patch bump,只加修复)
- ❌ 不改主功能改名逻辑
- ❌ 不加新的信息维度(只修复 3 个 bug + Tab 切换)
- ❌ 不动 v1.0.0 release(v1.1.1 是新 release)

## 版本号

`<Version>1.1.1</Version>` (patch bump)
`<FileVersion>1.1.1.0</FileVersion>`
`<AssemblyVersion>1.1.1.0</AssemblyVersion>`

## commit + push + build + release

1. 改代码 + 测试
2. commit: `fix(ui): tab switch + memory/network summary fix (v1.1.1)`
3. push 到 main
4. 等 build 通过
5. 打 tag v1.1.1 + 创新 release:
   ```bash
   git tag -a v1.1.1 -m "v1.1.1: tab switch + memory/network summary fix"
   git push origin v1.1.1
   gh release create v1.1.0 <新 EXE> --title 'Computer Rename Tool v1.1.1' --notes '...' --generate-notes
   ```
6. 报告:改了哪些文件、新 EXE md5、v1.1.1 release URL

## 验收(用户验收)
- [ ] 启动 EXE → 默认 Tab 1(改名),简洁
- [ ] 切到 Tab 2 → 摘要 4-5 卡片
- [ ] Tab 2 摘要"内存"显示 "64 GB (2×32GB, 2 根)" 之类
- [ ] Tab 2 摘要"IP"显示 "10.12.138.38" 不是 "以太网 3"
- [ ] Tab 2 内部可滚动,所有详情都看得到
- [ ] 改名功能在 Tab 1,完全不动
- [ ] 改名后切到 Tab 1 立即显示新名

记住:所有代码改动、build、release 全归你,hermes 不再改源码。
