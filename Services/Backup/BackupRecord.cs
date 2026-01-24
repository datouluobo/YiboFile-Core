using System;
using System.Collections.Generic;

namespace YiboFile.Services.Backup
{
    public class BackupRecord
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string OriginalPath { get; set; }
        public string BackupPath { get; set; }  // Relative path within the backup folder or full path
        public DateTime DeletionTime { get; set; }
        public long Size { get; set; }
        public bool IsDirectory { get; set; }
    }

    public class BackupManifest
    {
        public string Date { get; set; } // yyyyMMdd
        public List<BackupRecord> Records { get; set; } = new List<BackupRecord>();
    }
}
