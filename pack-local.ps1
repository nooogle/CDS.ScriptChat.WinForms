<#
.SYNOPSIS
    Builds, tests and packs CDS.ScriptChat, then copies the packages into a local
    NuGet feed folder for consumption by host apps.

.DESCRIPTION
    Versions come from MinVer. On a tagged commit that is the tag itself (V1.0.0
    -> 1.0.0); on any commit after it, a height-based prerelease such as
    1.0.1-alpha.0.7, which changes with every commit — so a consuming app never
    picks up a stale copy from the global NuGet cache.

    If a version IS reused (repacking the same commit), pass -Force to evict the
    matching folders from the global package cache. Without that, the consumer
    silently keeps the old assemblies.

.PARAMETER Feed
    Folder to copy packages into. Must be registered as a NuGet source — see the
    Setup notes below.

.PARAMETER SkipTests
    Skip the test run. For quick UI iteration only.

.PARAMETER Force
    Evict the packed versions from the global NuGet cache after copying.

.EXAMPLE
    .\pack-local.ps1

.EXAMPLE
    .\pack-local.ps1 -SkipTests -Force

.NOTES
    Setup, once per machine:
        dotnet nuget add source C:\dev\localfeed -n cds-local

    Then in the consuming app, either pin the released version:
        <PackageReference Include="CDS.ScriptChat.WinForms" Version="1.0.0" />

    ...or float, to pick up the newest local build on every restore:
        <PackageReference Include="CDS.ScriptChat.WinForms" Version="1.0.*-*" />

    The trailing -* matters: post-tag builds are prereleases (1.0.1-alpha.0.N),
    and a plain 1.0.* floating range excludes prerelease versions entirely.
#>
[CmdletBinding()]
param(
    [string]$Feed = 'C:\dev\localfeed',
    [string]$Configuration = 'Release',
    [switch]$SkipTests,
    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$repoRoot = $PSScriptRoot
$stagingDir = Join-Path $repoRoot 'artifacts\nuget'

function Invoke-Step
{
    param([string]$Name, [scriptblock]$Action)

    Write-Host "`n=== $Name ===" -ForegroundColor Cyan
    & $Action
    if ($LASTEXITCODE -ne 0) { throw "$Name failed (exit code $LASTEXITCODE)." }
}

# Staging is wiped each run so the copy step can't pick up packages from an
# earlier version and push them into the feed again.
if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
New-Item -ItemType Directory -Path $stagingDir | Out-Null

if (-not (Test-Path $Feed)) { New-Item -ItemType Directory -Path $Feed | Out-Null }

Invoke-Step 'Restore' { dotnet restore }
Invoke-Step 'Build' { dotnet build --no-restore --configuration $Configuration }

if (-not $SkipTests)
{
    # Microsoft.Testing.Platform projects — run as executables, not via dotnet test.
    $testProjects = Get-ChildItem -Path (Join-Path $repoRoot 'tests') -Filter '*.Tests.csproj' -Recurse
    foreach ($project in $testProjects)
    {
        Invoke-Step "Test $($project.BaseName)" {
            dotnet run --project $project.FullName --configuration $Configuration --no-build --no-restore
        }
    }
}

Invoke-Step 'Pack' {
    dotnet pack --no-build --no-restore --configuration $Configuration --output $stagingDir
}

$packages = Get-ChildItem -Path $stagingDir -Filter '*.nupkg'
if ($packages.Count -eq 0) { throw "Pack produced no packages. Check IsPackable in the src projects." }

if ($Force)
{
    foreach ($package in $packages)
    {
        # CDS.ScriptChat.Core.0.1.0-alpha.0.7.nupkg -> id and version
        if ($package.BaseName -notmatch '^(?<id>.+?)\.(?<version>\d+\.\d+\.\d+.*)$') { continue }

        $cached = Join-Path $env:USERPROFILE ".nuget\packages\$($Matches.id)\$($Matches.version)"
        if (Test-Path $cached)
        {
            Write-Host "Evicting $cached" -ForegroundColor Yellow
            Remove-Item $cached -Recurse -Force
        }
    }
}

Copy-Item -Path (Join-Path $stagingDir '*.nupkg') -Destination $Feed -Force
Copy-Item -Path (Join-Path $stagingDir '*.snupkg') -Destination $Feed -Force -ErrorAction SilentlyContinue

Write-Host "`nPublished to $Feed" -ForegroundColor Green
$packages | ForEach-Object { Write-Host "  $($_.Name)" }
