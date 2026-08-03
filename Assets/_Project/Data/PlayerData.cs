using UnityEngine;

namespace LegionBreak.Data
{
    /// <summary>
    /// 플레이어 밸런스 수치(체력 등)를 하드코딩하지 않고 에셋으로 관리하기 위한 ScriptableObject.
    /// MonsterData와 동일 패턴이며, Infrastructure의 PlayerHealthController가 직접 참조한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerData", menuName = "LegionBreak/Player Data")]
    public sealed class PlayerData : ScriptableObject
    {
        [SerializeField] private float _maxHp = 100f;

        public float MaxHp => _maxHp;
    }
}
