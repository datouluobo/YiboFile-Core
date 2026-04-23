using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace YiboFile.Interop.Shell
{
    public sealed class NativeShellMenuHost : IDisposable
    {
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
        
        // v34: 菜单项 ID → 文本 映射（用于推断动词 / ShellExecute 回退）
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetMenuStringW(IntPtr hMenu, uint uIDItem, [Out] StringBuilder lpString, int nMaxCount, uint uFlag);

        private static bool HrSucceeded(int hr) => (uint)hr < 0x80000000;

        /// <summary>让部分通过 PostMessage/异步分发的壳扩展在返回前有机会完成。</summary>
        private static void PumpThreadMessages()
        {
            try
            {
                for (int i = 0; i < 24; i++)
                    System.Windows.Forms.Application.DoEvents();
            }
            catch
            {
                // ignore
            }
        }
        
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

            // 保存 owner 窗口句柄供 InvokeCommand 使用
            IntPtr _ownerHwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;

            if (GetContextMenu(pathList, _ownerHwnd) == false)
            {
                Cleanup();
                return;
            }

            _hMenu = NativeMethods.CreatePopupMenu();
            uint queryFlags = ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE
                | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU;
            
            // 使用 RCW 对象调用 QueryContextMenu（这是安全的）
            int hr = _contextMenuRCW.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, queryFlags);

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
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore menu item parsing errors
                }
            }
            

            uint flags = ShellConstants.TPM_LEFTALIGN | ShellConstants.TPM_TOPALIGN
                | ShellConstants.TPM_RIGHTBUTTON | ShellConstants.TPM_RETURNCMD;
            
            int selectedId = NativeMethods.TrackPopupMenuEx(
                _hMenu,
                flags,
                (int)screenPoint.X,
                (int)screenPoint.Y,
                _hostWnd,
                IntPtr.Zero);

            // 必须在 TrackPopupMenuEx 返回后、同一调用栈中同步执行 InvokeCommand。
            // 依赖 PostMessage(WM_COMMAND) 在 WPF 下经常无法在本轮消息泵中分发给壳窗口，导致“菜单有，点了没反应”。
            if (selectedId > 0)
            {
                ExecuteCommand(selectedId, pathList[0], _ownerHwnd);
            }

            Cleanup();
        }


        public void InvokeDirect(int commandId, IEnumerable<string> paths, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return;

            var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            if (GetContextMenu(pathList, hwnd) == false) return;

            ExecuteCommand(commandId, pathList[0], hwnd);
            
            Cleanup();
        }

        private void ExecuteCommand(int menuId, string filePath, IntPtr hwnd)
        {
            if (_contextMenuRCW == null) return;

            int offset = menuId - 1; // 与 QueryContextMenu 的 idCmdFirst=1 一致
            if (offset < 0) return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] ExecuteCommand: menuId={menuId}, offset={offset}");

                // 与备份版本完全一致的方式：ref CMINVOKECOMMANDINFO + 偏移量
                // CLR 自动 marshaling，不需要手动 AllocHGlobal/StructureToPtr
                var pici = new CMINVOKECOMMANDINFO();
                pici.cbSize = (uint)Marshal.SizeOf(typeof(CMINVOKECOMMANDINFO));
                pici.fMask = 0;
                pici.hwnd = hwnd;
                pici.lpVerb = (IntPtr)offset;
                pici.nShow = NativeMethods.SW_SHOWNORMAL;

                System.Diagnostics.Debug.WriteLine($"[ShellMenu]   cbSize={pici.cbSize}, lpVerb={offset}, nShow={pici.nShow}");

                int hr = _contextMenuRCW.InvokeCommand(ref pici);
                System.Diagnostics.Debug.WriteLine($"[ShellMenu]   hr=0x{hr:X8} ({HResultToString(hr)})");

                if (hr >= 0)
                {
                    PumpThreadMessages();
                    return;
                }

                ShowShellError($"命令执行失败 (ID={menuId}): {HResultToString(hr)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] InvokeCommand EX: {ex.GetType().Name}: {ex.Message}");
                ShowShellError($"命令执行异常: {ex.Message}");
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

        private bool GetContextMenu(List<string> pathList, IntPtr ownerHwnd)
        {
            return GetContextMenuClassic(pathList, ownerHwnd);
        }

        private bool GetContextMenuClassic(List<string> pathList, IntPtr ownerHwnd)
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
                    parentFolder.ParseDisplayName(ownerHwnd, IntPtr.Zero, fileName, ref eaten, out childPidl, ref attr);

                    if (childPidl == IntPtr.Zero) return false;

                    IntPtr menuPtr = IntPtr.Zero;
                    try
                    {
                        parentFolder.GetUIObjectOf(ownerHwnd, 1, new[] { childPidl }, ref iidCM, IntPtr.Zero, out menuPtr);
                        if (menuPtr != IntPtr.Zero)
                        {
                            
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
            catch (Exception)
            {
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
