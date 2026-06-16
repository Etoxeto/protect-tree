using System;

namespace ProtectTree.Core.Network
{
    public sealed class HostCommandGate
    {
        private readonly string _matchId;
        private readonly long[] _lastAcceptedSequenceByPlayer =
            new long[GameLimits.MaxPlayers + 1];

        public HostCommandGate(string matchId)
        {
            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            _matchId = matchId;
        }

        public bool TryAccept(
            int assignedPlayerId,
            PlayerCommandEnvelope envelope,
            out CommandRejectionReason rejectionReason)
        {
            RequirePlayerId(assignedPlayerId);
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            // 主机只信任连接所分配的玩家身份，并以单玩家递增序号拒绝伪造、重复和乱序命令。
            if (!NetworkProtocol.IsSupported(envelope.ProtocolVersion))
            {
                rejectionReason = CommandRejectionReason.UnsupportedProtocol;
                return false;
            }

            if (!string.Equals(envelope.MatchId, _matchId, StringComparison.Ordinal))
            {
                rejectionReason = CommandRejectionReason.WrongMatch;
                return false;
            }

            if (envelope.PlayerId != assignedPlayerId)
            {
                rejectionReason = CommandRejectionReason.WrongPlayer;
                return false;
            }

            if (envelope.Sequence <= _lastAcceptedSequenceByPlayer[assignedPlayerId])
            {
                rejectionReason = CommandRejectionReason.StaleSequence;
                return false;
            }

            _lastAcceptedSequenceByPlayer[assignedPlayerId] = envelope.Sequence;
            rejectionReason = CommandRejectionReason.None;
            return true;
        }

        public long GetLastAcceptedSequence(int playerId)
        {
            RequirePlayerId(playerId);
            return _lastAcceptedSequenceByPlayer[playerId];
        }

        private static void RequirePlayerId(int playerId)
        {
            if (playerId <= 0 || playerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId));
            }
        }
    }
}
