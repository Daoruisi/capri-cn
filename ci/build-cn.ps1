param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$wpf = Join-Path $framework 'WPF'

# WinRT metadata: Windows Kits first, else contract winmds from the Microsoft.Windows.SDK.NET.Ref NuGet package
$winmds = @()
$kits = Get-ChildItem -LiteralPath (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\UnionMetadata') -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^\d+(\.\d+)+$' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'Windows.winmd' } |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($kits) { $winmds = @($kits) }
elseif (Test-Path (Join-Path $env:WINDIR 'System32\WinMetadata\Windows.Foundation.winmd')) {
    # The OS ships the same WinRT metadata the SDK installs (Windows 10 1809 and later).
    $winmds = Get-ChildItem -LiteralPath (Join-Path $env:WINDIR 'System32\WinMetadata') -Filter *.winmd | Where-Object { $_.Name -ne 'Windows.Foundation.winmd' } | ForEach-Object { $_.FullName }
}
else {
    $contracts = Join-Path $env:USERPROFILE '.nuget\packages\microsoft.windows.sdk.net.ref\10.0.26100.87\winmd'
    if (-not (Test-Path $contracts)) { throw 'Windows.winmd and the contract winmds are both missing.' }
    $winmds = @(
        (Join-Path $contracts 'Windows.Foundation.FoundationContract.winmd'),
        (Join-Path $contracts 'Windows.Foundation.UniversalApiContract.winmd')
    )
}
foreach ($w in $winmds) { Write-Output "winmd: $w" }

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $root 'out\windows' }
$out = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $out -Force | Out-Null
$app = Join-Path $out 'Capri.exe'
$icon = Join-Path $root 'assets\chip.ico'
$shared = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/warn:4', ('/win32manifest:' + (Join-Path $root 'app\app.manifest')), ('/win32icon:' + $icon), '/reference:System.dll', '/reference:System.Core.dll', '/reference:System.Drawing.dll', '/reference:System.Windows.Forms.dll')
$references = @('/reference:System.Runtime.Serialization.dll', '/reference:System.IO.Compression.dll', '/reference:System.IO.Compression.FileSystem.dll', '/reference:System.Security.dll', ('/reference:' + (Join-Path $wpf 'UIAutomationClient.dll')), ('/reference:' + (Join-Path $wpf 'UIAutomationTypes.dll')), ('/reference:' + (Join-Path $wpf 'WindowsBase.dll')), ('/reference:' + (Join-Path $wpf 'System.Speech.dll')), ('/reference:' + (Join-Path $framework 'System.Runtime.dll')), ('/reference:' + (Join-Path $framework 'System.Runtime.WindowsRuntime.dll')), ('/reference:' + (Join-Path $framework 'System.Runtime.InteropServices.WindowsRuntime.dll'))) + ($winmds | ForEach-Object { '/reference:' + $_ })
$resources = @(('/resource:' + $icon + ',Capri.Chip.ico'), ('/resource:' + (Join-Path $root 'assets\fonts\ShareTechMono-Regular.ttf') + ',Capri.Font.Mono.ttf'))
$resources += Get-ChildItem -LiteralPath (Join-Path $root 'assets\sprites') -Filter *.png | ForEach-Object { '/resource:' + $_.FullName + ',Capri.Sprite.' + $_.Name }
foreach ($pack in 'capri-clear','capri-pixel','capri-anime') {
    $resources += '/resource:' + (Join-Path $root ("pets\packs\" + $pack + "\spritesheet.png")) + ',Capri.Pack.' + $pack + '.png'
}
$resources += Get-ChildItem -LiteralPath (Join-Path $root 'assets\voice') -Filter *.wav | ForEach-Object { '/resource:' + $_.FullName + ',Capri.Voice.' + $_.Name }
$sources = @('Capri', 'Brain', 'Talk', 'Mainframe', 'Neon', 'Notice', 'Engine', 'Ledges', 'Voice', 'Ears', 'Dictation', 'Agent', 'Hands', 'Computer', 'VoicePack', 'Startup', 'Presence', 'Player', 'Splash', 'AssemblyInfo') | ForEach-Object { Join-Path $root ('app\' + $_ + '.cs') }
& $compiler ($shared + $references + $resources + @('/out:' + $app) + $sources)
if ($LASTEXITCODE -ne 0) { throw 'Capri compilation failed.' }
Write-Output "Built $app"
Get-Item $app | Select-Object FullName, Length, LastWriteTime | Format-List



