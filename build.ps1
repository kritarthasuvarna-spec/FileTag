# FileTag release build: publishes self-contained binaries, produces the
# distributable zip AND the Setup wizard exe in .\dist\
param([string]$Version = "5.4.0")

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

# Old release zips/exes in dist\ are kept — only the working folders are rebuilt.
$out = Join-Path $PSScriptRoot "dist"
$appOut = Join-Path $out "app"
foreach ($d in @($appOut, (Join-Path $out "uninstall"), (Join-Path $out "setup"))) {
    if (Test-Path $d) { Remove-Item $d -Recurse -Force }
}

Write-Host "Publishing FileTag.App (self-contained, single file, compressed)..."
dotnet publish FileTag.App -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:Version=$Version -o $appOut
if ($LASTEXITCODE -ne 0) { throw "App publish failed" }

Write-Host "Publishing Uninstall.exe (stub, self-contained, trimmed)..."
$unOut = Join-Path $out "uninstall"
dotnet publish FileTag.Uninstaller -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:PublishTrimmed=true -p:TrimMode=partial `
    -p:Version=$Version -o $unOut
if ($LASTEXITCODE -ne 0) { throw "Uninstaller publish failed" }

Copy-Item (Join-Path $unOut "Uninstall.exe") $appOut
# clean 4-file layout: app, uninstaller, on-disk reference card, license
Copy-Item (Join-Path $PSScriptRoot "install-assets\README.txt") $appOut
Copy-Item (Join-Path $PSScriptRoot "install-assets\LICENSE.txt") $appOut
Remove-Item $unOut -Recurse -Force
Get-ChildItem $appOut -Filter *.pdb | Remove-Item

$zip = Join-Path $out "FileTag-v$Version-win-x64.zip"
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $appOut "*") -DestinationPath $zip -CompressionLevel Optimal

Write-Host "Publishing FileTag.Setup (wizard with embedded payload)..."
$payload = Join-Path $PSScriptRoot "FileTag.Setup\payload.zip"
Copy-Item $zip $payload -Force
try {
    $setupOut = Join-Path $out "setup"
    dotnet publish FileTag.Setup -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:Version=$Version -o $setupOut
    if ($LASTEXITCODE -ne 0) { throw "Setup publish failed" }
    Copy-Item (Join-Path $setupOut "FileTag.Setup.exe") (Join-Path $out "FileTag-Setup-v$Version.exe") -Force
    Remove-Item $setupOut -Recurse -Force
}
finally {
    Remove-Item $payload -Force -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Done."
Get-ChildItem $out -File | Select-Object Name, @{n="MB";e={[math]::Round($_.Length/1MB,1)}}
