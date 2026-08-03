using LegionBreak.Application.Spawning;
using LegionBreak.Domain.Waves;
using UnityEngine;

namespace LegionBreak.Application.Waves
{
    public sealed class WaveSpawnUseCase : IWaveSpawnUseCase
    {
        private readonly WaveDirector _waveDirector;
        private readonly IMonsterSpawner _monsterSpawner;
        private readonly float _spawnRadius;

        public WaveSpawnUseCase(WaveDirector waveDirector, IMonsterSpawner monsterSpawner, float spawnRadius)
        {
            _waveDirector = waveDirector;
            _monsterSpawner = monsterSpawner;
            _spawnRadius = spawnRadius;
        }

        public void Tick(float deltaTime)
        {
            var result = _waveDirector.Tick(deltaTime);
            for (var i = 0; i < result.WaveIndexesToSpawn.Count; i++)
            {
                _monsterSpawner.Spawn(RandomPositionInRadius());
            }
        }

        // MonsterSpawnTester가 쓰던 것과 동일한 계산이다. 분기·밸런스 없는 범용 수학이라
        // PlayerMoveUseCase의 이동량 계산과 같은 이유로 Domain으로 분리하지 않는다.
        private Vector2 RandomPositionInRadius()
        {
            var angle = Random.Range(0f, Mathf.PI * 2f);
            var distance = Random.Range(0f, _spawnRadius);
            return new Vector2(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance);
        }
    }
}
