namespace ProtectTree.Core.Network
{
    public static class NetworkProtocol
    {
        // Enemy snapshots now synchronize Boss-wave enrage state.
        public const int CurrentVersion = 17;

        public static bool IsSupported(int version)
        {
            return version == CurrentVersion;
        }
    }
}
