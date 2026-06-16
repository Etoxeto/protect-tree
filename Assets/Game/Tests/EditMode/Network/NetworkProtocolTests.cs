using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;

namespace ProtectTree.Core.Tests
{
    public sealed class NetworkProtocolTests
    {
        [Test]
        public void MatchCommand_FactoriesExposeOnlyRequiredIntentPayload()
        {
            MatchCommand deploy = MatchCommand.DeployPiece(12, 103);
            Assert.That(deploy.Type, Is.EqualTo(MatchCommandType.DeployPiece));
            Assert.That(deploy.PieceInstanceId, Is.EqualTo(12));
            Assert.That(deploy.CellId, Is.EqualTo(103));
            Assert.That(deploy.ShopSlotIndex, Is.Null);
            Assert.That(deploy.Facing, Is.Null);

            MatchCommand facing = MatchCommand.SetPieceFacing(12, "Up");
            Assert.That(facing.Type, Is.EqualTo(MatchCommandType.SetPieceFacing));
            Assert.That(facing.PieceInstanceId, Is.EqualTo(12));
            Assert.That(facing.Facing, Is.EqualTo("Up"));
            Assert.That(facing.CellId, Is.Null);

            MatchCommand lockShop = MatchCommand.ToggleShopLock();
            Assert.That(lockShop.Type, Is.EqualTo(MatchCommandType.ToggleShopLock));

            MatchCommand place = MatchCommand.PlacePiece(12, 103, "Left");
            Assert.That(place.Type, Is.EqualTo(MatchCommandType.PlacePiece));
            Assert.That(place.PieceInstanceId, Is.EqualTo(12));
            Assert.That(place.CellId, Is.EqualTo(103));
            Assert.That(place.Facing, Is.EqualTo("Left"));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => MatchCommand.DeployPiece(0, 101));
            Assert.Throws<ArgumentException>(
                () => MatchCommand.SetPieceFacing(1, ""));
        }

        [Test]
        public void HostCommandGate_RejectsSpoofedWrongMatchAndRepeatedCommands()
        {
            HostCommandGate gate = new HostCommandGate("match-a");
            PlayerCommandEnvelope accepted = CreateCommandEnvelope(
                "match-a",
                playerId: 1,
                sequence: 1);

            Assert.That(
                gate.TryAccept(1, accepted, out CommandRejectionReason reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(CommandRejectionReason.None));

            PlayerCommandEnvelope spoofed = CreateCommandEnvelope(
                "match-a",
                playerId: 2,
                sequence: 1);
            Assert.That(gate.TryAccept(1, spoofed, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(CommandRejectionReason.WrongPlayer));

            Assert.That(gate.TryAccept(1, accepted, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(CommandRejectionReason.StaleSequence));

            PlayerCommandEnvelope wrongMatch = CreateCommandEnvelope(
                "match-b",
                playerId: 1,
                sequence: 2);
            Assert.That(gate.TryAccept(1, wrongMatch, out reason), Is.False);
            Assert.That(reason, Is.EqualTo(CommandRejectionReason.WrongMatch));

            PlayerCommandEnvelope unsupported = new PlayerCommandEnvelope(
                NetworkProtocol.CurrentVersion + 1,
                "match-a",
                1,
                2,
                MatchCommand.RefreshShop());
            Assert.That(gate.TryAccept(1, unsupported, out reason), Is.False);
            Assert.That(
                reason,
                Is.EqualTo(CommandRejectionReason.UnsupportedProtocol));

            Assert.That(
                gate.TryAccept(
                    2,
                    CreateCommandEnvelope("match-a", playerId: 2, sequence: 1),
                    out reason),
                Is.True);
            Assert.That(gate.GetLastAcceptedSequence(1), Is.EqualTo(1));
            Assert.That(gate.GetLastAcceptedSequence(2), Is.EqualTo(1));
        }

        [Test]
        public void LobbySnapshot_SupportsFourUniquePlayersAndOneHost()
        {
            List<LobbyPlayerSnapshot> players = new List<LobbyPlayerSnapshot>
            {
                new LobbyPlayerSnapshot(1, "Host", true, true, true),
                new LobbyPlayerSnapshot(2, "Two", true, true, false),
                new LobbyPlayerSnapshot(3, "Three", true, false, false),
                new LobbyPlayerSnapshot(4, "Four", true, false, false),
            };

            LobbySnapshot snapshot = new LobbySnapshot(7, false, players);
            Assert.That(snapshot.MaxPlayers, Is.EqualTo(4));
            Assert.That(snapshot.Players.Count, Is.EqualTo(4));
            Assert.That(snapshot.Revision, Is.EqualTo(7));

            Assert.Throws<ArgumentException>(
                () => new LobbySnapshot(
                    8,
                    false,
                    new[]
                    {
                        players[0],
                        new LobbyPlayerSnapshot(1, "Duplicate", true, false, false),
                    }));
            Assert.Throws<ArgumentException>(
                () => new LobbySnapshot(
                    8,
                    false,
                    new[]
                    {
                        players[0],
                        new LobbyPlayerSnapshot(2, "Second Host", true, false, true),
                    }));
            Assert.Throws<ArgumentException>(
                () => new LobbySnapshot(
                    8,
                    false,
                    new[] { players[0], players[1], players[2], players[3], players[0] }));
        }

        [Test]
        public void MatchStateSnapshot_ContainsOnlyRecipientPrivateShop()
        {
            MatchStateSnapshot snapshot = CreateMatchState(
                recipientPlayerId: 3,
                simulationTick: 12);
            Assert.That(snapshot.RecipientPlayerId, Is.EqualTo(3));
            Assert.That(snapshot.SimulationTick, Is.EqualTo(12));
            Assert.That(snapshot.Shop.PlayerId, Is.EqualTo(3));

            Assert.Throws<ArgumentException>(
                () => CreateMatchState(
                    recipientPlayerId: 3,
                    simulationTick: 12,
                    shopPlayerId: 2));
        }

        [Test]
        public void ClientSnapshotGate_RejectsOldSequenceAndSimulationRollback()
        {
            ClientSnapshotGate gate = new ClientSnapshotGate("match-a");
            SnapshotRejectionReason reason;

            Assert.That(
                gate.TryAccept(CreateSnapshotEnvelope("match-a", 1, 10), out reason),
                Is.True);
            Assert.That(reason, Is.EqualTo(SnapshotRejectionReason.None));

            Assert.That(
                gate.TryAccept(CreateSnapshotEnvelope("match-a", 1, 11), out reason),
                Is.False);
            Assert.That(reason, Is.EqualTo(SnapshotRejectionReason.StaleSequence));

            Assert.That(
                gate.TryAccept(CreateSnapshotEnvelope("match-a", 2, 9), out reason),
                Is.False);
            Assert.That(
                reason,
                Is.EqualTo(SnapshotRejectionReason.OlderSimulationTick));

            Assert.That(
                gate.TryAccept(CreateSnapshotEnvelope("other", 3, 12), out reason),
                Is.False);
            Assert.That(reason, Is.EqualTo(SnapshotRejectionReason.WrongMatch));

            Assert.That(
                gate.TryAccept(CreateSnapshotEnvelope("match-a", 4, 12), out reason),
                Is.True);
            Assert.That(gate.LastAcceptedSequence, Is.EqualTo(4));
            Assert.That(gate.LastAcceptedSimulationTick, Is.EqualTo(12));
        }

        private static PlayerCommandEnvelope CreateCommandEnvelope(
            string matchId,
            int playerId,
            long sequence)
        {
            return new PlayerCommandEnvelope(
                NetworkProtocol.CurrentVersion,
                matchId,
                playerId,
                sequence,
                MatchCommand.SetReady(true));
        }

        private static ServerSnapshotEnvelope CreateSnapshotEnvelope(
            string matchId,
            long sequence,
            long simulationTick)
        {
            return new ServerSnapshotEnvelope(
                NetworkProtocol.CurrentVersion,
                matchId,
                sequence,
                CreateMatchState(1, simulationTick));
        }

        private static MatchStateSnapshot CreateMatchState(
            int recipientPlayerId,
            long simulationTick,
            int? shopPlayerId = null)
        {
            return new MatchStateSnapshot(
                recipientPlayerId,
                simulationTick,
                new MatchFlowSnapshot("Preparation", 1, 5d, false, null),
                new EnemyRosterSnapshot(0, new List<EnemySnapshot>()),
                new PieceRosterSnapshot(
                    new List<PieceSnapshot>(),
                    new List<PieceCapacitySnapshot>(),
                    new List<ActiveSynergySnapshot>()),
                new PlayerRosterSnapshot(0, new List<PlayerSnapshot>()),
                CreateShop(shopPlayerId ?? recipientPlayerId));
        }

        private static ShopSnapshot CreateShop(int playerId)
        {
            return new ShopSnapshot(
                playerId,
                1,
                3,
                true,
                4,
                2,
                false,
                new List<ShopRarityWeightSnapshot>(),
                new List<ShopOfferSnapshot>());
        }
    }
}
