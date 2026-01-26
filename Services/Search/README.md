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

### 5. SearchResultGrouper.cs
结果分组器，负责：
- 将搜索结果按类型分组（备注、文件夹、文件）
- 支持从缓存恢复分组结果

### 6. EverythingSearchExecutor.cs
Everything 搜索执行器，负责：
- 执行 Everything 搜索的分页逻辑
- 异步分页加载搜索结果
- 单页搜索执行（用于分页操作）

### 7. NotesSearchExecutor.cs
备注搜索执行器，负责：
- 执行备注搜索操作
- 从数据库获取备注匹配的文件路径

### 8. SearchPaginationService.cs
搜索分页服务，负责：
- 处理搜索结果的分页操作
- 加载更多结果
- 刷新搜索（重新执行第一页）

### 9. SearchService.cs
搜索服务（编排器），负责：
- 协调各个搜索执行器
- 组合 Everything 搜索和备注搜索结果
- 提供统一的搜索接口

## 架构设计

搜索服务采用编排器模式，将搜索功能拆分为多个职责单一的执行器：

```
SearchService (编排器)
├── EverythingSearchExecutor (Everything 搜索执行器)
├── NotesSearchExecutor (备注搜索执行器)
└── SearchPaginationService (分页服务)
    └── EverythingSearchExecutor (复用)
```

### 职责划分

- **SearchService**: 作为编排器，协调各个执行器，组合搜索结果
- **EverythingSearchExecutor**: 专门处理 Everything 搜索的分页逻辑
- **NotesSearchExecutor**: 专门处理备注搜索
- **SearchPaginationService**: 专门处理分页操作（加载更多、刷新）

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
var searchService = new SearchService(
    filterService, 
    cacheService, 
    resultBuilder,
    pageSize: 1000,
    maxResults: 5000
);

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

// 4. 刷新搜索
var refreshResult = searchService.RefreshSearch(
    keyword: "test",
    searchOptions: options,
    currentPath: "C:\\"
);

// 5. 使用缓存
var cacheKey = $"search://{keyword}";
var cache = cacheService.GetCache(cacheKey);
if (cache != null && cache.HasMore)
{
    // 使用缓存的结果
}
```


