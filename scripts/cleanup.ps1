# 清理脚本：删除无用的过程、缓存、测试、旧编译等文件

Write-Host "开始清理..."

# 切换到项目根目录
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location (Join-Path $scriptPath "..")

# 清理构建产物
if (Test-Path "bin") {
    Write-Host "删除 bin 目录..."
    Remove-Item -Path "bin" -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path "obj") {
    Write-Host "删除 obj 目录..."
    Remove-Item -Path "obj" -Recurse -Force -ErrorAction SilentlyContinue
}

# 清理测试文件编译产物
if (Test-Path "Tests\bin") {
    Write-Host "删除 Tests\bin 目录..."
    Remove-Item -Path "Tests\bin" -Recurse -Force -ErrorAction SilentlyContinue
}

if (Test-Path "Tests\obj") {
    Write-Host "删除 Tests\obj 目录..."
    Remove-Item -Path "Tests\obj" -Recurse -Force -ErrorAction SilentlyContinue
}

# 清理工具项目编译产物
Get-ChildItem -Path "Tools" -Recurse -Directory -Filter "bin" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path "Tools" -Recurse -Directory -Filter "obj" -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

# 清理临时文件
Write-Host "清理临时文件..."
Get-ChildItem -Path . -Recurse -Include "*.tmp", "*.log", "*.cache", "*.suo", "*.user" -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# 清理 Visual Studio 缓存
if (Test-Path ".vs") {
    Write-Host "删除 .vs 目录..."
    Remove-Item -Path ".vs" -Recurse -Force -ErrorAction SilentlyContinue
}

# 清理无用的日志文件
if (Test-Path "dragdrop_log.txt") {
    Remove-Item -Path "dragdrop_log.txt" -Force -ErrorAction SilentlyContinue
}

Write-Host "清理完成！"





































