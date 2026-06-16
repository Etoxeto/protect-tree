namespace ProtectTree.Core.Match
{
    public sealed class MatchEvent
    {
        public MatchEvent(
            string type,
            int? wave = null,
            string phase = null,
            string enemyId = null,
            int? spawnIndex = null,
            int? enemyInstanceId = null,
            string result = null,
            int? pieceInstanceId = null,
            int? sourcePieceInstanceId = null,
            int? sourceEnemyInstanceId = null)
        {
            Type = type;
            Wave = wave;
            Phase = phase;
            EnemyId = enemyId;
            SpawnIndex = spawnIndex;
            EnemyInstanceId = enemyInstanceId;
            Result = result;
            PieceInstanceId = pieceInstanceId;
            SourcePieceInstanceId = sourcePieceInstanceId;
            SourceEnemyInstanceId = sourceEnemyInstanceId;
        }

        public string Type { get; }

        public int? Wave { get; }

        public string Phase { get; }

        public string EnemyId { get; }

        public int? SpawnIndex { get; }

        public int? EnemyInstanceId { get; }

        public string Result { get; }

        public int? PieceInstanceId { get; }

        public int? SourcePieceInstanceId { get; }

        public int? SourceEnemyInstanceId { get; }
    }
}
