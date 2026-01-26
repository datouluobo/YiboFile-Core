Write-Host "YiboFile - 文件资源管理器" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""
Write-Host "正在检查并启动应用程序..." -ForegroundColor Green
Write-Host ""

# 检查是否已经有实例在运行
$processes = Get-Process -Name "YiboFile" -ErrorAction SilentlyContinue
if ($processes) {
    Write-Host "检测到应用程序已在运行，请先关闭现有实例。" -ForegroundColor Yellow
    Write-Host ""
    Read-Host "按任意键退出"
    exit 1
}

# 尝试直接运行已编译的程序
if (Test-Path "bin\Debug\net8.0-windows\YiboFile.exe") {
    Write-Host "找到已编译的程序，正在启动..." -ForegroundColor Green
    & "bin\Debug\net8.0-windows\YiboFile.exe"
    Write-Host "应用程序已启动！" -ForegroundColor Green
} else {
    Write-Host "未找到已编译的程序，正在编译..." -ForegroundColor Yellow
    dotnet build
    if ($LASTEXITCODE -eq 0) {
        Write-Host "编译成功，正在启动..." -ForegroundColor Green
        & "bin\Debug\net8.0-windows\YiboFile.exe"
        Write-Host "应用程序已启动！" -ForegroundColor Green
    } else {
        Write-Host "编译失败，请检查错误信息。" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "按任意键退出..." -ForegroundColor Yellow
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")

