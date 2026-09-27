[CmdletBinding()]
param(
    [string]$InstallDirectory = (Join-Path $env:LOCALAPPDATA 'MatrixIdle')
)

$ErrorActionPreference = 'Stop'
$taskName = 'MatrixIdleWatchdog'
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$installDirectory = [System.IO.Path]::GetFullPath($InstallDirectory)
$exe = Join-Path $installDirectory 'MatrixIdle.exe'
$quotedExe = '"' + $exe + '"'

$task = Get-ScheduledTask -TaskName $taskName -ErrorAction SilentlyContinue
if ($task -and $task.Actions.Execute -eq $exe) {
    Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
}

$runValue = (Get-ItemProperty -Path $runKey -Name MatrixIdle -ErrorAction SilentlyContinue).MatrixIdle
if ($runValue -and $runValue.StartsWith($quotedExe, [StringComparison]::OrdinalIgnoreCase)) {
    Remove-ItemProperty -Path $runKey -Name MatrixIdle
}

Get-CimInstance Win32_Process -Filter "Name='MatrixIdle.exe'" |
    Where-Object { $_.ExecutablePath -eq $exe } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }

foreach ($file in @($exe, (Join-Path $installDirectory 'MatrixIdle.log'))) {
    if (Test-Path -LiteralPath $file) { Remove-Item -LiteralPath $file }
}
if (Test-Path -LiteralPath $installDirectory) {
    Remove-Item -LiteralPath $installDirectory -ErrorAction SilentlyContinue
}

Write-Output "Removed Matrix Idle from $installDirectory."
