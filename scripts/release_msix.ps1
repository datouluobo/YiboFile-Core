# YiboFile Core - Build and Package MSIX
$Version = "1.0.1930.0"
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
dotnet publish "$ProjectDir\YiboFile-Core.csproj" -c Release -r win-x64 --self-contained true -p:PublishReadyToRun=true -p:PublishTrimmed=false -o "$StoreDir"

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

# 2. Cleanup unnecessary files from publish output
Write-Host "Step 2: Cleaning up publish output..." -ForegroundColor Yellow

# Remove AppData (read-only in MSIX)
if (Test-Path "$StoreDir\AppData") {
    Remove-Item -Path "$StoreDir\AppData" -Recurse -Force
    Write-Host "  Removed AppData directory" -ForegroundColor DarkGray
}

# Remove libvlc win-arm64 (not needed for x64 package, saves ~82 MB)
$arm64Vlc = Get-ChildItem -Path "$StoreDir" -Directory -Filter "win-arm64" -Recurse -ErrorAction SilentlyContinue
foreach ($dir in $arm64Vlc) {
    if ($dir.FullName -like "*libvlc*" -or $dir.FullName -like "*VideoLAN*") {
        Remove-Item -Path $dir.FullName -Recurse -Force
        Write-Host "  Removed $($dir.FullName) (arm64 VLC)" -ForegroundColor DarkGray
    }
}

# Remove libvlc win-x86 (not needed for x64 package)
$x86Vlc = Get-ChildItem -Path "$StoreDir" -Directory -Filter "win-x86" -Recurse -ErrorAction SilentlyContinue
foreach ($dir in $x86Vlc) {
    if ($dir.FullName -like "*libvlc*" -or $dir.FullName -like "*VideoLAN*") {
        Remove-Item -Path $dir.FullName -Recurse -Force
        Write-Host "  Removed $($dir.FullName) (x86 VLC)" -ForegroundColor DarkGray
    }
}

# Remove test/mock assemblies that shouldn't be in production
$testAssemblies = @("NSubstitute.dll", "Castle.Core.dll", "NSubstitute.Analyzers.CSharp.dll")
foreach ($asm in $testAssemblies) {
    $found = Get-ChildItem -Path "$StoreDir" -Filter $asm -Recurse -ErrorAction SilentlyContinue
    foreach ($f in $found) {
        Remove-Item -Path $f.FullName -Force
        Write-Host "  Removed $($f.Name)" -ForegroundColor DarkGray
    }
}

# 3. Extract and Zip PDBs
Write-Host "Step 3: Zipping PDB files..." -ForegroundColor Yellow
$pdbs = Get-ChildItem -Path "$StoreDir" -Filter "*.pdb"
if ($pdbs) {
    Compress-Archive -Path $pdbs.FullName -DestinationPath "$PdbZip" -Force
    Write-Host "PDBs preserved in: $PdbZip" -ForegroundColor Green
    Remove-Item $pdbs.FullName -Force
} else {
    Write-Host "No PDB files found." -ForegroundColor DarkYellow
}

# 4. Create Portable ZIP
Write-Host "Step 4: Creating Portable version..." -ForegroundColor Yellow
$PortableZip = Join-Path $PublishDir "YiboFile_Core_$Version`_Portable.zip"
Compress-Archive -Path "$StoreDir\*" -DestinationPath "$PortableZip" -Force
Write-Host "Portable version created: $PortableZip" -ForegroundColor Green

# 5. Prepare MSIX Assets (Copy from Packaging)
Write-Host "Step 5: Preparing MSIX assets..." -ForegroundColor Yellow
$PackagingDir = Join-Path $ProjectDir "Packaging"
if (Test-Path $PackagingDir) {
    Copy-Item -Path "$PackagingDir\AppxManifest.xml" -Destination "$StoreDir\" -Force
    if (-not (Test-Path "$StoreDir\Resources")) { New-Item -Path "$StoreDir\Resources" -ItemType Directory -Force }
    Copy-Item -Path "$PackagingDir\Resources\*" -Destination "$StoreDir\Resources\" -Recurse -Force

    # Copy third-party notice
    $noticeFile = Join-Path $ProjectDir "THIRD_PARTY_NOTICES.md"
    if (Test-Path $noticeFile) {
        Copy-Item -Path $noticeFile -Destination "$StoreDir\" -Force
        Write-Host "  Third-party notices included" -ForegroundColor DarkGray
    }
} else {
    Write-Host "Warning: Packaging directory not found. MSIX might fail." -ForegroundColor DarkYellow
}

# 6. Report package composition
Write-Host "Step 6: Package composition report..." -ForegroundColor Yellow
$totalSize = (Get-ChildItem -Path $StoreDir -Recurse | Measure-Object -Property Length -Sum).Sum / 1MB
Write-Host "  Total size: $([math]::Round($totalSize, 1)) MB" -ForegroundColor Cyan

$vlcSize = 0
$vlcDir = Get-ChildItem -Path "$StoreDir" -Directory -Filter "libvlc*" -Recurse -ErrorAction SilentlyContinue
if ($vlcDir) {
    $vlcSize = (Get-ChildItem -Path $vlcDir.FullName -Recurse -File -ErrorAction SilentlyContinue | Measure-Object -Property Length -Sum).Sum / 1MB
}
Write-Host "  VLC: $([math]::Round($vlcSize, 1)) MB | .NET Runtime: ~70 MB | App: ~$([math]::Round($totalSize - $vlcSize - 70, 1)) MB" -ForegroundColor Cyan

# 7. Run MSIX Packaging
Write-Host "Step 7: Creating MSIX package..." -ForegroundColor Yellow

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
    $msixSize = (Get-Item $OutputFile).Length / 1MB
    Write-Host "Success! MSIX created: $OutputFile ($([math]::Round($msixSize, 1)) MB)" -ForegroundColor Green
} else {
    Write-Host "Error: MSIX packaging failed." -ForegroundColor Red
    exit 1
}

