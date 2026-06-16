using System.Collections.Generic;

namespace ProtectTree.Core.Match
{
    public sealed class BoardRouteSnapshot
    {
        public BoardRouteSnapshot(
            int routeId,
            double startProgress,
            double endpointProgress,
            IReadOnlyList<BoardRouteSampleSnapshot> samples)
        {
            RouteId = routeId;
            StartProgress = startProgress;
            EndpointProgress = endpointProgress;
            Samples = samples;
        }

        public int RouteId { get; }

        public double StartProgress { get; }

        public double EndpointProgress { get; }

        public IReadOnlyList<BoardRouteSampleSnapshot> Samples { get; }
    }
}
