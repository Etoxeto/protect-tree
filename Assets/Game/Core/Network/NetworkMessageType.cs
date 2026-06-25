namespace ProtectTree.Core.Network
{
    public enum NetworkMessageType
    {
        PlayerCommand = 1,
        ServerSnapshot = 2,
        LobbySnapshot = 3,
        LobbyCommand = 4,
        LobbyAssignment = 5,
        MatchStart = 6,
        MatchJoin = 7,
    }
}
