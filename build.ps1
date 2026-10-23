<#
.SYNOPSIS
    Builds Pitanga.exe, the installer, the portable zip and SHA256SUMS into .\artifacts.
.EXAMPLE
    .\build.ps1 -Version 1.0.0
.NOTES
    Requires the .NET 10 SDK. The installer step also needs Inno Setup 6 (ISCC.exe); it is skipped if missing.
    Clone/build from a short path (e.g. C:\dev\Pitanga): long paths break the XAML compiler.
#>
param(
    [string]$Version = "1.0.0",
    [switch]$SkipInstaller
)
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish"

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Force $publish | Out-Null

Write-Host "==> dotnet publish ($Version)" -ForegroundColor Cyan
dotnet publish (Join-Path $root "src\Pitanga\Pitanga.csproj") `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none -p:Version=$Version `
    -o $publish -nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "==> portable zip" -ForegroundColor Cyan
$zip = Join-Path $artifacts "Pitanga-$Version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $publish "Pitanga.exe"), (Join-Path $root "LICENSE"), (Join-Path $root "THIRD-PARTY-NOTICES.md"), (Join-Path $root "PRIVACY.md"), (Join-Path $root "licenses") -DestinationPath $zip

if (-not $SkipInstaller) {
    $iscc = @(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source,
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if ($iscc) {
        Write-Host "==> installer" -ForegroundColor Cyan
        $numeric = ($Version -split '[-+]')[0]
        & $iscc "/DAppVersion=$Version" "/DNumericVersion=$numeric" "/DPublishDir=$publish" (Join-Path $root "installer\Pitanga.iss") | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed" }
    }
    else {
        Write-Warning "Inno Setup (ISCC.exe) not found - skipping installer."
    }
}

Write-Host "==> checksums" -ForegroundColor Cyan
Get-ChildItem $artifacts -File | Where-Object Name -ne "SHA256SUMS.txt" | ForEach-Object {
    "{0}  {1}" -f (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name
} | Set-Content (Join-Path $artifacts "SHA256SUMS.txt") -Encoding ascii

Get-ChildItem $artifacts -File | Select-Object Name, @{ n = "MB"; e = { [math]::Round($_.Length / 1MB, 1) } } | Format-Table -AutoSize
