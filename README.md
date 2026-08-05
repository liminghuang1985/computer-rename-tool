# Computer Rename Tool

Windows 机器名修改工具 — 给 IT 同事用的绿色单文件 EXE,查看电脑信息 + 修改机器名,无需安装。

![Build Status](https://github.com/liminghuang1985/computer-rename-tool/actions/workflows/build.yml/badge.svg)

## 功能

- 📋 查看电脑基础信息(机器名 / Windows 版本 / 当前用户)
- 🖥️ 查看电脑配置(CPU / 内存 / 显卡 / 硬盘)
- ✏️ 修改机器名(实时校验 + 建议名)
- 🔒 自动检测管理员权限 + UAC 提权
- 🔄 改名后弹重启选择(立即 / 稍后)
- 📝 完整操作日志

## 下载

从 [Releases](https://github.com/liminghuang1985/computer-rename-tool/releases) 下载最新的 `ComputerRenameTool.exe`,双击运行。

## 系统要求

- Windows 10 / 11(64 位)
- 无需安装,无外部依赖

## 使用

1. 双击 `ComputerRenameTool.exe`
2. 如果弹出 UAC 提权,点"是"
3. 查看电脑信息
4. 输入新机器名(实时校验)
5. 点【修改机器名】,确认,选立即重启 / 稍后重启

## 机器名规则

| 允许 | 不允许 |
|---|---|
| A-Z, a-z, 0-9, - | 中文 / 空格 / `_` / `/` / `\` / `.` |

最大长度 15 字符。

## 项目结构

```
computer-rename-tool/
├── docs/PRD-V1.0.md              # 需求文档
├── DESIGN.md                     # 设计说明(给开发者)
├── src/ComputerRenameTool/       # 源码
│   ├── Models/
│   ├── ViewModels/
│   ├── Views/
│   ├── Services/
│   ├── MVVM/
│   └── Helpers/
├── .github/workflows/build.yml   # CI:Windows runner build
└── README.md
```

## 本地开发

需要 .NET 8 SDK。

```bash
# 编译
dotnet build src/ComputerRenameTool

# 跑(Windows)
dotnet run --project src/ComputerRenameTool

# 单元测试
dotnet test src/ComputerRenameTool.Tests

# 发布单文件 EXE
dotnet publish src/ComputerRenameTool -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:EnableCompressionInSingleFile=true
```

## 文档

- [PRD V1.0](docs/PRD-V1.0.md) — 产品需求
- [Design Document](DESIGN.md) — 架构 + 接口

## License

MIT
