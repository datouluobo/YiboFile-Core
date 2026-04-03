using System;
using System.Runtime.InteropServices;

namespace YiboFile.Interop.Shell
{
    public static class ShellConstants
    {
        // ── IContextMenu.QueryContextMenu 标志 ──
        public const uint CMF_NORMAL = 0x00000000;
        public const uint CMF_DEFAULTONLY = 0x00000001;
        public const uint CMF_VERBSONLY = 0x00000002;
        public const uint CMF_EXPLORE = 0x00000004;
        public const uint CMF_NOVERBS = 0x00000008;
        public const uint CMF_CANRENAME = 0x00000010;
        public const uint CMF_NODEFAULT = 0x00000020;
        public const uint CMF_INCLUDESTATIC = 0x00000040;
        public const uint CMF_ITEMMENU = 0x00000080;
        public const uint CMF_EXTENDEDVERBS = 0x00000100;
        public const uint CMF_RESERVED = 0xffff0000;

        // ── GCS (GetCommandString) 标志 ──
        public const uint GCS_VERBA = 0x00000000;     // Canonical verb (ANSI)
        public const uint GCS_HELPTEXTA = 0x00000001; // Help text (ANSI)
        public const uint GCS_VALIDATEA = 0x00000002; // Validate verb (ANSI)
        public const uint GCS_VERBW = 0x00000004;     // Canonical verb (Unicode)
        public const uint GCS_HELPTEXTW = 0x00000005; // Help text (Unicode)
        public const uint GCS_VALIDATEW = 0x00000006; // Validate verb (Unicode)
        public const uint GCS_UNICODE = 0x00000004;   // Unicode flag

        // ── CMINVOKECOMMANDINFO.fMask 标志 ──
        public const uint CMIC_MASK_HOTKEY = 0x00000020;
        public const uint CMIC_MASK_ICON = 0x00000010;
        public const uint CMIC_MASK_FLAG_NO_UI = 0x00000400;
        public const uint CMIC_MASK_UNICODE = 0x00004000;
        public const uint CMIC_MASK_NO_CONSOLE = 0x00008000;
        public const uint CMIC_MASK_ASYNCOK = 0x00100000;
        public const uint CMIC_MASK_SHIFT_DOWN = 0x10000000;
        public const uint CMIC_MASK_CONTROL_DOWN = 0x40000000;

        // ── TrackPopupMenuEx 标志 ──
        public const uint TPM_LEFTALIGN = 0x0000;
        public const uint TPM_TOPALIGN = 0x0000;
        public const uint TPM_RETURNCMD = 0x0100;
        public const uint TPM_NONOTIFY = 0x0080;
        public const uint TPM_RIGHTBUTTON = 0x0002;

        // ── Windows 消息 ──
        public const uint WM_INITMENUPOPUP = 0x0117;
        public const uint WM_MEASUREITEM = 0x002C;
        public const uint WM_DRAWITEM = 0x002B;
        public const uint WM_MENUCHAR = 0x0120;
        public const uint WM_COMMAND = 0x0111;

        // ── MIIM (MenuItemInfo Mask) ──
        public const uint MIIM_STATE = 0x00000001;
        public const uint MIIM_ID = 0x00000002;
        public const uint MIIM_SUBMENU = 0x00000004;
        public const uint MIIM_CHECKMARKS = 0x00000010;
        public const uint MIIM_TYPE = 0x00000010;
        public const uint MIIM_DATA = 0x00000020;
        public const uint MIIM_STRING = 0x00000040;
        public const uint MIIM_FTYPE = 0x00000100;
        public const uint MIIM_BITMAP = 0x00000080;

        // ── MFT (MenuItem Types) ──
        public const uint MFT_STRING = 0x00000000;
        public const uint MFT_BITMAP = 0x00000004;
        public const uint MFT_MENUBARBREAK = 0x00000020;
        public const uint MFT_MENUBREAK = 0x00000040;
        public const uint MFT_OWNERDRAW = 0x00000100;
        public const uint MFT_RADIOCHECK = 0x00000200;
        public const uint MFT_SEPARATOR = 0x00000800;
        public const uint MFT_RIGHTORDER = 0x00002000;
        public const uint MFT_RIGHTJUSTIFY = 0x00004000;

        // ── MFS (MenuItem State) ──
        public const uint MFS_ENABLED = 0x00000000;
        public const uint MFS_DISABLED = 0x00000003;
        public const uint MFS_GRAYED = 0x00000003;
        public const uint MFS_CHECKED = 0x00000008;

        // ── IShellFolder.GetDisplayNameOf 标志 ──
        public const uint SHGDN_NORMAL = 0x0000;
        public const uint SHGDN_FORADDRESSBAR = 0x4000;
        public const uint SHGDN_FORPARSING = 0x8000;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MENUITEMINFO
    {
        public uint cbSize;
        public uint fMask;
        public uint fType;
        public uint fState;
        public uint wID;
        public IntPtr hSubMenu;
        public IntPtr hbmpChecked;
        public IntPtr hbmpUnchecked;
        public IntPtr dwItemData;
        public IntPtr dwTypeData;
        public uint cch;
        public IntPtr hbmpItem;

        public static MENUITEMINFO Create()
        {
            return new MENUITEMINFO { cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO)) };
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
    public struct CMINVOKECOMMANDINFO
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public string lpVerb;
        public string lpParameters;
        public string lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct CMINVOKECOMMANDINFOEX
    {
        public uint cbSize;
        public uint fMask;
        public IntPtr hwnd;
        public IntPtr lpVerb; // Can be string or resource ID
        public IntPtr lpParameters;
        public IntPtr lpDirectory;
        public int nShow;
        public uint dwHotKey;
        public IntPtr hIcon;
        public IntPtr lpTitle;
        public IntPtr lpVerbW;
        public IntPtr lpParametersW;
        public IntPtr lpDirectoryW;
        public IntPtr lpTitleW;
        public POINT ptInvoke;

        public static CMINVOKECOMMANDINFOEX Create()
        {
            return new CMINVOKECOMMANDINFOEX { cbSize = (uint)Marshal.SizeOf(typeof(CMINVOKECOMMANDINFOEX)) };
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class NativeMethods
    {
        [DllImport("shell32.dll")]
        public static extern int SHGetDesktopFolder(out IShellFolder ppshf);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr ILCreateFromPathW(string pszPath);

        [DllImport("shell32.dll")]
        public static extern void ILFree(IntPtr pidl);

        [DllImport("user32.dll")]
        public static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll")]
        public static extern bool DestroyMenu(IntPtr hMenu);

        [DllImport("user32.dll")]
        public static extern int GetMenuItemCount(IntPtr hMenu);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern bool GetMenuItemInfoW(IntPtr hMenu, uint uItem, bool fByPosition, ref MENUITEMINFO lpmii);

        [DllImport("user32.dll")]
        public static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

        [DllImport("user32.dll")]
        public static extern int TrackPopupMenuEx(IntPtr hMenu, uint uFlags, int x, int y, IntPtr hwnd, IntPtr lptpm);

        [DllImport("user32.dll")]
        public static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr pv);
    }
}
