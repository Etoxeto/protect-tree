namespace ProtectTree.Core.Match
{
    public sealed class PieceCapacitySnapshot
    {
        public PieceCapacitySnapshot(
            int playerId,
            int benchCount,
            int benchCapacity,
            int boardCount,
            int deploymentLimit,
            int temporaryBenchCount = 0,
            int temporaryBenchCapacity = 0)
        {
            PlayerId = playerId;
            BenchCount = benchCount;
            BenchCapacity = benchCapacity;
            BoardCount = boardCount;
            DeploymentLimit = deploymentLimit;
            TemporaryBenchCount = temporaryBenchCount;
            TemporaryBenchCapacity = temporaryBenchCapacity;
        }

        public int PlayerId { get; }

        public int BenchCount { get; }

        public int BenchCapacity { get; }

        public int BoardCount { get; }

        public int DeploymentLimit { get; }

        public int TemporaryBenchCount { get; }

        public int TemporaryBenchCapacity { get; }
    }
}
