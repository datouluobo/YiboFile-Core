using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualBasic.FileIO;

namespace YiboFile.Services.FileOperations.RecycleBin
{
    public class RecycleBinService : IRecycleBinService, IDisposable
    {
        /// <summary>发送文件或目录到回收站</summary>
        public bool Send(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;

            try
            {
                if (Directory.Exists(path))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteDirectory(
                        path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else if (File.Exists(path))
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                }
                else
                {
                    return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>从回收站还原文件/目录（按原始路径匹配）</summary>
        public string Restore(string originalPath)
        {
            if (string.IsNullOrEmpty(originalPath)) return null;

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                return RestoreOnSTA(originalPath);

            string result = null;
            var thread = new Thread(() =>
            {
                try { result = RestoreOnSTA(originalPath); }
                catch { result = null; }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result;
        }

        /// <summary>枚举回收站中的所有项目</summary>
        public List<RecycleBinItem> ListItems()
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
                return ListItemsOnSTA();

            List<RecycleBinItem> result = null;
            var thread = new Thread(() =>
            {
                try { result = ListItemsOnSTA(); }
                catch { result = new List<RecycleBinItem>(); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return result ?? new List<RecycleBinItem>();
        }

        /// <summary>清空回收站</summary>
        public bool Empty()
        {
            try
            {
                uint flags = 0x1 | 0x2; // SHERB_NOCONFIRMATION | SHERB_NOPROGRESSUI
                int hr = SHEmptyRecycleBin(IntPtr.Zero, null, flags);
                return hr == 0;
            }
            catch { return false; }
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHEmptyRecycleBin(IntPtr hWnd, string pszRootPath, uint dwFlags);

        // ── STA 方法 ─────────────────────────────────────────────

        private static string RestoreOnSTA(string originalPath)
        {
            string target = Path.GetFullPath(originalPath).TrimEnd('\\');
            var (shell, recycler) = OpenRecycleBin();
            if (shell == null) return null;
            try
            {
                int count = GetItemCount(recycler);
                for (int i = 0; i < count; i++)
                {
                    object item = GetItem(recycler, i);
                    if (item == null) continue;

                    string itemName = GetDetail(recycler, item, 0);
                    string itemLoc = GetDetail(recycler, item, 1);
                    if (string.IsNullOrEmpty(itemName) || string.IsNullOrEmpty(itemLoc)) continue;

                    string reconstructed = Path.Combine(itemLoc.TrimEnd('\\'), itemName);
                    if (string.Equals(reconstructed, target, StringComparison.OrdinalIgnoreCase))
                    {
                        if (InvokeRestoreVerb(item))
                            return reconstructed;
                        return null;
                    }
                }
                return null;
            }
            finally
            {
                if (shell is IDisposable d) d.Dispose();
            }
        }

        private static List<RecycleBinItem> ListItemsOnSTA()
        {
            var items = new List<RecycleBinItem>();
            var (shell, recycler) = OpenRecycleBin();
            if (shell == null) return items;
            try
            {
                int count = GetItemCount(recycler);
                for (int i = 0; i < count; i++)
                {
                    object itemObj = GetItem(recycler, i);
                    if (itemObj == null) continue;

                    string name = GetDetail(recycler, itemObj, 0);
                    string origLoc = GetDetail(recycler, itemObj, 1);
                    if (string.IsNullOrEmpty(name)) continue;

                    string origPath = string.IsNullOrEmpty(origLoc)
                        ? name
                        : Path.Combine(origLoc.TrimEnd('\\'), name);

                    // Get size via FolderItem.Size property
                    long size = 0;
                    try
                    {
                        object sizeObj = itemObj.GetType().InvokeMember("Size",
                            System.Reflection.BindingFlags.GetProperty, null, itemObj, null);
                        if (sizeObj is long l) size = l;
                        else if (sizeObj is int iv) size = iv;
                    }
                    catch { }

                    // Get date via ModifyDate (often = deletion date for recycle bin)
                    DateTime date = DateTime.MinValue;
                    try
                    {
                        object dateObj = itemObj.GetType().InvokeMember("ModifyDate",
                            System.Reflection.BindingFlags.GetProperty, null, itemObj, null);
                        if (dateObj is DateTime dt) date = dt;
                    }
                    catch { }

                    bool isDir = false;
                    try
                    {
                        object isFolder = itemObj.GetType().InvokeMember("IsFolder",
                            System.Reflection.BindingFlags.GetProperty, null, itemObj, null);
                        isDir = (bool)isFolder;
                    }
                    catch { }

                    // Get actual bin path (for preview)
                    string binPath = "";
                    try
                    {
                        object pathObj = itemObj.GetType().InvokeMember("Path",
                            System.Reflection.BindingFlags.GetProperty, null, itemObj, null);
                        binPath = pathObj as string ?? "";
                    }
                    catch { }

                    items.Add(new RecycleBinItem
                    {
                        OriginalPath = origPath,
                        Name = name,
                        Size = size,
                        SizeDisplay = FormatSize(size),
                        IsDirectory = isDir,
                        DeletionTime = date,
                        BackupPath = binPath,
                        ShellIndex = i,
                        OriginalDirectory = origLoc ?? ""
                    });
                }
            }
            finally
            {
                if (shell is IDisposable d) d.Dispose();
            }
            return items;
        }

        // ── Shell COM Helpers ────────────────────────────────────

        private static (object shell, object recycler) OpenRecycleBin()
        {
            Type shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return (null, null);
            object shell = Activator.CreateInstance(shellType);
            object recycler = shell.GetType().InvokeMember("NameSpace",
                System.Reflection.BindingFlags.InvokeMethod, null, shell, new object[] { 10 });
            return (shell, recycler);
        }

        private static int GetItemCount(object recycler)
        {
            object items = recycler.GetType().InvokeMember("Items",
                System.Reflection.BindingFlags.InvokeMethod, null, recycler, null);
            return (int)items.GetType().InvokeMember("Count",
                System.Reflection.BindingFlags.GetProperty, null, items, null);
        }

        private static object GetItem(object recycler, int index)
        {
            object items = recycler.GetType().InvokeMember("Items",
                System.Reflection.BindingFlags.InvokeMethod, null, recycler, null);
            try
            {
                return items.GetType().InvokeMember("Item",
                    System.Reflection.BindingFlags.InvokeMethod, null, items, new object[] { index });
            }
            catch { return null; }
        }

        private static string GetDetail(object folder, object item, int column)
        {
            try
            {
                object result = folder.GetType().InvokeMember("GetDetailsOf",
                    System.Reflection.BindingFlags.InvokeMethod, null, folder, new object[] { item, column });
                return result as string ?? "";
            }
            catch { return ""; }
        }

        private static bool InvokeRestoreVerb(object item)
        {
            try
            {
                object verbs = item.GetType().InvokeMember("Verbs",
                    System.Reflection.BindingFlags.InvokeMethod, null, item, null);
                int verbCount = (int)verbs.GetType().InvokeMember("Count",
                    System.Reflection.BindingFlags.GetProperty, null, verbs, null);

                // Try "restore" first
                for (int v = 0; v < verbCount; v++)
                {
                    object verb = verbs.GetType().InvokeMember("Item",
                        System.Reflection.BindingFlags.InvokeMethod, null, verbs, new object[] { v });
                    string verbName = (string)verb.GetType().InvokeMember("Name",
                        System.Reflection.BindingFlags.GetProperty, null, verb, null) ?? "";

                    if (verbName.IndexOf("restore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        verbName.IndexOf("还原", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        verb.GetType().InvokeMember("DoIt",
                            System.Reflection.BindingFlags.InvokeMethod, null, verb, null);
                        return true;
                    }
                }

                // Fallback: first verb
                if (verbCount > 0)
                {
                    object firstVerb = verbs.GetType().InvokeMember("Item",
                        System.Reflection.BindingFlags.InvokeMethod, null, verbs, new object[] { 0 });
                    firstVerb.GetType().InvokeMember("DoIt",
                        System.Reflection.BindingFlags.InvokeMethod, null, firstVerb, null);
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("F1") + " KB";
            if (bytes < 1024 * 1024 * 1024) return (bytes / (1024.0 * 1024)).ToString("F1") + " MB";
            return (bytes / (1024.0 * 1024 * 1024)).ToString("F2") + " GB";
        }

        public void Dispose() { }
    }
}
