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
| B7 | Settings → Personalization → Colors: switch the Windows mode between Light and Dark | The text turns dark on the light taskbar and white on the dark one, without restarting BandBar |
| B8 | Show the accent colour on the taskbar (Colors → "Show accent color on Start and taskbar") with a light and with a dark accent | The text stays readable |

### Taskbar

| # | Steps | Expected |
|---|---|---|
| T1 | Click the taskbar, open Start, right-click taskbar buttons, switch windows | The bar stays visible; Start menu and jump lists appear above it |
| T2 | Start a program that adds a tray icon, then close it | The bar moves left/right within about a second |
| T3 | Restart Windows Explorer (Task Manager) | The bar returns to its place |
| T4 | Move the taskbar to each screen edge (if your setup allows) | The bar follows on the taskbar surface |
| T5 | Change display scaling or resolution | The bar re-scales and stays inside the taskbar |
| T6 | Switch virtual desktops (Win+Ctrl+← / →) | The bar stays on the taskbar on every desktop |
| T7 | On Windows 10, open many windows so taskbar buttons reach the notification area | Known limitation: the bar can overlap buttons; nothing breaks |

### Full screen and games

| # | Steps | Expected |
|---|---|---|
| F1 | Start a game in borderless windowed mode | The bar disappears; the log shows `Full-screen application opened; hiding` |
| F2 | Leave the game or Alt+Tab to the desktop | The bar comes back |
| F3 | Repeat F1–F2 with exclusive full screen | Same as F1–F2 |
| F4 | Play a game protected by anti-cheat | No warning, kick or stutter |
| F5 | Start BandBar while a full-screen game is already in front | Known limitation until 0.3.0: the bar stays visible until that game closes |

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
| I5 | Look at the Start menu entry, the setup file and Windows Settings → Apps | BandBar's own icon (red and green arrows) is shown |
| I6 | Run the setup on Windows 10 1809 without .NET Framework 4.8 | A clear message, setup ends without installing anything |

## Results

| Version | Environment | Result |
|---|---|---|
| 0.1.0 | Windows 11, NetLimiter 5.3 | Automated checks; B1, B2, B5, F1–F2 (simulated full-screen window); no listening ports or network connections; idle CPU about 0.02 %, about 40 MB private memory |
| 0.1.1 | Windows 11, NetLimiter 5.3 | B1, B2, B4–B6, T2, T3, F1–F4 passed (F4 with a title using kernel-mode anti-cheat). T1 failed: brief disappearance on Start and on taskbar right-click |
| 0.1.2 | Windows 11, NetLimiter 5.3 | B3 passed. T1 re-test pending |
| 0.1.3 | Windows 11, NetLimiter 5.3 | I1 passed (per-user install, Start menu entry, optional startup shortcut). Installer built and checksums verified by CI |
| 0.1.4 | Windows 11, NetLimiter 5.3 | Rename release (no behaviour change). I1 passed (installed per user with startup shortcut, runs); I3 passed for the previous version (uninstall left no folder, shortcut or uninstall entry); I4 verified by CI and after download |
| 0.1.5 | Windows 11, NetLimiter 5.3 | B2, B7, B8 passed (text follows light/dark mode and accent colour without restart); normal use unaffected. T1: a brief flicker remains on Start and taskbar right-click (known limitation; check interval shortened from 50 to ~16 ms afterwards). I1–I5 pending on the published build |
