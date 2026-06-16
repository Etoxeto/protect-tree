using System;

namespace ProtectTree.Core.Simulation
{
    public sealed class FixedStepClock
    {
        private const double ComparisonEpsilon = 0.0000001d;

        private readonly double _stepSeconds;
        private readonly int _maxStepsPerAdvance;

        private double _accumulatedSeconds;

        public FixedStepClock(double stepSeconds, int maxStepsPerAdvance)
        {
            if (double.IsNaN(stepSeconds)
                || double.IsInfinity(stepSeconds)
                || stepSeconds <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(stepSeconds),
                    "The fixed step must be a finite positive value.");
            }

            if (maxStepsPerAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxStepsPerAdvance),
                    "At least one step must be allowed per advance.");
            }

            _stepSeconds = stepSeconds;
            _maxStepsPerAdvance = maxStepsPerAdvance;
        }

        public long Tick { get; private set; }

        public double StepSeconds => _stepSeconds;

        public double DroppedSeconds { get; private set; }

        public int Advance(double elapsedSeconds, Action<double> onStep)
        {
            if (double.IsNaN(elapsedSeconds)
                || double.IsInfinity(elapsedSeconds)
                || elapsedSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(elapsedSeconds),
                    "Elapsed time must be a finite non-negative value.");
            }

            if (onStep == null)
            {
                throw new ArgumentNullException(nameof(onStep));
            }

            _accumulatedSeconds += elapsedSeconds;

            double maximumAccumulatedSeconds = _stepSeconds * _maxStepsPerAdvance;
            if (_accumulatedSeconds > maximumAccumulatedSeconds)
            {
                DroppedSeconds += _accumulatedSeconds - maximumAccumulatedSeconds;
                _accumulatedSeconds = maximumAccumulatedSeconds;
            }

            int stepCount = 0;
            while (stepCount < _maxStepsPerAdvance
                && _accumulatedSeconds + ComparisonEpsilon >= _stepSeconds)
            {
                _accumulatedSeconds -= _stepSeconds;
                if (_accumulatedSeconds < 0d)
                {
                    _accumulatedSeconds = 0d;
                }

                Tick++;
                stepCount++;
                onStep(_stepSeconds);
            }

            return stepCount;
        }

        public void Reset()
        {
            Tick = 0;
            DroppedSeconds = 0d;
            _accumulatedSeconds = 0d;
        }
    }
}
