<#
.SYNOPSIS
    Creates a minimal (no-OS) Hyper-V VM to prove the Hyper-V API is available
    on the runner, then tears it down. Reports VM_RESULT and VM_START to GITHUB_OUTPUT.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest

$vmName  = "Arbor-CI-Probe"
$vhdPath = Join-Path $env:TEMP "arbor-ci-probe.vhdx"

try {
    Import-Module Hyper-V -ErrorAction Stop
    Write-Host "Hyper-V module imported OK"
}
catch {
    Write-Host "Cannot import Hyper-V module: $_"
    if ($env:GITHUB_OUTPUT) {
        "VM_RESULT=HyperV-module-unavailable" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
    exit 0
}

# Remove any leftover from a previous run
Remove-VM   -Name $vmName -Force -ErrorAction SilentlyContinue
Remove-Item $vhdPath      -Force -ErrorAction SilentlyContinue

# Create a tiny (1 GB) blank VHDX — no OS installed, just tests the storage API
try {
    New-VHD -Path $vhdPath -SizeBytes 1GB -Dynamic | Out-Null
    Write-Host "New-VHD succeeded: $vhdPath"
}
catch {
    Write-Host "New-VHD failed: $_"
    if ($env:GITHUB_OUTPUT) {
        "VM_RESULT=New-VHD-failed: $_" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
    exit 0
}

# Create a Generation-2 VM (no switch — isolated, no network needed for probe)
try {
    $vm = New-VM -Name $vmName -Generation 2 -MemoryStartupBytes 512MB `
                 -VHDPath $vhdPath -ErrorAction Stop
    Write-Host "New-VM succeeded: $($vm.Name)  State=$($vm.State)"
    if ($env:GITHUB_OUTPUT) {
        "VM_RESULT=VM-created-OK" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}
catch {
    Write-Host "New-VM failed: $_"
    if ($env:GITHUB_OUTPUT) {
        "VM_RESULT=New-VM-failed: $_" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
    exit 0
}

# Try starting it (expected to fail — no bootable OS — but proves scheduling works)
try {
    Start-VM -Name $vmName -ErrorAction Stop
    Start-Sleep -Seconds 3
    $state = (Get-VM -Name $vmName).State
    Write-Host "Start-VM succeeded, state: $state"
    if ($env:GITHUB_OUTPUT) {
        "VM_START=started-state-$state" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}
catch {
    Write-Host "Start-VM failed (expected without an OS): $_"
    if ($env:GITHUB_OUTPUT) {
        "VM_START=failed-expected: $_" | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    }
}
finally {
    Stop-VM    -Name $vmName -TurnOff -Force -ErrorAction SilentlyContinue
    Remove-VM  -Name $vmName -Force         -ErrorAction SilentlyContinue
    Remove-Item $vhdPath -Force             -ErrorAction SilentlyContinue
    Write-Host "Cleanup complete."
}
