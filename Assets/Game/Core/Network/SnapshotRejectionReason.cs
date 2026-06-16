namespace ProtectTree.Core.Network
{
    public enum SnapshotRejectionReason
    {
        None = 0,
        UnsupportedProtocol = 1,
        WrongMatch = 2,
        StaleSequence = 3,
        OlderSimulationTick = 4,
    }
}
