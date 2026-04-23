<#
.SYNOPSIS
    从 SVG 文件批量提取 Path Data，生成 WPF XAML ResourceDictionary 图标集。

.DESCRIPTION
    遍历指定目录下的 SVG 文件，提取 <path d="..."/> 中的 d 属性，
    生成 DrawingImage 格式的 XAML 资源字典，用于 YiboFile 图标系统。

.PARAMETER SvgDirectory
    包含 SVG 文件的目录路径。

.PARAMETER OutputFile
    输出 XAML 文件路径。

.PARAMETER IconSetName
    图标集名称（如 Remix、Material、Fluent 等），用于元数据。

.PARAMETER IconSetDisplayName
    图标集显示名称（中文），用于设置界面。

.PARAMETER RenderMode
    渲染模式：Fill（实心填充）或 Stroke（轮廓描边）。

.PARAMETER StrokeThickness
    Stroke 模式下的线宽，默认 2。

.PARAMETER StrokeLineCap
    Stroke 模式下的线帽样式：Round / Square / Flat。

.PARAMETER StrokeLineJoin
    Stroke 模式下的连接样式：Round / Miter / Bevel。

.PARAMETER IconMapping
    文件名(不含扩展名) → 图标键名 的映射哈希表。
    例: @{ "settings" = "Icon_Settings"; "folder" = "Icon_Folder" }

.EXAMPLE
    .\Extract-SvgPaths.ps1 -SvgDirectory ".\temp\remix" -OutputFile "..\Styles\Icons\Remix.xaml" `
        -IconSetName "Remix" -IconSetDisplayName "Remix 图标" -RenderMode Fill `
        -IconMapping @{ "file-copy-line" = "Icon_Copy"; "settings-3-line" = "Icon_Settings" }
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$SvgDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputFile,

    [string]$IconSetName = "Custom",
    [string]$IconSetDisplayName = "自定义图标",
    [string]$IconSetDescription = "",

    [ValidateSet("Fill", "Stroke")]
    [string]$RenderMode = "Fill",

    [double]$StrokeThickness = 2,
    [string]$StrokeLineCap = "Round",
    [string]$StrokeLineJoin = "Round",

    [hashtable]$IconMapping = @{}
)

$ErrorActionPreference = "Stop"

# ── SVG 解析函数 ────────────────────────────────────────────

function Get-SvgPathData {
    param([string]$SvgFilePath)

    [xml]$svg = Get-Content $SvgFilePath -Raw
    $nsMgr = New-Object System.Xml.XmlNamespaceManager($svg.NameTable)
    $nsMgr.AddNamespace("svg", "http://www.w3.org/2000/svg")

    $paths = $svg.SelectNodes("//svg:path", $nsMgr)
    if (-not $paths -or $paths.Count -eq 0) {
        # 尝试无命名空间
        $paths = $svg.SelectNodes("//path")
    }

    $allPathData = @()
    foreach ($path in $paths) {
        $d = $path.GetAttribute("d")
        if ($d) {
            $allPathData += $d.Trim()
        }
    }

    # 合并多条 path 为一条（用空格分隔）
    return ($allPathData -join " ")
}

function Get-SvgViewBox {
    param([string]$SvgFilePath)

    [xml]$svg = Get-Content $SvgFilePath -Raw
    $root = $svg.DocumentElement
    $viewBox = $root.GetAttribute("viewBox")
    if ($viewBox) {
        $parts = $viewBox -split "[\s,]+"
        if ($parts.Count -eq 4) {
            return @{
                X      = [double]$parts[0]
                Y      = [double]$parts[1]
                Width  = [double]$parts[2]
                Height = [double]$parts[3]
            }
        }
    }
    return @{ X = 0; Y = 0; Width = 24; Height = 24 }
}

# ── XAML 生成函数 ────────────────────────────────────────────

function New-DrawingImageXaml_Fill {
    param([string]$GeoKey, [string]$IconKey)

    return @"
    <!-- $IconKey -->
    <DrawingImage x:Key="$IconKey">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Brush="Transparent" Geometry="M0,0 L24,0 L24,24 L0,24 Z"/>
                <GeometryDrawing Brush="{DynamicResource ForegroundPrimaryBrush}"
                                 Geometry="{StaticResource $GeoKey}"/>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>
"@
}

function New-DrawingImageXaml_Stroke {
    param([string]$GeoKey, [string]$IconKey,
          [double]$Thickness, [string]$LineCap, [string]$LineJoin)

    return @"
    <!-- $IconKey -->
    <DrawingImage x:Key="$IconKey">
        <DrawingImage.Drawing>
            <DrawingGroup>
                <GeometryDrawing Brush="Transparent" Geometry="M0,0 L24,0 L24,24 L0,24 Z"/>
                <GeometryDrawing Geometry="{StaticResource $GeoKey}">
                    <GeometryDrawing.Pen>
                        <Pen Brush="{DynamicResource ForegroundPrimaryBrush}"
                             Thickness="$Thickness"
                             StartLineCap="$LineCap" EndLineCap="$LineCap"
                             LineJoin="$LineJoin"/>
                    </GeometryDrawing.Pen>
                </GeometryDrawing>
            </DrawingGroup>
        </DrawingImage.Drawing>
    </DrawingImage>
"@
}

# ── 主流程 ────────────────────────────────────────────────────

Write-Host "=== YiboFile SVG → XAML 图标提取工具 ===" -ForegroundColor Cyan
Write-Host "图标集: $IconSetName ($RenderMode 模式)"
Write-Host "源目录: $SvgDirectory"
Write-Host "输出到: $OutputFile"
Write-Host ""

if (-not (Test-Path $SvgDirectory)) {
    Write-Error "SVG 目录不存在: $SvgDirectory"
    exit 1
}

$svgFiles = Get-ChildItem -Path $SvgDirectory -Filter "*.svg" -File
Write-Host "找到 $($svgFiles.Count) 个 SVG 文件"

# 构建 Geometry 和 DrawingImage 内容
$geometries = @()
$drawingImages = @()
$processedCount = 0
$skippedCount = 0

foreach ($svgFile in $svgFiles) {
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($svgFile.Name)

    # 查找映射的图标键名
    $iconKey = $null
    if ($IconMapping.ContainsKey($baseName)) {
        $iconKey = $IconMapping[$baseName]
    }
    else {
        # 未在映射表中，跳过
        $skippedCount++
        continue
    }

    # 提取 Path Data
    $pathData = Get-SvgPathData -SvgFilePath $svgFile.FullName
    if (-not $pathData) {
        Write-Warning "  ⚠ 无法提取路径: $($svgFile.Name)"
        $skippedCount++
        continue
    }

    # 检查 viewBox 是否为 24x24
    $viewBox = Get-SvgViewBox -SvgFilePath $svgFile.FullName
    if ($viewBox.Width -ne 24 -or $viewBox.Height -ne 24) {
        Write-Warning "  ⚠ viewBox 非 24x24 ($($viewBox.Width)x$($viewBox.Height)): $($svgFile.Name) — 需手动归一化"
    }

    $geoKey = "Geo_$($iconKey -replace '^Icon_', '')"

    # 添加 Geometry
    $geometries += "    <Geometry x:Key=`"$geoKey`">$pathData</Geometry>"

    # 添加 DrawingImage
    if ($RenderMode -eq "Fill") {
        $drawingImages += (New-DrawingImageXaml_Fill -GeoKey $geoKey -IconKey $iconKey)
    }
    else {
        $drawingImages += (New-DrawingImageXaml_Stroke -GeoKey $geoKey -IconKey $iconKey `
            -Thickness $StrokeThickness -LineCap $StrokeLineCap -LineJoin $StrokeLineJoin)
    }

    $processedCount++
    Write-Host "  ✓ $baseName → $iconKey" -ForegroundColor Green
}

Write-Host ""
Write-Host "处理完成: $processedCount 个图标, 跳过 $skippedCount 个" -ForegroundColor Cyan

# 组装完整 XAML
$renderModeValue = if ($RenderMode -eq "Fill") { "Path" } else { "Path" }
$xamlContent = @"
<!-- YiboFile 图标集: $IconSetName -->
<!-- 自动生成于 $(Get-Date -Format 'yyyy-MM-dd HH:mm') -->
<!-- 渲染模式: $RenderMode -->
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                    xmlns:sys="clr-namespace:System;assembly=mscorlib">

    <!-- ======== 元数据 ======== -->
    <sys:String x:Key="IconStyleId">$IconSetName</sys:String>
    <sys:String x:Key="IconStyleDisplayName">$IconSetDisplayName</sys:String>
    <sys:String x:Key="IconStyleDescription">$IconSetDescription</sys:String>
    <sys:String x:Key="IconRenderMode">Path</sys:String>

    <!-- ======== Geometry 定义 ======== -->
$($geometries -join "`r`n")

    <!-- ======== DrawingImage 图标 ======== -->
$($drawingImages -join "`r`n")

</ResourceDictionary>
"@

# 写入文件
$outputDir = Split-Path $OutputFile -Parent
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

$xamlContent | Out-File -FilePath $OutputFile -Encoding utf8
Write-Host ""
Write-Host "✅ 已生成: $OutputFile" -ForegroundColor Green
Write-Host "   共 $processedCount 个图标定义"
