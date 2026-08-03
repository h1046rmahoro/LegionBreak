using System.Collections.Generic;
using LegionBreak.Domain.Waves;
using NUnit.Framework;

namespace LegionBreak.Domain.Tests
{
    public class WaveDirectorTests
    {
        [Test]
        public void Tick_BeforeStartTime_DoesNotSpawn()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 5f, monsterCount: 3, spawnIntervalSeconds: 1f) });

            var result = director.Tick(1f);

            Assert.AreEqual(0, result.WaveIndexesToSpawn.Count);
        }

        [Test]
        public void Tick_AtStartTime_SpawnsFirstMonsterOfWave()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 1f, monsterCount: 3, spawnIntervalSeconds: 1f) });
            director.Tick(1f);

            var result = director.Tick(0f);

            CollectionAssert.AreEqual(new[] { 0 }, result.WaveIndexesToSpawn);
        }

        [Test]
        public void Tick_BeforeSpawnInterval_DoesNotSpawnAgain()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 0f, monsterCount: 3, spawnIntervalSeconds: 1f) });
            director.Tick(0f);

            var result = director.Tick(0.5f);

            Assert.AreEqual(0, result.WaveIndexesToSpawn.Count);
        }

        [Test]
        public void Tick_AfterSpawnInterval_SpawnsNextMonster()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 0f, monsterCount: 3, spawnIntervalSeconds: 1f) });
            director.Tick(0f);

            var result = director.Tick(1f);

            CollectionAssert.AreEqual(new[] { 0 }, result.WaveIndexesToSpawn);
        }

        [Test]
        public void Tick_AfterMonsterCountReached_StopsSpawningThatWave()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 0f, monsterCount: 2, spawnIntervalSeconds: 1f) });
            director.Tick(0f);
            director.Tick(1f);

            var result = director.Tick(1f);

            Assert.AreEqual(0, result.WaveIndexesToSpawn.Count);
        }

        [Test]
        public void Tick_OverlappingWaves_SpawnBothInSameTick()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(startTimeSeconds: 0f, monsterCount: 1, spawnIntervalSeconds: 1f),
                new WaveDefinition(startTimeSeconds: 0f, monsterCount: 1, spawnIntervalSeconds: 1f),
            };
            var director = new WaveDirector(waves);

            var result = director.Tick(0f);

            CollectionAssert.AreEquivalent(new[] { 0, 1 }, result.WaveIndexesToSpawn);
        }

        [Test]
        public void Tick_SecondWaveStartsBeforeFirstWaveFinishesSpawning()
        {
            var waves = new List<WaveDefinition>
            {
                new WaveDefinition(startTimeSeconds: 0f, monsterCount: 5, spawnIntervalSeconds: 10f),
                new WaveDefinition(startTimeSeconds: 1f, monsterCount: 1, spawnIntervalSeconds: 1f),
            };
            var director = new WaveDirector(waves);
            director.Tick(0f);

            var result = director.Tick(1f);

            CollectionAssert.AreEquivalent(new[] { 1 }, result.WaveIndexesToSpawn);
        }

        [Test]
        public void IsSequenceComplete_InitiallyFalse()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 0f, monsterCount: 1, spawnIntervalSeconds: 1f) });

            Assert.IsFalse(director.IsSequenceComplete);
        }

        [Test]
        public void Tick_AllWavesFinishedSpawning_SequenceComplete()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 0f, monsterCount: 1, spawnIntervalSeconds: 1f) });

            var result = director.Tick(0f);

            Assert.IsTrue(result.IsSequenceComplete);
            Assert.IsTrue(director.IsSequenceComplete);
        }

        [Test]
        public void Tick_EmptyWaveList_SequenceCompleteImmediately()
        {
            var director = new WaveDirector(new WaveDefinition[0]);

            var result = director.Tick(0f);

            Assert.IsTrue(result.IsSequenceComplete);
            Assert.AreEqual(0, result.WaveIndexesToSpawn.Count);
        }
    }
}
