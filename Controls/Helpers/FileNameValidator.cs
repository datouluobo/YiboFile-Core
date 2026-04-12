using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YiboFile.Controls.Helpers
{
    /// <summary>
    /// Windows 文件名合法性校验器
    /// 按优先级检查：空白 → 非法字符 → 末尾点/空格 → 保留名称
    /// </summary>
    public static class FileNameValidator
    {
        /// <summary>
        /// Windows 文件名中禁止使用的字符
        /// </summary>
        private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

        /// <summary>
        /// Windows 保留设备名称（不区分大小写）
        /// </summary>
        private static readonly HashSet<string> ReservedNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM0", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT0", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        /// <summary>
        /// 校验文件名合法性
        /// </summary>
        /// <param name="fileName">待校验的文件名</param>
        /// <returns>校验结果：IsValid 为 true 表示合法；ErrorMessage 为错误提示文本</returns>
        public static (bool IsValid, string ErrorMessage) Validate(string fileName)
        {
            // 规则 1：空白或全空格
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return (false, "文件名不能为空");
            }

            // 规则 2：包含非法字符
            foreach (char c in fileName)
            {
                if (InvalidChars.Contains(c))
                {
                    // 为不可见字符提供友好提示
                    string charDisplay = char.IsControl(c) ? $"控制字符(0x{(int)c:X2})" : $"\"{c}\"";
                    return (false, $"文件名不能包含字符：{charDisplay}");
                }
            }

            // 规则 3：以点或空格结尾
            if (fileName.EndsWith(".") || fileName.EndsWith(" "))
            {
                return (false, "文件名不能以点或空格结尾");
            }

            // 规则 4：保留名称（需要去掉扩展名再比较，如 "CON.txt" 也是非法的）
            string nameWithoutExt = fileName;
            int dotIndex = fileName.IndexOf('.');
            if (dotIndex > 0)
            {
                nameWithoutExt = fileName.Substring(0, dotIndex);
            }

            if (ReservedNames.Contains(nameWithoutExt))
            {
                return (false, $"\"{nameWithoutExt}\" 是系统保留名称");
            }

            return (true, null);
        }
    }
}
