using System;
using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class PieceRosterSnapshot
    {
        public PieceRosterSnapshot(
            IReadOnlyList<PieceSnapshot> pieces,
            IReadOnlyList<PieceCapacitySnapshot> players,
            IReadOnlyList<ActiveSynergySnapshot> activeSynergies,
            IReadOnlyList<SynergyProgressSnapshot> synergyProgresses = null)
        {
            Pieces = pieces;
            Players = players;
            ActiveSynergies = activeSynergies;
            SynergyProgresses =
                synergyProgresses ?? Array.Empty<SynergyProgressSnapshot>();
        }

        public IReadOnlyList<PieceSnapshot> Pieces { get; }

        public IReadOnlyList<PieceCapacitySnapshot> Players { get; }

        public IReadOnlyList<ActiveSynergySnapshot> ActiveSynergies { get; }

        public IReadOnlyList<SynergyProgressSnapshot> SynergyProgresses { get; }
    }
}
