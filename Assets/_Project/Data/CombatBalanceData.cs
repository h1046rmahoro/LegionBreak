using UnityEngine;

namespace LegionBreak.Data
{
    /// <summary>
    /// 특정 스킬에 종속되지 않는 전투 전반의 밸런스 수치(크리티컬 배율 등)를 관리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "CombatBalanceData", menuName = "LegionBreak/Combat Balance Data")]
    public sealed class CombatBalanceData : ScriptableObject
    {
        [SerializeField] private float _criticalDamageMultiplier = 1.5f;
        [SerializeField] private float _criticalChance = 0.1f;

        public float CriticalDamageMultiplier => _criticalDamageMultiplier;
        public float CriticalChance => _criticalChance;
    }
}
