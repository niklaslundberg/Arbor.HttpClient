<#
.SYNOPSIS
    Probes the runner environment for virtualisation capabilities and writes
    outputs to GITHUB_OUTPUT and probe-env.txt for the experimental VM probe job.
#>
[CmdletBinding()]
param()

Set-StrictMode -Version Latest

$report = [System.Collections.Generic.List[string]]::new()
$report.Add("## Environment Probe")
$report.Add("")

# OS information
$os = Get-WmiObject Win32_OperatingSystem
$report.Add("**OS:** $($os.Caption) build $($os.BuildNumber)")

# CPU virtualisation flags
$cpu = Get-WmiObject Win32_Processor | Select-Object -First 1
$vmxFlag = ($cpu.VirtualizationFirmwareEnabled -eq $true)
$report.Add("**CPU:** $($cpu.Name)")
$report.Add("**VirtualizationFirmwareEnabled:** $vmxFlag")

# Check if we are ourselves running inside a VM
$model = (Get-WmiObject Win32_ComputerSystem).Model
$report.Add("**ComputerSystem.Model:** $model")
$isVm = ($model -match 'Virtual|HyperV|VMware|Xen|KVM')
$report.Add("**Running inside a VM:** $isVm")

# Hyper-V Windows feature state
$hyperVFeature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue
$hvState = if ($hyperVFeature) { $hyperVFeature.State } else { "NotAvailable" }
$report.Add("**Hyper-V feature state:** $hvState")

# Hyper-V PowerShell module availability
$hvModule = Get-Module -ListAvailable -Name Hyper-V -ErrorAction SilentlyContinue
$report.Add("**Hyper-V PS module available:** $($null -ne $hvModule)")

# SLAT / Second Level Address Translation (required for Hyper-V) — reported via systeminfo
$sysinfo = systeminfo 2>&1
$slatLine = $sysinfo | Where-Object { $_ -match 'SLAT|Hyper-V' }
$report.Add("")
$report.Add("**systeminfo Hyper-V / SLAT lines:**")
$slatLine | ForEach-Object { $report.Add("``$_``") }

$report | Out-File -FilePath probe-env.txt -Encoding UTF8
Write-Host ($report -join "`n")

# Expose for later steps via GITHUB_OUTPUT
if ($env:GITHUB_OUTPUT) {
    "HV_STATE=$hvState"                    | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "CPU_VIRT=$vmxFlag"                    | Out-File -FilePath $env:GITHUB_OUTPUT -Append
    "HV_MODULE=$($null -ne $hvModule)"     | Out-File -FilePath $env:GITHUB_OUTPUT -Append
}
