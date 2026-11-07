# 部署新构建到 %LOCALAPPDATA%\Capri：
# 1) 干净地请她退出（先发 WM_CLOSE，等不到再强杀）
# 2) 清掉 running.on（强杀会留下这个标记，下次启动就会弹「Capri closed unexpectedly」）
# 3) 换 exe、重启
param(
    [string]$Build = 'D:\software\git_dow\capri-desktop\source\out\windows\Capri.exe'
)
$ErrorActionPreference = 'Stop'
$data = Join-Path $env:LOCALAPPDATA 'Capri'
$mark = Join-Path $data 'running.on'

if (-not (Test-Path $Build)) { throw "找不到构建产物：$Build" }

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class CapriDeploy {
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr w, IntPtr l);
}
'@

$proc = Get-Process Capri -ErrorAction SilentlyContinue
if ($proc) {
    $handles = $proc | ForEach-Object { $_.MainWindowHandle } | Where-Object { $_ -ne [IntPtr]::Zero }
    foreach ($h in $handles) { [CapriDeploy]::PostMessage($h, 0x0010, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null }
    for ($i = 0; $i -lt 10; $i++) {
        Start-Sleep -Milliseconds 400
        if (-not (Get-Process Capri -ErrorAction SilentlyContinue)) { break }
    }
    Get-Process Capri -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

if (Test-Path $mark) { Remove-Item $mark -Force }

Copy-Item $Build (Join-Path $data 'Capri.exe') -Force
Start-Process (Join-Path $data 'Capri.exe') | Out-Null
Start-Sleep -Seconds 3
$now = Get-Process Capri -ErrorAction SilentlyContinue
if ($now) { "已部署并启动，PID $($now.Id)" } else { '已部署，但她没起来——看 errors.txt' }
