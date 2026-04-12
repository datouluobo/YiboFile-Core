namespace YiboFile.ViewModels.Messaging.Messages
{
    /// <summary>
    /// 硬件设备变更消息
    /// </summary>
    public class DeviceChangedMessage
    {
        /// <summary>
        /// 变更类型 (A: Added, R: Removed)
        /// </summary>
        public char ChangeType { get; }

        /// <summary>
        /// 受到影响的驱动器名称 (如 "E:")
        /// </summary>
        public string DriveName { get; }

        public DeviceChangedMessage(char changeType, string driveName = null)
        {
            ChangeType = changeType;
            DriveName = driveName;
        }
    }

    /// <summary>
    /// 请求弹出驱动器消息
    /// </summary>
    public class RequestEjectDriveMessage
    {
        public string DrivePath { get; }

        public RequestEjectDriveMessage(string drivePath)
        {
            DrivePath = drivePath;
        }
    }
}
