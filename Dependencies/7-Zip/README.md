# 7-Zip独立版下载和配置指南

## 📥 下载7-Zip独立版

### 方案1: 官方下载（推荐）

1. **访问7-Zip官网**
   - URL: https://www.7-zip.org/download.html

2. **下载独立版（Standalone Console version）**
   - 选择: **7-Zip Extra: standalone console version**
   - 文件名: `7z<version>-extra.7z` 或 `7z<version>-x64.exe`
   - 大小: 约1.5MB

3. **提取所需文件**
   - 从下载的压缩包中提取:
     - `7za.exe` (独立可执行文件，约500KB)
     - `7z.exe` (可选)
     - `7z.dll` (可选)

### 方案2: 使用现有7-Zip安装

如果已安装7-Zip，可以从安装目录复制：
- 位置: `C:\Program Files\7-Zip\`
- 文件: `7z.exe`, `7z.dll`

---

## 📁 放置位置

将下载的文件放入以下目录：

```
OoiMRR/
└── Dependencies/
    └── 7-Zip/
        ├── 7za.exe  (或 7z.exe)
        └── 7z.dll   (可选)
```

### 创建目录命令

```powershell
# 在项目根目录执行
New-Item -ItemType Directory -Path "Dependencies\7-Zip" -Force
```

---

## ✅ 验证文件

确保文件结构如下：

```
f:\Download\GitHub\OoiMRR\
├── Dependencies\
│   └── 7-Zip\
│       └── 7za.exe  (或 7z.exe)
├── Previews\
├── Resources\
└── OoiMRR.csproj
```

---

## 🔧 自动配置（可选）

运行以下PowerShell脚本自动下载和配置：

```powershell
# 设置项目根目录
$projectRoot = "f:\Download\GitHub\OoiMRR"
$sevenZipDir = Join-Path $projectRoot "Dependencies\7-Zip"

# 创建目录
New-Item -ItemType Directory -Path $sevenZipDir -Force

# 如果系统已安装7-Zip，复制文件
$systemSevenZip = "C:\Program Files\7-Zip\7z.exe"
if (Test-Path $systemSevenZip) {
    Copy-Item $systemSevenZip -Destination $sevenZipDir
    Write-Host "✅ 已从系统7-Zip复制到项目" -ForegroundColor Green
} else {
    Write-Host "⚠️  未找到系统7-Zip，请手动下载" -ForegroundColor Yellow
    Write-Host "下载地址: https://www.7-zip.org/download.html"
    Start-Process "https://www.7-zip.org/download.html"
}
```

---

## 📝 许可证

7-Zip是开源软件（LGPL许可证），可以自由分发。

建议在`Dependencies/7-Zip/`目录下添加`LICENSE.txt`文件说明来源。

---

完成后，代码将自动使用项目内置的7-Zip，用户无需额外安装！
