using System;
using System.Collections.Generic;
using ClassicUO.Game;
using ClassicUO.Game.Data;
using Xunit;

namespace ClassicUO.UnitTests.Game.Movement
{
    // The step cadence credit lets a walk request go out slightly early when the previous one
    // was late. Online this must never look faster than the nominal speed to a server, so these
    // tests simulate long runs with uneven update ticks and replay the request times through the
    // speed checks used by ServUO, Source-X and SphereNet.
    public class StepCadenceTests
    {
        [Theory]
        [InlineData(false, false, 10, 100, 0)]
        [InlineData(true, false, 10, 100, 0)]
        [InlineData(false, true, 10, 100, 0)]
        [InlineData(true, true, -1, 100, 0)]
        [InlineData(true, true, 0, 100, 0)]
        [InlineData(true, true, 7, 100, 7)]
        [InlineData(true, true, 40, 100, Constants.STEP_CADENCE_MAX_CREDIT)]
        [InlineData(true, true, 101, 100, 0)]
        public void Credit_Only_Between_Steps_And_Capped(bool isStep, bool previousWasStep, long lateMs, int walkTime, int expected)
        {
            Assert.Equal(expected, MovementSpeed.StepCadenceCredit(isStep, previousWasStep, lateMs, walkTime));
        }

        [Theory]
        [InlineData(100, 1, 6)]    // mounted run, high fps
        [InlineData(100, 1, 40)]   // mounted run, very uneven frames (25-1000 fps)
        [InlineData(200, 5, 34)]   // run on foot, ~30-200 fps
        [InlineData(400, 10, 80)]  // walk on foot, 12 fps worst case
        public void Simulated_Run_Never_Faster_Than_Nominal(int walkTime, int minTick, int maxTick)
        {
            var sends = Simulate(walkTime, minTick, maxTick, steps: 5000, seed: walkTime * 31 + maxTick);

            for (int i = 1; i < sends.Count; i++)
            {
                long interval = sends[i] - sends[i - 1];
                Assert.True(interval >= walkTime - Constants.STEP_CADENCE_MAX_CREDIT, $"interval {interval} at step {i}");
            }

            // over any window the walker is never ahead of the nominal schedule by more than one credit
            for (int i = 0; i < sends.Count; i++)
            {
                long ahead = (sends[0] + (long)i * walkTime) - sends[i];
                Assert.True(ahead <= Constants.STEP_CADENCE_MAX_CREDIT, $"ahead {ahead} ms at step {i}");
            }
        }

        [Fact]
        public void Compensation_Keeps_Nominal_Rate_Where_Plain_Timing_Drifts()
        {
            // 60 fps style ticks: without credit every step loses part of a frame
            var withCredit = Simulate(100, 16, 17, steps: 1000, seed: 1, useCredit: true);
            var without = Simulate(100, 16, 17, steps: 1000, seed: 1, useCredit: false);

            double avgWith = (withCredit[^1] - withCredit[0]) / (double)(withCredit.Count - 1);
            double avgWithout = (without[^1] - without[0]) / (double)(without.Count - 1);

            Assert.InRange(avgWith, 100.0, 101.0);
            Assert.True(avgWithout > 105.0, $"plain timing average {avgWithout}");
        }

        [Theory]
        [InlineData(100, 1, 40)]
        [InlineData(200, 5, 34)]
        public void ServUO_Throttle_Never_Holds_A_Step(int walkTime, int minTick, int maxTick)
        {
            // ServUO PlayerMobile.MovementThrottle_Callback: a step is held while
            // m_NextMovementTime - now >= FastwalkThreshold (400); each step adds its speed.
            var sends = Simulate(walkTime, minTick, maxTick, steps: 5000, seed: 7);
            long next = sends[0];

            foreach (long now in sends)
            {
                if (next - now < 0)
                {
                    next = now;
                }

                Assert.True(next - now < 400);
                next += walkTime;
            }
        }

        [Theory]
        [InlineData(1, 40)]
        [InlineData(16, 17)]
        public void SourceX_WalkBuffer_Never_Flags_Mounted_Run(int minTick, int maxTick)
        {
            // Source-X CClient::Event_CheckWalkBuffer with sphere.ini defaults
            // (WalkBuffer=15 -> 1500 ms, WalkRegen=25), starting from the login value 500.
            var sends = Simulate(100, minTick, maxTick, steps: 5000, seed: 3);
            const int timeMin = 100;
            long avg = 500;

            for (int i = 1; i < sends.Count; i++)
            {
                long diff = sends[i] - sends[i - 1];

                if (diff > timeMin + 350)
                {
                    continue;
                }

                if (diff > timeMin)
                {
                    diff = timeMin + (diff - timeMin) * 25 / 20;
                }

                avg = Math.Min(avg + diff - timeMin, 1500);
                Assert.True(avg >= 0, $"walk buffer exhausted at step {i}");
            }
        }

        [Theory]
        [InlineData(100, 1, 40)]
        [InlineData(200, 5, 34)]
        public void SphereNet_Never_Queues_A_Step(int walkTime, int minTick, int maxTick)
        {
            // SphereNet processes a step immediately when it arrives after previous + walkTime - 30
            var sends = Simulate(walkTime, minTick, maxTick, steps: 5000, seed: 11);

            for (int i = 1; i < sends.Count; i++)
            {
                Assert.True(sends[i] - sends[i - 1] >= walkTime - 30);
            }
        }

        // Mirrors PlayerMobile.Walk scheduling: the walker holds the movement key, update ticks
        // arrive every minTick..maxTick ms, and a request goes out on the first tick at or after
        // LastStepRequestTime.
        private static List<long> Simulate(int walkTime, int minTick, int maxTick, int steps, int seed, bool useCredit = true)
        {
            var rnd = new Random(seed);
            var sends = new List<long>(steps);
            long now = 0;
            long lastStepRequestTime = 0;
            bool previousWasStep = false;

            while (sends.Count < steps)
            {
                now += rnd.Next(minTick, maxTick + 1);

                if (lastStepRequestTime > now)
                {
                    continue;
                }

                long lateMs = lastStepRequestTime != 0 ? now - lastStepRequestTime : -1;
                int credit = useCredit ? MovementSpeed.StepCadenceCredit(true, previousWasStep, lateMs, walkTime) : 0;

                sends.Add(now);
                previousWasStep = true;
                lastStepRequestTime = now + walkTime - credit;
            }

            return sends;
        }
    }
}
