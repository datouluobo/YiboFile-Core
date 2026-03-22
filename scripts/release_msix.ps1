# YiboFile Core - Build and Package MSIX
$Version = "1.0.1780.0"
$ProjectDir = "F:\Download\GitHub\YiboFile\YiboFile-Core"
$PublishDir = Join-Path $ProjectDir "Publish"
$StoreDir = Join-Path $PublishDir "Store_Core"
$PdbZip = Join-Path $PublishDir "YiboFile_Core_$Version`_PDBs.zip"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  YiboFile Core Release Build & Package" -ForegroundColor Cyan
Write-Host "  Version: $Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan

# 1. Dotnet Publish
Write-Host "Step 1: Publishing project..." -ForegroundColor Yellow
dotnet publish "$ProjectDir\YiboFile-Core.csproj" -c Release -r win-x64 --self-contained false -o "$StoreDir"

# Cleanup AppData in StoreDir to prevent it being included in MSIX (which makes it read-only)
if (Test-Path "$StoreDir\AppData") {
    Remove-Item -Path "$StoreDir\AppData" -Recurse -Force
}

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# 2. Extract and Zip PDBs
Write-Host "Step 2: Zipping PDB files..." -ForegroundColor Yellow
$pdbs = Get-ChildItem -Path "$StoreDir" -Filter "*.pdb"
if ($pdbs) {
    Compress-Archive -Path $pdbs.FullName -DestinationPath "$PdbZip" -Force
    Write-Host "PDBs preserved in: $PdbZip" -ForegroundColor Green
    # Remove PDBs from the MSIX source to reduce size
    Remove-Item $pdbs.FullName -Force
} else {
    Write-Host "No PDB files found." -ForegroundColor DarkYellow
}

# 3. Create Portable ZIP
Write-Host "Step 3: Creating Portable version..." -ForegroundColor Yellow
$PortableZip = Join-Path $PublishDir "YiboFile_Core_$Version`_Portable.zip"
Compress-Archive -Path "$StoreDir\*" -DestinationPath "$PortableZip" -Force
Write-Host "Portable version created: $PortableZip" -ForegroundColor Green

# 4. Prepare MSIX Assets (Copy from Packaging)
Write-Host "Step 4: Preparing MSIX assets..." -ForegroundColor Yellow
$PackagingDir = Join-Path $ProjectDir "Packaging"
if (Test-Path $PackagingDir) {
    Copy-Item -Path "$PackagingDir\AppxManifest.xml" -Destination "$StoreDir\" -Force
    # 确保 Resources 目录存在
    if (-not (Test-Path "$StoreDir\Resources")) { New-Item -Path "$StoreDir\Resources" -ItemType Directory -Force }
    Copy-Item -Path "$PackagingDir\Resources\*" -Destination "$StoreDir\Resources\" -Recurse -Force
} else {
    Write-Host "Warning: Packaging directory not found. MSIX might fail." -ForegroundColor DarkYellow
}

# 5. Run MSIX Packaging
Write-Host "Step 5: Creating MSIX package..." -ForegroundColor Yellow

# Find makeappx.exe
$possiblePaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.22621.0\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.0.19041.0\x64\makeappx.exe"
)

$SDKPath = $null
foreach ($path in $possiblePaths) {
    if (Test-Path $path) { $SDKPath = $path; break }
}

if (-not $SDKPath) {
    $SDKPath = Get-ChildItem -Path "C:\Program Files (x86)\Windows Kits\10\bin" -Filter "makeappx.exe" -Recurse -ErrorAction SilentlyContinue | 
               Where-Object { $_.FullName -like "*\x64\*" } | 
               Sort-Object -Property LastWriteTime -Descending | 
               Select-Object -First 1 -ExpandProperty FullName
}

if (-not $SDKPath) {
    Write-Host "Error: makeappx.exe not found." -ForegroundColor Red
    exit 1
}

$PackageName = "YiboFile_Core_$Version.msix"
$OutputFile = Join-Path $PublishDir $PackageName

$process = Start-Process -FilePath $SDKPath -ArgumentList "pack /d `"$StoreDir`" /p `"$OutputFile`" /o" -Wait -PassThru -NoNewWindow

if ($process.ExitCode -eq 0) {
    Write-Host "Success! MSIX created: $OutputFile" -ForegroundColor Green
} else {
    Write-Host "Error: MSIX packaging failed." -ForegroundColor Red
    exit 1
}
