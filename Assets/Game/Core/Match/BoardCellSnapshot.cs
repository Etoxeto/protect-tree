using System;
using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class BoardCellSnapshot
    {
        public BoardCellSnapshot(
            int cellId,
            string terrain,
            string zone,
            int gridX,
            int gridY,
            int visualX,
            int visualY,
            int visualHeight,
            string visualKey,
            bool allowsBattleDeployment,
            bool acceptsReservePiece,
            bool acceptsOverflowPiece,
            bool autoSellOnBattleStart,
            bool allowsEnemyRoute,
            IReadOnlyList<BoardCellRoutePositionSnapshot> routePositions)
        {
            CellId = cellId;
            Terrain = terrain;
            Zone = zone;
            GridX = gridX;
            GridY = gridY;
            VisualX = visualX;
            VisualY = visualY;
            VisualHeight = visualHeight;
            VisualKey = visualKey;
            AllowsBattleDeployment = allowsBattleDeployment;
            AcceptsReservePiece = acceptsReservePiece;
            AcceptsOverflowPiece = acceptsOverflowPiece;
            AutoSellOnBattleStart = autoSellOnBattleStart;
            AllowsEnemyRoute = allowsEnemyRoute;
            RoutePositions = routePositions
                ?? throw new ArgumentNullException(nameof(routePositions));
        }

        public int CellId { get; }

        public string Terrain { get; }

        public string Zone { get; }

        public int GridX { get; }

        public int GridY { get; }

        public int VisualX { get; }

        public int VisualY { get; }

        public int VisualHeight { get; }

        public string VisualKey { get; }

        public bool AllowsBattleDeployment { get; }

        public bool AcceptsReservePiece { get; }

        public bool AcceptsOverflowPiece { get; }

        public bool AutoSellOnBattleStart { get; }

        public bool AllowsEnemyRoute { get; }

        public IReadOnlyList<BoardCellRoutePositionSnapshot> RoutePositions { get; }

        public BoardCellRoutePositionSnapshot GetRoutePosition(int routeId)
        {
            foreach (BoardCellRoutePositionSnapshot position in RoutePositions)
            {
                if (position.RouteId == routeId)
                {
                    return position;
                }
            }

            return null;
        }
    }
}
