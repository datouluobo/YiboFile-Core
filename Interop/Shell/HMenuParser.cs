using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using YiboFile.Models.Shell;

namespace YiboFile.Interop.Shell
{
    /// <summary>
    /// 负责递归解析 HMENU 并提取文本、图标和命令
    /// </summary>
    public static class HMenuParser
    {
        // HBMMENU 特殊常量 (Windows SDK)
        private const long HBMMENU_CALLBACK = -1;
        private const long HBMMENU_SYSTEM = 1;
        private const long HBMMENU_MBAR_RESTORE = 2;
        private const long HBMMENU_MBAR_MINIMIZE = 3;
        private const long HBMMENU_MBAR_CLOSE = 5;
        private const long HBMMENU_MBAR_CLOSE_D = 6;
        private const long HBMMENU_MBAR_MINIMIZE_D = 7;
        private const long HBMMENU_POPUP_CLOSE = 8;
        private const long HBMMENU_POPUP_RESTORE = 9;
        private const long HBMMENU_POPUP_MAXIMIZE = 10;
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
                    | ShellConstants.MIIM_DATA;
                
                // 初次调用获取字符串长度 (cch 会被填充为长度)
                mii.dwTypeData = IntPtr.Zero;
                mii.cch = 0;
                
                if (!NativeMethods.GetMenuItemInfoW(hMenu, i, true, ref mii)) continue;

                if ((mii.fType & ShellConstants.MFT_SEPARATOR) != 0)
                {
                    result.Add(new ShellMenuItem { IsSeparator = true });
                    continue;
                }

                // 如果有文本，分配内存再次获取
                string itemText = string.Empty;
                if (mii.cch > 0)
                {
                    mii.cch++; // 包含终止符
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
                    CommandId = (int)mii.wID
                };

                // ── 图标提取 ──
                // 排除 HBMMENU_CALLBACK(-1) 和其他特殊系统常量 (0-11)
                long bmpVal = mii.hbmpItem.ToInt64();
                bool hasRealBmp = mii.hbmpItem != IntPtr.Zero && bmpVal > HBMMENU_POPUP_MINIMIZE && bmpVal != unchecked((long)(uint)0xFFFFFFFF);
                
                if (hasRealBmp)
                {
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpItem);
                }

                // 策略 1.5: 如果是 MFT_BITMAP，dwTypeData 可能指向位图
                if (item.Icon == null && (mii.fType & ShellConstants.MFT_BITMAP) != 0)
                {
                    if (mii.hbmpItem != IntPtr.Zero) // MenuItemInfo.hbmpItem often overlaps with dwTypeData for MFT_BITMAP
                        item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpItem);
                }

                // 策略 2: 从 hbmpChecked / hbmpUnchecked 提取 (MIIM_CHECKMARKS)
                if (item.Icon == null && mii.hbmpUnchecked != IntPtr.Zero)
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpUnchecked);
                if (item.Icon == null && mii.hbmpChecked != IntPtr.Zero)
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpChecked);

                // 策略 3: 对于 OWNERDRAW 项，尝试从 dwItemData 找图标（有些厂商会传句柄）
                if (item.Icon == null && (mii.fType & ShellConstants.MFT_OWNERDRAW) != 0 && mii.dwItemData != IntPtr.Zero)
                {
                    // 这是一个模糊测试，如果 dwItemData 看起来像一个有效的 GDI 句柄，尝试转换
                    // 但通常这不可靠，所以只在万不得已时尝试
                }

                // 2. 获取 Verb (用于唯一标识和固定功能)
                if (item.CommandId > 0 && item.CommandId <= 0x7FFF)
                {
                    item.Verb = GetVerb(contextMenu, item.CommandId);
                }

                // 3. 处理子菜单
                if (mii.hSubMenu != IntPtr.Zero)
                {
                    // 通知 Shell 扩展初始化子菜单
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

    /// <summary>
    /// 图标处理助手
    /// </summary>
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
            // 安全范围检查：HBMMENU_CALLBACK = -1, 系统常量 = 0~11
            if (hBitmap == IntPtr.Zero) return null;
            long val = hBitmap.ToInt64();
            if (val <= 11 || val == -1 || val == unchecked((long)(uint)0xFFFFFFFF)) return null;

            try
            {
                // 验证 HBITMAP 是否有效
                var bmp = new BITMAP();
                if (GetObject(hBitmap, Marshal.SizeOf(typeof(BITMAP)), ref bmp) == 0)
                    return null;

                // 跳过尺寸异常的位图
                if (bmp.bmWidth <= 0 || bmp.bmHeight <= 0 || bmp.bmWidth > 256 || bmp.bmHeight > 256)
                    return null;

                BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                
                if (source == null) return null;

                // 克隆并冻结，断开与原始 HBITMAP 的关联
                var result = source.Clone();
                if (result.CanFreeze) result.Freeze();
                
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
