[CmdletBinding()]
param(
    [ValidateRange(5, 3600)]
    [int]$IdleSeconds = 60,
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'MatrixIdle'),
    [switch]$ReplaceExisting
)

$ErrorActionPreference = 'Stop'
$taskName = 'MatrixIdleWatchdog'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$installDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$exe = Join-Path $installDirectory 'MatrixIdle.exe'
$quotedExe = '"' + $exe + '"'

$existingRun = (Get-ItemProperty -Path $runKey -Name MatrixIdle -ErrorAction SilentlyContinue).MatrixIdle
if ($existingRun -and -not $existingRun.StartsWith($quotedExe, [StringComparison]::OrdinalIgnoreCase) -and -not $ReplaceExisting) {
    throw 'A different MatrixIdle startup entry exists. Review it and rerun with -ReplaceExisting to replace it.'
}

$existingTask = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($existingTask -and $existingTask.Actions.Execute -ne $exe -and -not $ReplaceExisting) {
    throw 'A different MatrixIdleWatchdog task exists. Review it and rerun with -ReplaceExisting to replace it.'
}

$buildExe = & (Join-Path $PSScriptRoot 'build.ps1') | Select-Object -Last 1
New-Item -ItemType Directory -Path $installDirectory -Force | Out-Null

$oldPaths = @()
if ($existingTask) { $oldPaths += $existingTask.Actions.Execute }
if ($existingRun -match '^\s*"([^"]+\.exe)"') { $oldPaths += $Matches[1] }
if ($ReplaceExisting -and $existingTask) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}
if ($ReplaceExisting) {
    Get-CimInstance Win32_Process -Filter "Name='MatrixIdle.exe'" |
        Where-Object { $oldPaths -contains $_.ExecutablePath } |
        ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
}
Get-CimInstance Win32_Process -Filter "Name='MatrixIdle.exe'" |
    Where-Object { $_.ExecutablePath -eq $exe } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
Copy-Item -LiteralPath $buildExe -Destination $exe -Force

$arguments = "--idle-seconds $IdleSeconds"
$runValue = $quotedExe + ' ' + $arguments
Set-ItemProperty -Path $runKey -Name MatrixIdle -Value $runValue

$action = New-ScheduledTaskAction -Execute $exe -Argument $arguments
$trigger = New-ScheduledTaskTrigger -Once -At (Get-Date).AddMinutes(1) -RepetitionInterval (New-TimeSpan -Minutes 1)
$currentUser = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$logonTrigger = New-ScheduledTaskTrigger -AtLogOn -User $currentUser
$principal = New-ScheduledTaskPrincipal -UserId $currentUser -LogonType Interactive -RunLevel Limited
$settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -ExecutionTimeLimit (New-TimeSpan -Seconds 0) -StartWhenAvailable -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger @($trigger, $logonTrigger) -Principal $principal -Settings $settings -Description 'Restarts Matrix Idle if it stops while this user is signed in.' -Force | Out-Null

Start-Process -FilePath $exe -ArgumentList @('--idle-seconds', $IdleSeconds)
Write-Output "Installed $exe (idle: $IdleSeconds seconds)."
