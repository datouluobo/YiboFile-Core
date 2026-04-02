using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using YiboFile.Interop.Shell;
using YiboFile.Models.Shell;

namespace YiboFile.Services.Shell
{
    public interface IShellContextMenuService
    {
        void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner);
        List<ShellMenuItem> QueryShellSubMenuItems(IEnumerable<string> paths);
        void InvokeShellCommand(int commandId, IEnumerable<string> paths, Window owner);
    }

    public class ShellContextMenuService : IShellContextMenuService
    {
        private readonly NativeShellMenuHost _nativeHost = new();

        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (paths == null || !paths.Any()) return;
            _nativeHost.ShowNativeMenu(paths, screenPoint, owner);
        }

        public List<ShellMenuItem> QueryShellSubMenuItems(IEnumerable<string> paths)
        {
            // 在 Phase 2 中实现 HMENU 解析
            return new List<ShellMenuItem>();
        }

        public void InvokeShellCommand(int commandId, IEnumerable<string> paths, Window owner)
        {
            // 在 Phase 2 中通过直接调用 IContextMenu.InvokeCommand 实现
            // 目前通过 NativeShellMenuHost 的内部方法集成
        }
    }
}
