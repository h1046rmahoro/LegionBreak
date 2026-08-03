using LegionBreak.Application.Spawning;
using LegionBreak.Application.Waves;
using LegionBreak.Domain.Waves;
using NUnit.Framework;
using UnityEngine;

namespace LegionBreak.Application.Tests
{
    public class WaveSpawnUseCaseTests
    {
        private sealed class FakeMonsterSpawner : IMonsterSpawner
        {
            public int SpawnCallCount;

            public int ActiveCount => 0;

            public void Spawn(Vector2 position)
            {
                SpawnCallCount++;
            }

            public int ApplyDamageInRange(Vector2 center, float radius, float damage) => 0;
        }

        [Test]
        public void Tick_WaveNotStarted_DoesNotSpawn()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 5f, monsterCount: 1, spawnIntervalSeconds: 1f) });
            var spawner = new FakeMonsterSpawner();
            var useCase = new WaveSpawnUseCase(director, spawner, spawnRadius: 10f);

            useCase.Tick(1f);

            Assert.AreEqual(0, spawner.SpawnCallCount);
        }

        [Test]
        public void Tick_WaveStarted_CallsSpawnOnce()
        {
            var director = new WaveDirector(new[] { new WaveDefinition(startTimeSeconds: 0f, monsterCount: 3, spawnIntervalSeconds: 1f) });
            var spawner = new FakeMonsterSpawner();
            var useCase = new WaveSpawnUseCase(director, spawner, spawnRadius: 10f);

            useCase.Tick(0f);

            Assert.AreEqual(1, spawner.SpawnCallCount);
        }

        [Test]
        public void Tick_OverlappingWaves_CallsSpawnOncePerWave()
        {
            var director = new WaveDirector(new[]
            {
                new WaveDefinition(startTimeSeconds: 0f, monsterCount: 1, spawnIntervalSeconds: 1f),
                new WaveDefinition(startTimeSeconds: 0f, monsterCount: 1, spawnIntervalSeconds: 1f),
            });
            var spawner = new FakeMonsterSpawner();
            var useCase = new WaveSpawnUseCase(director, spawner, spawnRadius: 10f);

            useCase.Tick(0f);

            Assert.AreEqual(2, spawner.SpawnCallCount);
        }
    }
}
