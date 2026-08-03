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

        // 스폰 반경(20f, MonsterSpawnTester 기준)보다 크게 잡아, 5주차 재측정 시 기존
        // 1~4주차 프로파일링과 "동시 이동 개체 수" 조건을 맞춘다. Idle 상태를 직접 보고
        // 싶을 땐 데모 시점에 Inspector에서 이 값을 일시적으로 낮춘다.
        [SerializeField] private float _chaseRange = 25f;
        // AttackRange는 ChaseRange 이하여야 한다.
        [SerializeField] private float _attackRange = 1.5f;
        [SerializeField] private float _attackCooldownSeconds = 1f;
        [SerializeField] private float _attackDamage = 5f;

        public float MaxHp => _maxHp;
        public float ChaseRange => _chaseRange;
        public float AttackRange => _attackRange;
        public float AttackCooldownSeconds => _attackCooldownSeconds;
        public float AttackDamage => _attackDamage;
    }
}
