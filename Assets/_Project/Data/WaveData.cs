using UnityEngine;

namespace LegionBreak.Data
{
    /// <summary>
    /// 웨이브 시퀀스(각 웨이브의 시작 시점/수량/간격)와 스폰 반경을 하드코딩하지 않고
    /// 에셋으로 관리하기 위한 ScriptableObject. Domain의 WaveDirector로 변환되어 사용되며
    /// (Application.Waves.WaveDirectorFactory), 이 클래스 자체는 게임 로직에서 직접
    /// 참조되지 않는다(SkillData/CombatBalanceData와 동일 패턴).
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "LegionBreak/Wave Data")]
    public sealed class WaveData : ScriptableObject
    {
        [SerializeField] private WaveEntry[] _waves;
        [SerializeField] private float _spawnRadius = 20f;

        public WaveEntry[] Waves => _waves;
        public float SpawnRadius => _spawnRadius;
    }
}
