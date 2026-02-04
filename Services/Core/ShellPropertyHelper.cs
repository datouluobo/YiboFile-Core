using System;
using System.Runtime.InteropServices;

namespace YiboFile.Services.Core
{
    public static class ShellPropertyHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            [In] IntPtr pbc,
            [In] ref Guid riid,
            [Out, MarshalAs(UnmanagedType.Interface)] out IShellItem2 ppv);

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93")]
        private interface IShellItem2 : IShellItem
        {
            // IShellItem methods
            [PreserveSig]
            new int BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);
            [PreserveSig]
            new int GetParent([Out, MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            [PreserveSig]
            new int GetDisplayName([In] SIGDN sigdnName, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            [PreserveSig]
            new int GetAttributes([In] uint sfgaoMask, [Out] out uint psfgaoAttribs);
            [PreserveSig]
            new int Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, [Out] out int piOrder);

            // IShellItem2 methods
            [PreserveSig]
            int GetPropertyStore([In] GETPROPERTYSTOREFLAGS flags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
            [PreserveSig]
            int GetPropertyStoreWithCreateObject([In] GETPROPERTYSTOREFLAGS flags, [In, MarshalAs(UnmanagedType.Interface)] object punkCreateObject, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
            [PreserveSig]
            int GetPropertyStoreForKeys([In] ref PropertyKey rgKeys, [In] uint cKeys, [In] GETPROPERTYSTOREFLAGS flags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
            [PreserveSig]
            int GetPropertyDescriptionList([In] ref PropertyKey keyType, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);
            [PreserveSig]
            int Update([In, MarshalAs(UnmanagedType.Interface)] IntPtr pbc);
            [PreserveSig]
            int GetProperty([In] ref PropertyKey key, [Out] out PropVariant ppropvar);
            [PreserveSig]
            int GetCLSID([In] ref PropertyKey key, [Out] out Guid pclsid);
            [PreserveSig]
            int GetFileTime([In] ref PropertyKey key, [Out] out System.Runtime.InteropServices.ComTypes.FILETIME pft);
            [PreserveSig]
            int GetInt32([In] ref PropertyKey key, [Out] out int pi);
            [PreserveSig]
            int GetString([In] ref PropertyKey key, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
            [PreserveSig]
            int GetUInt32([In] ref PropertyKey key, [Out] out uint pui);
            [PreserveSig]
            int GetUInt64([In] ref PropertyKey key, [Out] out ulong pull);
            [PreserveSig]
            int GetBool([In] ref PropertyKey key, [Out] out int pf);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem
        {
            [PreserveSig]
            int BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);
            [PreserveSig]
            int GetParent([Out, MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            [PreserveSig]
            int GetDisplayName([In] SIGDN sigdnName, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            [PreserveSig]
            int GetAttributes([In] uint sfgaoMask, [Out] out uint psfgaoAttribs);
            [PreserveSig]
            int Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, [Out] out int piOrder);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
        private interface IPropertyStore
        {
            [PreserveSig]
            int GetCount([Out] out uint cProps);
            [PreserveSig]
            int GetAt([In] uint iProp, [Out] out PropertyKey pkey);
            [PreserveSig]
            int GetValue([In] ref PropertyKey key, [Out] out PropVariant pv);
            [PreserveSig]
            int SetValue([In] ref PropertyKey key, [In] ref PropVariant propvar);
            [PreserveSig]
            int Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PropertyKey
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct PropVariant
        {
            [FieldOffset(0)] public ushort vt;
            [FieldOffset(8)] public IntPtr unionMember;
            [FieldOffset(8)] public ulong uhVal;
        }

        private enum SIGDN : uint
        {
            NORMALDISPLAY = 0,
            PARENTRELATIVEPARSING = 0x80018001,
            DESKTOPABSOLUTEPARSING = 0x80028000,
            PARENTRELATIVEEDITING = 0x80031001,
            DESKTOPABSOLUTEEDITING = 0x8004c000,
            FILESYSPATH = 0x80058000,
            URL = 0x80068000,
            PARENTRELATIVEFORADDRESSBAR = 0x8007c001,
            PARENTRELATIVE = 0x80080001
        }

        private enum GETPROPERTYSTOREFLAGS : uint
        {
            DEFAULT = 0,
            HANDLERPROPERTIESONLY = 0x1,
            READWRITE = 0x2,
            TEMPORARY = 0x4,
            FASTPROPERTIESONLY = 0x8,
            OPENSLLOWITEM = 0x10,
            DELAYCREATION = 0x20,
            BESTEFFORT = 0x40,
            NO_OPLOCK = 0x80,
            MASK_VALID = 0xff
        }

        // PKEY_Media_Duration: {64440490-4C8B-11D1-8B70-080036B11A03}, 3
        // Uint64, 100ns units
        private static PropertyKey PKEY_Media_Duration = new PropertyKey
        {
            fmtid = new Guid("64440490-4C8B-11D1-8B70-080036B11A03"),
            pid = 3
        };

        // PKEY_Image_HorizontalSize: {64440490-4C8B-11D1-8B70-080036B11A03}, 3 (Wait, check GUID)
        // PKEY_Image_HorizontalSize: {64440490-4C8B-11D1-8B70-080036B11A03}, 3 is Duration? No.
        // System.Image.HorizontalSize: {64440490-4C8B-11D1-8B70-080036B11A03}, 3 ? 
        // Actually PKEY_Media_Duration is {64440490-4C8B-11D1-8B70-080036B11A03}, 3.
        // PKEY_Image_HorizontalSize is {64440490-4C8B-11D1-8B70-080036B11A03}, 3 ? No. 
        // Let's stick to Duration for now.

        public static long GetDuration(string filePath)
        {
            try
            {
                Guid iidShellItem2 = new Guid("7e9fb0d3-919f-4307-ab2e-9b1860310c93");
                IShellItem2 shellItem;
                int hr = SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iidShellItem2, out shellItem);

                if (hr >= 0 && shellItem != null)
                {
                    ulong duration = 0;
                    shellItem.GetUInt64(ref PKEY_Media_Duration, out duration);
                    // 100ns units -> ms
                    return (long)(duration / 10000);
                }
            }
            catch (COMException)
            {
                // COM errors are expected on some files or threads
                return 0;
            }
            catch (Exception)
            {
                // return 0 on fail
            }
            return 0;
        }
    }
}

