<#
.SYNOPSIS
    Builds an (unsigned) MSIX package for Microsoft Store submission into .\artifacts.
.DESCRIPTION
    The Store signs the package itself, so no certificate is needed for submission.
    Values come from Partner Center → your app → Product identity. See docs/STORE.md.
    Requires the .NET 10 SDK and the Windows 10/11 SDK (makeappx.exe) — both present on GitHub's windows runners.
.EXAMPLE
    .\packaging\msix\build-msix.ps1 -Version 1.0.0 -IdentityName "12345GusGk.Pitanga" -Publisher "CN=ABCD-1234" -PublisherDisplayName "GusGk"
#>
param(
    [Parameter(Mandatory)] [string]$IdentityName,
    [Parameter(Mandatory)] [string]$Publisher,
    [Parameter(Mandatory)] [string]$PublisherDisplayName,
    [string]$Version = "1.0.0"
)
$ErrorActionPreference = "Stop"
$root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$artifacts = Join-Path $root "artifacts"
$layout = Join-Path $artifacts "msix-layout"
if (Test-Path $layout) { Remove-Item $layout -Recurse -Force }
New-Item -ItemType Directory -Force $layout | Out-Null

# A Store exige versão a.b.c.0
$parts = ($Version -split '[-+]')[0].Split('.')
while ($parts.Count -lt 3) { $parts += "0" }
$msixVersion = "{0}.{1}.{2}.0" -f $parts[0], $parts[1], $parts[2]

Write-Host "==> dotnet publish (folder layout for MSIX)" -ForegroundColor Cyan
dotnet publish (Join-Path $root "src\Pitanga\Pitanga.csproj") -c Release -r win-x64 --self-contained true `
    -p:DebugType=none -p:Version=$Version -o $layout -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Copy-Item (Join-Path $PSScriptRoot "Assets") (Join-Path $layout "Assets") -Recurse -Force
Copy-Item (Join-Path $root "licenses") (Join-Path $layout "licenses") -Recurse -Force
Copy-Item (Join-Path $root "LICENSE"), (Join-Path $root "THIRD-PARTY-NOTICES.md"), (Join-Path $root "PRIVACY.md") $layout

$manifest = Get-Content (Join-Path $PSScriptRoot "AppxManifest.xml") -Raw
$manifest = $manifest.Replace('$IDENTITY_NAME$', $IdentityName).Replace('$PUBLISHER$', $Publisher).
    Replace('$PUBLISHER_DISPLAY_NAME$', $PublisherDisplayName).Replace('$VERSION$', $msixVersion)
Set-Content (Join-Path $layout "AppxManifest.xml") $manifest -Encoding utf8

$makeappx = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\makeappx.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending | Select-Object -First 1
if (-not $makeappx) { throw "makeappx.exe not found. Install the Windows SDK: https://developer.microsoft.com/windows/downloads/windows-sdk/" }

$output = Join-Path $artifacts "Pitanga-$Version-x64.msix"
Write-Host "==> makeappx pack" -ForegroundColor Cyan
& $makeappx.FullName pack /d $layout /p $output /o /h SHA256 | Out-Host
if ($LASTEXITCODE -ne 0) { throw "makeappx failed" }
Write-Host "MSIX: $output" -ForegroundColor Green
