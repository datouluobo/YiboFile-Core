using System.Collections.Generic;

namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 执行 Shell 动词请求
    /// </summary>
    public class ExecuteShellVerbRequestMessage
    {
        public string Verb { get; }
        public List<string> Paths { get; }

        public ExecuteShellVerbRequestMessage(string verb, List<string> paths)
        {
            Verb = verb;
            Paths = paths;
        }
    }
}
