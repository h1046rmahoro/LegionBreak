using System;
using UnityEngine;

namespace LegionBreak.Data
{
    /// <summary>
    /// WaveData가 들고 있는 웨이브 하나의 밸런스 수치. Domain.Waves.WaveDefinition과
    /// 필드가 대응되며, Application.Waves.WaveDirectorFactory가 변환한다.
    /// </summary>
    [Serializable]
    public struct WaveEntry
    {
        [SerializeField] private float _startTimeSeconds;
        [SerializeField] private int _monsterCount;
        [SerializeField] private float _spawnIntervalSeconds;

        public float StartTimeSeconds => _startTimeSeconds;
        public int MonsterCount => _monsterCount;
        public float SpawnIntervalSeconds => _spawnIntervalSeconds;
    }
}
