using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using YiboFile.Models.Shell;

namespace YiboFile.Interop.Shell
{
    public sealed class NativeShellMenuHost : IDisposable
    {
        public event Action<string> RenameRequested;

        private IntPtr _contextMenuPtr;
        private IContextMenu _contextMenuRCW;
        private IContextMenu2 _contextMenu2RCW;
        private IContextMenu3 _contextMenu3RCW;
        private IntPtr _hMenu;
        private bool _disposed;

        private const string MENU_WND_CLASS = "YiboFileShellMenuHost";
        private static bool _classRegistered;
        private IntPtr _hostWnd;
        private static NativeShellMenuHost _currentHost;
        
        // v36: 保持 COM 对象引用，防止过早释放
        private IShellFolder _retainedDesktopFolder;
        private IShellFolder _retainedParentFolder;
        
        // v37: WM_COMMAND 消息上下文数据
        private int _pendingCommandId;
        private string _pendingFilePath;
        private IntPtr _pendingOwnerHwnd;
        
        // v34: 菜单项 ID → 文本 映射（用于推断动词）
        private Dictionary<int, string> _menuTextMap;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowExW(
            uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle,
            int x, int y, int nWidth, int nHeight,
            IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProcW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool PostMessageW(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMenuStringW(IntPtr hMenu, uint uIDItem, [Out] StringBuilder lpString, int nMaxCount, uint uFlag);
        
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr ShellExecuteW(
            IntPtr hwnd, [MarshalAs(UnmanagedType.LPWStr)] string lpOperation,
            [MarshalAs(UnmanagedType.LPWStr)] string lpFile,
            [MarshalAs(UnmanagedType.LPWStr)] string lpParameters,
            [MarshalAs(UnmanagedType.LPWStr)] string lpDirectory, int nShowCmd);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        private static WndProcDelegate _wndProcDelegate;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }

        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return;

            // v30: 详细路径诊断
            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v30] ========== 路径诊断 ==========");
            for (int i = 0; i < pathList.Count; i++)
            {
                string p = pathList[i];
                bool exists = System.IO.File.Exists(p) || System.IO.Directory.Exists(p);
                bool isFile = System.IO.File.Exists(p);
                bool isDir = System.IO.Directory.Exists(p);
                string fileName = System.IO.Path.GetFileName(p);
                string dirName = System.IO.Path.GetDirectoryName(p) ?? "";
                
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v30] paths[{i}]='{p}'");
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v30]   存在={exists}, 是文件={isFile}, 是目录={isDir}");
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v30]   文件名='{fileName}', 目录='{dirName}'");
            }

            // 保存 owner 窗口句柄供 InvokeCommand 使用
            IntPtr _ownerHwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;

            if (GetContextMenu(pathList) == false)
            {
                Cleanup();
                return;
            }

            _hMenu = NativeMethods.CreatePopupMenu();
            uint queryFlags = ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE
                | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU;
            
            // 使用 RCW 对象调用 QueryContextMenu（这是安全的）
            int hr = _contextMenuRCW.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, queryFlags);
            System.Diagnostics.Debug.WriteLine($"[ShellMenu] QueryContextMenu: hr=0x{hr:X8}");

            _currentHost = this;
            EnsureWindowClass();
            
            var ownerHandle = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            
            _hostWnd = CreateWindowExW(
                0,
                MENU_WND_CLASS,
                "ShellMenuHost",
                0,
                0, 0, 1, 1,
                ownerHandle,
                IntPtr.Zero,
                GetModuleHandle(null),
                IntPtr.Zero);

            if (_hostWnd == IntPtr.Zero)
            {
                Cleanup();
                return;
            }

            SetForegroundWindow(_hostWnd);

            // v39: 使用 GetMenuStringW 获取所有菜单项的文本
            _menuTextMap = new Dictionary<int, string>();
            int itemCount = NativeMethods.GetMenuItemCount(_hMenu);
            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] 菜单项总数: {itemCount}");
            
            for (int i = 0; i < itemCount; i++)
            {
                try
                {
                    // 先获取菜单项 ID
                    var mii = MENUITEMINFO.Create();
                    mii.fMask = ShellConstants.MIIM_ID;
                    
                    if (NativeMethods.GetMenuItemInfoW(_hMenu, (uint)i, true, ref mii))
                    {
                        int menuId = (int)mii.wID;
                        if (menuId > 0)
                        {
                            // 使用 GetMenuStringW 获取文本（更可靠的方法）
                            var sb = new StringBuilder(256);
                            int len = GetMenuStringW(_hMenu, (uint)menuId, sb, sb.Capacity, 0x00000000);  // MF_BYCOMMAND
                            
                            if (len > 0)
                            {
                                string text = sb.ToString();
                                _menuTextMap[menuId] = text;
                                
                                string displayText = text.Length > 40 ? text.Substring(0, 40) + "..." : text;
                                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39]   菜单[{i}] ID={menuId}, Text='{displayText}'");
                            }
                            else
                            {
                                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39]   菜单[{i}] ID={menuId}, 文本为空 (len={len})");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39]   菜单[{i}] 获取失败: {ex.Message}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] 成功获取 {_menuTextMap.Count} 个菜单项文本");

            uint flags = ShellConstants.TPM_LEFTALIGN | ShellConstants.TPM_TOPALIGN
                | ShellConstants.TPM_RIGHTBUTTON | ShellConstants.TPM_RETURNCMD;
            
            int selectedId = NativeMethods.TrackPopupMenuEx(
                _hMenu,
                flags,
                (int)screenPoint.X,
                (int)screenPoint.Y,
                _hostWnd,
                IntPtr.Zero);

            if (selectedId > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37] selectedId={selectedId}, 使用 PostMessage(WM_COMMAND) 方式");
                
                // v37: 保存上下文数据，通过 WM_COMMAND 消息在消息循环中执行
                _pendingCommandId = selectedId;
                _pendingFilePath = pathList[0];
                _pendingOwnerHwnd = _ownerHwnd;
                
                // 发送 WM_COMMAND 消息到宿主窗口
                // 这模拟了 Windows 资源管理器的标准行为：必须在消息循环中调用 InvokeCommand
                IntPtr wParam = new IntPtr(selectedId);
                bool posted = PostMessageW(_hostWnd, ShellConstants.WM_COMMAND, wParam, IntPtr.Zero);
                
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37] PostMessageW 结果: {posted}, WM_COMMAND wParam={selectedId}");
                
                if (!posted)
                {
                    // 如果 PostMessage 失败，回退到直接调用
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37] ⚠️ PostMessage 失败，回退到直接调用");
                    ExecuteCommand(selectedId, pathList[0], _ownerHwnd);
                    Cleanup();
                }
                else
                {
                    // 不立即 Cleanup！等待 WM_COMMAND 处理完成
                    return;  
                }
            }

            Cleanup();
        }

        public List<Models.Shell.ShellMenuItem> GetMenuItems(IEnumerable<string> paths)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return new List<Models.Shell.ShellMenuItem>();

            if (GetContextMenu(pathList) == false) return new List<Models.Shell.ShellMenuItem>();

            _hMenu = NativeMethods.CreatePopupMenu();
            
            int hr = _contextMenuRCW.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, 
                ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU);

            try { return HMenuParser.ParseMenu(_hMenu, _contextMenuRCW); }
            finally { Cleanup(); }
        }

        public List<object> BuildWpfMenuItems(IEnumerable<string> paths, IntPtr ownerHwnd)
        {
            var pathList = paths?.ToList() ?? new List<string>();
            var shellItems = GetMenuItems(pathList);
            var wpfItems = new List<object>();

            foreach (var shellItem in shellItems)
            {
                if (shellItem.IsSeparator)
                {
                    wpfItems.Add(null);
                    continue;
                }

                wpfItems.Add(CreateWpfMenuItem(shellItem, pathList, ownerHwnd));
            }

            return wpfItems;
        }

        public void InvokeDirect(int commandId, IEnumerable<string> paths, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return;

            if (GetContextMenu(pathList) == false) return;

            var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            ExecuteCommand(commandId, pathList[0], hwnd);
            
            Cleanup();
        }

        public void CleanupResources()
        {
            if (!_disposed)
            {
                Cleanup();
            }
        }

        private void ExecuteCommand(int menuId, string filePath, IntPtr hwnd)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] ========== 开始执行命令 ==========");
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] menuId={menuId}, filePath='{filePath}'");
                
                // v39: 获取菜单项文本用于推断动词
                string menuText = "";
                if (_menuTextMap != null && _menuTextMap.TryGetValue(menuId, out menuText))
                {
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] 菜单文本: '{menuText}'");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] ⚠️ 未找到菜单ID={menuId}的文本! (map包含{_menuTextMap?.Count ?? 0}个项)");
                    if (_menuTextMap != null && _menuTextMap.Count > 0)
                    {
                        var sampleIds = _menuTextMap.Keys.Take(5).ToList();
                        System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39]   示例ID: {string.Join(", ", sampleIds)}");
                    }
                }

                if (IsRenameCommand(menuId, menuText))
                {
                    RenameRequested?.Invoke(filePath);
                    return;
                }
                
                // v39 务实方案：优先使用可靠的 ShellExecuteW
                
                // 策略 1: GetCommandString 获取到动词 → ShellExecuteW (最适合第三方扩展)
                string verb = GetCommandVerb(menuId);
                if (!string.IsNullOrEmpty(verb))
                {
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] ✅ 策略1: GetCommandString verb='{verb}' → ShellExecuteW");
                    ExecuteWithShellExecute(filePath, verb, hwnd);
                    return;
                }

                // 策略 2: 基于菜单文本推断动词 🆕
                if (!string.IsNullOrEmpty(menuText))
                {
                    verb = InferVerbFromMenuText(menuText);
                    if (!string.IsNullOrEmpty(verb))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] ✅ 策略2: 文本推断 verb='{verb}' → ShellExecuteW");
                        ExecuteWithShellExecute(filePath, verb, hwnd);
                        return;
                    }
                }

                // 策略 3: 最后尝试 InvokeCommand
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] ⚠️ 策略3: 回退到 InvokeCommand (可能不准确)");
                
                try
                {
                    var cmi = CMINVOKECOMMANDINFOEX.Create();
                    cmi.fMask = ShellConstants.CMIC_MASK_UNICODE | ShellConstants.CMIC_MASK_FLAG_NO_UI;
                    cmi.hwnd = hwnd;
                    cmi.lpVerb = (IntPtr)(unchecked((int)(uint)(menuId - 1)));
                    cmi.nShow = NativeMethods.SW_SHOWNORMAL;
                    
                    string parentDir = Path.GetDirectoryName(filePath) ?? "";
                    if (!string.IsNullOrEmpty(parentDir))
                        cmi.lpDirectoryW = Marshal.StringToCoTaskMemUni(parentDir);
                    
                    IntPtr pCmi = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX)));
                    try
                    {
                        Marshal.StructureToPtr(cmi, pCmi, false);
                        _contextMenuRCW.InvokeCommand(pCmi);
                        System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] InvokeCommand 完成 (效果不确定)");
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pCmi);
                        if (cmi.lpDirectoryW != IntPtr.Zero) Marshal.FreeCoTaskMem(cmi.lpDirectoryW);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] InvokeCommand 异常: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] ========== 命令执行结束 ==========");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu-v39] 💥 异常: {ex.Message}");
                ShowShellError($"命令执行异常：{ex.Message}");
            }
        }
        
        /// <summary>
        /// v38: 基于菜单位置偏移量推断 ShellExecuteW 动词
        /// 针对 Windows 10/11 中文版的启发式方法
        /// </summary>
        private string InferVerbFromOffset(int offset, string filePath)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v38] 推断: offset={offset}, 文件={Path.GetExtension(filePath)}");
            
            // 获取文件扩展名（小写）
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            
            // Windows 标准菜单布局（中文版，近似值）:
            // 注意：实际位置会因文件类型、安装的扩展而变化
            // 这是一个最佳努力的方法
            
            // 前 10 个位置通常是标准操作
            if (offset >= 0 && offset <= 9)
            {
                switch (offset)
                {
                    case 0: return "open";      // 打开
                    case 1: 
                        // 编辑 (通常在第2位，对文本/图片等)
                        if (IsEditable(ext)) return "edit";
                        return "open";
                    case 2: return "print";     // 打印
                    case 3: return "preview";   // 预览 (Quick Look)
                    case 4: return "openas";    // 打开方式 (会弹出对话框)
                    default: 
                        if (offset <= 6) return "open";
                        break;
                }
            }
            
            // 检测常见的特殊操作位置
            // 这些是基于观察的近似值，可能需要调整
            
            // 属性通常在最后几个位置之一
            if (offset > 30 && offset < 50) return "properties";
            
            // 删除、重命名、剪切、复制 通常在中间位置
            // 由于无法精确确定，返回空让后续策略处理
            
            return string.Empty;
        }
        
        private bool IsEditable(string ext)
        {
            // 可编辑的文件类型
            string[] editableExts = { ".txt", ".ini", ".log", ".xml", ".json", ".html", ".css",
                                       ".md", ".py", ".js", ".cs", ".java", ".cpp", ".h", ".c",
                                       ".bat", ".cmd", ".ps1", ".vbs", ".csv", ".cfg", ".conf" };
            return Array.Exists(editableExts, e => e == ext);
        }
        
        /// <summary>
        /// v34: 根据菜单文本推断 ShellExecuteW 的动词
        /// </summary>
        private string InferVerbFromMenuText(string menuText)
        {
            if (string.IsNullOrEmpty(menuText)) return string.Empty;
            
            // 移除快捷键标记 (&O, &E 等) 和省略号
            string text = menuText.Replace("&", "").Replace("...", "").Trim().ToLowerInvariant();
            
            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v35] 推断: 原始='{menuText}' → 处理后='{text}'");

            // Windows 标准上下文菜单项 → ShellExecuteW 动词映射
            if (text.Contains("打开") && !text.Contains("方式")) return "open";
            if (text.Contains("编辑")) return "edit";
            if (text.Contains("打印")) return "print";
            if (text.Contains("属性")) return "properties";
            if (text.Contains("删除")) return "delete";
            if (text.Contains("重命名")) return "rename";
            if (text.Contains("剪切")) return "cut";
            if (text.Contains("复制") && !text.Contains("路径") && !text.Contains("为")) return "copy";
            if (text.Contains("粘贴")) return "paste";
            if (text.Contains("压缩") || text.Contains("zip") || text.Contains("7z") || text.Contains("rar"))
            {
                // 压缩类操作通常由第三方扩展处理，尝试用空字符串让系统决定
                return "";
            }
            
            // 特殊处理：打开方式、发送到等子菜单（无法直接执行）
            if (text.Contains("打开方式") || text.Contains("发送到") || 
                text.Contains("新建") || text.Contains("扫描"))
            {
                return "";  // 返回空，让后续策略处理
            }
            
            return string.Empty;
        }
        
        private string InferDefaultVerb(int menuId)
        {
            // 注意：这是一个启发式方法，基于常见的 Shell 菜单布局
            // 实际的菜单项位置可能因系统配置和安装的扩展而异
            
            // 常见菜单项的典型偏移量（相对于 idCmdFirst=1）：
            // 这些值需要根据实际测试调整
            
            int offset = menuId - 1;  // 计算偏移量
            
            // Windows 10/11 的标准上下文菜单通常结构：
            // 偏移量 0 = 打开 (Open)
            // 但实际偏移量取决于前面有多少个分隔符和自定义项
            
            // 由于我们无法确定确切映射，返回空让调用者使用 "open" 作为最终回退
            return string.Empty;
        }

        private bool IsRenameCommand(int menuId, string menuText)
        {
            string verb = GetCommandVerb(menuId);
            if (string.Equals(verb, "rename", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(menuText)
                && menuText.Replace("&", string.Empty).Contains("重命名", StringComparison.OrdinalIgnoreCase);
        }

        private MenuItem CreateWpfMenuItem(ShellMenuItem shellItem, IReadOnlyCollection<string> paths, IntPtr ownerHwnd)
        {
            var menuItem = new MenuItem
            {
                Header = string.IsNullOrWhiteSpace(shellItem.Text) ? "(未命名命令)" : shellItem.Text,
                IsEnabled = shellItem.IsEnabled
            };

            if (shellItem.Children != null && shellItem.Children.Count > 0)
            {
                foreach (var child in shellItem.Children)
                {
                    if (child.IsSeparator)
                    {
                        menuItem.Items.Add(new Separator());
                    }
                    else
                    {
                        menuItem.Items.Add(CreateWpfMenuItem(child, paths, ownerHwnd));
                    }
                }
            }
            else if (shellItem.CommandId > 0)
            {
                menuItem.Click += (_, _) =>
                {
                    string primaryPath = paths.FirstOrDefault();
                    if (primaryPath == null)
                    {
                        return;
                    }

                    if (IsRenameShellItem(shellItem))
                    {
                        RenameRequested?.Invoke(primaryPath);
                        return;
                    }

                    ExecuteShellCommandFromMenu(shellItem.CommandId, primaryPath, ownerHwnd);
                };
            }

            return menuItem;
        }

        private bool IsRenameShellItem(ShellMenuItem shellItem)
        {
            if (shellItem == null)
            {
                return false;
            }

            if (string.Equals(shellItem.Verb, "rename", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(shellItem.Text)
                && shellItem.Text.Replace("&", string.Empty).Contains("重命名", StringComparison.OrdinalIgnoreCase);
        }

        private void ExecuteShellCommandFromMenu(int commandId, string filePath, IntPtr ownerHwnd)
        {
            Cleanup();

            if (!GetContextMenu(new List<string> { filePath }))
            {
                Cleanup();
                return;
            }

            ExecuteCommand(commandId, filePath, ownerHwnd);
            Cleanup();
        }

        private string GetCommandVerb(int menuId)
        {
            if (_contextMenuRCW == null) return string.Empty;

            try
            {
                // 尝试 Unicode 版本 (GCS_UNICODEW = 4)
                var sb = new StringBuilder(260);
                _contextMenuRCW.GetCommandString((uint)(menuId - 1), 4, IntPtr.Zero, sb, (uint)sb.Capacity);
                
                if (sb.Length > 0)
                {
                    string result = sb.ToString().ToLowerInvariant().Trim();
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetCommandString(Unicode): '{result}'");
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetCommandString(Unicode) EX: {ex.Message}");
            }

            try
            {
                // 尝试 ANSI 版本 (GCS_VERBA = 0)
                var sb = new StringBuilder(260);
                _contextMenuRCW.GetCommandString((uint)(menuId - 1), 0, IntPtr.Zero, sb, (uint)sb.Capacity);
                
                if (sb.Length > 0)
                {
                    string result = sb.ToString().ToLowerInvariant().Trim();
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetCommandString(Ansi): '{result}'");
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetCommandString(Ansi) EX: {ex.Message}");
            }

            return string.Empty;
        }

        private void ExecuteWithShellExecute(string filePath, string verb, IntPtr hwnd)
        {
            try
            {
                string parentDir = Path.GetDirectoryName(filePath) ?? string.Empty;
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] ShellExecuteW: verb='{verb}', file='{filePath}'");

                IntPtr result = ShellExecuteW(hwnd, verb, filePath, null, parentDir, NativeMethods.SW_SHOWNORMAL);
                long resultCode = result.ToInt64();
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] ShellExecuteW result: {resultCode}");

                if (resultCode <= 32)
                {
                    ShowShellError($"ShellExecuteW 失败 (代码={resultCode})");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] ExecuteWithShellExecute EX: {ex.GetType().Name}: {ex.Message}");
                ShowShellError($"执行异常：{ex.Message}");
            }
        }

        private static IntPtr ShellMenuWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            var host = _currentHost;
            if (host != null && !host._disposed)
            {
                switch (msg)
                {
                    case ShellConstants.WM_INITMENUPOPUP:
                    case ShellConstants.WM_MEASUREITEM:
                    case ShellConstants.WM_DRAWITEM:
                        if (host._contextMenu3RCW != null)
                        {
                            host._contextMenu3RCW.HandleMenuMsg2(msg, wParam, lParam, out _);
                            return (msg != ShellConstants.WM_INITMENUPOPUP) ? (IntPtr)1 : IntPtr.Zero;
                        }
                        if (host._contextMenu2RCW != null)
                        {
                            host._contextMenu2RCW.HandleMenuMsg(msg, wParam, lParam);
                            return (msg != ShellConstants.WM_INITMENUPOPUP) ? (IntPtr)1 : IntPtr.Zero;
                        }
                        break;

                    case ShellConstants.WM_MENUCHAR:
                        if (host._contextMenu3RCW != null)
                        {
                            host._contextMenu3RCW.HandleMenuMsg2(msg, wParam, lParam, out var result);
                            return result;
                        }
                        break;

                    // v37: 处理 WM_COMMAND - 在消息循环上下文中执行命令
                    case ShellConstants.WM_COMMAND:
                        int commandId = wParam.ToInt32();
                        System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37] WndProc 收到 WM_COMMAND: commandId={commandId}");
                        
                        if (commandId > 0 && host._pendingCommandId == commandId)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37] 执行待处理的命令:");
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37]   ID={host._pendingCommandId}");
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v37]   File='{host._pendingFilePath}'");
                            
                            // 在消息循环上下文中调用 ExecuteCommand
                            host.ExecuteCommand(host._pendingCommandId, host._pendingFilePath, host._pendingOwnerHwnd);
                            
                            // 命令执行完成后清理
                            System.Threading.Thread.Sleep(100);  // 短暂等待确保命令完成
                            host.Cleanup();
                        }
                        
                        return IntPtr.Zero;
                }
            }
            return DefWindowProcW(hWnd, msg, wParam, lParam);
        }

        private static void EnsureWindowClass()
        {
            if (_classRegistered) return;
            _wndProcDelegate = ShellMenuWndProc;
            var wc = new WNDCLASS
            {
                style = 0,
                lpfnWndProc = _wndProcDelegate,
                cbClsExtra = 0,
                cbWndExtra = 0,
                hInstance = GetModuleHandle(null),
                hIcon = IntPtr.Zero,
                hCursor = IntPtr.Zero,
                hbrBackground = IntPtr.Zero,
                lpszMenuName = null,
                lpszClassName = MENU_WND_CLASS
            };
            ushort atom = RegisterClassW(ref wc);
            _classRegistered = atom != 0;
        }

        private bool GetContextMenu(List<string> pathList)
        {
            return GetContextMenuClassic(pathList);
        }

        private bool GetContextMenuClassic(List<string> pathList)
        {
            IShellFolder desktopFolder = null;
            try
            {
                NativeMethods.SHGetDesktopFolder(out desktopFolder);
                if (desktopFolder == null) return false;

                // v36: 保存引用，防止过早释放
                _retainedDesktopFolder = desktopFolder;

                var iidCM = new Guid("000214E4-0000-0000-C000-000000000046");
                string firstPath = pathList[0];
                string parentDirPath = Path.GetDirectoryName(firstPath);

                IShellFolder parentFolder = null;
                IntPtr parentDirPidl = IntPtr.Zero;

                if (string.IsNullOrEmpty(parentDirPath))
                {
                    parentFolder = desktopFolder;
                    _retainedParentFolder = desktopFolder;
                }
                else
                {
                    parentDirPidl = NativeMethods.ILCreateFromPathW(parentDirPath);
                    if (parentDirPidl == IntPtr.Zero)
                    {
                        Marshal.ReleaseComObject(desktopFolder);
                        return false;
                    }

                    var guid = new Guid("000214E6-0000-0000-C000-000000000046");
                    IntPtr folderPtr = IntPtr.Zero;
                    try
                    {
                        desktopFolder.BindToObject(parentDirPidl, IntPtr.Zero, ref guid, out folderPtr);
                        if (folderPtr == IntPtr.Zero) return false;
                        parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                        
                        // v36: 保存父文件夹引用
                        _retainedParentFolder = parentFolder;
                        
                        Marshal.Release(folderPtr);
                    }
                    catch { return false; }
                    finally
                    {
                        NativeMethods.ILFree(parentDirPidl);
                        // v36: 不再释放 desktopFolder！保持引用直到 Cleanup()
                        // Marshal.ReleaseComObject(desktopFolder);  ← 移除这行
                    }
                }

                if (parentFolder == null) return false;

                IntPtr childPidl = IntPtr.Zero;
                try
                {
                    uint eaten = 0;
                    uint attr = 0;
                    string fileName = Path.GetFileName(firstPath);
                    parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, fileName, ref eaten, out childPidl, ref attr);

                    if (childPidl == IntPtr.Zero) return false;

                    IntPtr menuPtr = IntPtr.Zero;
                    try
                    {
                        parentFolder.GetUIObjectOf(IntPtr.Zero, 1, new[] { childPidl }, ref iidCM, IntPtr.Zero, out menuPtr);
                        if (menuPtr != IntPtr.Zero)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v36] 获取 IContextMenu，保持 COM 对象引用:");
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v36]   DesktopFolder: {_retainedDesktopFolder != null}");
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu-v36]   ParentFolder: {_retainedParentFolder != null}");
                            
                            // 创建 RCW 并保存引用
                            _contextMenuRCW = (IContextMenu)Marshal.GetObjectForIUnknown(menuPtr);
                            
                            // 同时保存原始指针供 HMenuParser 使用
                            _contextMenuPtr = menuPtr;
                            
                            // 尝试获取 IContextMenu2/3
                            var iidCM2 = new Guid("000214f4-0000-0000-c000-000000000046");
                            IntPtr cm2Ptr = IntPtr.Zero;
                            int hr = Marshal.QueryInterface(menuPtr, ref iidCM2, out cm2Ptr);
                            if (hr >= 0 && cm2Ptr != IntPtr.Zero)
                            {
                                _contextMenu2RCW = (IContextMenu2)Marshal.GetObjectForIUnknown(cm2Ptr);
                                _contextMenu3RCW = _contextMenu2RCW as IContextMenu3;
                                
                                Marshal.Release(cm2Ptr); // RCW 会保持引用
                            }

                            System.Diagnostics.Debug.WriteLine($"[ShellMenu] IContextMenu acquired (ptr=0x{menuPtr:X})");
                            return true;
                        }
                    }
                    catch { return false; }
                }
                finally
                {
                    if (childPidl != IntPtr.Zero) NativeMethods.ILFree(childPidl);
                    // v36: 不再释放 parentFolder！保持引用直到 Cleanup()
                    // if (parentFolder != desktopFolder)
                    //     Marshal.ReleaseComObject(parentFolder);  ← 移除这行
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetContextMenuClassic EX: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private static string HResultToString(int hr)
        {
            if (hr >= 0) return "S_OK/S_FALSE";
            return hr switch
            {
                unchecked((int)0x80070057) => "E_INVALIDARG",
                unchecked((int)0x80004001) => "E_NOTIMPL",
                unchecked((int)0x80004002) => "E_NOINTERFACE",
                unchecked((int)0x800401F0) => "CO_E_NOTINITIALIZED",
                unchecked((int)0x80010108) => "RPC_E_DISCONNECTED",
                unchecked((int)0x800704C7) => "ERROR_CANCELLED",
                unchecked((int)0x80070005) => "E_ACCESSDENIED",
                unchecked((int)0x80070006) => "E_HANDLE",
                unchecked((int)0x80070490) => "ERROR_NOT_FOUND",
                unchecked((int)0x8007007B) => "ERROR_INVALID_NAME",
                _ => $"Unknown(0x{hr:X8})"
            };
        }

        private static void ShowShellError(string message)
        {
            try
            {
                var app = System.Windows.Application.Current;
                if (app?.Dispatcher.CheckAccess() == true)
                {
                    System.Windows.MessageBox.Show(
                        $"Shell 命令执行失败：\n{message}\n\n请确认已安装相应的程序并重新尝试。",
                        "YiboFile",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] ERROR: {message}");
            }
        }

        private void Cleanup()
        {
            if (_hostWnd != IntPtr.Zero)
            {
                DestroyWindow(_hostWnd);
                _hostWnd = IntPtr.Zero;
            }

            if (_hMenu != IntPtr.Zero)
            {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }

            // 释放 RCW 对象
            if (_contextMenu3RCW != null)
            {
                try { Marshal.ReleaseComObject(_contextMenu3RCW); } catch { }
                _contextMenu3RCW = null;
            }
            if (_contextMenu2RCW != null && _contextMenu2RCW != _contextMenu3RCW)
            {
                try { Marshal.ReleaseComObject(_contextMenu2RCW); } catch { }
                _contextMenu2RCW = null;
            }
            if (_contextMenuRCW != null)
            {
                try { Marshal.ReleaseComObject(_contextMenuRCW); } catch { }
                _contextMenuRCW = null;
            }
            _contextMenuPtr = IntPtr.Zero;
            
            // v36: 释放保留的文件夹引用
            if (_retainedParentFolder != null && _retainedParentFolder != _retainedDesktopFolder)
            {
                try { Marshal.ReleaseComObject(_retainedParentFolder); } catch { }
                _retainedParentFolder = null;
            }
            if (_retainedDesktopFolder != null)
            {
                try { Marshal.ReleaseComObject(_retainedDesktopFolder); } catch { }
                _retainedDesktopFolder = null;
            }
            
            // 清理菜单文本映射
            _menuTextMap?.Clear();
            _menuTextMap = null;

            if (_currentHost == this)
                _currentHost = null;
        }

        public void Dispose()
        {
            if (!_disposed) { Cleanup(); _disposed = true; }
        }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        IntPtr GetParent();
        IntPtr GetDisplayName(uint sigdnName);
        uint GetAttributes(uint sfgaoMask);
        int Compare(IShellItem psi, uint hint);
    }
}
