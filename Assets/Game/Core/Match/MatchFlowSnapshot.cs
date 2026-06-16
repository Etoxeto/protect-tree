namespace ProtectTree.Core.Match
{
    public sealed class MatchFlowSnapshot
    {
        public MatchFlowSnapshot(
            string phase,
            int wave,
            double remainingSeconds,
            bool isFinished,
            string result)
        {
            Phase = phase;
            Wave = wave;
            RemainingSeconds = remainingSeconds;
            IsFinished = isFinished;
            Result = result;
        }

        public string Phase { get; }

        public int Wave { get; }

        public double RemainingSeconds { get; }

        public bool IsFinished { get; }

        public string Result { get; }
    }
}
