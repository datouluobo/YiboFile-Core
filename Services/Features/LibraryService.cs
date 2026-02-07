using System;
using YiboFile.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using YiboFile.Controls;
using YiboFile.Services.FileList;
using YiboFile.Services.Navigation;

namespace YiboFile.Services
{
    /// <summary>
    /// 库管理服务
    /// 负责库的加载、创建、更新、删除以及库文件的加载和显示
    /// </summary>
    public class LibraryService
    {
        #region 事件定义

        /// <summary>
        /// 库列表已加载事件
        /// </summary>
        public event EventHandler<List<Library>> LibrariesLoaded;

        // LibrarySelected 事件暂时未使用，保留以备将来使用
        // public event EventHandler<Library> LibrarySelected;

        /// <summary>
        /// 库文件已加载事件
        /// </summary>
        public event EventHandler<LibraryFilesLoadedEventArgs> LibraryFilesLoaded;

        /// <summary>
        /// 库需要高亮事件
        /// </summary>
        public event EventHandler<Library> LibraryHighlightRequested;

        #endregion

        #region 私有字段

        private readonly Dispatcher _dispatcher;
        private readonly SemaphoreSlim _loadFilesSemaphore = new SemaphoreSlim(1, 1);
        private readonly FileListService _fileListService;
        private readonly YiboFile.Services.Data.Repositories.ILibraryRepository _repository;

        #endregion

        public LibraryService(Dispatcher dispatcher, YiboFile.Services.Core.Error.ErrorService errorService, YiboFile.Services.Data.Repositories.ILibraryRepository repository = null)
        {
            _dispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _repository = repository ?? App.ServiceProvider?.GetService(typeof(YiboFile.Services.Data.Repositories.ILibraryRepository)) as YiboFile.Services.Data.Repositories.ILibraryRepository;
            _fileListService = new FileListService(_dispatcher, errorService);
        }



        #region 公共方法

        /// <summary>
        /// 加载所有库
        /// </summary>
        public List<Library> LoadLibraries()
        {
            try
            {
                var libraries = _repository.GetAllLibraries();

                LibrariesLoaded?.Invoke(this, libraries);
                return libraries;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryService] LoadLibraries Error: {ex}");
                YiboFile.DialogService.Error($"加载库列表失败: {ex.Message}");
                return new List<Library>();
            }
        }

        /// <summary>
        /// 获取所有库（不触发事件，仅用于数据读取）
        /// </summary>
        public List<Library> GetAllLibraries()
        {
            try
            {
                return _repository.GetAllLibraries();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryService] GetAllLibraries Error: {ex}");
                return new List<Library>();
            }
        }

        /// <summary>
        /// 添加库
        /// </summary>
        public int AddLibrary(string name, string initialPath = null)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryService] AddLibrary called. Name: {name}, InitialPath: {initialPath}");
                var libraryId = _repository.AddLibrary(name);

                if (libraryId > 0)
                {
                    // 如果提供了初始路径,添加到库中
                    if (!string.IsNullOrWhiteSpace(initialPath))
                    {
                        string fullPath = initialPath;
                        try
                        {
                            fullPath = Path.GetFullPath(initialPath);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[LibraryService] Path.GetFullPath failed for initial path {initialPath}: {ex.Message}");
                        }
                        _repository.AddLibraryPath(libraryId, fullPath);
                    }

                    // 触发库列表已加载事件
                    LoadLibraries();

                    // 返回新创建的库ID
                    return libraryId;
                }
                else if (libraryId < 0)
                {
                    // 库已存在，刷新列表
                    LoadLibraries();
                    YiboFile.DialogService.Info($"库名称已存在，已刷新库列表");
                    return libraryId;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[LibraryService] AddLibrary failed. Id=0");
                    YiboFile.DialogService.Error("创建库失败");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryService] AddLibrary Exception: {ex}");
                YiboFile.DialogService.Error($"创建库失败: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// 导入库（从文件夹路径创建新库）
        /// </summary>
        public bool ImportLibrary(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            {
                return false;
            }

            try
            {
                var name = new DirectoryInfo(path).Name;
                return AddLibrary(name, path) > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 更新库名称
        /// </summary>
        public bool UpdateLibraryName(int libraryId, string newName)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(newName))
                {
                    YiboFile.DialogService.Warning("库名称不能为空");
                    return false;
                }

                _repository.UpdateLibraryName(libraryId, newName);
                LoadLibraries();
                return true;
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"重命名失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 删除库
        /// </summary>
        public bool DeleteLibrary(int libraryId, string libraryName)
        {
            try
            {
                _repository.DeleteLibrary(libraryId);
                LoadLibraries();
                return true;
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"删除库失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 获取库信息
        /// </summary>
        public Library GetLibrary(int libraryId)
        {
            return _repository.GetLibrary(libraryId);
        }

        /// <summary>
        /// 获取库路径列表
        /// </summary>
        public List<LibraryPath> GetLibraryPaths(int libraryId)
        {
            return _repository.GetLibraryPaths(libraryId);
        }

        /// <summary>
        /// 添加库路径
        /// </summary>
        public bool AddLibraryPath(int libraryId, string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;

                // 确保保存为绝对路径，避免由于程序运行目录不同导致路径识别错误
                string fullPath = path;
                try
                {
                    fullPath = Path.GetFullPath(path);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LibraryService] Path.GetFullPath failed for {path}: {ex.Message}");
                }

                _repository.AddLibraryPath(libraryId, fullPath);
                LoadLibraries();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LibraryService] AddLibraryPath failed: {ex}");
                YiboFile.DialogService.Error($"添加库路径失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 移除库路径
        /// </summary>
        public bool RemoveLibraryPath(int libraryId, string path)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path)) return false;
                _repository.RemoveLibraryPath(libraryId, path);
                LoadLibraries();
                return true;
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"移除库路径失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 在资源管理器中打开库
        /// </summary>
        public bool OpenLibraryInExplorer(int libraryId)
        {
            try
            {
                var library = _repository.GetLibrary(libraryId);
                if (library == null || library.Paths == null || library.Paths.Count == 0)
                {
                    YiboFile.DialogService.Info("该库没有添加任何位置");
                    return false;
                }

                // 打开第一个位置
                var firstPath = library.Paths[0];
                if (Directory.Exists(firstPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", firstPath);
                    return true;
                }
                else
                {
                    YiboFile.DialogService.Warning($"路径不存在: {firstPath}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"无法打开文件夹: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载库文件
        /// </summary>
        public void LoadLibraryFiles(Library library,
            Func<string, long?> getFolderSizeCache = null,
            Func<long, string> formatFileSize = null,
            PaneId targetPane = PaneId.Main)
        {
            if (library == null)
                return;

            // 使用信号量防止重复加载
            if (!_loadFilesSemaphore.Wait(0))
            {
                return;
            }

            try
            {
                if (library.Paths == null || library.Paths.Count == 0)
                {
                    _loadFilesSemaphore.Release();

                    // 触发空库事件
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        LibraryFilesLoaded?.Invoke(this, new LibraryFilesLoadedEventArgs
                        {
                            Library = library,
                            Files = new List<FileSystemItem>(),
                            IsEmpty = true,
                            TargetPane = targetPane
                        });
                    }), DispatcherPriority.Background);
                    return;
                }

                // 使用默认的格式化函数
                if (formatFileSize == null)
                {
                    formatFileSize = (bytes) => _fileListService.FormatFileSize(bytes);
                }

                // 使用默认的文件夹大小缓存函数
                if (getFolderSizeCache == null)
                {
                    getFolderSizeCache = (path) => DatabaseManager.GetFolderSize(path);
                }

                // 异步加载库文件，避免阻塞UI线程
                Task.Run(async () =>
                {
                    try
                    {
                        // 使用 FileListService 从多个路径加载文件
                        var allItems = await _fileListService.LoadFileSystemItemsFromMultiplePathsAsync(
                            library.Paths,
                            getFolderSizeCache,
                            formatFileSize);

                        // 在UI线程更新文件列表
                        await _dispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                LibraryFilesLoaded?.Invoke(this, new LibraryFilesLoadedEventArgs
                                {
                                    Library = library,
                                    Files = allItems,
                                    IsEmpty = allItems.Count == 0,
                                    TargetPane = targetPane
                                });
                            }
                            finally
                            {
                                _loadFilesSemaphore.Release();
                            }
                        }, DispatcherPriority.Background);
                    }
                    catch (Exception ex)
                    {
                        // 在UI线程显示错误
                        await _dispatcher.InvokeAsync(() =>
                        {
                            _loadFilesSemaphore.Release();
                            YiboFile.DialogService.Error($"加载库文件失败: {ex.Message}");
                        }, DispatcherPriority.Background);
                    }
                });
            }
            catch (Exception ex)
            {
                // 确保释放锁
                _loadFilesSemaphore.Release();
                YiboFile.DialogService.Error($"加载库文件失败: {ex.Message}");
            }
        }


        /// <summary>
        /// 请求高亮库
        /// </summary>
        public void HighlightLibrary(Library library)
        {
            if (library != null)
            {
                LibraryHighlightRequested?.Invoke(this, library);
            }
        }

        #endregion

        #region 导入/导出功能

        /// <summary>
        /// 导出所有库配置为JSON字符串
        /// </summary>
        public string ExportLibrariesToJson()
        {
            try
            {
                var libraries = LoadLibraries();
                // 转换为DTO以避免导出不必要的字段或ID冲突
                var exportData = libraries.Select(l => new
                {
                    Name = l.Name,
                    Paths = l.Paths,
                    DisplayOrder = l.DisplayOrder
                }).ToList();

                // 使用 System.Text.Json 进行序列化
                // 兼容性注意：如果 .NET 版本较低可能需要 Newtonsoft.Json
                // 这里假设环境支持 System.Text.Json
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                return System.Text.Json.JsonSerializer.Serialize(exportData, options);
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"导出库配置失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从JSON字符串导入库配置
        /// </summary>
        public bool ImportLibrariesFromJson(string json)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return false;

                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                // 定义临时 DTO 类结构以匹配 JSON
                var importList = System.Text.Json.JsonSerializer.Deserialize<List<LibraryImportDto>>(json, options);

                if (importList == null || importList.Count == 0)
                {
                    YiboFile.DialogService.Warning("导入的数据为空或格式不正确");
                    return false;
                }

                int successCount = 0;
                foreach (var item in importList)
                {
                    if (string.IsNullOrWhiteSpace(item.Name)) continue;

                    // 尝试添加库
                    // AddLibrary 会处理重名情况（返回负ID或0）
                    // 我们可以先尝试获取现有的，或者直接调用AddLibrary
                    // 为了简单起见，我们先创建库，然后添加路径

                    int libId = AddLibrary(item.Name);
                    if (libId == 0) continue; // 创建失败

                    // 如果库已存在(libId < 0)，我们需要获取它的正ID来添加路径
                    if (libId < 0)
                    {
                        libId = -libId;
                    }

                    // 添加路径
                    if (item.Paths != null)
                    {
                        foreach (var path in item.Paths)
                        {
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                string fullPath = path;
                                try { fullPath = Path.GetFullPath(path); } catch { }
                                _repository.AddLibraryPath(libId, fullPath);
                            }
                        }
                    }

                    successCount++;
                }

                LoadLibraries();
                YiboFile.DialogService.Info($"成功导入 {successCount} 个库配置");
                return true;
            }
            catch (Exception ex)
            {
                YiboFile.DialogService.Error($"导入库配置失败: {ex.Message}");
                return false;
            }
        }

        private class LibraryImportDto
        {
            public string Name { get; set; }
            public List<string> Paths { get; set; }
        }

        #endregion

        #region 私有方法

        // 格式化文件大小功能已移至 FileListService

        #endregion
    }

    /// <summary>
    /// 库文件加载事件参数
    /// </summary>
    public class LibraryFilesLoadedEventArgs : EventArgs
    {
        public Library Library { get; set; }
        public List<FileSystemItem> Files { get; set; }
        public bool IsEmpty { get; set; }
        public PaneId TargetPane { get; set; }
    }
}


