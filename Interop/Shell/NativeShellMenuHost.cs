using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinForms = System.Windows.Forms;

namespace YiboFile.Interop.Shell
{
    public sealed class NativeShellMenuHost : IDisposable
    {
        private IShellFolder _desktopFolder;
        private IShellFolder _parentFolder;
        private IContextMenu _cm1;
        private IContextMenu2 _cm2;
        private IContextMenu3 _cm3;
        private IntPtr _hMenu;
        private MenuHostForm _hostForm;
        private Dictionary<int, string> _menuTextMap;
        private Dictionary<int, string> _menuVerbMap;
        private List<string> _paths;
        private uint _idCmdFirst = 1;
        private bool _disposed;

        public event Action<string> RenameRequested;

        private static bool HrSucceeded(int hr) => hr >= 0;

        /// <summary>
        /// Show a native Win32 popup menu at the specified screen position (modal, blocking).
        /// </summary>
        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NativeShellMenuHost));
            if (paths == null) return;

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return;

            IntPtr ownerHwnd = owner != null ? new WindowInteropHelper(owner).Handle : IntPtr.Zero;

            Cleanup();
            _paths = pathList;

            try
            {
                if (!GetContextMenu(pathList, ownerHwnd))
                    return;

                _hMenu = NativeMethods.CreatePopupMenu();
                if (_hMenu == IntPtr.Zero)
                    return;

                uint flags = ShellConstants.CMF_EXPLORE | ShellConstants.CMF_CANRENAME;
                int hr = _cm1.QueryContextMenu(_hMenu, 0, _idCmdFirst, 0x7FFF, flags);
                if (hr < 0)
                    return;

                int itemCount = NativeMethods.GetMenuItemCount(_hMenu);
                BuildMenuMaps(itemCount);

                _hostForm = new MenuHostForm(_cm2, _cm3);
                _ = _hostForm.Handle;

                NativeMethods.SetForegroundWindow(_hostForm.Handle);

                uint tpmFlags = ShellConstants.TPM_LEFTALIGN | ShellConstants.TPM_TOPALIGN
                    | ShellConstants.TPM_RIGHTBUTTON | ShellConstants.TPM_RETURNCMD;

                int selectedId = NativeMethods.TrackPopupMenuEx(
                    _hMenu, tpmFlags, (int)screenPoint.X, (int)screenPoint.Y,
                    _hostForm.Handle, IntPtr.Zero);

                if (selectedId > 0)
                {
                    ExecuteWithFallback(selectedId, _paths[0], ownerHwnd);
                }

                Cleanup();
            }
            catch (Exception)
            {
                Cleanup();
            }
        }

        /// <summary>
        /// Build WPF MenuItem list from the native shell context menu for the given paths.
        /// The caller must call <see cref="Dispose"/> after the menu is closed and commands are executed.
        /// COM objects are kept alive until disposal.
        /// </summary>
        public List<MenuItem> BuildWpfMenuItems(IEnumerable<string> paths, IntPtr ownerHwnd)
        {
            var result = new List<MenuItem>();
            if (_disposed || paths == null) return result;

            var pathList = new List<string>(paths);
            if (pathList.Count == 0) return result;

            Cleanup();
            _paths = pathList;

            try
            {
                if (!GetContextMenu(pathList, ownerHwnd))
                    return result;

                _hMenu = NativeMethods.CreatePopupMenu();
                if (_hMenu == IntPtr.Zero)
                    return result;

                uint flags = ShellConstants.CMF_EXPLORE | ShellConstants.CMF_CANRENAME;
                int hr = _cm1.QueryContextMenu(_hMenu, 0, _idCmdFirst, 0x7FFF, flags);
                if (hr < 0)
                    return result;

                int itemCount = NativeMethods.GetMenuItemCount(_hMenu);
                BuildMenuMaps(itemCount);

                for (int i = 0; i < itemCount; i++)
                {
                    var mii = MENUITEMINFO.Create();
                    mii.fMask = ShellConstants.MIIM_FTYPE | ShellConstants.MIIM_STATE | ShellConstants.MIIM_ID | ShellConstants.MIIM_SUBMENU;
                    if (!NativeMethods.GetMenuItemInfoW(_hMenu, (uint)i, true, ref mii))
                        continue;

                    // Separator
                    if ((mii.fType & ShellConstants.MFT_SEPARATOR) != 0)
                    {
                        var sep = new Separator();
                        result.Add(null!); // placeholder for separator — handled by caller
                        continue;
                    }

                    // Skip owner-draw items we can't represent in WPF (they still execute fine via InvokeCommand)
                    int menuId = (int)mii.wID;
                    string text = _menuTextMap?.TryGetValue(menuId, out var t) == true ? t : "";
                    string verb = _menuVerbMap?.TryGetValue(menuId, out var v) == true ? v : "";

                    // Submenu
                    if (mii.hSubMenu != IntPtr.Zero)
                    {
                        var subParent = new MenuItem { Header = CleanMenuText(text) };
                        BuildWpfSubMenu(subParent, mii.hSubMenu, ownerHwnd);
                        result.Add(subParent);
                        continue;
                    }

                    // Normal item
                    var item = new MenuItem { Header = CleanMenuText(text) };

                    if ((mii.fState & ShellConstants.MFS_DISABLED) != 0)
                        item.IsEnabled = false;

                    if ((mii.fState & ShellConstants.MFS_CHECKED) != 0)
                    {
                        item.IsCheckable = true;
                        item.IsChecked = true;
                    }

                    // Capture variables for closure
                    int capturedId = menuId;
                    string capturedVerb = verb;
                    item.Click += (s, e) =>
                    {
                        OnWpfMenuItemClick(capturedId, capturedVerb, ownerHwnd);
                    };

                    result.Add(item);
                }
            }
            catch (Exception)
            {
                Cleanup();
            }

            return result;
        }

        private void BuildWpfSubMenu(MenuItem parent, IntPtr hSubMenu, IntPtr ownerHwnd)
        {
            int subCount = NativeMethods.GetMenuItemCount(hSubMenu);
            for (int i = 0; i < subCount; i++)
            {
                var mii = MENUITEMINFO.Create();
                mii.fMask = ShellConstants.MIIM_FTYPE | ShellConstants.MIIM_STATE | ShellConstants.MIIM_ID | ShellConstants.MIIM_SUBMENU;
                if (!NativeMethods.GetMenuItemInfoW(hSubMenu, (uint)i, true, ref mii))
                    continue;

                if ((mii.fType & ShellConstants.MFT_SEPARATOR) != 0)
                {
                    parent.Items.Add(new Separator());
                    continue;
                }

                int menuId = (int)mii.wID;

                // Get text for submenu item
                string text = "";
                var sb = new StringBuilder(512);
                int len = NativeMethods.GetMenuStringW(hSubMenu, (uint)menuId, sb, sb.Capacity, 0);
                if (len > 0) text = sb.ToString();

                string verb = "";
                int offset = menuId - (int)_idCmdFirst;
                if (offset >= 0)
                {
                    verb = GetVerbForOffset((uint)offset) ?? "";
                    if (!_menuVerbMap.ContainsKey(menuId) && !string.IsNullOrEmpty(verb))
                        _menuVerbMap[menuId] = verb;
                    if (!_menuTextMap.ContainsKey(menuId))
                        _menuTextMap[menuId] = text;
                }

                if (mii.hSubMenu != IntPtr.Zero)
                {
                    var subSub = new MenuItem { Header = CleanMenuText(text) };
                    BuildWpfSubMenu(subSub, mii.hSubMenu, ownerHwnd);
                    parent.Items.Add(subSub);
                    continue;
                }

                var item = new MenuItem { Header = CleanMenuText(text) };

                if ((mii.fState & ShellConstants.MFS_DISABLED) != 0)
                    item.IsEnabled = false;

                if ((mii.fState & ShellConstants.MFS_CHECKED) != 0)
                {
                    item.IsCheckable = true;
                    item.IsChecked = true;
                }

                int capturedId = menuId;
                string capturedVerb = verb;
                item.Click += (s, e) =>
                {
                    OnWpfMenuItemClick(capturedId, capturedVerb, ownerHwnd);
                };

                parent.Items.Add(item);
            }
        }

        private void OnWpfMenuItemClick(int menuId, string verb, IntPtr ownerHwnd)
        {
            if (_paths == null || _paths.Count == 0) return;

            string filePath = _paths[0];

            if (string.Equals(verb, "rename", StringComparison.OrdinalIgnoreCase))
            {
                RenameRequested?.Invoke(filePath);
                return;
            }

            ExecuteWithFallback(menuId, filePath, ownerHwnd);
        }

        /// <summary>
        /// Remove accelerator markers (&amp;) and trailing accelerator hints from menu text.
        /// </summary>
        private static string CleanMenuText(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            // Remove & accelerator markers (but keep && as literal &)
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '&' && i + 1 < text.Length && text[i + 1] != '&')
                    continue;
                sb.Append(text[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clean up COM objects and native resources. Call this after the WPF menu is closed.
        /// </summary>
        public void CleanupResources()
        {
            Cleanup();
        }

        private void BuildMenuMaps(int itemCount)
        {
            _menuTextMap = new Dictionary<int, string>();
            _menuVerbMap = new Dictionary<int, string>();

            for (int i = 0; i < itemCount; i++)
            {
                try
                {
                    var mii = MENUITEMINFO.Create();
                    mii.fMask = ShellConstants.MIIM_ID;
                    if (!NativeMethods.GetMenuItemInfoW(_hMenu, (uint)i, true, ref mii)) continue;

                    int menuId = (int)mii.wID;
                    if (menuId <= 0) continue;

                    var sb = new StringBuilder(512);
                    int len = NativeMethods.GetMenuStringW(_hMenu, (uint)menuId, sb, sb.Capacity, 0);
                    if (len > 0)
                        _menuTextMap[menuId] = sb.ToString();

                    int offset = menuId - (int)_idCmdFirst;
                    string verb = GetVerbForOffset((uint)offset);
                    if (verb != null)
                        _menuVerbMap[menuId] = verb;
                }
                catch { }
            }
        }

        private string GetVerbForOffset(uint offset)
        {
            if (_cm1 == null) return null;
            try
            {
                var sb = new StringBuilder(256);
                int hr = _cm1.GetCommandString(offset, ShellConstants.GCS_VERBW, IntPtr.Zero, sb, 256);
                if (hr >= 0 && sb.Length > 0) return sb.ToString();

                sb.Clear();
                hr = _cm1.GetCommandString(offset, ShellConstants.GCS_VERBA, IntPtr.Zero, sb, 256);
                if (hr >= 0 && sb.Length > 0) return sb.ToString();
            }
            catch { }
            return null;
        }

        private bool ExecuteWithFallback(int menuId, string filePath, IntPtr hwnd)
        {
            if (_cm1 == null) return false;

            int offset = menuId - (int)_idCmdFirst;
            if (offset < 0) return false;

            string verb = _menuVerbMap?.TryGetValue(menuId, out var v) == true ? v : null;

            if (string.Equals(verb, "rename", StringComparison.OrdinalIgnoreCase))
            {
                RenameRequested?.Invoke(filePath);
                return true;
            }

            int hr = InvokeCommandEx(offset, null, hwnd);
            if (HrSucceeded(hr))
            {
                PumpMessages();
                return true;
            }

            if (!string.IsNullOrEmpty(verb))
            {
                hr = InvokeCommandEx(-1, verb, hwnd);
                if (HrSucceeded(hr))
                {
                    PumpMessages();
                    return true;
                }
            }

            if (!string.IsNullOrEmpty(verb))
            {
                if (TryShellExecute(verb, filePath))
                    return true;
            }

            return false;
        }

        private int InvokeCommandEx(int verbOffset, string verbName, IntPtr hwnd)
        {
            try
            {
                if (verbName == null)
                {
                    int cbBasic = Marshal.SizeOf<CMINVOKECOMMANDINFO>();
                    IntPtr pMem = Marshal.AllocHGlobal(cbBasic);
                    try
                    {
                        var basic = new CMINVOKECOMMANDINFO();
                        basic.cbSize = (uint)cbBasic;
                        basic.fMask = 0;
                        basic.hwnd = hwnd;
                        basic.lpVerb = (IntPtr)verbOffset;
                        basic.nShow = NativeMethods.SW_SHOWNORMAL;

                        Marshal.StructureToPtr(basic, pMem, false);
                        return _cm1.InvokeCommand(pMem);
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pMem);
                    }
                }
                else
                {
                    int cbEx = Marshal.SizeOf<CMINVOKECOMMANDINFOEX>();
                    IntPtr pMem = Marshal.AllocHGlobal(cbEx);
                    try
                    {
                        var ex = new CMINVOKECOMMANDINFOEX();
                        ex.cbSize = (uint)cbEx;
                        ex.fMask = ShellConstants.CMIC_MASK_UNICODE;
                        ex.hwnd = hwnd;
                        ex.lpVerb = Marshal.StringToHGlobalAnsi(verbName);
                        ex.lpVerbW = Marshal.StringToHGlobalUni(verbName);
                        ex.nShow = NativeMethods.SW_SHOWNORMAL;

                        Marshal.StructureToPtr(ex, pMem, false);
                        int hr = _cm1.InvokeCommand(pMem);

                        Marshal.FreeHGlobal(ex.lpVerb);
                        Marshal.FreeHGlobal(ex.lpVerbW);
                        return hr;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(pMem);
                    }
                }
            }
            catch
            {
                return unchecked((int)0x80004005);
            }
        }

        private bool TryShellExecute(string verb, string filePath)
        {
            try
            {
                var sei = SHELLEXECUTEINFO.Create();
                sei.fMask = NativeMethods.SEE_MASK_INVOKEIDLIST | NativeMethods.SEE_MASK_FLAG_NO_UI;
                sei.lpVerb = verb;
                sei.lpFile = filePath;
                sei.nShow = NativeMethods.SW_SHOWNORMAL;
                return NativeMethods.ShellExecuteExW(ref sei);
            }
            catch
            {
                return false;
            }
        }

        private static void PumpMessages()
        {
            try
            {
                for (int i = 0; i < 20; i++)
                    WinForms.Application.DoEvents();
            }
            catch { }
        }

        private bool GetContextMenu(List<string> pathList, IntPtr ownerHwnd)
        {
            string firstPath = pathList[0];
            string parentDir = Path.GetDirectoryName(firstPath);
            string fileName = Path.GetFileName(firstPath);

            int hr = NativeMethods.SHGetDesktopFolder(out _desktopFolder);
            if (hr < 0 || _desktopFolder == null)
                return false;

            if (string.IsNullOrEmpty(parentDir))
            {
                _parentFolder = _desktopFolder;
            }
            else
            {
                IntPtr parentPidl = NativeMethods.ILCreateFromPathW(parentDir);
                if (parentPidl == IntPtr.Zero)
                    return false;

                try
                {
                    var iidSF = typeof(IShellFolder).GUID;
                    hr = _desktopFolder.BindToObject(parentPidl, IntPtr.Zero, ref iidSF, out var folderPtr);
                    if (hr < 0 || folderPtr == IntPtr.Zero)
                        return false;

                    _parentFolder = (IShellFolder)Marshal.GetObjectForIUnknown(folderPtr);
                    Marshal.Release(folderPtr);
                }
                finally
                {
                    NativeMethods.ILFree(parentPidl);
                }
            }

            uint eaten = 0, attr = 0;
            hr = _parentFolder.ParseDisplayName(ownerHwnd, IntPtr.Zero, fileName, ref eaten, out var childPidl, ref attr);
            if (hr < 0 || childPidl == IntPtr.Zero)
                return false;

            try
            {
                var iidCM = typeof(IContextMenu).GUID;
                IntPtr[] apidl = { childPidl };

                if (pathList.Count > 1)
                    apidl = BuildChildPidls(pathList, ownerHwnd);

                hr = _parentFolder.GetUIObjectOf(ownerHwnd, (uint)apidl.Length, apidl, ref iidCM, IntPtr.Zero, out var menuPtr);
                if (hr < 0 || menuPtr == IntPtr.Zero)
                    return false;

                _cm1 = (IContextMenu)Marshal.GetObjectForIUnknown(menuPtr);

                var iidCM2 = typeof(IContextMenu2).GUID;
                hr = Marshal.QueryInterface(menuPtr, ref iidCM2, out var cm2Ptr);
                if (hr >= 0 && cm2Ptr != IntPtr.Zero)
                {
                    _cm2 = (IContextMenu2)Marshal.GetObjectForIUnknown(cm2Ptr);
                    Marshal.Release(cm2Ptr);
                }

                var iidCM3 = typeof(IContextMenu3).GUID;
                hr = Marshal.QueryInterface(menuPtr, ref iidCM3, out var cm3Ptr);
                if (hr >= 0 && cm3Ptr != IntPtr.Zero)
                {
                    _cm3 = (IContextMenu3)Marshal.GetObjectForIUnknown(cm3Ptr);
                    Marshal.Release(cm3Ptr);
                }

                Marshal.Release(menuPtr);
                return _cm1 != null;
            }
            finally
            {
                NativeMethods.ILFree(childPidl);
            }
        }

        private IntPtr[] BuildChildPidls(List<string> paths, IntPtr ownerHwnd)
        {
            var pidls = new List<IntPtr>();
            foreach (var path in paths)
            {
                string fileName = Path.GetFileName(path);
                uint eaten = 0, attr = 0;
                int hr = _parentFolder.ParseDisplayName(ownerHwnd, IntPtr.Zero, fileName, ref eaten, out var pidl, ref attr);
                if (hr >= 0 && pidl != IntPtr.Zero)
                    pidls.Add(pidl);
            }
            return pidls.Count > 0 ? pidls.ToArray() : new[] { IntPtr.Zero };
        }

        private void Cleanup()
        {
            if (_hostForm != null)
            {
                try { _hostForm.Close(); _hostForm.Dispose(); } catch { }
                _hostForm = null;
            }

            if (_hMenu != IntPtr.Zero)
            {
                try { NativeMethods.DestroyMenu(_hMenu); } catch { }
                _hMenu = IntPtr.Zero;
            }

            _menuTextMap = null;
            _menuVerbMap = null;

            if (_cm3 != null) { try { Marshal.ReleaseComObject(_cm3); } catch { } _cm3 = null; }
            if (_cm2 != null) { try { Marshal.ReleaseComObject(_cm2); } catch { } _cm2 = null; }
            if (_cm1 != null) { try { Marshal.ReleaseComObject(_cm1); } catch { } _cm1 = null; }

            if (_parentFolder != null && _parentFolder != _desktopFolder)
            {
                try { Marshal.ReleaseComObject(_parentFolder); } catch { }
            }
            _parentFolder = null;

            if (_desktopFolder != null)
            {
                try { Marshal.ReleaseComObject(_desktopFolder); } catch { }
                _desktopFolder = null;
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

        // ── WinForms host for native menu message handling ──
        private class MenuHostForm : WinForms.Form
        {
            private readonly IContextMenu2 _cm2;
            private readonly IContextMenu3 _cm3;

            public MenuHostForm(IContextMenu2 cm2, IContextMenu3 cm3)
            {
                _cm2 = cm2;
                _cm3 = cm3;

                Opacity = 0;
                ShowInTaskbar = false;
                FormBorderStyle = WinForms.FormBorderStyle.None;
                Size = new System.Drawing.Size(0, 0);
            }

            protected override void WndProc(ref WinForms.Message m)
            {
                switch (m.Msg)
                {
                    case (int)ShellConstants.WM_INITMENUPOPUP:
                    case (int)ShellConstants.WM_MEASUREITEM:
                    case (int)ShellConstants.WM_DRAWITEM:
                        if (_cm3 != null)
                        {
                            _cm3.HandleMenuMsg2((uint)m.Msg, m.WParam, m.LParam, out _);
                            if (m.Msg != ShellConstants.WM_INITMENUPOPUP) { m.Result = (IntPtr)1; return; }
                            return;
                        }
                        if (_cm2 != null)
                        {
                            _cm2.HandleMenuMsg((uint)m.Msg, m.WParam, m.LParam);
                            if (m.Msg != ShellConstants.WM_INITMENUPOPUP) { m.Result = (IntPtr)1; return; }
                            return;
                        }
                        break;

                    case (int)ShellConstants.WM_MENUCHAR:
                        if (_cm3 != null)
                        {
                            _cm3.HandleMenuMsg2((uint)m.Msg, m.WParam, m.LParam, out var result);
                            m.Result = result;
                            return;
                        }
                        break;
                }
                base.WndProc(ref m);
            }
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
