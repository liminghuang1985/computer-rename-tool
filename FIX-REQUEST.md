# 修复 build 报错(actions run 30984223918)

## 错因汇总(全部是 `using System;` 缺失)

### 1. `src/ComputerRenameTool/MVVM/RelayCommand.cs`
缺 `using System;`,导致以下找不到:
- `Action`
- `Action<>`
- `Func<>`
- `Func<,>`
- `EventHandler`
- `'RelayCommand.CanExecuteChanged' cannot implement 'ICommand.CanExecuteChanged' because it does not have the matching return type of 'EventHandler'.`

修法:在文件顶部加 `using System;`

### 2. `src/ComputerRenameTool/App.xaml.cs`
第 37 行用到 `UnhandledExceptionEventArgs` 缺 `using System;`

修法:在文件顶部加 `using System;`

## 触发链
Windows runner build 报错 8 个,全部是 `using System;` 缺失:

```
build: src/ComputerRenameTool/MVVM/RelayCommand.cs#25
  X 'RelayCommand.CanExecuteChanged': event must be of a delegate type
  X The type or namespace name 'Func<,>' could not be found
  X The type or namespace name 'Action<>' could not be found
  X The type or namespace name 'Func<>' could not be found
  X The type or namespace name 'Action' could not be found
  X The type or namespace name 'EventHandler' could not be found
  X 'RelayCommand' does not implement interface member 'ICommand.CanExecuteChanged'

build: src/ComputerRenameTool/App.xaml.cs#37
  X The type or namespace name 'UnhandledExceptionEventArgs' could not be found
```

## 你的任务
1. 给 `src/ComputerRenameTool/MVVM/RelayCommand.cs` 加 `using System;`
2. 给 `src/ComputerRenameTool/App.xaml.cs` 加 `using System;`
3. commit + push
4. 等 GitHub Actions 重 build 成功(预计 1-2 分钟)
5. 报告:build 状态 + EXE artifact URL

## 修完后做
- 看是否还有其他文件缺 `using System;`(grep `Action` / `Func` / `EventHandler` / `Type` / `Exception` 看哪些文件没 using System 就在 build 错)
- 这是第三次 build 失败类似错因,务必检查所有 helper / viewmodel / service 文件

## 根因分析
你之前写 .cs 时习惯不显式写 `using System;`,但 .NET 8 SDK 默认 `<ImplicitUsings>disable</ImplicitUsings>` 时,System 命名空间不自动导入。要么:
A. 每个文件加 `using System;`(你之前漏了)
B. `.csproj` 加 `<ImplicitUsings>enable</ImplicitUsings>`(推荐,以后所有 .cs 都不用写)

**建议选 B**,一次解决所有类似问题。Microsoft 默认 .NET 8 项目模板都开 ImplicitUsings。

## 验收
- [ ] build 通过
- [ ] EXE artifact 上传(预计 28-30 MB)
- [ ] 报告 EXE 下载 URL
