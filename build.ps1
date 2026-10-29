<#
.SYNOPSIS
    Builds fse-tool.exe with the version embedded.

.EXAMPLE
    ./build.ps1

.EXAMPLE
    ./build.ps1 -Version v0.2.0 -Output ./fse-tool.exe
#>
param([string]$Version, [string]$Output = './build/fse-tool.exe')

$ErrorActionPreference = 'Stop'

# Only Windows PowerShell's Add-Type can write an exe; PowerShell 7 hands off to it.
if ($PSVersionTable.PSEdition -eq 'Core') {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -Version $Version -Output $Output
    exit $LASTEXITCODE
}

Set-Location $PSScriptRoot

# Without -Version, describe the checkout: a tag, commits since it, and a dirty marker.
if (-not $Version) {
    $Version = git describe --tags --always --dirty 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $Version) { $Version = 'dev' }
}
$Version = $Version.Trim() -replace '^v', ''
$numeric = if ($Version -match '^(\d+)\.(\d+)\.(\d+)') { "$($Matches[1]).$($Matches[2]).$($Matches[3]).0" } else { '0.0.0.0' }

New-Item -ItemType Directory -Force -Path ./build | Out-Null
$versionFile = Join-Path $PSScriptRoot 'build/Version.g.cs'
Set-Content -Path $versionFile -Encoding UTF8 -Value @"
[assembly: System.Reflection.AssemblyVersion("$numeric")]
[assembly: System.Reflection.AssemblyFileVersion("$numeric")]
[assembly: System.Reflection.AssemblyInformationalVersion("$($Version -replace '"', '')")]
[assembly: System.Reflection.AssemblyProduct("fse-tool")]
"@

$sources = @((Get-ChildItem ./src/*.cs).FullName) + $versionFile
Add-Type -Path $sources -OutputAssembly $Output -OutputType ConsoleApplication
Write-Host "Built $Output ($Version)"
