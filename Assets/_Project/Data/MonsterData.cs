using UnityEngine;

namespace LegionBreak.Data
{
    /// <summary>
    /// 몬스터 밸런스 수치(체력 등)를 하드코딩하지 않고 에셋으로 관리하기 위한 ScriptableObject.
    /// Infrastructure의 스포너가 직접 참조해 MonsterView.Initialize에 전달한다.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterData", menuName = "LegionBreak/Monster Data")]
    public sealed class MonsterData : ScriptableObject
    {
        [SerializeField] private float _maxHp = 20f;

        public float MaxHp => _maxHp;
    }
}
