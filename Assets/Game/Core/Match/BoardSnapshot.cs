using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class BoardSnapshot
    {
        public BoardSnapshot(
            string boardId,
            string boardKind,
            int width,
            int height,
            int originGridX,
            int originGridY,
            int reserveCapacity,
            int temporaryReserveCapacity,
            IReadOnlyList<BoardCellSnapshot> cells,
            IReadOnlyList<BoardRouteSnapshot> routes)
        {
            BoardId = boardId;
            BoardKind = boardKind;
            Width = width;
            Height = height;
            OriginGridX = originGridX;
            OriginGridY = originGridY;
            ReserveCapacity = reserveCapacity;
            TemporaryReserveCapacity = temporaryReserveCapacity;
            Cells = cells;
            Routes = routes;
        }

        public string BoardId { get; }

        public string BoardKind { get; }

        public int Width { get; }

        public int Height { get; }

        public int OriginGridX { get; }

        public int OriginGridY { get; }

        public int ReserveCapacity { get; }

        public int TemporaryReserveCapacity { get; }

        public IReadOnlyList<BoardCellSnapshot> Cells { get; }

        public IReadOnlyList<BoardRouteSnapshot> Routes { get; }
    }
}
