param()
$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
$product = Join-Path $PSScriptRoot 'dist\奶龙桌宠'
$archive = Join-Path $PSScriptRoot 'dist\奶龙桌宠-便携版.zip'
$executable = Join-Path $product '奶龙桌宠.exe'
$assets = Join-Path $product 'assets'
if (!(Test-Path -LiteralPath $executable) -or !(Test-Path -LiteralPath $assets)) {
    throw '缺少程序或素材，未生成压缩包。'
}
$extra = @(Get-ChildItem -LiteralPath $product -Force | Where-Object { $_.Name -ne '奶龙桌宠.exe' -and $_.Name -ne 'assets' })
if ($extra.Count -gt 0) { throw '输出目录存在额外文件。请先移走这些文件，再重新打包。' }
Compress-Archive -LiteralPath $product -DestinationPath $archive -Force
Write-Output "已打包：$archive"
