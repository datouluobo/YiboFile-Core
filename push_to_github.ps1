# YiboFile - 推送到 GitHub 脚本

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  YiboFile - Push to GitHub" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查是否已配置远程仓库
$remoteUrl = git remote get-url origin 2>$null

if (-not $remoteUrl) {
    Write-Host "⚠️  未配置远程仓库" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "请先在 GitHub 上创建私有仓库，然后运行：" -ForegroundColor White
    Write-Host "git remote add origin https://github.com/你的用户名/YiboFile.git" -ForegroundColor Green
    Write-Host ""
    Write-Host "详细步骤请参考 GITHUB_SETUP.md 文件" -ForegroundColor White
    Write-Host ""
    pause
    exit
}

Write-Host "✅ 远程仓库: $remoteUrl" -ForegroundColor Green
Write-Host ""

# 显示当前状态
Write-Host "📊 当前状态:" -ForegroundColor Cyan
git status --short
Write-Host ""

# 显示提交历史
Write-Host "📝 提交历史:" -ForegroundColor Cyan
git log --oneline -5
Write-Host ""

# 询问是否推送
$confirm = Read-Host "是否推送到 GitHub? (y/n)"

if ($confirm -eq 'y' -or $confirm -eq 'Y') {
    Write-Host ""
    Write-Host "🚀 正在推送到 GitHub..." -ForegroundColor Yellow
    
    git push -u origin main
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host ""
        Write-Host "✅ 推送成功！" -ForegroundColor Green
        Write-Host ""
        Write-Host "访问你的仓库查看: $remoteUrl" -ForegroundColor Cyan
    } else {
        Write-Host ""
        Write-Host "❌ 推送失败！" -ForegroundColor Red
        Write-Host ""
        Write-Host "可能的原因：" -ForegroundColor Yellow
        Write-Host "1. 需要认证 - 请使用 Personal Access Token" -ForegroundColor White
        Write-Host "2. 网络问题 - 请检查网络连接" -ForegroundColor White
        Write-Host "3. 权限问题 - 请检查仓库权限" -ForegroundColor White
        Write-Host ""
        Write-Host "详细步骤请参考 GITHUB_SETUP.md 文件" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "❌ 已取消推送" -ForegroundColor Yellow
}

Write-Host ""
pause


