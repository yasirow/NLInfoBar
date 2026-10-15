# Testing

BandBar talks to a Windows service and to the Windows shell, so most of its behaviour can only be verified on a
real desktop. Every release goes through the automated checks below and the manual test plan.

## Automated checks (every build, locally and in CI)

| Check | Where | Fails the build when |
|---|---|---|
| Warnings as errors | `Directory.Build.props` | Any compiler or MSBuild warning |
| NetLimiter API pin | `VerifyNetLimiterApi` target | The compile-time `NetLimiter.dll` does not match the pinned SHA-256 |
| Locked restore | `build.ps1` | A package differs from `packages.lock.json` |
| Single-file output | `VerifyOutputIsSingleExe` target | The output contains anything besides `BandBar.exe` and `BandBar.exe.config` |
| Installer build | `tools/package.ps1 -RequireInstaller` (CI) | The Inno Setup script does not compile |
| Tag matches version | Release workflow | The pushed tag differs from `<Version>` in the project |

## Manual test plan

Run the build to test from `src\BandBar\bin\Release\net48\BandBar.exe`, or install the release candidate.
The log (`bandbar.log`, next to the executable) should contain no `WARN` or `ERROR` lines after a normal session.

### Basics

| # | Steps | Expected |
|---|---|---|
| B1 | Start BandBar with NetLimiter running | Two lines appear just before the notification area: red ⬆ upload on top, green ⬇ download below; values follow NetLimiter's Internet zone |
| B2 | Hover the bar | Tooltip: `BandBar <version>`, zone, `Connected`, NetLimiter version |
| B3 | Type in any window, left-click the bar, keep typing | The typing window keeps focus |
| B4 | Right-click the bar; click elsewhere; right-click again and choose Exit | Menu opens, closes on the outside click, Exit closes the bar |
| B5 | Start the executable a second time | Still exactly one bar |
| B6 | Lock (Win+L) and unlock | The bar keeps updating after unlock |

### Taskbar

| # | Steps | Expected |
|---|---|---|
| T1 | Click the taskbar, open Start, right-click taskbar buttons, switch windows | The bar stays visible; Start menu and jump lists appear above it |
| T2 | Start a program that adds a tray icon, then close it | The bar moves left/right within about a second |
| T3 | Restart Windows Explorer (Task Manager) | The bar returns to its place |
| T4 | Move the taskbar to each screen edge (if your setup allows) | The bar follows on the taskbar surface |
| T5 | Change display scaling or resolution | The bar re-scales and stays inside the taskbar |

### Full screen and games

| # | Steps | Expected |
|---|---|---|
| F1 | Start a game in borderless windowed mode | The bar disappears; the log shows `Full-screen application opened; hiding` |
| F2 | Leave the game or Alt+Tab to the desktop | The bar comes back |
| F3 | Repeat F1–F2 with exclusive full screen | Same as F1–F2 |
| F4 | Play a game protected by anti-cheat | No warning, kick or stutter |

### Service and failure handling

| # | Steps | Expected |
|---|---|---|
| S1 | Stop the NetLimiter service (`services.msc`, needs administrator; protection pauses briefly) | Bar shows grey arrows and `—`; no dialog |
| S2 | Start the service again | Speeds return within a few seconds |
| S3 | Start BandBar on a PC without NetLimiter | Bar shows `NL?`; no dialog |

### Installer (release candidates)

| # | Steps | Expected |
|---|---|---|
| I1 | Run `BandBar-Setup-x.y.z.exe` | No administrator prompt; installs to `%LocalAppData%\Programs\BandBar`; Start menu entry |
| I2 | Install a newer version over it | Upgrades in place; `settings.json` kept |
| I3 | Uninstall from Windows Settings → Apps | The install folder, shortcuts and uninstall entry are gone; nothing else changed |
| I4 | Check `SHA256SUMS.txt` against the downloads | Hashes match |

## Results

| Version | Environment | Result |
|---|---|---|
| 0.1.0 | Windows 11, NetLimiter 5.3 | Automated checks; B1, B2, B5, F1–F2 (simulated full-screen window); no listening ports or network connections; idle CPU about 0.02 %, about 40 MB private memory |
| 0.1.1 | Windows 11, NetLimiter 5.3 | B1, B2, B4–B6, T2, T3, F1–F4 passed (F4 with a title using kernel-mode anti-cheat). T1 failed: brief disappearance on Start and on taskbar right-click |
| 0.1.2 | Windows 11, NetLimiter 5.3 | B3 passed. T1 re-test pending |
| 0.1.3 | Windows 11, NetLimiter 5.3 | I1 passed (per-user install, Start menu entry, optional startup shortcut). Installer built and checksums verified by CI |
| 0.1.4 | — | Rename release (no behaviour change). Automated checks; I1–I4 pending on the published build |
