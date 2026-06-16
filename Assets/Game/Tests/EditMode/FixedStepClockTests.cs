using System;
using System.Collections.Generic;
using NUnit.Framework;
using ProtectTree.Core.Simulation;

namespace ProtectTree.Core.Tests
{
    public sealed class FixedStepClockTests
    {
        [Test]
        public void Advance_EmitsOnlyCompleteFixedSteps()
        {
            FixedStepClock clock = new FixedStepClock(0.1d, 8);
            List<double> steps = new List<double>();

            Assert.That(clock.Advance((double)0.04f, steps.Add), Is.EqualTo(0));
            Assert.That(clock.Advance((double)0.06f, steps.Add), Is.EqualTo(1));
            Assert.That(clock.Advance((double)0.2f, steps.Add), Is.EqualTo(2));

            Assert.That(clock.Tick, Is.EqualTo(3));
            Assert.That(steps, Is.EqualTo(new[] { 0.1d, 0.1d, 0.1d }));
        }

        [Test]
        public void Advance_ClampsLargeFrameAndReportsDroppedTime()
        {
            FixedStepClock clock = new FixedStepClock(0.1d, 3);
            int emittedSteps = 0;

            int stepCount = clock.Advance(1d, _ => emittedSteps++);

            Assert.That(stepCount, Is.EqualTo(3));
            Assert.That(emittedSteps, Is.EqualTo(3));
            Assert.That(clock.DroppedSeconds, Is.EqualTo(0.7d).Within(0.0000001d));
        }

        [Test]
        public void Reset_ClearsTickAccumulatorAndDroppedTime()
        {
            FixedStepClock clock = new FixedStepClock(0.1d, 2);

            clock.Advance(1d, _ => { });
            clock.Reset();

            Assert.That(clock.Tick, Is.EqualTo(0));
            Assert.That(clock.DroppedSeconds, Is.Zero);
            Assert.That(clock.Advance(0.05d, _ => { }), Is.Zero);
        }

        [Test]
        public void Constructor_RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(0d, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(double.NaN, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FixedStepClock(0.1d, 0));
        }

        [Test]
        public void Advance_RejectsInvalidElapsedTimeAndCallback()
        {
            FixedStepClock clock = new FixedStepClock(0.1d, 8);

            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(-0.1d, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(double.PositiveInfinity, _ => { }));
            Assert.Throws<ArgumentNullException>(() => clock.Advance(0.1d, null));
        }
    }
}
