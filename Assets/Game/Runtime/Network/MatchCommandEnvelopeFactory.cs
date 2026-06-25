using System;
using ProtectTree.Core;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Creates client-authored command envelopes before a transport sends them.
    /// </summary>
    public sealed class MatchCommandEnvelopeFactory
    {
        private readonly string _matchId;
        private readonly int _localPlayerId;

        private long _nextSequence = 1;

        public MatchCommandEnvelopeFactory(int localPlayerId, string matchId)
        {
            if (localPlayerId <= 0 || localPlayerId > GameLimits.MaxPlayers)
            {
                throw new ArgumentOutOfRangeException(nameof(localPlayerId));
            }

            if (string.IsNullOrWhiteSpace(matchId))
            {
                throw new ArgumentException("Match ID is required.", nameof(matchId));
            }

            _localPlayerId = localPlayerId;
            _matchId = matchId;
        }

        public int LocalPlayerId => _localPlayerId;

        public long NextSequence => _nextSequence;

        public PlayerCommandEnvelope CreateEnvelope(MatchCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            long sequence = _nextSequence;
            _nextSequence++;

            // 重传时应复用已创建的 envelope，而不是重新创建并消耗新的序号。
            return new PlayerCommandEnvelope(
                NetworkProtocol.CurrentVersion,
                _matchId,
                _localPlayerId,
                sequence,
                command);
        }
    }
}
