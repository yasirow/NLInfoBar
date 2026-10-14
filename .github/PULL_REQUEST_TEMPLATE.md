## What and why

<!-- What does this change, and which issue does it address? -->

## Checklist

- [ ] Follows the ground rules in [CONTRIBUTING.md](../CONTRIBUTING.md): no new runtime dependency, no hooks or
      injection, no network, no elevation, nothing written outside the app's own folder
- [ ] Any new Win32 call is added to the allow-list in [docs/DESIGN.md §7.2](../docs/DESIGN.md#72-allowed-win32-calls)
- [ ] [docs/DESIGN.md](../docs/DESIGN.md) updated if behaviour or a rule changed
- [ ] Entry added under `## [Unreleased]` in [CHANGELOG.md](../CHANGELOG.md)
- [ ] Builds with zero warnings (`build.ps1`)
- [ ] No personal data in code, logs, screenshots or this description

## Tests

<!-- Which parts of docs/TESTING.md did you run, and on which Windows / NetLimiter versions? -->
