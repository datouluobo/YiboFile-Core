using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace OoiMRR.Tests
{
    internal static class NoteSearchTests
    {
        public static bool RunSmoke()
        {
            try
            {
                DatabaseManager.Initialize();
                var tempDir = Path.Combine(Path.GetTempPath(), "OoiMRR_NotesTest");
                Directory.CreateDirectory(tempDir);
                var f1 = Path.Combine(tempDir, "测试文档一.txt");
                var f2 = Path.Combine(tempDir, "报告二.docx");
                File.WriteAllText(f1, "a");
                File.WriteAllText(f2, "b");
                DatabaseManager.SetFileNotes(f1, "层压机设备维护记录，中文分词验证");
                DatabaseManager.SetFileNotes(f2, "报告包含层压机关键词");
                var r1 = DatabaseManager.SearchFilesByNotes("层压机");
                var ok1 = r1.Contains(f1) && r1.Contains(f2);
                var r2 = DatabaseManager.SearchFilesByNotes("维护");
                var ok2 = r2.Contains(f1);
                return ok1 && ok2;
            }
            catch
            {
                return false;
            }
        }
    }
}
