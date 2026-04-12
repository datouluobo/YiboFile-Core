using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YiboFile.Interop.Shell
{
    /// <summary>
    /// 负责弹出原生 Shell 上下文菜单并处理消息转发。
    /// 使用专用的隐藏 Win32 窗口来正确接收所有 owner-draw 消息，确保图标渲染。
    /// </summary>
    public sealed class NativeShellMenuHost : IDisposable
    {
        private IContextMenu _contextMenu;
        private IContextMenu2 _contextMenu2;
        private IContextMenu3 _contextMenu3;
        private IntPtr _hMenu;
        private bool _disposed;

        // ── Win32 消息窗口相关 ──
        private const string MENU_WND_CLASS = "YiboFileShellMenuMsgWnd";
        private static bool _classRegistered;
        private IntPtr _msgWnd;
        // 静态引用，因为 WndProc 委托必须在回调期间保持存活
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

        // HWND_MESSAGE parent for message-only window
        private static readonly IntPtr HWND_MESSAGE = new IntPtr(-3);

        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));

            // 1. 清理旧资源
            Cleanup();

            // 2. 获取 IContextMenu
            _contextMenu = GetContextMenu(paths, out var parentFolder);
            if (_contextMenu == null) return;

            _contextMenu2 = _contextMenu as IContextMenu2;
            _contextMenu3 = _contextMenu as IContextMenu3;

            _hMenu = NativeMethods.CreatePopupMenu();
            uint queryFlags = ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE
                | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU;
            _contextMenu.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, queryFlags);

            // 3. 创建专用消息窗口（而非挂钩到 WPF 窗口）
            // 使用常规隐藏窗口而非 HWND_MESSAGE 以确保某些扩展能正确获取父窗口状态
            _currentHost = this;
            EnsureWindowClass();
            var ownerHandle = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
            _msgWnd = CreateWindowExW(
                0, MENU_WND_CLASS, "ShellMenuMsgWnd", 0x80000000 /* WS_POPUP */,
                0, 0, 1, 1,
                ownerHandle, IntPtr.Zero, GetModuleHandle(null), IntPtr.Zero);

            // 4. 弹出菜单 —— 使用专用消息窗口的 HWND
            uint flags = ShellConstants.TPM_LEFTALIGN | ShellConstants.TPM_TOPALIGN
                | ShellConstants.TPM_RETURNCMD | ShellConstants.TPM_RIGHTBUTTON;
            int selectedId = NativeMethods.TrackPopupMenuEx(
                _hMenu, flags, (int)screenPoint.X, (int)screenPoint.Y, _msgWnd, IntPtr.Zero);

            // 5. 执行命令
            if (selectedId > 0)
            {
                var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
                InvokeCommand(selectedId, hwnd, paths);
            }

            // 6. 清理
            Cleanup();
            if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
        }

        public List<Models.Shell.ShellMenuItem> GetMenuItems(IEnumerable<string> paths)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));

            Cleanup();
            _contextMenu = GetContextMenu(paths, out var parentFolder);
            try
            {
                if (_contextMenu == null) return new List<Models.Shell.ShellMenuItem>();

                _contextMenu2 = _contextMenu as IContextMenu2;
                _contextMenu3 = _contextMenu as IContextMenu3;

                _hMenu = NativeMethods.CreatePopupMenu();
                _contextMenu.QueryContextMenu(_hMenu, 0, 1, 0x7FFF,
                    ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE | ShellConstants.CMF_CANRENAME | ShellConstants.CMF_ITEMMENU);

                var items = HMenuParser.ParseMenu(_hMenu, _contextMenu);
                return items;
            }
            finally
            {
                if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
            }
        }

        public void InvokeDirect(int commandId, IEnumerable<string> paths, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));

            Cleanup();
            _contextMenu = GetContextMenu(paths, out var parentFolder);
            try
            {
                if (_contextMenu == null) return;

                var hwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;
                InvokeCommand(commandId, hwnd, paths);
            }
            finally
            {
                if (parentFolder != null) Marshal.ReleaseComObject(parentFolder);
                Cleanup();
            }
        }

        // ════════════════════════════════════════
        // 专用消息窗口的 WndProc
        // ════════════════════════════════════════
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
                        // 优先使用 IContextMenu3（支持 out result），否则回退到 IContextMenu2
                        if (host._contextMenu3 != null)
                        {
                            host._contextMenu3.HandleMenuMsg2(msg, wParam, lParam, out _);
                            // WM_MEASUREITEM / WM_DRAWITEM 必须返回 TRUE 表示已处理
                            if (msg != ShellConstants.WM_INITMENUPOPUP)
                                return (IntPtr)1;
                            return IntPtr.Zero;
                        }
                        if (host._contextMenu2 != null)
                        {
                            host._contextMenu2.HandleMenuMsg(msg, wParam, lParam);
                            if (msg != ShellConstants.WM_INITMENUPOPUP)
                                return (IntPtr)1;
                            return IntPtr.Zero;
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

        // ════════════════════════════════════════
        // IContextMenu 获取
        // ════════════════════════════════════════
        private IContextMenu GetContextMenu(IEnumerable<string> paths, out IShellFolder parentFolder)
        {
            parentFolder = null;
            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return null;

            try
            {
                string firstPath = pathList[0];
                string parentDirPath = Path.GetDirectoryName(firstPath);
                
                if (string.IsNullOrEmpty(parentDirPath))
                {
                    NativeMethods.SHGetDesktopFolder(out parentFolder);
                }
                else
                {
                    NativeMethods.SHGetDesktopFolder(out var desktopFolder);
                    IntPtr parentPidl = NativeMethods.ILCreateFromPathW(parentDirPath);
                    var guid = new Guid("000214E6-0000-0000-C000-000000000046"); // IShellFolder
                    desktopFolder.BindToObject(parentPidl, IntPtr.Zero, ref guid, out var folderPtr);
                    parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                    NativeMethods.ILFree(parentPidl);
                    Marshal.ReleaseComObject(desktopFolder);
                }

                var childPidls = new IntPtr[pathList.Count];
                for (int i = 0; i < pathList.Count; i++)
                {
                    string name = Path.GetFileName(pathList[i]);
                    uint eaten = 0;
                    uint attr = 0;
                    parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, name, ref eaten, out childPidls[i], ref attr);
                }

                var iid = new Guid("000214E4-0000-0000-C000-000000000046"); // IContextMenu
                parentFolder.GetUIObjectOf(IntPtr.Zero, (uint)childPidls.Length, childPidls, ref iid, IntPtr.Zero, out var menuPtr);
                
                foreach (var pidl in childPidls) NativeMethods.ILFree(pidl);

                return (IContextMenu)Marshal.GetUniqueObjectForIUnknown(menuPtr);
            }
            catch
            {
                return null;
            }
        }

        private void InvokeCommand(int selectedId, IntPtr hwnd, IEnumerable<string> paths)
        {
            var pici = CMINVOKECOMMANDINFOEX.Create();
            pici.hwnd = hwnd;
            pici.lpVerb = (IntPtr)(selectedId - 1);
            pici.nShow = 1; // SW_SHOWNORMAL
            pici.fMask = ShellConstants.CMIC_MASK_UNICODE | ShellConstants.CMIC_MASK_ASYNCOK;
            
            _contextMenu.InvokeCommand(ref pici);
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
                Marshal.ReleaseComObject(_contextMenu);
                _contextMenu = null;
                _contextMenu2 = null;
                _contextMenu3 = null;
            }

            if (_currentHost == this)
                _currentHost = null;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Cleanup();
                _disposed = true;
            }
        }
    }
}
