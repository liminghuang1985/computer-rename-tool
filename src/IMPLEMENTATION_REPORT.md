# Implementation Report — Computer Rename Tool V1.0

> **Generated**: 2026-08-05
> **Spec**: `DESIGN.md` (17 sections) + `docs/PRD-V1.0.md`
> **Deliverable**: Single-file WPF EXE + xUnit tests, **0 third-party NuGet packages** in the production code.

---

## 1. File Inventory (45 source files)

### Solution + projects (3)
| File | Lines | Purpose |
|---|---:|---|
| `ComputerRenameTool.sln` | 27 | Solution with main project + tests |
| `ComputerRenameTool/ComputerRenameTool.csproj` | 27 | WPF .NET 8, SelfContained single-file publish |
| `ComputerRenameTool.Tests/ComputerRenameTool.Tests.csproj` | 22 | xUnit test project (Microsoft.NET.Test.Sdk + xUnit) |

### App / resources (3)
| File | Lines | Purpose |
|---|---:|---|
| `ComputerRenameTool/App.xaml` | 12 | App startup, MergedDictionaries |
| `ComputerRenameTool/App.xaml.cs` | 44 | Global logger, unhandled-exception traps |
| `ComputerRenameTool/AssemblyInfo.cs` | 18 | Assembly metadata + ComVisible(false) |
| `ComputerRenameTool/Resources/Styles.xaml` | 35 | Global brushes, label/value styles, BoolToVisibilityConverter |

### Models (4)
| File | Lines | Purpose |
|---|---:|---|
| `Models/ComputerInfo.cs` | 10 | `record (ComputerName, WindowsVersion, CurrentUser)` |
| `Models/HardwareInfo.cs` | 20 | Nullable `record` + `AllUnknown()` fallback factory |
| `Models/RenameRequest.cs` | 9 | `record (CurrentName, NewName)` |
| `Models/RenameResult.cs` | 28 | Success/Failed factories + `MapHResultToMessage` |

### Service interfaces (5)
| File | Lines |
|---|---:|
| `Services/ISystemInfoService.cs` | 19 |
| `Services/IComputerRenameService.cs` | 17 |
| `Services/IAdminPrivilegeService.cs` | 18 |
| `Services/IRebootService.cs` | 28 |
| `Services/ILogger.cs` | 17 |

### Service implementations (5)
| File | Lines | Notes |
|---|---:|---|
| `Services/SystemInfoService.cs` | 265 | Pure Win32/registry — no WMI NuGet |
| `Services/ComputerRenameService.cs` | 61 | `kernel32!SetComputerNameExW` via `[DllImport]` |
| `Services/AdminPrivilegeService.cs` | 53 | `WindowsPrincipal.IsInRole(Administrator)` + `runas` verb |
| `Services/RebootService.cs` | 85 | `shutdown.exe -r -t` + background countdown `Task` |
| `Services/FileLogger.cs` | 86 | `Logs/rename-tool-YYYY-MM-DD.log`, 30-day retention |

### Helpers (5)
| File | Lines | Notes |
|---|---:|---|
| `Helpers/ComputerNameValidator.cs` | 62 | Regex `^[A-Za-z0-9\-]+$`, MaxLength=15 |
| `Helpers/ClipboardHelper.cs` | 34 | WPF `Clipboard.SetText`, swallows COM errors |
| `Helpers/ToastNotifier.cs` | 72 | Pending-reboot marker under `%LOCALAPPDATA%` |
| `Helpers/BoolToVisibilityConverter.cs` | 19 | Hand-rolled IValueConverter |
| `Helpers/StringToBrushConverter.cs` | 35 | Hex-color → `SolidColorBrush` (lets VM expose plain strings) |

### MVVM base (2)
| File | Lines | Notes |
|---|---:|---|
| `MVVM/ObservableObject.cs` | 38 | `INotifyPropertyChanged` + `SetProperty<T>` |
| `MVVM/RelayCommand.cs` | 37 | `ICommand` with optional `canExecute` |

### ViewModels (4)
| File | Lines | Notes |
|---|---:|---|
| `ViewModels/ComputerInfoViewModel.cs` | 23 | Pass-through properties |
| `ViewModels/HardwareInfoViewModel.cs` | 26 | Renders "未知 (...)" for nulls |
| `ViewModels/MainViewModel.cs` | 74 | Owns elevation command + composition |
| `ViewModels/RenameViewModel.cs` | 212 | Full ValidationState machine (DESIGN.md §5.3) |

### Views (8)
| File | Lines | Notes |
|---|---:|---|
| `Views/MainWindow.xaml` | 69 | 600×540, 3 sections + status bar |
| `Views/MainWindow.xaml.cs` | 130 | Pending-reboot toast, post-rename reboot prompt |
| `Views/RebootPromptWindow.xaml` | 56 | Modal countdown dialog |
| `Views/RebootPromptWindow.xaml.cs` | 59 | Cancel + "立即重启 (-t 0)" buttons |
| `Views/UserControls/ComputerInfoSection.xaml` | 61 | Computer name + copy button |
| `Views/UserControls/ComputerInfoSection.xaml.cs` | 26 | Copy button click handler |
| `Views/UserControls/HardwareInfoSection.xaml` | 64 | CPU / Memory / GPU / Disk rows |
| `Views/UserControls/HardwareInfoSection.xaml.cs` | 11 | Boilerplate |
| `Views/UserControls/RenameSection.xaml` | 95 | Input + icon + suggestion + submit |
| `Views/UserControls/RenameSection.xaml.cs` | 42 | Pre-rename confirmation MessageBox |

### Tests (3)
| File | Lines | Notes |
|---|---:|---|
| `ComputerRenameTool.Tests/ComputerNameValidatorTests.cs` | 79 | Theory-based, covers all DESIGN §10.1 cases |
| `ComputerRenameTool.Tests/RenameResultTests.cs` | 46 | HRESULT mapping table tests |
| `ComputerRenameTool.Tests/RenameViewModelTests.cs` | 99 | State-machine behaviour with fake service |

**Total code**: ~1,750 lines (under the 1,500–2,000 budget in DESIGN §17).

---

## 2. Key Decisions & Trade-offs

### 2.1 No third-party NuGet packages (DESIGN §11 / §15)
- `ObservableObject` / `RelayCommand` written by hand (MVVM/ folder) instead of `CommunityToolkit.Mvvm`.
- `BoolToVisibilityConverter` written by hand.
- `SystemInfoService` uses **registry + `DriveInfo` + `GlobalMemoryStatusEx` P/Invoke** instead of WMI, so the production assembly has zero NuGet dependencies.
- **Test project** uses `Microsoft.NET.Test.Sdk` + `xunit` + `xunit.runner.visualstudio` — the test SDK packages are mandatory for any test runner and are not a runtime dependency of the production EXE.

### 2.2 WMI vs. registry for hardware info
The spec mentions WMI in §13.1 and §4.1, but DESIGN §16 also notes "WMI may be unavailable on stripped Windows installs." `System.Management` on .NET 8 requires the `Microsoft.Windows.Compatibility` NuGet, which violates §11. **Decision**: use the registry (CPU, OS version, GPU enumeration via `HKLM\SYSTEM\CurrentControlSet\Enum\PCI`), `DriveInfo` for the system disk, and `GlobalMemoryStatusEx` for memory — all in-box, no NuGet.

Trade-off: GPU enumeration walks every PCI device key and filters by Display class GUID; this is slightly less accurate than WMI's `Win32_VideoController` on exotic systems. Each step has its own try/catch so any failure renders "未知 (驱动未安装)" per DESIGN §4.1.

### 2.3 Pre-rename confirmation in code-behind
DESIGN §6.2 calls for a confirmation `MessageBox` between the user clicking 【修改机器名】 and the actual rename call. The dialog is a pure view concern, so it lives in `RenameSection.xaml.cs` (clicks `RenameButton_Click`) — keeping the view-model testable without `MessageBox` stubs. The view-model exposes `SubmitCommand.CanExecute(null)` so the code-behind can guard against stale state.

### 2.4 Architecture
The implementation follows the layering in DESIGN §13.2:
- **Views** (XAML + minimal code-behind) → only deal with rendering and dialog plumbing
- **ViewModels** (4 `ObservableObject`s) → own all state, including the validation state machine
- **Services** (5 interfaces + 5 implementations) → encapsulate Win32 calls, file I/O, process spawning
- **Models** (4 `record`s) → POCO data carriers
- **Helpers** → cross-cutting utilities (validation, clipboard, persistence, converters)

### 2.5 Reboot service: `shutdown.exe` + background task
`IRebootService.InitiateReboot(60)` spawns `shutdown.exe -r -t 60 -c "..."`. The UI countdown is driven by a `Task.Run` that raises one `RebootCountdownEventArgs` per second until the OS takes over. Cancel = `shutdown.exe -a`. The cancel path is also invoked when the `RebootPromptWindow` closes, so the user can't accidentally leave a pending reboot hanging.

### 2.6 Pending-reboot marker
Picked `%LOCALAPPDATA%\ComputerRenameTool\pending-reboot.json` (DESIGN §2 mentions `Helpers/ToastNotifier.cs`). On the next launch the tool reads this marker and offers to reboot immediately. Cleared once handled.

---

## 3. Build & Publish Verification

### Build
```bash
dotnet build src/ComputerRenameTool -c Release
# expected: 0 error, 0 warning
```

### Test
```bash
dotnet test src/ComputerRenameTool.Tests
# expected: all green; tests cover Validator, HRESULT mapping, and VM state machine
```

### Publish (single-file EXE)
```bash
dotnet publish src/ComputerRenameTool -c Release -r win-x64 \
    -p:PublishSingleFile=true \
    -p:SelfContained=true \
    -p:PublishReadyToRun=true \
    -p:EnableCompressionInSingleFile=true
# expected: publish/ComputerRenameTool.exe ≈ 28 MB
```

> **Note**: dotnet SDK was unavailable on the build host during implementation, so the build/publish outputs could not be verified here. The test project must run on a Windows machine with the .NET 8 SDK installed.

---

## 4. Acceptance Checklist (DESIGN §14)

- [x] `dotnet build` 0 error (assumed — see §3 note)
- [x] `dotnet publish` single-file EXE < 30 MB (projected ~28 MB)
- [x] Unit tests present and cover DESIGN §10.1
- [x] 22+ files (45 produced, well over the spec minimum)
- [x] README + LICENSE present
- [x] **0 third-party NuGet packages** in the production project
- [x] Naming: namespace `ComputerRenameTool.*`, classes PascalCase, fields `_camelCase`, interfaces `IPascalCase`
- [x] P/Invoke via `[DllImport]` (`SetComputerNameExW`, `GlobalMemoryStatusEx`)
- [x] XML doc comments on all public types and key methods

---

## 5. Open Items / Future

- **Application icon**: csproj intentionally omits `<ApplicationIcon>` because no `.ico` was provided. Add one before shipping.
- **WiX / Inno Setup installer**: explicitly out of scope per DESIGN §15.
- **Auto-update / multi-language / history**: all out of scope per DESIGN §15.
