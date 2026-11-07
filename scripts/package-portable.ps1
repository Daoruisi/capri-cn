# 生成免安装 zip：dist\capri-cn-<版本>-portable.zip
#
# 用法（在仓库根目录）：
#   pwsh -NoProfile -File scripts/package-portable.ps1
#   pwsh -NoProfile -File scripts/package-portable.ps1 -Version 0.8.5.1   # 指定版本号
#   pwsh -NoProfile -File scripts/package-portable.ps1 -Rebuild           # 先重新编译再打包
#   pwsh -NoProfile -File scripts/package-portable.ps1 -SkipBuild         # 要求产物已存在
#
# zip 内容：Capri.exe、README-免安装.txt、LICENSE.md、THIRD-PARTY-NOTICES.md
# （后两个仓库根目录有才带）。dist\ 在 .gitignore 里，zip 不入库。

[CmdletBinding()]
param(
    [string]$Version,
    [switch]$Rebuild,
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$buildScript = Join-Path $root 'ci\build-windows.ps1'
$outDir = Join-Path $root 'out\windows'
$exe = Join-Path $outDir 'Capri.exe'
$distDir = Join-Path $root 'dist'
$template = Join-Path $PSScriptRoot 'README-免安装.txt'

if (-not (Test-Path -LiteralPath $buildScript)) { throw "找不到编译脚本：$buildScript" }
if (-not (Test-Path -LiteralPath $template)) { throw "找不到免安装说明模板：$template" }

# 1) out\windows\Capri.exe 不存在就先编译（.NET Framework 4.8 自带 csc.exe，不需要 Visual Studio）。
if (-not $SkipBuild -and ($Rebuild -or -not (Test-Path -LiteralPath $exe))) {
    Write-Host "==> 没有找到 $exe，先调用 ci\build-windows.ps1 编译…"
    # 编译脚本自己会在 csc.exe 失败时抛错，这里不用再看退出码。
    & $buildScript
}
if (-not (Test-Path -LiteralPath $exe)) {
    if ($SkipBuild) { throw "找不到 $exe：去掉 -SkipBuild，让脚本先编译。" }
    throw "编译结束了，但仍然找不到 $exe"
}
if ((Get-Item -LiteralPath $exe).Length -le 0) { throw "$exe 是空文件" }

# 2) 版本号：-Version > app\AssemblyInfo.cs > exe 文件版本 > 当天日期。
if ([string]::IsNullOrWhiteSpace($Version)) {
    $versionFile = Join-Path $root 'app\AssemblyInfo.cs'
    if (Test-Path -LiteralPath $versionFile) {
        $versionText = Get-Content -LiteralPath $versionFile -Raw -Encoding UTF8
        $found = [regex]::Match($versionText, 'AssemblyVersion\("([^"]+)"\)')
        if ($found.Success) { $Version = $found.Groups[1].Value }
    }
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = (Get-Item -LiteralPath $exe).VersionInfo.FileVersion
}
if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-Date -Format 'yyyyMMdd'
    Write-Warning "从源码和 exe 都取不到版本号，改用日期：$Version"
}
$Version = $Version.Trim().TrimStart('v', 'V')
if ($Version -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]{0,63}$') { throw "版本号 '$Version' 不能用作文件名" }

$zipName = "capri-cn-$Version-portable.zip"
$zip = Join-Path $distDir $zipName

New-Item -ItemType Directory -Path $distDir -Force | Out-Null
if (Test-Path -LiteralPath $zip) {
    Write-Host "==> 删除旧的 $zipName"
    Remove-Item -LiteralPath $zip -Force
}

# 3) 在临时目录里摆好内容再整体压缩，可重复运行，zip 始终只有一份。
$staging = Join-Path $distDir ('.staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging -Force | Out-Null
try {
    Copy-Item -LiteralPath $exe -Destination (Join-Path $staging 'Capri.exe') -Force

    $readme = Get-Content -LiteralPath $template -Raw -Encoding UTF8
    $readme = $readme.Replace('{{VERSION}}', $Version).Replace('{{ZIPNAME}}', $zipName)
    $utf8Bom = New-Object System.Text.UTF8Encoding($true)
    [IO.File]::WriteAllText((Join-Path $staging 'README-免安装.txt'), $readme, $utf8Bom)

    foreach ($doc in @('LICENSE.md', 'THIRD-PARTY-NOTICES.md')) {
        $docPath = Join-Path $root $doc
        if (Test-Path -LiteralPath $docPath) {
            Copy-Item -LiteralPath $docPath -Destination (Join-Path $staging $doc) -Force
        }
        else { Write-Warning "仓库根目录没有 $doc，跳过。" }
    }

    $contents = @(Get-ChildItem -LiteralPath $staging -File | Select-Object -ExpandProperty FullName)
    if ($contents.Count -lt 2) { throw "打包内容不对：只找到 $($contents.Count) 个文件" }
    Compress-Archive -Path $contents -DestinationPath $zip -CompressionLevel Optimal
}
finally {
    Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue
}

if (-not (Test-Path -LiteralPath $zip) -or (Get-Item -LiteralPath $zip).Length -le 0) { throw "zip 没有生成：$zip" }

# 4) 自查：列一遍 zip 里的文件，确认关键文件在、名字没坏。
$names = @()
$listed = $false
try {
    if (-not ('System.IO.Compression.ZipFile' -as [type])) { Add-Type -AssemblyName System.IO.Compression.FileSystem }
    $archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
    try { $names = @($archive.Entries | ForEach-Object { $_.FullName }) } finally { $archive.Dispose() }
    $listed = $true
}
catch {
    Write-Warning "没能列 zip 内容自查（不影响结果）：$($_.Exception.Message)"
}
if ($listed) {
    foreach ($needed in @('Capri.exe', 'README-免安装.txt')) {
        if ($names -notcontains $needed) { throw "zip 里缺少 $needed" }
    }
}

$size = (Get-Item -LiteralPath $zip).Length
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash
Write-Host ''
Write-Host '==> 免安装包已生成'
Write-Host ("    路径：$zip")
Write-Host ("    大小：{0:N1} MB" -f ($size / 1MB))
Write-Host ("    SHA256：$hash")
if ($names.Count -gt 0) { Write-Host ("    内容：$($names -join '、')") }
Write-Host '    发布时把 zip 上传到 Release，并附上上面的 SHA256。'
