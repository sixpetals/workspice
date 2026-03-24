param(
    [string]$DistroName
)

$ErrorActionPreference = "Continue"
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[Console]::OutputEncoding = $utf8NoBom
$OutputEncoding = $utf8NoBom

$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$logPath = Join-Path -Path $PSScriptRoot -ChildPath "wsl-interop-diagnostic-$timestamp.log"

function Write-Section {
    param([string]$Title)
    $line = "`n=== $Title ==="
    Write-Host $line -ForegroundColor Cyan
    Add-Content -Path $logPath -Value $line
}

function Write-Block {
    param([string]$Text)
    Write-Host $Text
    [System.IO.File]::AppendAllText($logPath, $Text + [Environment]::NewLine, $utf8NoBom)
}

function Invoke-Capture {
    param(
        [string]$Title,
        [scriptblock]$Script
    )

    Write-Section $Title
    try {
        $output = & $Script 2>&1 | Out-String
        if ([string]::IsNullOrWhiteSpace($output)) {
            $output = "<no output>"
        }
        Write-Block $output.TrimEnd()
    }
    catch {
        Write-Block ("ERROR: " + $_.Exception.Message)
    }
}

function Invoke-ExternalCapture {
    param(
        [string]$Title,
        [string]$FilePath,
        [string[]]$Arguments
    )

    Write-Section $Title
    Write-Block ("Command: " + $FilePath + " " + ($Arguments -join " "))

    try {
        $output = & $FilePath @Arguments 2>&1 | Out-String
        $exitCode = $LASTEXITCODE
        Write-Block ("ExitCode: " + $exitCode)
        if (-not [string]::IsNullOrWhiteSpace($output)) {
            Write-Block "[combined output]"
            Write-Block $output.TrimEnd()
        }
    }
    catch {
        Write-Block ("ERROR: " + $_.Exception.Message)
    }
}

[System.IO.File]::WriteAllText($logPath, "", $utf8NoBom)

Write-Block ("Log: $logPath")
Write-Block ("Started: " + (Get-Date).ToString("o"))

Invoke-Capture -Title "Host Info" -Script {
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).
        IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)

    [pscustomobject]@{
        UserName = [Environment]::UserName
        MachineName = [Environment]::MachineName
        IsAdministrator = $isAdmin
        PowerShellVersion = $PSVersionTable.PSVersion.ToString()
        OsVersion = [Environment]::OSVersion.VersionString
    } | Format-List
}

Invoke-Capture -Title "Windows dotnet.exe" -Script {
    Get-Command dotnet.exe -ErrorAction SilentlyContinue | Format-List Source, Version
}

Invoke-Capture -Title "WSL Related Services" -Script {
    Get-Service -Name wslservice, LxssManager, vmcompute, hns -ErrorAction SilentlyContinue |
        Select-Object Name, Status, StartType |
        Format-Table -AutoSize
}

Invoke-ExternalCapture -Title "wsl --version" -FilePath "wsl.exe" -Arguments @("--version")
Invoke-ExternalCapture -Title "wsl --status" -FilePath "wsl.exe" -Arguments @("--status")
Invoke-ExternalCapture -Title "wsl -l -v" -FilePath "wsl.exe" -Arguments @("-l", "-v")

$resolvedDistro = $DistroName
if ([string]::IsNullOrWhiteSpace($resolvedDistro)) {
    try {
        $listRaw = & wsl.exe -l -q 2>$null
        $resolvedDistro = [string](($listRaw | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -First 1))
        $resolvedDistro = $resolvedDistro.Trim()
        $resolvedDistro = ($resolvedDistro -replace '\s+', '')
    }
    catch {
        $resolvedDistro = $null
    }
}

Write-Section "Selected Distro"
if ([string]::IsNullOrWhiteSpace($resolvedDistro)) {
    Write-Block "No distro resolved."
}
else {
    Write-Block $resolvedDistro
}

if (-not [string]::IsNullOrWhiteSpace($resolvedDistro)) {
    $linuxInteropScript = 'printf ''PATH=%s\n'' "$PATH"; printf ''\n[wsl.conf]\n''; cat /etc/wsl.conf 2>/dev/null || true; printf ''\n[WSLInterop]\n''; cat /proc/sys/fs/binfmt_misc/WSLInterop 2>/dev/null || true'
    $powerShellInteropScript = 'powershell.exe -NoProfile -Command ''$PSVersionTable.PSVersion.ToString()'''

    Invoke-ExternalCapture -Title "WSL PATH and interop flags" -FilePath "wsl.exe" -Arguments @(
        "-d", $resolvedDistro, "--", "sh", "-lc",
        $linuxInteropScript
    )

    Invoke-ExternalCapture -Title "WSL -> cmd.exe test" -FilePath "wsl.exe" -Arguments @(
        "-d", $resolvedDistro, "--", "sh", "-lc",
        "cmd.exe /c ver"
    )

    Invoke-ExternalCapture -Title "WSL -> powershell.exe test" -FilePath "wsl.exe" -Arguments @(
        "-d", $resolvedDistro, "--", "sh", "-lc",
        $powerShellInteropScript
    )

    Invoke-ExternalCapture -Title "WSL -> dotnet.exe test" -FilePath "wsl.exe" -Arguments @(
        "-d", $resolvedDistro, "--", "sh", "-lc",
        "'/mnt/c/Program Files/dotnet/dotnet.exe' --info"
    )
}

Invoke-Capture -Title "Legacy Console Registry Hint" -Script {
    Get-ItemProperty -Path "HKCU:\Console" -ErrorAction SilentlyContinue |
        Select-Object ForceV2, QuickEdit, InsertMode, VirtualTerminalLevel |
        Format-List
}

Write-Block ("Finished: " + (Get-Date).ToString("o"))
Write-Host "`nDiagnostic log written to: $logPath" -ForegroundColor Green
