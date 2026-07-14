# FileTag release build: publishes self-contained binaries and produces the
# distributable zip in .\dist\
param([string]$Version = "3.0.0")

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# Old release zips in dist\ are kept — only the working folders are rebuilt.
$out = Join-Path $PSScriptRoot "dist"
$appOut = Join-Path $out "app"
foreach ($d in @($appOut, (Join-Path $out "uninstall"))) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force }
}

Write-Host "Publishing FileTag.App (self-contained, single file)..."
dotnet publish FileTag.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:Version=$Version -o $appOut
if ($LASTEXITCODE -ne 0) { throw "App publish failed" }

Write-Host "Publishing Uninstall.exe (self-contained, single file, trimmed)..."
$unOut = Join-Path $out "uninstall"
dotnet publish FileTag.Uninstaller -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial `
    -p:Version=$Version -o $unOut
if ($LASTEXITCODE -ne 0) { throw "Uninstaller publish failed" }

Copy-Item (Join-Path $unOut "Uninstall.exe") $appOut
Copy-Item (Join-Path $PSScriptRoot "README.md") $appOut
Remove-Item $unOut -Recurse -Force
Get-ChildItem $appOut -Filter *.pdb | Remove-Item

$zip = Join-Path $out "FileTag-v$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $appOut "*") -DestinationPath $zip -CompressionLevel Optimal

Write-Host ""
Write-Host "Done: $zip"
Get-ChildItem $appOut | Select-Object Name, @{n="MB";e={[math]::Round($_.Length/1MB,1)}}
Get-Item $zip | Select-Object Name, @{n="MB";e={[math]::Round($_.Length/1MB,1)}}
