using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace YiboFile.Interop.Shell
{
    /// <summary>
    /// 负责弹出原生 Shell 上下文菜单并处理消息转发
    /// </summary>
    public sealed class NativeShellMenuHost : IDisposable
    {
        private IContextMenu _contextMenu;
        private IContextMenu2 _contextMenu2;
        private IContextMenu3 _contextMenu3;
        private IntPtr _hMenu;
        private HwndSource _hwndSource;
        private bool _disposed;

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

            // 3. 创建并填充 HMENU
            _hMenu = NativeMethods.CreatePopupMenu();
            _contextMenu.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE);

            // 4. 设置消息钩子（转发菜单消息给 IContextMenu2/3）
            var hwnd = new WindowInteropHelper(owner).Handle;
            _hwndSource = HwndSource.FromHwnd(hwnd);
            _hwndSource.AddHook(MenuWndProc);

            // 5. 弹出菜单
            uint flags = ShellConstants.TPM_LEFTALIGN | ShellConstants.TPM_TOPALIGN | ShellConstants.TPM_RETURNCMD | ShellConstants.TPM_RIGHTBUTTON;
            int selectedId = NativeMethods.TrackPopupMenuEx(_hMenu, flags, (int)screenPoint.X, (int)screenPoint.Y, hwnd, IntPtr.Zero);

            // 6. 执行命令
            if (selectedId > 0)
            {
                InvokeCommand(selectedId, hwnd, paths);
            }

            // 7. 清理
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
                _contextMenu.QueryContextMenu(_hMenu, 0, 1, 0x7FFF, ShellConstants.CMF_NORMAL | ShellConstants.CMF_EXPLORE);

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


        private IContextMenu GetContextMenu(IEnumerable<string> paths, out IShellFolder parentFolder)
        {
            parentFolder = null;
            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return null;

            try
            {
                // 获取父文件夹的 IShellFolder
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

                // 获取子项的 PIDLs
                var childPidls = new IntPtr[pathList.Count];
                for (int i = 0; i < pathList.Count; i++)
                {
                    string name = Path.GetFileName(pathList[i]);
                    uint eaten = 0;
                    uint attr = 0;
                    parentFolder.ParseDisplayName(IntPtr.Zero, IntPtr.Zero, name, ref eaten, out childPidls[i], ref attr);
                }

                // 获取 IContextMenu
                var iid = new Guid("000214E4-0000-0000-C000-000000000046"); // IContextMenu
                parentFolder.GetUIObjectOf(IntPtr.Zero, (uint)childPidls.Length, childPidls, ref iid, IntPtr.Zero, out var menuPtr);
                
                // 释放子项 PIDLs
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

        private IntPtr MenuWndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_disposed) return IntPtr.Zero;

            switch ((uint)msg)
            {
                case ShellConstants.WM_INITMENUPOPUP:
                case ShellConstants.WM_MEASUREITEM:
                case ShellConstants.WM_DRAWITEM:
                    if (_contextMenu2 != null)
                    {
                        _contextMenu2.HandleMenuMsg((uint)msg, wParam, lParam);
                    }
                    break;

                case ShellConstants.WM_MENUCHAR:
                    if (_contextMenu3 != null)
                    {
                        _contextMenu3.HandleMenuMsg2((uint)msg, wParam, lParam, out var result);
                        return result;
                    }
                    break;
            }

            return IntPtr.Zero;
        }

        private void Cleanup()
        {
            if (_hwndSource != null)
            {
                _hwndSource.RemoveHook(MenuWndProc);
                _hwndSource = null;
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
