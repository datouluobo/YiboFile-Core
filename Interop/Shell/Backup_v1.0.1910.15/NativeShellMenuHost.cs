using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YiboFile.Interop.Shell
{
    public sealed class NativeShellMenuHost : IDisposable
    {
        private IContextMenu _contextMenu;
        private IContextMenu2 _contextMenu2;
        private IContextMenu3 _contextMenu3;
        private IntPtr _hMenu;
        private bool _disposed;

        private const string MENU_WND_CLASS = "YiboFileShellMenuMsgWnd";
        private static bool _classRegistered;
        private IntPtr _msgWnd;
        private static NativeShellMenuHost _currentHost;

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

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

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

        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return;

            // 1. Get IContextMenu using modern Shell API (SHCreateItemFromParsingName)
            if (GetContextMenuFromPaths(pathList) == false)
            {
                Cleanup();
                return;
            }

            // 2. Create menu and query items
            _hMenu = NativeMethods.CreatePopupMenu();
            uint queryFlags = ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE
                | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU;
            int qchr = _contextMenu.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, queryFlags);
            System.Diagnostics.Debug.WriteLine($"[ShellMenu] QueryContextMenu: hr=0x{qchr:X8}");

            // 3. Create message-only window
            _currentHost = this;
            EnsureWindowClass();
            var ownerHandle = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            _msgWnd = CreateWindowExW(
                0, MENU_WND_CLASS, "ShellMenuMsgWnd", 0x80000000,
                0, 0, 1, 1,
                ownerHandle, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

            // 4. TrackPopupMenuEx
            uint flags = ShellConstants.TPM_LEFTALIGN | ShellConstants.TPM_TOPALIGN
                | ShellConstants.TPM_RETURNCMD | ShellConstants.TPM_RIGHTBUTTON;
            int selectedId = NativeMethods.TrackPopupMenuEx(
                _hMenu, flags, (int)screenPoint.X, (int)screenPoint.Y, _msgWnd, IntPtr.Zero);

            System.Diagnostics.Debug.WriteLine($"[ShellMenu] TrackPopupMenuEx: selectedId={selectedId}");

            // 5. Execute command
            if (selectedId > 0)
            {
                var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
                InvokeShellCommand(selectedId, hwnd);
            }

            Cleanup();
        }

        public List<Models.Shell.ShellMenuItem> GetMenuItems(IEnumerable<string> paths)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return new List<Models.Shell.ShellMenuItem>();

            if (GetContextMenuFromPaths(pathList) == false) return new List<Models.Shell.ShellMenuItem>();

            _contextMenu2 = _contextMenu as IContextMenu2;
            _contextMenu3 = _contextMenu as IContextMenu3;
            _hMenu = NativeMethods.CreatePopupMenu();
            _contextMenu.QueryContextMenu(_hMenu, 0, 1, 0x7FFF,
                ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU);

            try { return HMenuParser.ParseMenu(_hMenu, _contextMenu); }
            finally { Cleanup(); }
        }

        public void InvokeDirect(int commandId, IEnumerable<string> paths, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            Cleanup();

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return;

            if (GetContextMenuFromPaths(pathList) == false) return;

            try
            {
                var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
                InvokeShellCommand(commandId, hwnd);
            }
            finally { Cleanup(); }
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
                        if (host._contextMenu3 != null)
                        {
                            host._contextMenu3.HandleMenuMsg2(msg, wParam, lParam, out _);
                            return (msg != ShellConstants.WM_INITMENUPOPUP) ? (IntPtr)1 : IntPtr.Zero;
                        }
                        if (host._contextMenu2 != null)
                        {
                            host._contextMenu2.HandleMenuMsg(msg, wParam, lParam);
                            return (msg != ShellConstants.WM_INITMENUPOPUP) ? (IntPtr)1 : IntPtr.Zero;
                        }
                        break;
                    case ShellConstants.WM_MENUCHAR:
                        if (host._contextMenu3 != null)
                        {
                            host._contextMenu3.HandleMenuMsg2(msg, wParam, lParam, out var result);
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
                lpfnWndProc = _wndProcDelegate,
                hInstance = GetModuleHandle(null),
                lpszClassName = MENU_WND_CLASS
            };
            RegisterClassW(ref wc);
            _classRegistered = true;
        }

        /// <summary>
        /// 使用现代 Shell API (SHCreateItemFromParsingName + IShellItem::BindToHandler) 获取 IContextMenu
        /// </summary>
        private bool GetContextMenuFromPaths(List<string> pathList)
        {
            try
            {
                var iidShellItem = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"); // IShellItem
                var iidContextMenu = new Guid("000214E4-0000-0000-C000-000000000046"); // IContextMenu
                var bhidSfUiObject = new Guid("3981E225-F559-11D3-8E3A-00C04F6837D5"); // BHID_SFUIObject

                string firstPath = pathList[0];

                // 步骤1：使用 SHCreateItemFromParsingName 获取 IShellItem
                IntPtr shellItemPtr = IntPtr.Zero;
                try
                {
                    int hr = SHCreateItemFromParsingName(firstPath, IntPtr.Zero, ref iidShellItem, out shellItemPtr);
                    System.Diagnostics.Debug.WriteLine($"[ShellMenu] SHCreateItemFromParsingName: hr=0x{hr:X8}");

                    if (hr != 0 || shellItemPtr == IntPtr.Zero)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShellMenu] SHCreateItemFromParsingName failed");
                        return GetContextMenuClassic(pathList);
                    }

                    // 步骤2：获取 IShellItem COM 对象
                    object shellItemObj = Marshal.GetObjectForIUnknown(shellItemPtr);
                    if (!(shellItemObj is IShellItem shellItem))
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShellMenu] Failed to get IShellItem");
                        return GetContextMenuClassic(pathList);
                    }

                    // 步骤3：使用 BindToHandler 获取 IContextMenu
                    IntPtr contextMenuPtr = IntPtr.Zero;
                    try
                    {
                        shellItem.BindToHandler(IntPtr.Zero, ref bhidSfUiObject, ref iidContextMenu, out contextMenuPtr);

                        if (contextMenuPtr == IntPtr.Zero)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ShellMenu] BindToHandler returned null");
                            return GetContextMenuClassic(pathList);
                        }

                        _contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(contextMenuPtr);
                        _contextMenu2 = _contextMenu as IContextMenu2;
                        _contextMenu3 = _contextMenu as IContextMenu3;

                        System.Diagnostics.Debug.WriteLine($"[ShellMenu] IContextMenu acquired successfully");
                        return _contextMenu != null;
                    }
                    finally
                    {
                        if (contextMenuPtr != IntPtr.Zero)
                            Marshal.Release(contextMenuPtr);
                    }
                }
                finally
                {
                    if (shellItemPtr != IntPtr.Zero)
                        Marshal.Release(shellItemPtr);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetContextMenuFromPaths EX: {ex.GetType().Name}: {ex.Message}");
                return GetContextMenuClassic(pathList);
            }
        }

        private bool GetContextMenuClassic(List<string> pathList)
        {
            IShellFolder desktopFolder = null;
            try
            {
                NativeMethods.SHGetDesktopFolder(out desktopFolder);
                if (desktopFolder == null) return false;

                var iidCM = new Guid("000214E4-0000-0000-C000-000000000046");
                string firstPath = pathList[0];
                string parentDirPath = Path.GetDirectoryName(firstPath);

                IShellFolder parentFolder = null;
                IntPtr parentDirPidl = IntPtr.Zero;

                if (string.IsNullOrEmpty(parentDirPath))
                {
                    parentFolder = desktopFolder;
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
                        Marshal.Release(folderPtr);
                    }
                    catch { return false; }
                    finally
                    {
                        NativeMethods.ILFree(parentDirPidl);
                        Marshal.ReleaseComObject(desktopFolder);
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
                            _contextMenu = (IContextMenu)Marshal.GetObjectForIUnknown(menuPtr);
                            _contextMenu2 = _contextMenu as IContextMenu2;
                            _contextMenu3 = _contextMenu as IContextMenu3;
                            Marshal.Release(menuPtr);
                            return true;
                        }
                    }
                    catch { return false; }
                }
                finally
                {
                    if (childPidl != IntPtr.Zero) NativeMethods.ILFree(childPidl);
                    if (parentFolder != desktopFolder)
                        Marshal.ReleaseComObject(parentFolder);
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] GetContextMenuClassic EX: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 执行 Shell 菜单命令
        /// </summary>
        private void InvokeShellCommand(int selectedId, IntPtr hwnd)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellMenu] InvokeShellCommand: id={selectedId}, hwnd=0x{hwnd:X}");

            if (selectedId < 1 || selectedId > 0x7FFF)
            {
                ShowShellError("无效的命令标识符");
                return;
            }

            if (_contextMenu == null)
            {
                ShowShellError("上下文菜单对象为空");
                return;
            }

            try
            {
                var pici = new CMINVOKECOMMANDINFO();
                pici.cbSize = (uint)Marshal.SizeOf(typeof(CMINVOKECOMMANDINFO));
                pici.fMask = 0;
                pici.hwnd = hwnd;
                pici.lpVerb = (IntPtr)(selectedId - 1);  // TrackPopupMenuEx 返回的 ID 需要减去 1
                pici.nShow = NativeMethods.SW_SHOWNORMAL;

                System.Diagnostics.Debug.WriteLine($"[ShellMenu]   cbSize={pici.cbSize}, lpVerb={selectedId - 1}, nShow={pici.nShow}");

                int hr = _contextMenu.InvokeCommand(ref pici);
                System.Diagnostics.Debug.WriteLine($"[ShellMenu]   hr=0x{hr:X8} ({HResultToString(hr)})");

                if (hr >= 0) return;

                ShowShellError($"命令执行失败 (ID={selectedId}): {HResultToString(hr)}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellMenu] InvokeCommand EX: {ex.GetType().Name}: {ex.Message}");
                ShowShellError($"命令执行异常: {ex.Message}");
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
            if (_msgWnd != IntPtr.Zero)
            {
                DestroyWindow(_msgWnd);
                _msgWnd = IntPtr.Zero;
            }

            if (_hMenu != IntPtr.Zero)
            {
                NativeMethods.DestroyMenu(_hMenu);
                _hMenu = IntPtr.Zero;
            }

            if (_contextMenu != null)
            {
                try { Marshal.ReleaseComObject(_contextMenu); } catch { }
                _contextMenu = null;
                _contextMenu2 = null;
                _contextMenu3 = null;
            }

            if (_currentHost == this)
                _currentHost = null;
        }

        public void Dispose()
        {
            if (!_disposed) { Cleanup(); _disposed = true; }
        }

        // ── P/Invoke ──
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            ref Guid riid,
            out IntPtr ppv);
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
