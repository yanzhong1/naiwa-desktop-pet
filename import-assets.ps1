param([Parameter(Mandatory = $true)][string]$Source)
$ErrorActionPreference = 'Stop'
$sourceRoot = (Resolve-Path -LiteralPath $Source).Path
if (Test-Path -LiteralPath (Join-Path $sourceRoot 'assets\animations') -PathType Container) {
    $sourceRoot = Join-Path $sourceRoot 'assets'
}
$destination = Join-Path $PSScriptRoot 'assets'
if ([IO.Path]::GetFullPath($sourceRoot).TrimEnd('\') -eq [IO.Path]::GetFullPath($destination).TrimEnd('\')) {
    throw '素材已经在目标目录中，无需再次导入。'
}
$actions = @('idle', 'walk', 'laugh', 'wave', 'sleep', 'dragged', 'run', 'near')
$frames = @{}
foreach ($action in $actions) {
    $frames[$action] = @(Get-ChildItem -LiteralPath (Join-Path $sourceRoot "animations\$action") -Filter '*.png' -File -ErrorAction SilentlyContinue)
    if ($frames[$action].Count -eq 0) { throw "来源中缺少 $action 动画 PNG，尚未复制任何文件。" }
}
$count = 0
foreach ($action in $actions) {
    $target = Join-Path $destination "animations\$action"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    foreach ($frame in $frames[$action]) {
        Copy-Item -LiteralPath $frame.FullName -Destination $target -Force
        $count++
    }
}
foreach ($relative in @('animation.ini', 'icon.ico', 'sounds\laugh.wav')) {
    $path = Join-Path $sourceRoot $relative
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        $target = Join-Path $destination $relative
        New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
        Copy-Item -LiteralPath $path -Destination $target -Force
    }
}
Write-Output "已导入 $count 张动画帧及可用的配置、音效和图标。媒体文件默认不会被 Git 提交。"
