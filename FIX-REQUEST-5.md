# FIX-REQUEST-5:应用用户设计的头像(2026-08-05)

## 需求
用户提供了 EXE 图标设计(盾形 + Windows logo + 计算机名 banner + 齿轮装饰 + 深蓝渐变背景 + 二进制代码粒子)。需要把这个 PNG 转成 .ico,嵌入 EXE 资源。

## 源图位置
`docs/icon-source.png`(564 KB,用户上传的原始 PNG)

## 视觉描述(供参考,如要调可改)
- 深蓝渐变背景 (#0A1A3A → #1E40AF 之类)
- 漂浮的二进制数字 (0/1) 粒子
- 中央银色盾形徽章(带斜角边)
  - 顶部:Windows logo(四色方块)
  - 中部:浅米色 banner "COMPUTERNAME" + 鼠标 cursor
  - 下部:数字显示 "NEW-PC" cyan 发光
  - 底部齿轮装饰
  - ribbon banner "ComputerRenameTool" 白色字

## 你的任务

### 1. 准备 .ico 文件
**icon 制作要求**(Windows EXE icon):
- 包含**多尺寸**: 16x16 / 32x32 / 48x48 / 64x64 / 128x128 / 256x256(Windows 资源管理器会按需选)
- ICO 格式(不是 PNG,不是 ICNS)
- Windows 自带工具不能用,在 macOS 上用以下任一:

**方案 A:ImageMagick**(推荐,CLI 简单)
```bash
# brew install imagemagick
brew install imagemagick
magick convert icon-source.png \
  -define icon:auto-resize=16,32,48,64,128,256 \
  -background none \
  Resources/app.ico
# 或老版本:
convert icon-source.png \
  -define icon:auto-resize=16,32,48,64,128,256 \
  Resources/app.ico
```

**方案 B:Python + Pillow**
```bash
pip3 install Pillow
python3 -c "
from PIL import Image
img = Image.open('docs/icon-source.png')
sizes = [(16,16),(32,32),(48,48),(64,64),(128,128),(256,256)]
img.save('Resources/app.ico', sizes=sizes)
"
```

**方案 C:.NET + System.Drawing**(需 NuGet,谨慎)
- 不推荐,windows-only 且需 NuGet

### 2. 修改 .csproj
`src/ComputerRenameTool/ComputerRenameTool.csproj` 加:
```xml
<PropertyGroup>
    <ApplicationIcon>Resources/app.ico</ApplicationIcon>
</PropertyGroup>
```

### 3. 验证 build
- `dotnet build` 0 error
- 编译产物 .exe 包含 icon(可以用 `xxd ComputerRenameTool.exe | grep "icon"` 看)
- publish 后 EXE 仍包含 icon(单文件 publish 会把 icon 编进 PE header)

### 4. 检查 EXE 资源
GitHub Actions windows-latest runner 上 build 完后:
- 找 `obj/Release/net8.0-windows/win-x64/ComputerRenameTool.exe` 用 Properties → Details 看 icon
- 或 PowerShell:
  ```powershell
  $shell = New-Object -COMObject Shell.Application
  $folder = $shell.NameSpace("path\to\exe")
  $file = $folder.ParseName("ComputerRenameTool.exe")
  $file.GetDetailsOf(1, 1)  # Name
  ```

### 5. 单元测试(可选)
- 验证 app.ico 存在且非空
- 验证 .csproj 含 ApplicationIcon

### 6. 不做
- ❌ 不要改 icon 颜色 / 内容(用户已设计好)
- ❌ 不要只放单尺寸(要 16+32+48+64+128+256)
- ❌ 不要嵌入 .icns(macOS 用,Windows 不认)

## 验收(用户验收)
- [ ] 装新 EXE 后,EXE 文件图标变成盾形设计
- [ ] 任务栏里 ComputerRenameTool.exe 显示盾形图标
- [ ] Alt+Tab 切换窗 显示盾形图标
- [ ] 启动器 / 开始菜单 显示盾形图标
- [ ] 所有尺寸(任务栏小图 / 任务视图大图 / 开始菜单) 都正常

## commit + push + build + release
跟之前一样流程,hermes 不动手。完成后报告:
- 改了哪些文件
- 新 EXE md5
- Release URL
