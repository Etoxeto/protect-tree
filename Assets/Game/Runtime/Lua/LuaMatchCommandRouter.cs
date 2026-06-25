using System;
using ProtectTree.Core.Network;

namespace ProtectTree.Runtime.Lua
{
    /// <summary>
    /// Converts accepted network protocol commands into existing Lua authority calls.
    /// </summary>
    public sealed class LuaMatchCommandRouter
    {
        private readonly LuaRuntime _runtime;
        private readonly HostCommandGate _commandGate;

        public LuaMatchCommandRouter(LuaRuntime runtime, HostCommandGate commandGate)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _commandGate = commandGate
                ?? throw new ArgumentNullException(nameof(commandGate));
        }

        public Exception LastGameplayException { get; private set; }

        public bool TryRoute(
            int assignedPlayerId,
            PlayerCommandEnvelope envelope,
            out CommandRejectionReason rejectionReason)
        {
            LastGameplayException = null;

            if (!_commandGate.TryAccept(
                assignedPlayerId,
                envelope,
                out rejectionReason))
            {
                return false;
            }

            // 网络层只负责身份与顺序；真正的费用、阶段、归属和容量仍交给 Lua 权威判断。
            try
            {
                Apply(envelope.PlayerId, envelope.Command);
            }
            catch (Exception exception)
            {
                // 能通过协议门的命令仍可能被 Lua 权威以玩法规则拒绝；主机不能因此崩溃。
                LastGameplayException = exception;
                rejectionReason = CommandRejectionReason.GameplayRejected;
                return false;
            }

            rejectionReason = CommandRejectionReason.None;
            return true;
        }

        public void Apply(int playerId, MatchCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            switch (command.Type)
            {
                case MatchCommandType.SetReady:
                    _runtime.SetPlayerReady(
                        playerId,
                        RequireValue(command.IsReady, nameof(command.IsReady)));
                    break;

                case MatchCommandType.PurchaseShopOffer:
                    _runtime.PurchaseShopOffer(
                        playerId,
                        RequireValue(
                            command.ShopSlotIndex,
                            nameof(command.ShopSlotIndex)));
                    break;

                case MatchCommandType.RefreshShop:
                    _runtime.RefreshShop(playerId);
                    break;

                case MatchCommandType.UpgradeShop:
                    _runtime.UpgradeShop(playerId);
                    break;

                case MatchCommandType.DeployPiece:
                    _runtime.DeployPiece(
                        playerId,
                        RequireValue(
                            command.PieceInstanceId,
                            nameof(command.PieceInstanceId)),
                        RequireValue(command.CellId, nameof(command.CellId)));
                    break;

                case MatchCommandType.BenchPiece:
                    _runtime.BenchPiece(
                        playerId,
                        RequireValue(
                            command.PieceInstanceId,
                            nameof(command.PieceInstanceId)));
                    break;

                case MatchCommandType.SellPiece:
                    _runtime.SellPiece(
                        playerId,
                        RequireValue(
                            command.PieceInstanceId,
                            nameof(command.PieceInstanceId)));
                    break;

                case MatchCommandType.SetPieceFacing:
                    _runtime.SetPieceFacing(
                        playerId,
                        RequireValue(
                            command.PieceInstanceId,
                            nameof(command.PieceInstanceId)),
                        RequireFacing(command.Facing));
                    break;

                case MatchCommandType.ToggleShopLock:
                    _runtime.ToggleShopLock(playerId);
                    break;

                case MatchCommandType.PlacePiece:
                    _runtime.PlacePiece(
                        playerId,
                        RequireValue(
                            command.PieceInstanceId,
                            nameof(command.PieceInstanceId)),
                        RequireValue(command.CellId, nameof(command.CellId)),
                        command.Facing);
                    break;

                case MatchCommandType.RequestSnapshot:
                    // 仅用于建立局内连接身份并请求 Host 回传快照，不改变 Lua 权威玩法状态。
                    break;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(command),
                        command.Type,
                        "Unsupported match command type.");
            }
        }

        private static T RequireValue<T>(T? value, string fieldName)
            where T : struct
        {
            if (!value.HasValue)
            {
                throw new ArgumentException(
                    $"{fieldName} is required for this command.",
                    fieldName);
            }

            return value.Value;
        }

        private static string RequireFacing(string facing)
        {
            if (string.IsNullOrWhiteSpace(facing))
            {
                throw new ArgumentException(
                    "Facing is required for this command.",
                    nameof(facing));
            }

            return facing;
        }
    }
}
