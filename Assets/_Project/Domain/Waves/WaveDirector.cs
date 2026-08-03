using System.Collections.Generic;

namespace LegionBreak.Domain.Waves
{
    /// <summary>
    /// 웨이브 시퀀스의 스폰 타이밍 판정을 담당하는 순수 도메인 규칙. Skill의 쿨다운 게이트,
    /// MonsterAI의 상태 전이와 같은 이유로("시간 경과 + 임계값 비교" 분기) Domain에 둔다.
    /// 웨이브가 겹칠 수 있어 웨이브별로 독립된 스폰 타이머를 각각 추적한다.
    /// </summary>
    public sealed class WaveDirector
    {
        private readonly IReadOnlyList<WaveDefinition> _waves;
        private readonly int[] _spawnedCounts;
        private readonly float[] _nextSpawnTimes;

        // ApplyDamageInRange의 _damageQueryBuffer와 동일한 이유로, 매 Tick 재할당하지 않고
        // Clear()만 해서 재사용한다. 반환된 결과는 같은 프레임 안에서만 유효하다.
        private readonly List<int> _spawnBuffer = new List<int>();

        private float _elapsed;

        public WaveDirector(IReadOnlyList<WaveDefinition> waves)
        {
            _waves = waves;
            _spawnedCounts = new int[waves.Count];
            _nextSpawnTimes = new float[waves.Count];
            for (var i = 0; i < waves.Count; i++)
            {
                _nextSpawnTimes[i] = waves[i].StartTimeSeconds;
            }
        }

        public bool IsSequenceComplete { get; private set; }

        public WaveTickResult Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            _spawnBuffer.Clear();

            var allDone = true;
            for (var i = 0; i < _waves.Count; i++)
            {
                var wave = _waves[i];
                if (_spawnedCounts[i] >= wave.MonsterCount)
                {
                    continue;
                }

                allDone = false;
                if (_elapsed < _nextSpawnTimes[i])
                {
                    continue;
                }

                _spawnedCounts[i]++;
                _nextSpawnTimes[i] = _elapsed + wave.SpawnIntervalSeconds;
                _spawnBuffer.Add(i);
            }

            IsSequenceComplete = allDone;
            return new WaveTickResult(_spawnBuffer, IsSequenceComplete);
        }
    }
}
