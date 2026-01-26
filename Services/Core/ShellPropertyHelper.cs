using System;
using System.Runtime.InteropServices;

namespace YiboFile.Services.Core
{
    public static class ShellPropertyHelper
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        private static extern void SHCreateItemFromParsingName(
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
            new void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);
            new void GetParent([Out, MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            new void GetDisplayName([In] SIGDN sigdnName, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            new void GetAttributes([In] uint sfgaoMask, [Out] out uint psfgaoAttribs);
            new void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, [Out] out int piOrder);

            // IShellItem2 methods
            void GetPropertyStore([In] GETPROPERTYSTOREFLAGS flags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
            void GetPropertyStoreWithCreateObject([In] GETPROPERTYSTOREFLAGS flags, [In, MarshalAs(UnmanagedType.Interface)] object punkCreateObject, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
            void GetPropertyStoreForKeys([In] ref PropertyKey rgKeys, [In] uint cKeys, [In] GETPROPERTYSTOREFLAGS flags, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
            void GetPropertyDescriptionList([In] ref PropertyKey keyType, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);
            void Update([In, MarshalAs(UnmanagedType.Interface)] IntPtr pbc);
            void GetProperty([In] ref PropertyKey key, [Out] out PropVariant ppropvar);
            void GetCLSID([In] ref PropertyKey key, [Out] out Guid pclsid);
            void GetFileTime([In] ref PropertyKey key, [Out] out System.Runtime.InteropServices.ComTypes.FILETIME pft);
            void GetInt32([In] ref PropertyKey key, [Out] out int pi);
            void GetString([In] ref PropertyKey key, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppsz);
            void GetUInt32([In] ref PropertyKey key, [Out] out uint pui);
            void GetUInt64([In] ref PropertyKey key, [Out] out ulong pull);
            void GetBool([In] ref PropertyKey key, [Out] out int pf);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("43826d1e-e718-42ee-bc55-a1e261c37bfe")]
        private interface IShellItem
        {
            void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IntPtr ppv);
            void GetParent([Out, MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);
            void GetDisplayName([In] SIGDN sigdnName, [Out, MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes([In] uint sfgaoMask, [Out] out uint psfgaoAttribs);
            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, [Out] out int piOrder);
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99")]
        private interface IPropertyStore
        {
            void GetCount([Out] out uint cProps);
            void GetAt([In] uint iProp, [Out] out PropertyKey pkey);
            void GetValue([In] ref PropertyKey key, [Out] out PropVariant pv);
            void SetValue([In] ref PropertyKey key, [In] ref PropVariant propvar);
            void Commit();
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
                SHCreateItemFromParsingName(filePath, IntPtr.Zero, ref iidShellItem2, out shellItem);

                if (shellItem != null)
                {
                    ulong duration = 0;
                    // Pass local variable logic? 
                    // Or just remove readonly.
                    shellItem.GetUInt64(ref PKEY_Media_Duration, out duration);
                    // 100ns units -> ms
                    return (long)(duration / 10000);
                }
            }
            catch (Exception)
            {
                // return 0 on fail
            }
            return 0;
        }
    }
}

