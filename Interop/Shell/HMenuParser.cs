using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;
using YiboFile.Models.Shell;

namespace YiboFile.Interop.Shell
{
    /// <summary>
    /// 负责递归解析 HMENU 并提取文本、图标和命令。
    /// </summary>
    public static class HMenuParser
    {
        private const long HBMMENU_CALLBACK = -1;
        private const long HBMMENU_POPUP_MINIMIZE = 11;

        public static List<ShellMenuItem> ParseMenu(IntPtr hMenu, IContextMenu contextMenu)
        {
            var result = new List<ShellMenuItem>();
            int count = NativeMethods.GetMenuItemCount(hMenu);

            for (uint i = 0; i < (uint)count; i++)
            {
                var mii = MENUITEMINFO.Create();
                mii.fMask = ShellConstants.MIIM_ID | ShellConstants.MIIM_SUBMENU
                    | ShellConstants.MIIM_STRING | ShellConstants.MIIM_FTYPE
                    | ShellConstants.MIIM_BITMAP | ShellConstants.MIIM_CHECKMARKS
                    | ShellConstants.MIIM_DATA | ShellConstants.MIIM_STATE;
                mii.dwTypeData = IntPtr.Zero;
                mii.cch = 0;

                if (!NativeMethods.GetMenuItemInfoW(hMenu, i, true, ref mii))
                {
                    continue;
                }

                if ((mii.fType & ShellConstants.MFT_SEPARATOR) != 0)
                {
                    result.Add(new ShellMenuItem { IsSeparator = true });
                    continue;
                }

                string itemText = string.Empty;
                if (mii.cch > 0)
                {
                    mii.cch++;
                    mii.dwTypeData = Marshal.AllocHGlobal((int)mii.cch * 2);
                    try
                    {
                        if (NativeMethods.GetMenuItemInfoW(hMenu, i, true, ref mii))
                        {
                            itemText = Marshal.PtrToStringUni(mii.dwTypeData);
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(mii.dwTypeData);
                    }
                }

                var item = new ShellMenuItem
                {
                    Text = itemText,
                    CommandId = (int)mii.wID,
                    IsEnabled = (mii.fState & ShellConstants.MFS_DISABLED) == 0
                };

                long bmpVal = mii.hbmpItem.ToInt64();
                bool hasRealBmp = mii.hbmpItem != IntPtr.Zero
                    && bmpVal > HBMMENU_POPUP_MINIMIZE
                    && bmpVal != unchecked((long)(uint)0xFFFFFFFF)
                    && bmpVal != HBMMENU_CALLBACK;
                if (hasRealBmp)
                {
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpItem);
                }

                if (item.Icon == null && (mii.fType & ShellConstants.MFT_BITMAP) != 0 && mii.hbmpItem != IntPtr.Zero)
                {
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpItem);
                }

                if (item.Icon == null && mii.hbmpUnchecked != IntPtr.Zero)
                {
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpUnchecked);
                }
                if (item.Icon == null && mii.hbmpChecked != IntPtr.Zero)
                {
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpChecked);
                }
                if (item.Icon == null && item.CommandId > 0 && item.CommandId <= 0x7FFF)
                {
                    item.Icon = IconHelper.TryGetShellIcon(contextMenu, item.CommandId);
                }

                if (item.CommandId > 0 && item.CommandId <= 0x7FFF)
                {
                    item.Verb = GetVerb(contextMenu, item.CommandId);
                }

                if (mii.hSubMenu != IntPtr.Zero)
                {
                    if (contextMenu is IContextMenu3 cm3)
                    {
                        cm3.HandleMenuMsg2(ShellConstants.WM_INITMENUPOPUP, mii.hSubMenu, (IntPtr)i, out _);
                    }
                    else if (contextMenu is IContextMenu2 cm2)
                    {
                        cm2.HandleMenuMsg(ShellConstants.WM_INITMENUPOPUP, mii.hSubMenu, (IntPtr)i);
                    }

                    item.Children = ParseMenu(mii.hSubMenu, contextMenu);
                }

                result.Add(item);
            }

            return result;
        }

        private static string GetVerb(IContextMenu contextMenu, int commandId)
        {
            try
            {
                var sb = new StringBuilder(256);
                contextMenu.GetCommandString((uint)commandId, ShellConstants.GCS_VERBW, IntPtr.Zero, sb, 256);
                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }
    }

    internal static class IconHelper
    {
        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [DllImport("gdi32.dll")]
        private static extern int GetObject(IntPtr hObject, int nCount, ref BITMAP lpObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        public static BitmapSource BitmapSourceFromHBitmap(IntPtr hBitmap)
        {
            if (hBitmap == IntPtr.Zero)
            {
                return null;
            }

            long val = hBitmap.ToInt64();
            if (val <= 11 || val == -1 || val == unchecked((long)(uint)0xFFFFFFFF))
            {
                return null;
            }

            try
            {
                var bmp = new BITMAP();
                if (GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref bmp) == 0)
                {
                    return null;
                }

                if (bmp.bmWidth <= 0 || bmp.bmHeight <= 0 || bmp.bmWidth > 256 || bmp.bmHeight > 256)
                {
                    return null;
                }

                BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());

                if (source == null)
                {
                    return null;
                }

                var result = source.Clone();
                if (result.CanFreeze)
                {
                    result.Freeze();
                }

                return result;
            }
            catch
            {
                return null;
            }
        }

        public static BitmapSource TryGetShellIcon(IContextMenu contextMenu, int commandId)
        {
            try
            {
                var helpText = new StringBuilder(512);
                contextMenu.GetCommandString((uint)commandId, ShellConstants.GCS_HELPTEXTW, IntPtr.Zero, helpText, 512);
                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
