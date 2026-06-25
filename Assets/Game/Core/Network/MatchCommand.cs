using System;

namespace ProtectTree.Core.Network
{
    public sealed class MatchCommand
    {
        private MatchCommand(
            MatchCommandType type,
            int? pieceInstanceId = null,
            int? cellId = null,
            int? shopSlotIndex = null,
            string facing = null,
            bool? isReady = null)
        {
            Type = type;
            PieceInstanceId = pieceInstanceId;
            CellId = cellId;
            ShopSlotIndex = shopSlotIndex;
            Facing = facing;
            IsReady = isReady;
        }

        public MatchCommandType Type { get; }

        public int? PieceInstanceId { get; }

        public int? CellId { get; }

        public int? ShopSlotIndex { get; }

        public string Facing { get; }

        public bool? IsReady { get; }

        public static MatchCommand SetReady(bool isReady)
        {
            return new MatchCommand(MatchCommandType.SetReady, isReady: isReady);
        }

        public static MatchCommand PurchaseShopOffer(int shopSlotIndex)
        {
            RequirePositive(shopSlotIndex, nameof(shopSlotIndex));
            return new MatchCommand(
                MatchCommandType.PurchaseShopOffer,
                shopSlotIndex: shopSlotIndex);
        }

        public static MatchCommand RefreshShop()
        {
            return new MatchCommand(MatchCommandType.RefreshShop);
        }

        public static MatchCommand UpgradeShop()
        {
            return new MatchCommand(MatchCommandType.UpgradeShop);
        }

        public static MatchCommand ToggleShopLock()
        {
            return new MatchCommand(MatchCommandType.ToggleShopLock);
        }

        public static MatchCommand RequestSnapshot()
        {
            return new MatchCommand(MatchCommandType.RequestSnapshot);
        }

        public static MatchCommand DeployPiece(int pieceInstanceId, int cellId)
        {
            RequirePositive(pieceInstanceId, nameof(pieceInstanceId));
            RequirePositive(cellId, nameof(cellId));
            return new MatchCommand(
                MatchCommandType.DeployPiece,
                pieceInstanceId,
                cellId);
        }

        public static MatchCommand BenchPiece(int pieceInstanceId)
        {
            RequirePositive(pieceInstanceId, nameof(pieceInstanceId));
            return new MatchCommand(
                MatchCommandType.BenchPiece,
                pieceInstanceId);
        }

        public static MatchCommand PlacePiece(
            int pieceInstanceId,
            int cellId,
            string facing = null)
        {
            RequirePositive(pieceInstanceId, nameof(pieceInstanceId));
            RequirePositive(cellId, nameof(cellId));
            return new MatchCommand(
                MatchCommandType.PlacePiece,
                pieceInstanceId,
                cellId,
                facing: facing);
        }

        public static MatchCommand SellPiece(int pieceInstanceId)
        {
            RequirePositive(pieceInstanceId, nameof(pieceInstanceId));
            return new MatchCommand(
                MatchCommandType.SellPiece,
                pieceInstanceId);
        }

        public static MatchCommand SetPieceFacing(int pieceInstanceId, string facing)
        {
            RequirePositive(pieceInstanceId, nameof(pieceInstanceId));
            if (string.IsNullOrWhiteSpace(facing))
            {
                throw new ArgumentException("Facing is required.", nameof(facing));
            }

            return new MatchCommand(
                MatchCommandType.SetPieceFacing,
                pieceInstanceId,
                facing: facing);
        }

        private static void RequirePositive(int value, string parameterName)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }
    }
}
