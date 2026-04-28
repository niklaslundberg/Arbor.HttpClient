<#
.SYNOPSIS
    Fast, non-destructive probe to check whether Hyper-V is available and usable
    on the current machine. Does NOT create any VMs or VHDs.

.DESCRIPTION
    Checks the following conditions in order:
      1. The current OS is Windows.
      2. The Hyper-V PowerShell module is available and importable.
      3. The current session has elevated (Administrator) rights.
      4. The Hyper-V Virtual Machine Management Service (VMMS) is running and
         able to enumerate VMs.

    Exit codes:
      0 — Hyper-V is available. Use scripts/Start-UIAutomation.ps1 for
          full VM-based system tests.
      1 — Hyper-V is not available (see console output for reason).

    When running inside GitHub Actions (GITHUB_OUTPUT is set), also writes
    HYPERV_AVAILABLE=True/False to $env:GITHUB_OUTPUT so downstream steps
    can gate on the output.

.EXAMPLE
    # Simple availability check:
    .\scripts\Test-HyperVAvailability.ps1
    if ($LASTEXITCODE -eq 0) { Write-Host "Hyper-V OK — run system tests" }

.EXAMPLE
    # In a pre-commit hook:
    if (& scripts\Test-HyperVAvailability.ps1; $LASTEXITCODE -eq 0) {
        Write-Host "Running VM-based system tests..."
        .\scripts\Start-UIAutomation.ps1 -BaseVhdx $env:ARBOR_BASE_VHDX
    }
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest

function Write-Outcome {
    param([bool]$Available, [string]$Reason)
    $value = if ($Available) { 'True' } else { 'False' }
    Write-Host "Hyper-V available: $value  ($Reason)"
    if ($env:GITHUB_OUTPUT) {
        "HYPERV_AVAILABLE=$value" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}

# 1. Must be Windows
$onWindows = ($IsWindows -or ($env:OS -match 'Windows_NT'))
if (-not $onWindows) {
    Write-Outcome -Available $false -Reason 'not running on Windows'
    exit 1
}

# 2. Hyper-V PowerShell module must be available
$hvModule = Get-Module -ListAvailable -Name Hyper-V -ErrorAction SilentlyContinue
if (-not $hvModule) {
    Write-Outcome -Available $false -Reason 'Hyper-V PowerShell module not installed'
    exit 1
}

try {
    Import-Module Hyper-V -ErrorAction Stop
} catch {
    Write-Outcome -Available $false -Reason "cannot import Hyper-V module: $_"
    exit 1
}

# 3. Elevated (Administrator) rights are required to manage VMs
$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
$isAdmin = $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Outcome -Available $false -Reason 'not running as Administrator (elevation required for VM management)'
    exit 1
}

# 4. VMMS service must be running — quick VM enumeration as a smoke test
try {
    $null = Get-VM -ErrorAction Stop
} catch {
    Write-Outcome -Available $false -Reason "VMMS service unavailable: $_"
    exit 1
}

Write-Outcome -Available $true -Reason 'all checks passed'
Write-Host 'Use scripts/Start-UIAutomation.ps1 to run full VM-based system tests.'
exit 0
