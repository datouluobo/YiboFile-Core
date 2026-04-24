# YiboFile Core - MSIX Packaging Script
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptDir

$Version = "1.0.1930.0"
$SourceDir = (Resolve-Path "..\Publish\Store_Core").Path
$OutputDir = (Resolve-Path "..\Publish").Path
$PackageName = "YiboFile_Core_$Version.msix"
$OutputFile = Join-Path $OutputDir $PackageName

Write-Host "========================================"
Write-Host "  YiboFile Core - MSIX Packaging"
Write-Host "========================================"

# 1. Find makeappx.exe
Write-Host "Searching for makeappx.exe..."
$possiblePaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
)

$SDKPath = $null
foreach ($path in $possiblePaths) {
    if (Test-Path $path) {
        $SDKPath = $path
        break
    }
}

if (-not $SDKPath) {
    Write-Host "Searching in SDK folders..."
    $SDKPath = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | 
               Where-Object { $_.FullName -like "*\x64\*" } | 
               Sort-Object -Property LastWriteTime -Descending | 
               Select-Object -First 1 -ExpandProperty FullName
}

if (-not $SDKPath) {
    Write-Host "Error: makeappx.exe not found."
    exit 1
}

Write-Host "Tool found: $SDKPath"

# 2. Package
Write-Host "Packaging to: $OutputFile ..."

$process = Start-Process -FilePath $SDKPath -ArgumentList "pack /d `"$SourceDir`" /p `"$OutputFile`" /o" -Wait -PassThru -NoNewWindow

if ($process.ExitCode -eq 0) {
    Write-Host "Success: MSIX package created."
    Write-Host "Location: $OutputFile"
} else {
    Write-Host "Error: Packaging failed with exit code $($process.ExitCode)"
    exit 1
}

