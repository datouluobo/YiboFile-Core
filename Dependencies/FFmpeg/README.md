# FFmpeg 依赖说明

本文件夹用于存放 FFmpeg 相关的依赖和配置。

## 使用方式

项目使用 **FFMpegCore** NuGet 包来处理视频缩略图提取。

### 自动查找 FFmpeg

FFMpegCore 会自动在以下位置查找 FFmpeg：
1. 系统 PATH 环境变量
2. 程序目录
3. 常见的安装路径

### 手动指定 FFmpeg 路径

如果需要手动指定 FFmpeg 路径，可以在代码中配置：

```csharp
GlobalFFOptions.Configure(new FFOptions 
{ 
    BinaryFolder = @"C:\ffmpeg\bin",
    TemporaryFilesFolder = Path.GetTempPath()
});
```

### FFmpeg 下载

如果系统未安装 FFmpeg，可以从以下地址下载：
- Windows: https://www.gyan.dev/ffmpeg/builds/
- 或使用 Chocolatey: `choco install ffmpeg`

## 功能

当前用于：
- 视频文件缩略图提取（提取第一帧作为预览图）




