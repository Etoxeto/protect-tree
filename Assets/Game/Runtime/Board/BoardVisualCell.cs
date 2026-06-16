using UnityEngine;

namespace ProtectTree.Runtime.Board
{
    /// <summary>
    /// C# 表现层使用的静态格子数据，不负责部署、占用或路线规则判定。
    /// </summary>
    public sealed class BoardVisualCell
    {
        public BoardVisualCell(
            int cellId,
            int x,
            int y,
            int height,
            BoardTerrainType terrain,
            string visualKey = "",
            string zone = "",
            bool allowsBattleDeployment = false,
            bool acceptsReservePiece = false)
        {
            CellId = cellId;
            Coord = new Vector2Int(x, y);
            Height = height;
            Terrain = terrain;
            VisualKey = visualKey ?? string.Empty;
            Zone = zone ?? string.Empty;
            AllowsBattleDeployment = allowsBattleDeployment;
            AcceptsReservePiece = acceptsReservePiece;
        }

        public int CellId { get; }
        public Vector2Int Coord { get; }
        public int Height { get; }
        public BoardTerrainType Terrain { get; }
        public string VisualKey { get; }
        public string Zone { get; }
        public bool AllowsBattleDeployment { get; }
        public bool AcceptsReservePiece { get; }
        public int X => Coord.x;
        public int Y => Coord.y;
    }
}
