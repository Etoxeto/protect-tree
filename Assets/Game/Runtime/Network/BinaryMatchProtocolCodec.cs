using System;
using System.Collections.Generic;
using System.IO;
using ProtectTree.Core.Match;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Network
{
    /// <summary>
    /// Small binary protocol codec used by the early LAN plumbing.
    /// </summary>
    public sealed class BinaryMatchProtocolCodec : IMatchProtocolCodec
    {
        private const int Magic = 0x50544E31; // "PTN1"

        public NetworkMessageType GetMessageType(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(reader, out _, out NetworkMessageType messageType);
                return messageType;
            }
        }

        public byte[] EncodePlayerCommand(PlayerCommandEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    envelope.ProtocolVersion,
                    NetworkMessageType.PlayerCommand);

                writer.Write(envelope.MatchId);
                writer.Write(envelope.PlayerId);
                writer.Write(envelope.Sequence);
                WriteCommand(writer, envelope.Command);
                return stream.ToArray();
            }
        }

        public PlayerCommandEnvelope DecodePlayerCommand(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out int protocolVersion,
                    out NetworkMessageType messageType);
                RequireMessageType(
                    messageType,
                    NetworkMessageType.PlayerCommand);

                string matchId = reader.ReadString();
                int playerId = reader.ReadInt32();
                long sequence = reader.ReadInt64();
                MatchCommand command = ReadCommand(reader);
                return new PlayerCommandEnvelope(
                    protocolVersion,
                    matchId,
                    playerId,
                    sequence,
                    command);
            }
        }

        public byte[] EncodeServerSnapshot(ServerSnapshotEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    envelope.ProtocolVersion,
                    NetworkMessageType.ServerSnapshot);

                writer.Write(envelope.MatchId);
                writer.Write(envelope.Sequence);
                WriteMatchStateSnapshot(writer, envelope.Snapshot);
                return stream.ToArray();
            }
        }

        public ServerSnapshotEnvelope DecodeServerSnapshot(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out int protocolVersion,
                    out NetworkMessageType messageType);
                RequireMessageType(messageType, NetworkMessageType.ServerSnapshot);

                string matchId = reader.ReadString();
                long sequence = reader.ReadInt64();
                MatchStateSnapshot snapshot = ReadMatchStateSnapshot(reader);
                return new ServerSnapshotEnvelope(
                    protocolVersion,
                    matchId,
                    sequence,
                    snapshot);
            }
        }

        public byte[] EncodeLobbySnapshot(LobbySnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    NetworkProtocol.CurrentVersion,
                    NetworkMessageType.LobbySnapshot);

                writer.Write(snapshot.Revision);
                writer.Write(snapshot.CanStart);
                writer.Write(snapshot.Players.Count);
                foreach (LobbyPlayerSnapshot player in snapshot.Players)
                {
                    writer.Write(player.PlayerId);
                    writer.Write(player.DisplayName);
                    WriteNullableString(writer, player.AvatarResourcePath);
                    writer.Write(player.IsConnected);
                    writer.Write(player.IsReady);
                    writer.Write(player.IsHost);
                }

                return stream.ToArray();
            }
        }

        public LobbySnapshot DecodeLobbySnapshot(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out _,
                    out NetworkMessageType messageType);
                RequireMessageType(messageType, NetworkMessageType.LobbySnapshot);

                long revision = reader.ReadInt64();
                bool canStart = reader.ReadBoolean();
                int playerCount = reader.ReadInt32();
                List<LobbyPlayerSnapshot> players =
                    new List<LobbyPlayerSnapshot>(playerCount);

                for (int index = 0; index < playerCount; index++)
                {
                    int playerId = reader.ReadInt32();
                    string displayName = reader.ReadString();
                    string avatarResourcePath = ReadNullableString(reader);
                    bool isConnected = reader.ReadBoolean();
                    bool isReady = reader.ReadBoolean();
                    bool isHost = reader.ReadBoolean();
                    players.Add(new LobbyPlayerSnapshot(
                        playerId,
                        displayName,
                        isConnected,
                        isReady,
                        isHost,
                        avatarResourcePath));
                }

                return new LobbySnapshot(revision, canStart, players);
            }
        }

        public byte[] EncodeLobbyCommand(LobbyCommandEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    envelope.ProtocolVersion,
                    NetworkMessageType.LobbyCommand);

                writer.Write(envelope.RoomId);
                writer.Write(envelope.PlayerId);
                writer.Write(envelope.Sequence);
                WriteLobbyCommand(writer, envelope.Command);
                return stream.ToArray();
            }
        }

        public LobbyCommandEnvelope DecodeLobbyCommand(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out int protocolVersion,
                    out NetworkMessageType messageType);
                RequireMessageType(messageType, NetworkMessageType.LobbyCommand);

                string roomId = reader.ReadString();
                int playerId = reader.ReadInt32();
                long sequence = reader.ReadInt64();
                LobbyCommand command = ReadLobbyCommand(reader);
                return new LobbyCommandEnvelope(
                    protocolVersion,
                    roomId,
                    playerId,
                    sequence,
                    command);
            }
        }

        public byte[] EncodeLobbyAssignment(LobbyAssignmentEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    envelope.ProtocolVersion,
                    NetworkMessageType.LobbyAssignment);

                writer.Write(envelope.RoomId);
                writer.Write(envelope.AssignedPlayerId);
                writer.Write(envelope.MaxPlayers);
                return stream.ToArray();
            }
        }

        public LobbyAssignmentEnvelope DecodeLobbyAssignment(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out int protocolVersion,
                    out NetworkMessageType messageType);
                RequireMessageType(
                    messageType,
                    NetworkMessageType.LobbyAssignment);

                return new LobbyAssignmentEnvelope(
                    protocolVersion,
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadInt32());
            }
        }

        public byte[] EncodeMatchStart(MatchStartEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    envelope.ProtocolVersion,
                    NetworkMessageType.MatchStart);

                writer.Write(envelope.RoomId);
                writer.Write(envelope.MatchId);
                writer.Write(envelope.PlayerCount);
                writer.Write(envelope.JoinToken);
                return stream.ToArray();
            }
        }

        public MatchStartEnvelope DecodeMatchStart(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out int protocolVersion,
                    out NetworkMessageType messageType);
                RequireMessageType(messageType, NetworkMessageType.MatchStart);

                return new MatchStartEnvelope(
                    protocolVersion,
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadString());
            }
        }

        public byte[] EncodeMatchJoin(MatchJoinEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                WriteHeader(
                    writer,
                    envelope.ProtocolVersion,
                    NetworkMessageType.MatchJoin);

                writer.Write(envelope.RoomId);
                writer.Write(envelope.MatchId);
                writer.Write(envelope.PlayerId);
                writer.Write(envelope.JoinToken);
                return stream.ToArray();
            }
        }

        public MatchJoinEnvelope DecodeMatchJoin(byte[] payload)
        {
            using (BinaryReader reader = CreateReader(payload))
            {
                ReadHeader(
                    reader,
                    out int protocolVersion,
                    out NetworkMessageType messageType);
                RequireMessageType(messageType, NetworkMessageType.MatchJoin);

                return new MatchJoinEnvelope(
                    protocolVersion,
                    reader.ReadString(),
                    reader.ReadString(),
                    reader.ReadInt32(),
                    reader.ReadString());
            }
        }

        private static BinaryReader CreateReader(byte[] payload)
        {
            if (payload == null)
            {
                throw new ArgumentNullException(nameof(payload));
            }

            return new BinaryReader(new MemoryStream(payload));
        }

        private static void WriteHeader(
            BinaryWriter writer,
            int protocolVersion,
            NetworkMessageType messageType)
        {
            writer.Write(Magic);
            writer.Write(protocolVersion);
            writer.Write((byte)messageType);
        }

        private static void ReadHeader(
            BinaryReader reader,
            out int protocolVersion,
            out NetworkMessageType messageType)
        {
            int magic = reader.ReadInt32();
            if (magic != Magic)
            {
                throw new InvalidDataException("Invalid Protect Tree packet magic.");
            }

            protocolVersion = reader.ReadInt32();
            byte rawMessageType = reader.ReadByte();
            if (!Enum.IsDefined(
                typeof(NetworkMessageType),
                (int)rawMessageType))
            {
                throw new InvalidDataException(
                    "Unknown network message type: " + rawMessageType);
            }

            messageType = (NetworkMessageType)rawMessageType;
        }

        private static void RequireMessageType(
            NetworkMessageType actual,
            NetworkMessageType expected)
        {
            if (actual != expected)
            {
                throw new InvalidDataException(
                    $"Expected {expected} payload but found {actual}.");
            }
        }

        private static void WriteCommand(
            BinaryWriter writer,
            MatchCommand command)
        {
            writer.Write((int)command.Type);
            WriteNullableInt(writer, command.PieceInstanceId);
            WriteNullableInt(writer, command.CellId);
            WriteNullableInt(writer, command.ShopSlotIndex);
            WriteNullableString(writer, command.Facing);
            WriteNullableBool(writer, command.IsReady);
        }

        private static MatchCommand ReadCommand(BinaryReader reader)
        {
            MatchCommandType commandType =
                (MatchCommandType)reader.ReadInt32();
            int? pieceInstanceId = ReadNullableInt(reader);
            int? cellId = ReadNullableInt(reader);
            int? shopSlotIndex = ReadNullableInt(reader);
            string facing = ReadNullableString(reader);
            bool? isReady = ReadNullableBool(reader);

            // 解码后仍通过 MatchCommand 工厂方法重建命令，保留原有字段校验。
            switch (commandType)
            {
                case MatchCommandType.SetReady:
                    return MatchCommand.SetReady(
                        RequireValue(isReady, nameof(isReady)));
                case MatchCommandType.PurchaseShopOffer:
                    return MatchCommand.PurchaseShopOffer(
                        RequireValue(shopSlotIndex, nameof(shopSlotIndex)));
                case MatchCommandType.RefreshShop:
                    return MatchCommand.RefreshShop();
                case MatchCommandType.UpgradeShop:
                    return MatchCommand.UpgradeShop();
                case MatchCommandType.DeployPiece:
                    return MatchCommand.DeployPiece(
                        RequireValue(pieceInstanceId, nameof(pieceInstanceId)),
                        RequireValue(cellId, nameof(cellId)));
                case MatchCommandType.BenchPiece:
                    return MatchCommand.BenchPiece(
                        RequireValue(pieceInstanceId, nameof(pieceInstanceId)));
                case MatchCommandType.SellPiece:
                    return MatchCommand.SellPiece(
                        RequireValue(pieceInstanceId, nameof(pieceInstanceId)));
                case MatchCommandType.SetPieceFacing:
                    return MatchCommand.SetPieceFacing(
                        RequireValue(pieceInstanceId, nameof(pieceInstanceId)),
                        RequireString(facing, nameof(facing)));
                case MatchCommandType.ToggleShopLock:
                    return MatchCommand.ToggleShopLock();
                case MatchCommandType.PlacePiece:
                    return MatchCommand.PlacePiece(
                        RequireValue(pieceInstanceId, nameof(pieceInstanceId)),
                        RequireValue(cellId, nameof(cellId)),
                        facing);
                case MatchCommandType.RequestSnapshot:
                    return MatchCommand.RequestSnapshot();
                default:
                    throw new InvalidDataException(
                        "Unknown match command type: " + commandType);
            }
        }

        private static void WriteLobbyCommand(
            BinaryWriter writer,
            LobbyCommand command)
        {
            writer.Write((int)command.Type);
            WriteNullableBool(writer, command.IsReady);
            WriteNullableString(writer, command.DisplayName);
            WriteNullableString(writer, command.AvatarResourcePath);
        }

        private static LobbyCommand ReadLobbyCommand(BinaryReader reader)
        {
            LobbyCommandType commandType =
                (LobbyCommandType)reader.ReadInt32();
            bool? isReady = ReadNullableBool(reader);
            string displayName = ReadNullableString(reader);
            string avatarResourcePath = ReadNullableString(reader);

            switch (commandType)
            {
                case LobbyCommandType.SetReady:
                    return LobbyCommand.SetReady(
                        RequireValue(isReady, nameof(isReady)));
                case LobbyCommandType.SetDisplayName:
                    return LobbyCommand.SetDisplayName(
                        RequireString(displayName, nameof(displayName)));
                case LobbyCommandType.SetAvatar:
                    return LobbyCommand.SetAvatar(
                        RequireString(
                            avatarResourcePath,
                            nameof(avatarResourcePath)));
                default:
                    throw new InvalidDataException(
                        "Unknown lobby command type: " + commandType);
            }
        }

        private static void WriteMatchStateSnapshot(
            BinaryWriter writer,
            MatchStateSnapshot snapshot)
        {
            writer.Write(snapshot.RecipientPlayerId);
            writer.Write(snapshot.SimulationTick);

            // 这里的字段顺序就是当前协议契约；以后快照加字段时，需要同步提升协议版本。
            WriteMatchFlowSnapshot(writer, snapshot.Flow);
            WriteEnemyRosterSnapshot(writer, snapshot.Enemies);
            WritePieceRosterSnapshot(writer, snapshot.Pieces);
            WritePlayerRosterSnapshot(writer, snapshot.Players);
            WriteShopSnapshot(writer, snapshot.Shop);
            WriteList(writer, snapshot.Events, WriteMatchEvent);
        }

        private static MatchStateSnapshot ReadMatchStateSnapshot(
            BinaryReader reader)
        {
            int recipientPlayerId = reader.ReadInt32();
            long simulationTick = reader.ReadInt64();
            MatchFlowSnapshot flow = ReadMatchFlowSnapshot(reader);
            EnemyRosterSnapshot enemies = ReadEnemyRosterSnapshot(reader);
            PieceRosterSnapshot pieces = ReadPieceRosterSnapshot(reader);
            PlayerRosterSnapshot players = ReadPlayerRosterSnapshot(reader);
            ShopSnapshot shop = ReadShopSnapshot(reader);
            List<MatchEvent> events = ReadList(reader, ReadMatchEvent);

            return new MatchStateSnapshot(
                recipientPlayerId,
                simulationTick,
                flow,
                enemies,
                pieces,
                players,
                shop,
                events);
        }

        private static void WriteMatchEvent(
            BinaryWriter writer,
            MatchEvent matchEvent)
        {
            WriteNullableString(writer, matchEvent.Type);
            WriteNullableInt(writer, matchEvent.Wave);
            WriteNullableString(writer, matchEvent.Phase);
            WriteNullableString(writer, matchEvent.EnemyId);
            WriteNullableInt(writer, matchEvent.SpawnIndex);
            WriteNullableInt(writer, matchEvent.EnemyInstanceId);
            WriteNullableString(writer, matchEvent.Result);
            WriteNullableInt(writer, matchEvent.PieceInstanceId);
            WriteNullableInt(writer, matchEvent.SourcePieceInstanceId);
            WriteNullableInt(writer, matchEvent.SourceEnemyInstanceId);
            WriteNullableInt(writer, matchEvent.PlayerId);
            WriteNullableInt(writer, matchEvent.TargetPlayerId);
            WriteNullableInt(writer, matchEvent.DefenderPlayerId);
            WriteNullableInt(writer, matchEvent.LeakOwnerPlayerId);
            WriteNullableInt(writer, matchEvent.InitialLeakCount);
            WriteNullableInt(writer, matchEvent.RescuedCount);
            WriteNullableInt(writer, matchEvent.FinalLeakCount);
            WriteNullableInt(writer, matchEvent.Damage);
            WriteNullableInt(writer, matchEvent.LeakCount);
            WriteNullableInt(writer, matchEvent.Health);
            WriteNullableInt(writer, matchEvent.LeakingPlayerCount);
            WriteNullableInt(writer, matchEvent.TransferredEnemyCount);
            WriteNullableInt(writer, matchEvent.PreviousTargetPlayerId);
            WriteNullableInt(writer, matchEvent.MaxHealth);
            WriteNullableBool(writer, matchEvent.IsBoss);
            WriteNullableString(writer, matchEvent.ProjectileId);
            WriteNullableDouble(writer, matchEvent.CastLockSeconds);
        }

        private static MatchEvent ReadMatchEvent(BinaryReader reader)
        {
            return new MatchEvent(
                ReadNullableString(reader),
                ReadNullableInt(reader),
                ReadNullableString(reader),
                ReadNullableString(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableString(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableInt(reader),
                ReadNullableBool(reader),
                ReadNullableString(reader),
                ReadNullableDouble(reader));
        }

        private static void WriteMatchFlowSnapshot(
            BinaryWriter writer,
            MatchFlowSnapshot snapshot)
        {
            WriteNullableString(writer, snapshot.Phase);
            writer.Write(snapshot.Wave);
            writer.Write(snapshot.RemainingSeconds);
            writer.Write(snapshot.IsFinished);
            WriteNullableString(writer, snapshot.Result);
        }

        private static MatchFlowSnapshot ReadMatchFlowSnapshot(
            BinaryReader reader)
        {
            return new MatchFlowSnapshot(
                ReadNullableString(reader),
                reader.ReadInt32(),
                reader.ReadDouble(),
                reader.ReadBoolean(),
                ReadNullableString(reader));
        }

        private static void WriteEnemyRosterSnapshot(
            BinaryWriter writer,
            EnemyRosterSnapshot snapshot)
        {
            writer.Write(snapshot.AliveCount);
            WriteList(writer, snapshot.Enemies, WriteEnemySnapshot);
        }

        private static EnemyRosterSnapshot ReadEnemyRosterSnapshot(
            BinaryReader reader)
        {
            return new EnemyRosterSnapshot(
                reader.ReadInt32(),
                ReadList(reader, ReadEnemySnapshot));
        }

        private static void WriteEnemySnapshot(
            BinaryWriter writer,
            EnemySnapshot snapshot)
        {
            writer.Write(snapshot.InstanceId);
            WriteNullableString(writer, snapshot.EnemyId);
            writer.Write(snapshot.Wave);
            writer.Write(snapshot.SpawnIndex);
            writer.Write(snapshot.TargetPlayerId);
            writer.Write(snapshot.Health);
            writer.Write(snapshot.MaxHealth);
            WriteNullableString(writer, snapshot.Status);
            writer.Write(snapshot.AttackDamage);
            writer.Write(snapshot.AttackIntervalSeconds);
            WriteNullableString(writer, snapshot.AttackType);
            WriteNullableString(writer, snapshot.AttackSfxId);
            writer.Write(snapshot.IsBoss);
            writer.Write(snapshot.IsEnraged);
            writer.Write(snapshot.RouteId);
            writer.Write(snapshot.PathSpeed);
            writer.Write(snapshot.PathProgress);
            WriteNullableInt(writer, snapshot.BlockedByPieceInstanceId);
        }

        private static EnemySnapshot ReadEnemySnapshot(BinaryReader reader)
        {
            return new EnemySnapshot(
                reader.ReadInt32(),
                ReadNullableString(reader),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadNullableString(reader),
                reader.ReadInt32(),
                reader.ReadDouble(),
                ReadNullableString(reader),
                ReadNullableString(reader),
                reader.ReadBoolean(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadDouble(),
                reader.ReadDouble(),
                ReadNullableInt(reader));
        }

        private static void WritePieceRosterSnapshot(
            BinaryWriter writer,
            PieceRosterSnapshot snapshot)
        {
            WriteList(writer, snapshot.Pieces, WritePieceSnapshot);
            WriteList(writer, snapshot.Players, WritePieceCapacitySnapshot);
            WriteList(writer, snapshot.ActiveSynergies, WriteActiveSynergySnapshot);
            WriteList(writer, snapshot.SynergyProgresses, WriteSynergyProgressSnapshot);
        }

        private static PieceRosterSnapshot ReadPieceRosterSnapshot(
            BinaryReader reader)
        {
            return new PieceRosterSnapshot(
                ReadList(reader, ReadPieceSnapshot),
                ReadList(reader, ReadPieceCapacitySnapshot),
                ReadList(reader, ReadActiveSynergySnapshot),
                ReadList(reader, ReadSynergyProgressSnapshot));
        }

        private static void WritePieceSnapshot(
            BinaryWriter writer,
            PieceSnapshot snapshot)
        {
            writer.Write(snapshot.InstanceId);
            WriteNullableString(writer, snapshot.PieceId);
            writer.Write(snapshot.OwnerPlayerId);
            writer.Write(snapshot.Level);
            WriteNullableString(writer, snapshot.Location);
            WriteNullableInt(writer, snapshot.CellId);
            WriteNullableString(writer, snapshot.Terrain);
            WriteNullableString(writer, snapshot.Facing);
            writer.Write(snapshot.Health);
            writer.Write(snapshot.MaxHealth);
            WriteNullableString(writer, snapshot.Status);
            writer.Write(snapshot.MaxBlockCount);
            WriteIntList(writer, snapshot.BlockedEnemyInstanceIds);
            writer.Write(snapshot.RecoverySecondsRemaining);
            writer.Write(snapshot.BaseDamage);
            writer.Write(snapshot.Damage);
            writer.Write(snapshot.AttackIntervalSeconds);
            writer.Write(snapshot.SellValue);
            WriteStringList(writer, snapshot.DeployableTerrains);
            WriteNullableString(writer, snapshot.DisplayName);
            WriteNullableString(writer, snapshot.Portrait);
            WriteNullableString(writer, snapshot.ClassId);
            WriteNullableString(writer, snapshot.AttackSfxId);
            writer.Write(snapshot.Rarity);
            WriteList(writer, snapshot.Synergies, WriteShopSynergySnapshot);
            WriteList(
                writer,
                snapshot.AttackRange,
                WritePieceAttackRangeOffsetSnapshot);
            WriteNullableString(writer, snapshot.FeatureDescription);
        }

        private static PieceSnapshot ReadPieceSnapshot(BinaryReader reader)
        {
            int instanceId = reader.ReadInt32();
            string pieceId = ReadNullableString(reader);
            int ownerPlayerId = reader.ReadInt32();
            int level = reader.ReadInt32();
            string location = ReadNullableString(reader);
            int? cellId = ReadNullableInt(reader);
            string terrain = ReadNullableString(reader);
            string facing = ReadNullableString(reader);
            int health = reader.ReadInt32();
            int maxHealth = reader.ReadInt32();
            string status = ReadNullableString(reader);
            int maxBlockCount = reader.ReadInt32();
            List<int> blockedEnemyInstanceIds = ReadIntList(reader);
            double recoverySecondsRemaining = reader.ReadDouble();
            int baseDamage = reader.ReadInt32();
            int damage = reader.ReadInt32();
            double attackIntervalSeconds = reader.ReadDouble();
            int sellValue = reader.ReadInt32();
            List<string> deployableTerrains = ReadStringList(reader);
            string displayName = ReadNullableString(reader);
            string portrait = ReadNullableString(reader);
            string classId = ReadNullableString(reader);
            string attackSfxId = ReadNullableString(reader);
            int rarity = reader.ReadInt32();
            List<ShopSynergySnapshot> synergies =
                ReadList(reader, ReadShopSynergySnapshot);
            List<PieceAttackRangeOffsetSnapshot> attackRange =
                ReadList(reader, ReadPieceAttackRangeOffsetSnapshot);
            string featureDescription = ReadNullableString(reader);

            return new PieceSnapshot(
                instanceId,
                pieceId,
                ownerPlayerId,
                level,
                location,
                cellId,
                terrain,
                facing,
                health,
                maxHealth,
                status,
                maxBlockCount,
                blockedEnemyInstanceIds,
                recoverySecondsRemaining,
                baseDamage,
                damage,
                attackIntervalSeconds,
                sellValue,
                deployableTerrains,
                displayName,
                portrait,
                classId,
                attackSfxId,
                rarity,
                synergies,
                attackRange,
                featureDescription);
        }

        private static void WritePieceAttackRangeOffsetSnapshot(
            BinaryWriter writer,
            PieceAttackRangeOffsetSnapshot snapshot)
        {
            writer.Write(snapshot.Forward);
            writer.Write(snapshot.Right);
        }

        private static PieceAttackRangeOffsetSnapshot ReadPieceAttackRangeOffsetSnapshot(
            BinaryReader reader)
        {
            return new PieceAttackRangeOffsetSnapshot(
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        private static void WritePieceCapacitySnapshot(
            BinaryWriter writer,
            PieceCapacitySnapshot snapshot)
        {
            writer.Write(snapshot.PlayerId);
            writer.Write(snapshot.BenchCount);
            writer.Write(snapshot.BenchCapacity);
            writer.Write(snapshot.BoardCount);
            writer.Write(snapshot.DeploymentLimit);
            writer.Write(snapshot.TemporaryBenchCount);
            writer.Write(snapshot.TemporaryBenchCapacity);
        }

        private static PieceCapacitySnapshot ReadPieceCapacitySnapshot(
            BinaryReader reader)
        {
            return new PieceCapacitySnapshot(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        private static void WriteActiveSynergySnapshot(
            BinaryWriter writer,
            ActiveSynergySnapshot snapshot)
        {
            writer.Write(snapshot.PlayerId);
            WriteNullableString(writer, snapshot.SynergyId);
            writer.Write(snapshot.Level);
            writer.Write(snapshot.LayerCount);
            writer.Write(snapshot.UniquePieceCount);
            writer.Write(snapshot.RequiredUniquePieces);
            writer.Write(snapshot.DamageBonus);
            WriteNullableString(writer, snapshot.EffectDescription);
        }

        private static ActiveSynergySnapshot ReadActiveSynergySnapshot(
            BinaryReader reader)
        {
            return new ActiveSynergySnapshot(
                reader.ReadInt32(),
                ReadNullableString(reader),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadNullableString(reader));
        }

        private static void WriteSynergyProgressSnapshot(
            BinaryWriter writer,
            SynergyProgressSnapshot snapshot)
        {
            writer.Write(snapshot.PlayerId);
            WriteNullableString(writer, snapshot.SynergyId);
            WriteNullableString(writer, snapshot.DisplayName);
            writer.Write(snapshot.Level);
            writer.Write(snapshot.LayerCount);
            writer.Write(snapshot.UniquePieceCount);
            writer.Write(snapshot.RequiredUniquePieces);
            writer.Write(snapshot.DamageBonus);
            WriteNullableString(writer, snapshot.EffectDescription);
        }

        private static SynergyProgressSnapshot ReadSynergyProgressSnapshot(
            BinaryReader reader)
        {
            return new SynergyProgressSnapshot(
                reader.ReadInt32(),
                ReadNullableString(reader),
                ReadNullableString(reader),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadNullableString(reader));
        }

        private static void WritePlayerRosterSnapshot(
            BinaryWriter writer,
            PlayerRosterSnapshot snapshot)
        {
            writer.Write(snapshot.AliveCount);
            WriteList(writer, snapshot.Players, WritePlayerSnapshot);
        }

        private static PlayerRosterSnapshot ReadPlayerRosterSnapshot(
            BinaryReader reader)
        {
            return new PlayerRosterSnapshot(
                reader.ReadInt32(),
                ReadList(reader, ReadPlayerSnapshot));
        }

        private static void WritePlayerSnapshot(
            BinaryWriter writer,
            PlayerSnapshot snapshot)
        {
            writer.Write(snapshot.PlayerId);
            writer.Write(snapshot.Health);
            writer.Write(snapshot.MaxHealth);
            writer.Write(snapshot.Gold);
            WriteNullableString(writer, snapshot.Status);
            writer.Write(snapshot.IsReady);
        }

        private static PlayerSnapshot ReadPlayerSnapshot(BinaryReader reader)
        {
            return new PlayerSnapshot(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadNullableString(reader),
                reader.ReadBoolean());
        }

        private static void WriteShopSnapshot(
            BinaryWriter writer,
            ShopSnapshot snapshot)
        {
            writer.Write(snapshot.PlayerId);
            writer.Write(snapshot.Level);
            writer.Write(snapshot.MaxLevel);
            writer.Write(snapshot.CanUpgrade);
            writer.Write(snapshot.UpgradeCost);
            writer.Write(snapshot.RefreshCost);
            writer.Write(snapshot.IsLocked);
            WriteList(writer, snapshot.RarityWeights, WriteShopRarityWeightSnapshot);
            WriteList(writer, snapshot.Offers, WriteShopOfferSnapshot);
        }

        private static ShopSnapshot ReadShopSnapshot(BinaryReader reader)
        {
            return new ShopSnapshot(
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadBoolean(),
                ReadList(reader, ReadShopRarityWeightSnapshot),
                ReadList(reader, ReadShopOfferSnapshot));
        }

        private static void WriteShopRarityWeightSnapshot(
            BinaryWriter writer,
            ShopRarityWeightSnapshot snapshot)
        {
            writer.Write(snapshot.Rarity);
            writer.Write(snapshot.Weight);
        }

        private static ShopRarityWeightSnapshot ReadShopRarityWeightSnapshot(
            BinaryReader reader)
        {
            return new ShopRarityWeightSnapshot(
                reader.ReadInt32(),
                reader.ReadInt32());
        }

        private static void WriteShopOfferSnapshot(
            BinaryWriter writer,
            ShopOfferSnapshot snapshot)
        {
            writer.Write(snapshot.SlotIndex);
            WriteNullableString(writer, snapshot.PieceId);
            WriteNullableString(writer, snapshot.DisplayName);
            WriteNullableString(writer, snapshot.Portrait);
            WriteNullableString(writer, snapshot.ClassId);
            writer.Write(snapshot.Rarity);
            writer.Write(snapshot.Cost);
            WriteList(writer, snapshot.Synergies, WriteShopSynergySnapshot);
            writer.Write(snapshot.IsSold);
            writer.Write(snapshot.MaxHealth);
            writer.Write(snapshot.MaxBlockCount);
            writer.Write(snapshot.Damage);
            writer.Write(snapshot.AttackIntervalSeconds);
            WriteNullableString(writer, snapshot.FeatureDescription);
        }

        private static ShopOfferSnapshot ReadShopOfferSnapshot(
            BinaryReader reader)
        {
            return new ShopOfferSnapshot(
                reader.ReadInt32(),
                ReadNullableString(reader),
                ReadNullableString(reader),
                ReadNullableString(reader),
                ReadNullableString(reader),
                reader.ReadInt32(),
                reader.ReadInt32(),
                ReadList(reader, ReadShopSynergySnapshot),
                reader.ReadBoolean(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadInt32(),
                reader.ReadDouble(),
                ReadNullableString(reader));
        }

        private static void WriteShopSynergySnapshot(
            BinaryWriter writer,
            ShopSynergySnapshot snapshot)
        {
            WriteNullableString(writer, snapshot.SynergyId);
            WriteNullableString(writer, snapshot.DisplayName);
        }

        private static ShopSynergySnapshot ReadShopSynergySnapshot(
            BinaryReader reader)
        {
            return new ShopSynergySnapshot(
                ReadNullableString(reader),
                ReadNullableString(reader));
        }

        private static void WriteNullableInt(BinaryWriter writer, int? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        private static int? ReadNullableInt(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadInt32() : (int?)null;
        }

        private static void WriteNullableBool(BinaryWriter writer, bool? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        private static bool? ReadNullableBool(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadBoolean() : (bool?)null;
        }

        private static void WriteNullableDouble(BinaryWriter writer, double? value)
        {
            writer.Write(value.HasValue);
            if (value.HasValue)
            {
                writer.Write(value.Value);
            }
        }

        private static double? ReadNullableDouble(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadDouble() : (double?)null;
        }

        private static void WriteNullableString(
            BinaryWriter writer,
            string value)
        {
            writer.Write(value != null);
            if (value != null)
            {
                writer.Write(value);
            }
        }

        private static string ReadNullableString(BinaryReader reader)
        {
            return reader.ReadBoolean() ? reader.ReadString() : null;
        }

        private static void WriteIntList(
            BinaryWriter writer,
            IReadOnlyList<int> values)
        {
            writer.Write(values == null ? 0 : values.Count);
            if (values == null)
            {
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                writer.Write(values[index]);
            }
        }

        private static List<int> ReadIntList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<int> values = new List<int>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(reader.ReadInt32());
            }

            return values;
        }

        private static void WriteStringList(
            BinaryWriter writer,
            IReadOnlyList<string> values)
        {
            writer.Write(values == null ? 0 : values.Count);
            if (values == null)
            {
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                WriteNullableString(writer, values[index]);
            }
        }

        private static List<string> ReadStringList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            List<string> values = new List<string>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(ReadNullableString(reader));
            }

            return values;
        }

        private static void WriteList<T>(
            BinaryWriter writer,
            IReadOnlyList<T> values,
            Action<BinaryWriter, T> writeItem)
        {
            writer.Write(values == null ? 0 : values.Count);
            if (values == null)
            {
                return;
            }

            for (int index = 0; index < values.Count; index++)
            {
                writeItem(writer, values[index]);
            }
        }

        private static List<T> ReadList<T>(
            BinaryReader reader,
            Func<BinaryReader, T> readItem)
        {
            int count = reader.ReadInt32();
            List<T> values = new List<T>(count);
            for (int index = 0; index < count; index++)
            {
                values.Add(readItem(reader));
            }

            return values;
        }

        private static T RequireValue<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new InvalidDataException(
                    fieldName + " is required for this command.");
            }

            return value.Value;
        }

        private static string RequireString(string value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidDataException(
                    fieldName + " is required for this command.");
            }

            return value;
        }
    }
}
