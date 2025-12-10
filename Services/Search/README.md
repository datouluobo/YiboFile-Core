# Search Service 拆分说明

## 结构

搜索功能已拆分为以下模块：

### 1. SearchModels.cs
数据模型定义：
- `FileTypeFilter` - 文件类型过滤器枚举
- `PathRangeFilter` - 路径范围过滤器枚举
- `SearchOptions` - 搜索选项类
- `SearchCache` - 搜索缓存项
- `SearchResult` - 搜索结果
- `SearchResultEventArgs` - 搜索结果事件参数

### 2. SearchFilterService.cs
过滤器服务，负责：
- 应用文件类型过滤（图片、视频、文档、文件夹等）
- 获取路径范围（当前磁盘/全部磁盘）

### 3. SearchCacheService.cs
缓存管理服务，负责：
- 搜索结果的缓存管理
- 缓存过期检查
- 缓存更新和清除

### 4. SearchResultBuilder.cs
结果构建器，负责：
- 从文件路径构建 `FileSystemItem` 对象
- 获取文件标签和备注
- 按相关性对搜索结果排序

### 5. SearchService.cs
核心搜索服务，负责：
- 执行搜索操作（Everything/默认搜索/备注搜索）
- 异步分页加载
- 加载更多结果
- 刷新搜索

## 使用示例

```csharp
using OoiMRR.Services.FileNotes;

// 1. 初始化服务
var filterService = new SearchFilterService();
var cacheService = new SearchCacheService();
var resultBuilder = new SearchResultBuilder(
    formatFileSize: FormatFileSize,
    getFileTagIds: path => OoiMRRIntegration.GetFileTagIds(path),
    getTagName: tagId => OoiMRRIntegration.GetTagName(tagId),
    getFileNotes: path => FileNotesService.GetFileNotes(path)
);
var searchService = new SearchService(filterService, cacheService, resultBuilder);

// 2. 执行搜索
var options = new SearchOptions 
{ 
    Type = FileTypeFilter.All, 
    PathRange = PathRangeFilter.AllDrives 
};
var result = await searchService.PerformSearchAsync(
    keyword: "test",
    searchOptions: options,
    currentPath: "C:\\",
    searchNames: true,
    searchNotes: true,
    getNotesFromDb: keyword => FileNotesService.SearchFilesByNotes(keyword),
    progressCallback: (pageResult) => 
    {
        // 更新UI显示每页结果
    }
);

// 3. 加载更多
var moreResult = searchService.LoadMore(
    keyword: "test",
    offset: result.Offset,
    searchOptions: options,
    currentPath: "C:\\"
);

// 4. 使用缓存
var cacheKey = $"search://{keyword}";
var cache = cacheService.GetCache(cacheKey);
if (cache != null && cache.HasMore)
{
    // 使用缓存的结果
}
```


