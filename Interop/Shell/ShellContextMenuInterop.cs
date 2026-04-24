using System;
using System.Runtime.InteropServices;
using System.Text;

namespace YiboFile.Interop.Shell
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214E6-0000-0000-C000-000000000046")]
    public interface IShellFolder
    {
        [PreserveSig]
        int ParseDisplayName(IntPtr hwnd, IntPtr pbc,
            [MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName,
            ref uint pchEaten, out IntPtr ppidl, ref uint pdwAttributes);

        [PreserveSig]
        int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

        [PreserveSig]
        int BindToObject(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int BindToStorage(IntPtr pidl, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

        [PreserveSig]
        int CreateViewObject(IntPtr hwndOwner, ref Guid riid, out IntPtr ppv);

        [PreserveSig]
        int GetAttributesOf(uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
            ref uint rgfInOut);

        [PreserveSig]
        int GetUIObjectOf(IntPtr hwndOwner, uint cidl,
            [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] IntPtr[] apidl,
            ref Guid riid, IntPtr rgfReserved, out IntPtr ppv);

        [PreserveSig]
        int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);

        [PreserveSig]
        int SetNameOf(IntPtr hwnd, IntPtr pidl,
            [MarshalAs(UnmanagedType.LPWStr)] string pszName,
            uint uFlags, out IntPtr ppidlOut);
    }

    // InvokeCommand uses IntPtr so we can pass both CMINVOKECOMMANDINFO and CMINVOKECOMMANDINFOEX
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    public interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(IntPtr pici);

        [PreserveSig]
        int GetCommandString(uint idCmd, uint uType, IntPtr pReserved,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, uint cchMax);
    }

    // Flat interface (non-inheriting) to avoid vtable slot issues with COM interop
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214f4-0000-0000-c000-000000000046")]
    public interface IContextMenu2
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(IntPtr pici);

        [PreserveSig]
        int GetCommandString(uint idCmd, uint uType, IntPtr pReserved,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, uint cchMax);

        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("bcfce0a0-ec17-11d0-8d10-00a0c90f2719")]
    public interface IContextMenu3
    {
        [PreserveSig]
        int QueryContextMenu(IntPtr hMenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

        [PreserveSig]
        int InvokeCommand(IntPtr pici);

        [PreserveSig]
        int GetCommandString(uint idCmd, uint uType, IntPtr pReserved,
            [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, uint cchMax);

        [PreserveSig]
        int HandleMenuMsg(uint uMsg, IntPtr wParam, IntPtr lParam);

        [PreserveSig]
        int HandleMenuMsg2(uint uMsg, IntPtr wParam, IntPtr lParam, out IntPtr result);
    }
}
