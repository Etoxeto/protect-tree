namespace ProtectTree.Core.Network
{
    public enum CommandRejectionReason
    {
        None = 0,
        UnsupportedProtocol = 1,
        WrongMatch = 2,
        WrongPlayer = 3,
        StaleSequence = 4,
        GameplayRejected = 5,
    }
}
