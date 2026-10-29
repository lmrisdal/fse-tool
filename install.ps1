<#
.SYNOPSIS
    Installs fse-tool and adds it to the user or system PATH.

.EXAMPLE
    irm https://raw.githubusercontent.com/lmrisdal/fse-tool/main/install.ps1 | iex

.EXAMPLE
    & ([scriptblock]::Create((irm https://raw.githubusercontent.com/lmrisdal/fse-tool/main/install.ps1))) -Scope Machine

.EXAMPLE
    irm https://raw.githubusercontent.com/lmrisdal/fse-tool/main/uninstall.ps1 | iex
#>
# No validation attributes here: under `irm | iex` these parameters become variables in the
# caller's scope with no value, and an attribute like ValidateSet would reject that.
param($Scope)

# Everything lives in one function so that `irm | iex`, which runs in the caller's scope,
# doesn't leave helper functions or preference changes behind in the user's session.
function Invoke-FseToolInstaller {
    param([string]$Scope)

    $ErrorActionPreference = 'Stop'
    if ($Scope -and $Scope -notin 'User', 'Machine') {
        throw "Invalid -Scope '$Scope'. Use User or Machine."
    }
    $ProgressPreference = 'SilentlyContinue'
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

    $scriptUrl = 'https://raw.githubusercontent.com/lmrisdal/fse-tool/main/install.ps1'
    $uninstallUrl = 'https://raw.githubusercontent.com/lmrisdal/fse-tool/main/uninstall.ps1'
    $exeUrl = 'https://github.com/lmrisdal/fse-tool/releases/latest/download/fse-tool.exe'
    $scriptPath = $PSCommandPath

    function Get-InstallDir([string]$Scope) {
        if ($Scope -eq 'Machine') { Join-Path $env:ProgramFiles 'fse-tool' }
        else { Join-Path $env:LOCALAPPDATA 'Programs\fse-tool' }
    }

    function Get-EnvironmentKey([string]$Scope) {
        if ($Scope -eq 'Machine') { 'HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Environment' }
        else { 'HKCU:\Environment' }
    }

    function Test-SamePath([string]$A, [string]$B) {
        [Environment]::ExpandEnvironmentVariables($A).TrimEnd('\') -ieq [Environment]::ExpandEnvironmentVariables($B).TrimEnd('\')
    }

    function Get-PathEntries([string]$Scope) {
        # Read the raw value so entries such as %USERPROFILE%\bin stay unexpanded when written back.
        $raw = (Get-Item (Get-EnvironmentKey $Scope)).GetValue('Path', '', 'DoNotExpandEnvironmentNames')
        @($raw -split ';' | Where-Object { $_ })
    }

    function Set-PathEntries([string]$Scope, [string[]]$Entries) {
        Set-ItemProperty -Path (Get-EnvironmentKey $Scope) -Name Path -Value ($Entries -join ';') -Type ExpandString
        # A registry write doesn't notify Explorer and other programs; a .NET environment change does.
        [Environment]::SetEnvironmentVariable('FSE_TOOL_INSTALLER', '1', $Scope)
        [Environment]::SetEnvironmentVariable('FSE_TOOL_INSTALLER', $null, $Scope)
    }

    function Install-FseTool([string]$Scope) {
        $dir = Get-InstallDir $Scope
        New-Item -ItemType Directory -Force -Path $dir | Out-Null
        $exe = Join-Path $dir 'fse-tool.exe'
        Invoke-WebRequest -Uri $exeUrl -OutFile $exe -UseBasicParsing

        # The home app runs a copy of the exe, so refresh it when one is registered.
        if ((& $exe home status 2>$null) -in 'Installed', 'Selected') {
            & $exe home install | Out-Null
            if ($LASTEXITCODE -ne 0) { Write-Warning 'Could not update the fse-tool home app. Run "fse-tool home install" to retry.' }
        }

        $entries = Get-PathEntries $Scope
        if (-not ($entries | Where-Object { Test-SamePath $_ $dir })) {
            Set-PathEntries $Scope ($entries + $dir)
        }
    }

    function Test-Administrator {
        $principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
        $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    }

    # Runs this installer again in an elevated PowerShell. Under `irm | iex` there is no script file
    # to relaunch, so the elevated process downloads the script again instead.
    function Invoke-Elevated([string]$Arguments) {
        $log = Join-Path ([IO.Path]::GetTempPath()) "fse-tool-install-$PID.log"
        $invoke = if ($scriptPath) { "& '$($scriptPath -replace "'", "''")' $Arguments" }
                  else { "& ([scriptblock]::Create((Invoke-RestMethod '$scriptUrl'))) $Arguments" }
        $command = "[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12; " +
                   "try { $invoke } catch { Set-Content -LiteralPath '$log' -Value `$_; exit 1 }"
        $encoded = [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($command))

        Write-Host 'Requesting administrator rights...'
        try {
            $process = Start-Process (Get-Process -Id $PID).Path -Verb RunAs -Wait -PassThru -WindowStyle Hidden `
                -ArgumentList '-NoProfile', '-ExecutionPolicy', 'Bypass', '-EncodedCommand', $encoded
        } catch {
            throw 'Administrator rights are required for a system-wide install. Run the installer again and accept the prompt, or choose User.'
        }
        if ($process.ExitCode -ne 0) {
            $message = if (Test-Path $log) { (Get-Content -Raw $log).Trim() } else { "exit code $($process.ExitCode)" }
            Remove-Item -Force $log -ErrorAction SilentlyContinue
            throw "The elevated installer failed: $message"
        }
    }

    function Add-SessionPath([string]$Dir) {
        if (-not ($env:Path -split ';' | Where-Object { $_ -and (Test-SamePath $_ $Dir) })) {
            $env:Path = ($env:Path.TrimEnd(';'), $Dir) -join ';'
        }
    }

    $isAdmin = Test-Administrator
    $installed = @(@('User', 'Machine') | Where-Object { Test-Path (Get-InstallDir $_) })
    $scopes = @($Scope | Where-Object { $_ })

    # The plain one-liner leaves an existing install alone; pass -Scope to reinstall over it.
    if (-not $Scope -and $installed) {
        $where = ($installed | ForEach-Object { Get-InstallDir $_ }) -join ' and '
        Write-Host "fse-tool is already installed in $where."
        Write-Host 'Run "fse-tool update" to update it, or uninstall it with:'
        Write-Host "  irm $uninstallUrl | iex"
        return
    }

    if (-not $scopes) {
        $choices = [Management.Automation.Host.ChoiceDescription[]]@(
            [Management.Automation.Host.ChoiceDescription]::new('&User', "Install for the current user in $(Get-InstallDir User)."),
            [Management.Automation.Host.ChoiceDescription]::new('&System', "Install for all users in $(Get-InstallDir Machine). Requires administrator rights.")
        )
        $answer = $Host.UI.PromptForChoice('Install fse-tool', 'Add fse-tool to the user PATH or the system PATH?', $choices, 0)
        $scopes = @(@('User', 'Machine')[$answer])
    }

    Write-Host 'Downloading the latest fse-tool.exe...'
    foreach ($s in $scopes) {
        if ($s -eq 'Machine' -and -not $isAdmin) { Invoke-Elevated '-Scope Machine' }
        else { Install-FseTool $s }

        $dir = Get-InstallDir $s
        Add-SessionPath $dir
        Write-Host "Installed fse-tool to $dir and added it to the $($s.ToLower()) PATH."
    }
    Write-Host 'Run "fse-tool status" to try it. Terminals that were already open need a restart to see the PATH change.'
    Write-Host 'To use fse-tool as the XBOX mode home app, turn on Developer Mode and run "fse-tool home install".'
}

Invoke-FseToolInstaller -Scope $Scope
