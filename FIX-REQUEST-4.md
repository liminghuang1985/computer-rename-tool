# UI 优化需求(方案 B 科技感深色) — 2026-08-05 黎明大哥确认

## 目标
把 v1.0.0 UI 从"默认 WPF 灰白"改成**科技感深色**风格,不改任何业务逻辑,不改 ViewModel/Service。

## 视觉规范

### 配色(写到 `ColorPalette.cs` + `Styles.xaml` 资源字典)
```csharp
public static class ColorPalette
{
    // 背景
    public static readonly Color BgPrimary = Color.FromRgb(0x0D, 0x11, 0x17);      // #0D1117 主背景(GitHub dark)
    public static readonly Color BgSecondary = Color.FromRgb(0x16, 0x1B, 0x22);    // #161B22 卡片背景
    public static readonly Color BgTertiary = Color.FromRgb(0x1A, 0x1A, 0x2E);     // #1A1A2E 卡片 hover
    public static readonly Color BorderSubtle = Color.FromRgb(0x2D, 0x2D, 0x3F);   // #2D2D3F 卡片边框

    // 强调色
    public static readonly Color AccentCyan = Color.FromRgb(0x00, 0xD9, 0xFF);      // #00D9FF 主强调(科技感)
    public static readonly Color AccentPurple = Color.FromRgb(0x7C, 0x3A, 0xED);    // #7C3AED 次强调(数据/AI)
    public static readonly Color AccentGreen = Color.FromRgb(0x10, 0xB9, 0x81);    // #10B981 校验通过
    public static readonly Color AccentRed = Color.FromRgb(0xEF, 0x44, 0x44);      // #EF4444 校验失败
    public static readonly Color AccentAmber = Color.FromRgb(0xF5, 0x9E, 0x0B);     // #F59E0B 警告

    // 文本
    public static readonly Color TextPrimary = Color.FromRgb(0xE6, 0xED, 0xF3);    // #E6EDF3 主文本
    public static readonly Color TextSecondary = Color.FromRgb(0x7D, 0x85, 0x90);  // #7D8590 副文本
    public static readonly Color TextMuted = Color.FromRgb(0x4D, 0x55, 0x60);      // #4D5560 极淡文本
}
```

### 字体
- UI 字体: `"Segoe UI Variable", "Segoe UI", "Inter", sans-serif` 14px
- 数据字体(电脑信息数值): `"Cascadia Code", "Consolas", "SF Mono", monospace` 14px
- 标题(GroupBox 标题): 16px, SemiBold
- 副标题(标签 "CPU:"): 12px, Regular, TextSecondary

### 组件样式(全部写到 `Styles.xaml`)

#### Window 背景
```xaml
<Style TargetType="Window">
    <Setter Property="Background" Value="{StaticResource BgPrimary}"/>
    <Setter Property="Foreground" Value="{StaticResource TextPrimary}"/>
    <Setter Property="FontFamily" Value="Segoe UI Variable, Segoe UI, Inter, sans-serif"/>
    <Setter Property="FontSize" Value="14"/>
</Style>
```

#### GroupBox 改 Card 风格(用 Border 替代)
- 替换 `GroupBox` 为 `Border`
- 圆角 8px,内边距 16px
- 背景 `#161B22` + 边框 1px `#2D2D3F`
- 标题(原 GroupBox.Header)改为 TextBlock + icon 装饰,放在 Border 内部顶部

#### 按钮
```xaml
<Style TargetType="Button" x:Key="PrimaryButton">
    <Setter Property="Background" Value="{StaticResource AccentCyan}"/>
    <Setter Property="Foreground" Value="#0D1117"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="20,10"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="Bd" CornerRadius="6"
                        Background="{TemplateBinding Background}"
                        Padding="{TemplateBinding Padding}">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="Bd" Property="Background" Value="#33D9FF"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="Bd" Property="Background" Value="#00B8DB"/>
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="Bd" Property="Opacity" Value="0.4"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

#### 输入框(TextBox)
- 背景 `#0D1117` + 边框 1px `#2D2D3F` + 圆角 6px
- focus 时边框变 `#00D9FF` + 1px 外发光(DropShadowEffect,模糊 8,颜色 AccentCyan 50% alpha)
- caret 颜色 `#00D9FF`
- selection 背景 `#00D9FF` 30% alpha
- placeholder(占位文字)颜色 `#4D5560`

#### 校验状态图标
- ✅ 通过:`#10B981` 绿色
- ❌ 失败:`#EF4444` 红色
- ⚠️ 重名:`#F59E0B` 橙色

#### 电脑信息条目
- 标签(如 "CPU:") 12px `#7D8590` 副文本
- 数值(如 "Intel Core i7-13700H") 14px 等宽字体 `#E6EDF3` 主文本
- 间距 8px

### 装饰元素(可选,挑 1-2 个加,别堆)

1. **顶部 accent line**:MainWindow 顶部 2px 高的渐变条(从 AccentCyan 渐变到 AccentPurple),暗示"科技感"
2. **CPU/内存条旁边加 monitor-style 进度条**(用 Border + Width 绑定数值,刷青色渐变)— **如果数据能拿到**(SystemInfoService 已有 CPU/内存读取逻辑,内存可以直接显示 %)
3. **"修改机器名"按钮 hover 时有微微上浮**(`RenderTransform TranslateY="-2px"` + DropShadow)

## 需要改的文件

| 文件 | 改动 |
|---|---|
| `src/ComputerRenameTool/ColorPalette.cs` | **新建** — 上面那堆 Color 静态属性 |
| `src/ComputerRenameTool/Resources/Styles.xaml` | **重写** — 所有控件样式 + 资源字典 |
| `src/ComputerRenameTool/Views/MainWindow.xaml` | **重排** — GroupBox → Border Card,加 accent line |
| `src/ComputerRenameTool/Views/UserControls/ComputerInfoSection.xaml` | **改样式** — 标签/数值字体 + 间距 |
| `src/ComputerRenameTool/Views/UserControls/HardwareInfoSection.xaml` | **改样式** — 标签/数值字体 + 间距(可选加 CPU/内存进度条) |
| `src/ComputerRenameTool/Views/UserControls/RenameSection.xaml` | **改样式** — 输入框 / 按钮 / 校验图标颜色 |
| `src/ComputerRenameTool/Views/RebootPromptWindow.xaml` | **改样式** — 按钮 / 倒计时数字 |

## 不要动

- ❌ 任何 `.cs` 业务代码(ViewModel / Service / Model)
- ❌ `App.xaml` / `App.xaml.cs`(除非需要全局资源)
- ❌ `ComputerRenameTool.csproj` / workflow / build 配置
- ❌ PRD/DESIGN 文档(只是 UI 美化,功能不变)

## 验收标准

- [ ] `dotnet build` 0 error
- [ ] `dotnet publish` EXE size < 100 MB(不应该增加太多,可能 +1-2 MB)
- [ ] UI 看起来"科技感深色",不是默认灰白
- [ ] 按钮 / 输入框 / 卡片样式符合上面规范
- [ ] 所有原来功能(改名 / 校验 / 重启弹窗)都正常
- [ ] **不改任何业务代码**(commit diff 只动 XAML + 新 ColorPalette.cs)

## 你的任务

1. 建 `ColorPalette.cs`(按上面规范)
2. 重写 `Styles.xaml`(深色资源字典 + 控件样式)
3. 改 4 个 XAML(布局 + 样式引用)
4. `dotnet build` 验证
5. commit + push
6. **直接 gh CLI 拿新 EXE + 上传 v1.0.0**(`gh release upload v1.0.0 <exe> --clobber`)
7. 报告:改了哪些文件、新 EXE md5、Release URL

记住:所有代码改动、build、release 全归你,hermes 不再改源码。
