<#
.SYNOPSIS
    Writes the GitHub release notes for one version: its CHANGELOG.md section plus download and verification help.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\tools\release-notes.ps1 -Version 0.1.3 -OutFile artifacts\RELEASE_NOTES.md
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string] $Version,
    [Parameter(Mandatory)] [string] $OutFile
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = Split-Path $PSScriptRoot -Parent
$changelog = Get-Content -LiteralPath (Join-Path $root 'CHANGELOG.md') -Encoding UTF8

$section = New-Object System.Collections.Generic.List[string]
$inSection = $false
foreach ($line in $changelog) {
    if ($line -match '^## \[(?<v>[^\]]+)\]') {
        if ($inSection) { break }
        $inSection = $Matches.v -eq $Version
        continue
    }
    if ($inSection) { $section.Add($line) }
}
if (-not $inSection -and $section.Count -eq 0) { throw "CHANGELOG.md has no section for $Version." }

$notes = @"
$(($section -join "`n").Trim())

## Download

| File | For |
|---|---|
| **BandBar-Setup-$Version.exe** | Recommended. Installs for the current user, no administrator rights. Installing over an older version upgrades it and keeps your settings. |
| BandBar-$Version-portable.zip | Unzip anywhere and run ``BandBar.exe``. Delete the folder to remove it. |
| SHA256SUMS.txt | Checksums of both files. |

**Requirements:** Windows 11 or Windows 10 1903+ (64-bit; 1809 with .NET Framework 4.8); a licensed NetLimiter 5 installation.

The files are not code-signed yet, so Windows SmartScreen may show a warning on first run (**More info → Run anyway**).
To verify a download, compare ``Get-FileHash <file> -Algorithm SHA256`` with ``SHA256SUMS.txt``.

BandBar is an independent project and is not affiliated with Locktime Software, the developer of NetLimiter.
"@

$dir = Split-Path $OutFile -Parent
if ($dir -and -not (Test-Path -LiteralPath $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[IO.File]::WriteAllText($OutFile, $notes.Replace("`r`n", "`n"), (New-Object Text.UTF8Encoding($false)))
Write-Host "Release notes for $Version written to $OutFile"
