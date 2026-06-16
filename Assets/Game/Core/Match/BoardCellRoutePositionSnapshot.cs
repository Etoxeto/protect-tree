namespace ProtectTree.Core.Match
{
    public sealed class BoardCellRoutePositionSnapshot
    {
        public BoardCellRoutePositionSnapshot(int routeId, double pathProgress)
        {
            RouteId = routeId;
            PathProgress = pathProgress;
        }

        public int RouteId { get; }

        public double PathProgress { get; }
    }
}
