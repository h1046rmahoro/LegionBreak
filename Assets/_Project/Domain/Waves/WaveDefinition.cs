namespace LegionBreak.Domain.Waves
{
    /// <summary>
    /// 웨이브 하나의 스폰 규칙. StartTimeSeconds는 시퀀스 시작 시점 기준 절대 시간이라,
    /// 이전 웨이브가 끝나기를 기다리지 않고 여러 웨이브가 겹쳐 진행될 수 있다
    /// (사용자 결정: 시간 기반 연속 웨이브 — 동시 개체 수가 자연스럽게 누적/램프업되어야
    /// CLAUDE.md의 "200~500마리 동시 스폰" 프로파일링 목표에 맞음).
    /// </summary>
    public readonly struct WaveDefinition
    {
        public float StartTimeSeconds { get; }
        public int MonsterCount { get; }
        public float SpawnIntervalSeconds { get; }

        public WaveDefinition(float startTimeSeconds, int monsterCount, float spawnIntervalSeconds)
        {
            StartTimeSeconds = startTimeSeconds;
            MonsterCount = monsterCount;
            SpawnIntervalSeconds = spawnIntervalSeconds;
        }
    }
}
