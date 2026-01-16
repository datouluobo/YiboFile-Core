# 编译脚本 - 自动关闭进程并编译

Write-Host "正在关闭 YiboFile 进程..." -ForegroundColor Yellow
Stop-Process -Name "YiboFile" -Force -ErrorAction SilentlyContinue
Start-Sleep -Milliseconds 500

Write-Host "开始编译..." -ForegroundColor Green
dotnet build

if ($LASTEXITCODE -eq 0) {
    Write-Host "编译成功！" -ForegroundColor Green
    
    # 询问是否运行
    $run = Read-Host "是否运行程序? [Y/N]"
    if ($run -eq "Y" -or $run -eq "y" -or $run -eq "") {
        Write-Host "启动程序..." -ForegroundColor Cyan
        Start-Process ".\bin\Debug\net8.0-windows\YiboFile.exe"
    }
} else {
    Write-Host "编译失败！" -ForegroundColor Red
}


