# 文件列表绑定架构 (FileList Binding Architecture)

## 概述
本文档描述了 YiboFile 中“文件列表”组件的数据绑定架构。该架构旨在支持 MVVM 模式，实现 ViewModel 与 UI 的解耦。

## 组件 (Components)

### 1. FileListViewModel
*   **角色**: 管理文件集合 (`Files`) 和加载逻辑。
*   **关键属性**:
    *   `Files`: `ObservableCollection<FileSystemItem>` - 文件列表的唯一真实数据源。
    *   `IsLoading`: `bool` - 指示是否正在进行加载操作。
    *   `CurrentPath`: `string` - 当前加载的路径。
*   **关键方法**:
    *   `LoadPathAsync(string)`: 异步加载指定路径的文件。
    *   `SetFiles(IEnumerable<FileSystemItem>)`: 直接设置文件列表（用于“库”模式或“搜索”结果）。

### 2. FileBrowserControl
*   **角色**: 显示文件列表和地址栏。
*   **关键依赖属性 (Dependency Properties)**:
    *   `FilesItemsSource`: 绑定到 `FileListViewModel.Files`。
    *   `IsLoading`: 绑定到 `FileListViewModel.IsLoading`。
    *   `AddressText`: 双向绑定到 `FileListViewModel.CurrentPath`。

### 3. MainWindow
*   **角色**: 作为宿主窗口，设置 DataContext 和绑定关系。
*   **绑定设置**:
    ```xml
    <controls:FileBrowserControl
        FilesItemsSource="{Binding FileList.Files}"
        IsLoading="{Binding FileList.IsLoading}"
        AddressText="{Binding CurrentPath, Mode=TwoWay}"
        ... />
    ```

## 逻辑流程 (Logic Flow)

### 路径导航 (Path Navigation)
1.  用户或系统触发导航。
2.  `MainWindow` 调用 `_viewModel.FileList.LoadPathAsync(path)`。
3.  ViewModel 更新 `Files` 集合。
4.  数据绑定自动更新 `FileBrowserControl` 的 UI。

### 库 / 特殊模式 (Library / Special Mode)
1.  `MainWindow` 通过 `LibraryService` 加载库数据。
2.  `MainWindow` 调用 `_viewModel.FileList.SetFiles(items)`。
3.  ViewModel 更新 `Files` 集合。
4.  数据绑定更新 `FileBrowserControl` 的 UI。
5.  **重要**: `MainWindow` 不再直接设置 `FileBrowser.FilesItemsSource`，从而避免破坏绑定关系。

## 优势 (Benefits)
*   **解耦**: `FileListViewModel` 不再引用 `FileBrowserControl`，降低了耦合度。
*   **一致性**: 所有文件列表的更新都通过单一的数据流（ViewModel 的 `Files` 集合）进行。
*   **可维护性**: 更容易测试和修改加载逻辑，且不会意外破坏 UI 行为。
