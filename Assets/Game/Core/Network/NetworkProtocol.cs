namespace ProtectTree.Core.Network
{
    public static class NetworkProtocol
    {
        // 商店快照加入锁定、展示信息与六品质数据，并新增锁定命令。
        public const int CurrentVersion = 2;

        public static bool IsSupported(int version)
        {
            return version == CurrentVersion;
        }
    }
}
