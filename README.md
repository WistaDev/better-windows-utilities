# BWU — Better Windows Utilities

**Make Windows Better.**

All-in-one Windows companion: optimize, customize, maintain, clean and expand Windows.
C# · .NET 8 · WPF · MVVM · Windows 10 / 11 (x64) · Dark + Blue theme.

> **Status of this source drop:** all modules below are implemented in code, but this code was written
> without access to a Windows machine or the .NET SDK, so it has **not been compiled or run yet**.
> Expect to fix a few compile errors on the first build, and run the test checklist at the bottom
> on Windows 10 and Windows 11 before releasing (roadmap Stage 6).

## Build

Requirements: Windows 10/11 x64, **.NET 8 SDK**, optionally [NSIS](https://nsis.sourceforge.io) for the installer.

```powershell
.\build.ps1                       # restore, build, publish (self-contained) and, if NSIS exists, create dist\BWU-Setup-1.0.0.exe
# or manually
dotnet build BWU.sln -c Release
dotnet run --project BWU\BWU.csproj
```

`global.json` pins the 8.0.x SDK (`rollForward: latestFeature`). This is deliberate: a machine that also has
.NET 10 SDK installed can otherwise pick it and fail with `hostpolicy.dll` errors during `dotnet publish`.
Open `BWU.sln` in Visual Studio 2022 17.8+ (or use `dotnet` from a terminal).

## Administrator model

BWU starts as a **normal user** (`asInvoker`). Any action that needs elevation (Compact OS, SFC/DISM, HKLM settings,
Windows\Temp, Prefetch read, restore points, service changes) first explains why, then offers
**"Restart BWU as administrator"**. Nothing silently elevates.

## Modules

| Module | What it really does |
|---|---|
| Dashboard | Windows edition/version/build (registry), CPU (WMI), RAM (`GetPhysicallyInstalledSystemMemory`), architecture, system-drive capacity/free space, BWU version. Anything unreadable shows "Unavailable" — never invented data. |
| System Information | Windows, processor, memory modules, volumes and physical disks (MSFT_PhysicalDisk), BIOS/UEFI/Secure Boot, manufacturer/model. "Copy report" to clipboard. |
| Optimization | Reversible registry settings + two profiles; startup entries (Run keys, enabled/disabled through the same `StartupApproved` mechanism as Task Manager, nothing deleted); a reviewed list of optional services that can be set to **Manual** (original start type saved, one-click restore). Critical services are not listed. |
| Debloater | `Get-AppxPackage` for the current user, classified Optional / Other / **Protected** (frameworks, non-removable, system and dependency packages cannot be selected). Review dialog, `Remove-AppxPackage`, per-app result. |
| Customization | winver "licensed to" owner/organization, OEM manufacturer/model/support phone/hours/URL/logo (any image converted to the 120×120 BMP Windows needs), personalization settings, `ms-settings:` shortcuts. All values are backed up and restorable. Windows updates may overwrite them. BWU does **not** edit the Windows version/build shown by winver (it can break servicing). |
| Package Manager | Detects `winget`, install/repair via Microsoft's official `Microsoft.WinGet.Client` module or the Store page, search, installed list, install, uninstall, live output and progress. Only winget's own sources are used; BWU downloads nothing itself. |
| Maintenance | `sfc /verifyonly`, `sfc /scannow`, DISM Check/Scan/RestoreHealth, online `chkdsk /scan` (read-only), restore point (verified afterwards — failure is reported as failure), basic diagnostics, Windows Update shortcuts. Real output is streamed. |
| **More Spaces** | Compact OS query/enable/disable through `compact.exe /CompactOS` (state checked before and verified after), Windows Temp and User Temp analysis + cleanup, Prefetch analysis (read-only, never deleted), storage analysis, DISM component-store cleanup (no `/ResetBase`), overall progress and a session summary. |
| Privacy | Advertising ID, tailored experiences, suggestions, feedback prompts, diagnostic-data policy, activity history, background Store apps — each explained, backed up, reversible. Shortcuts to Windows permission pages. Security features are never touched. |
| Settings | Dark/Light theme, language, start with Windows, update check, administrator status/restart, About. |

### How temp cleaning stays safe
A file is deleted only if it is older than 1 hour, not read-only/system, not a symlink/junction (and not reached through
one), and can be opened **exclusively** at that moment (so nothing in use is touched). Nothing is force-deleted and no
attributes are changed. The folder itself must be named `Temp`/`Tmp` and not be a drive root, so a misconfigured `%TEMP%`
cannot cause damage. Recoverable space is always labelled an *estimate*; results show what was actually removed and skipped.

### Backups
Original registry values are saved before the first change to `%APPDATA%\BWU\backups\backup.json` (first original is never
overwritten). "Revert/Restore" returns to that original. Uninstalling BWU keeps this folder on purpose.

## Known limitations of v1.0
- **Compact OS detection** parses the text of `compact.exe /CompactOS:query` (English and Brazilian Portuguese patterns).
  On other languages the state is shown as *Unknown* with the raw output instead of guessing.
- **Language** covers navigation and page headers only (English / Português-BR); page bodies are English.
- **Update checking** needs an HTTPS JSON manifest URL (`{"version":"1.1","url":"https://..."}`) that you host; none is bundled.
- `winget` tables are parsed from text output; very long names may be truncated by winget itself (the Id is what matters).
- Startup review covers the registry Run keys (not Startup-folder shortcuts or scheduled tasks).
- Developer name is set in `Services/AppInfo.cs`.

## Project layout
```
BWU/
├─ BWU.sln · global.json · build.ps1 · installer/bwu.nsi
└─ BWU/
   ├─ App.xaml(.cs) · MainWindow.xaml(.cs) · app.manifest
   ├─ Themes/      Colors.xaml (dark) · ColorsLight.xaml · Styles.xaml
   ├─ Views/       one UserControl per module (no logic)
   ├─ ViewModels/  MVVM view models, async commands
   ├─ Models/      records and small data classes
   ├─ Services/    system operations only (no UI)
   │  └─ MoreSpaces/  CompactOSService · WindowsTempService · UserTempService · PrefetchService · StorageAnalysisService · ComponentStoreService
   └─ Utilities/   ProcessRunner · AdminHelper · RelayCommand · converters · native methods
```

## Test checklist (Stage 6)
Run on Windows 10 22H2 and Windows 11, as a standard user **and** as administrator:
1. Dashboard/System Information values match `winver`, Task Manager and Settings > About.
2. Optimization: apply then revert each setting; disable/enable a startup entry; Manual/Restore a service; compare with Task Manager and `services.msc`.
3. Debloater: remove a harmless optional app (e.g. Tips) and confirm protected items cannot be ticked.
4. Package Manager: search, install, list and uninstall a small package; try the Repair path on a spare VM.
5. More Spaces: analyze; clean User Temp with an app holding a file open (file must be skipped); Compact OS enable → verify → disable; check state text on your Windows language.
6. Maintenance: run each operation once; confirm the restore-point result matches `rstrui.exe`.
7. Privacy/Customization: apply, check the Windows UI, revert; confirm OEM info in Settings > About.
8. Build the NSIS installer, install, start, uninstall.
