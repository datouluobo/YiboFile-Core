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
            if (paths == null || !paths.Any()) return new List<ShellMenuItem>();
            try
            {
                return _nativeHost.GetMenuItems(paths);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to query shell menu items: {ex.Message}");
                return new List<ShellMenuItem>();
            }
        }

        public void InvokeShellCommand(int commandId, IEnumerable<string> paths, Window owner)
        {
            if (paths == null || !paths.Any()) return;
            try
            {
                _nativeHost.InvokeDirect(commandId, paths, owner);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to invoke shell command {commandId}: {ex.Message}");
            }
        }
    }
}
