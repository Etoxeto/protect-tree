namespace ProtectTree.Core.Match
{
    public sealed class BoardRouteSampleSnapshot
    {
        public BoardRouteSampleSnapshot(
            int cellId,
            int gridX,
            int gridY,
            double pathProgress)
        {
            CellId = cellId;
            GridX = gridX;
            GridY = gridY;
            PathProgress = pathProgress;
        }

        public int CellId { get; }

        public int GridX { get; }

        public int GridY { get; }

        public double PathProgress { get; }
    }
}
