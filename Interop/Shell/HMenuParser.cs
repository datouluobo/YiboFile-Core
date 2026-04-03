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
        public static List<ShellMenuItem> ParseMenu(IntPtr hMenu, IContextMenu contextMenu)
        {
            var result = new List<ShellMenuItem>();
            int count = NativeMethods.GetMenuItemCount(hMenu);

            for (uint i = 0; i < (uint)count; i++)
            {
                var mii = MENUITEMINFO.Create();
                mii.fMask = ShellConstants.MIIM_ID | ShellConstants.MIIM_SUBMENU | ShellConstants.MIIM_STRING | ShellConstants.MIIM_FTYPE | ShellConstants.MIIM_BITMAP;
                
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

                // 1. 提取图标 (HBMPBIT_CALLBACK or specific HBITMAP)
                if (mii.hbmpItem != IntPtr.Zero && mii.hbmpItem.ToInt64() > 10) // 排除特殊标记如 HBMMENU_CALLBACK
                {
                    item.Icon = IconHelper.BitmapSourceFromHBitmap(mii.hbmpItem);
                }

                // 2. 获取 Verb (用于唯一标识和固定功能)
                if (item.CommandId > 0 && item.CommandId <= 0x7FFF)
                {
                    item.Verb = GetVerb(contextMenu, item.CommandId);
                }

                // 3. 处理子菜单
                if (mii.hSubMenu != IntPtr.Zero)
                {
                    // Phase 4: 通知 Shell 扩展初始化子菜单（必不可少，否则 SendTo 等项为空）
                    if (contextMenu is IContextMenu2 cm2)
                    {
                        cm2.HandleMenuMsg(ShellConstants.WM_INITMENUPOPUP, mii.hSubMenu, (IntPtr)i);
                    }
                    else if (contextMenu is IContextMenu3 cm3)
                    {
                        cm3.HandleMenuMsg2(ShellConstants.WM_INITMENUPOPUP, mii.hSubMenu, (IntPtr)i, out _);
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

        public static BitmapSource BitmapSourceFromHBitmap(IntPtr hBitmap)
        {
            // 过滤非法句柄。Shell 菜单位图常包含 HBMMENU_CALLBACK (1) 或其他常量。
            // 强转为 long 进行安全范围检查 (通常有效句柄 > 65535)。
            if (hBitmap == IntPtr.Zero || (long)hBitmap <= 100) return null;

            try
            {
                // 使用托管层 CreateBitmapSourceFromHBitmap
                BitmapSource source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap,
                    IntPtr.Zero,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                
                if (source == null) return null;

                // 核心修复点：克隆 (Clone) 图标并冻结。
                // 这会将依赖于原始 HBITMAP 的底层缓冲区物理复制一份，
                // 彻底断开与 Win32 HMENU 生长期的关联。
                var result = source.Clone();
                if (result.CanFreeze) result.Freeze();
                
                return result;
            }
            catch
            {
                // 忽略转换失败
                return null;
            }
        }
    }
}
