param(
    [switch]$CompileOnly,
    [string]$OutputDirectory = (Join-Path $PSScriptRoot 'dist\奶龙桌宠')
)
$ErrorActionPreference = 'Stop'
if ($env:OS -ne 'Windows_NT') { throw '请在 Windows 上构建。' }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (!(Test-Path -LiteralPath $compiler)) { throw '未找到 .NET Framework 4.x C# 编译器。' }
$assetRoot = Join-Path $PSScriptRoot 'assets'
$actions = @('idle', 'walk', 'laugh', 'wave', 'sleep', 'dragged', 'run', 'near')
if (!$CompileOnly) {
    foreach ($action in $actions) {
        $frames = @(Get-ChildItem -LiteralPath (Join-Path $assetRoot "animations\$action") -Filter '*.png' -File -ErrorAction SilentlyContinue)
        if ($frames.Count -eq 0) { throw "缺少 $action 动画。请先运行 import-assets.ps1 导入本地素材，或使用 -CompileOnly 仅编译。" }
    }
}
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$executable = Join-Path $OutputDirectory '奶龙桌宠.exe'
$arguments = @('/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/utf8output', '/codepage:65001', '/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', "/out:$executable", "/win32manifest:$(Join-Path $PSScriptRoot 'src\app.manifest')")
$icon = Join-Path $assetRoot 'icon.ico'
if (Test-Path -LiteralPath $icon) { $arguments += "/win32icon:$icon" }
$arguments += Join-Path $PSScriptRoot 'src\PetApp.cs'
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw "编译失败：$LASTEXITCODE" }
if (!$CompileOnly) {
    $destination = Join-Path $OutputDirectory 'assets'
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    foreach ($action in $actions) {
        $frameDestination = Join-Path $destination "animations\$action"
        New-Item -ItemType Directory -Path $frameDestination -Force | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $assetRoot "animations\$action") -Filter '*.png' -File | ForEach-Object {
            Copy-Item -LiteralPath $_.FullName -Destination $frameDestination -Force
        }
    }
    Copy-Item -LiteralPath (Join-Path $assetRoot 'animation.ini') -Destination $destination -Force
    if (Test-Path -LiteralPath $icon) { Copy-Item -LiteralPath $icon -Destination $destination -Force }
    $sound = Join-Path $assetRoot 'sounds\laugh.wav'
    if (Test-Path -LiteralPath $sound) {
        $soundDestination = Join-Path $destination 'sounds'
        New-Item -ItemType Directory -Path $soundDestination -Force | Out-Null
        Copy-Item -LiteralPath $sound -Destination $soundDestination -Force
    }
}
Write-Output "已生成：$executable"
