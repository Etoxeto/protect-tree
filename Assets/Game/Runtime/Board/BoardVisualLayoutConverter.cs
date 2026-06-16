using System;
using System.Collections.Generic;
using ProtectTree.Core.Match;

namespace ProtectTree.Runtime.Board
{
    public static class BoardVisualLayoutConverter
    {
        public static BoardVisualLayout Create(BoardSnapshot snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            List<BoardVisualCell> cells =
                new List<BoardVisualCell>(snapshot.Cells.Count);

            foreach (BoardCellSnapshot cell in snapshot.Cells)
            {
                // 这里只转换权威棋盘数据，不在表现层判断格子能否部署或参与战斗。
                cells.Add(new BoardVisualCell(
                    cell.CellId,
                    cell.VisualX,
                    cell.VisualY,
                    cell.VisualHeight,
                    ParseTerrain(cell.Terrain),
                    cell.VisualKey,
                    cell.Zone,
                    cell.AllowsBattleDeployment,
                    cell.AcceptsReservePiece));
            }

            return new BoardVisualLayout(snapshot.Width, snapshot.Height, cells);
        }

        private static BoardTerrainType ParseTerrain(string terrain)
        {
            switch (terrain)
            {
                case "Ground":
                    return BoardTerrainType.Ground;
                case "HighGround":
                    return BoardTerrainType.HighGround;
                case "Obstacle":
                    return BoardTerrainType.Obstacle;
                default:
                    throw new ArgumentException(
                        $"Unknown authority board terrain: {terrain ?? "<null>"}.",
                        nameof(terrain));
            }
        }
    }
}
