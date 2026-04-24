using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using YiboFile.Interop.Shell;

namespace YiboFile.Services.Shell
{
    public interface IShellContextMenuService
    {
        void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner);
        event Action<string> RenameRequested;
    }

    public class ShellContextMenuService : IShellContextMenuService
    {
        public event Action<string> RenameRequested;

        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (paths == null || !paths.Any()) return;

            using var host = new NativeShellMenuHost();
            host.RenameRequested += (path) => RenameRequested?.Invoke(path);
            host.ShowNativeMenu(paths, screenPoint, owner);
        }
    }
}
