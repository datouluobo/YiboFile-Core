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
    }

    public class ShellContextMenuService : IShellContextMenuService
    {
        private readonly NativeShellMenuHost _nativeHost = new();

        public void ShowNativeMenu(IEnumerable<string> paths, Point screenPoint, Window owner)
        {
            if (paths == null || !paths.Any()) return;

            _nativeHost.ShowNativeMenu(paths, screenPoint, owner);
        }
    }
}
