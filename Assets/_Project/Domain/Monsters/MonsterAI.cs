using System;

namespace LegionBreak.Domain.Monsters
{
    /// <summary>
    /// 몬스터 상태 판단(Idle/Chase/Attack/Dead)을 담당하는 순수 도메인 규칙.
    /// Skill의 쿨다운 게이트(RemainingCooldown/IsReady/Tick/ConsumeCooldown)와 같은 이유로
    /// Domain에 두되, MonsterHealth/Skill과 마찬가지로 엔티티 상태 홀더라 인터페이스 없이
    /// concrete 클래스로 둔다(ICriticalHitJudge류의 "교체 가능한 알고리즘"과는 다름).
    /// AttackRange는 ChaseRange 이하여야 한다는 전제를 가진다(검증 코드는 넣지 않음 — YAGNI).
    /// </summary>
    public sealed class MonsterAI
    {
        public float ChaseRange { get; }
        public float AttackRange { get; }
        public float AttackCooldownSeconds { get; }
        public MonsterAIState CurrentState { get; private set; } = MonsterAIState.Idle;

        private float _attackCooldownRemaining;

        public MonsterAI(float chaseRange, float attackRange, float attackCooldownSeconds)
        {
            ChaseRange = chaseRange;
            AttackRange = attackRange;
            AttackCooldownSeconds = attackCooldownSeconds;
        }

        public MonsterAIState Tick(float distanceToPlayer, bool isDead, float deltaTime)
        {
            // Skill.Tick과 동일하게 상태와 무관하게 항상 흘러가야, Chase<->Attack을
            // 오가도 쿨다운이 부자연스럽게 리셋되지 않는다.
            _attackCooldownRemaining = Math.Max(0f, _attackCooldownRemaining - deltaTime);

            if (isDead)
            {
                CurrentState = MonsterAIState.Dead;
                return CurrentState;
            }

            CurrentState = CurrentState switch
            {
                MonsterAIState.Idle => distanceToPlayer <= ChaseRange ? MonsterAIState.Chase : MonsterAIState.Idle,
                MonsterAIState.Chase => distanceToPlayer <= AttackRange ? MonsterAIState.Attack
                    : distanceToPlayer > ChaseRange ? MonsterAIState.Idle
                    : MonsterAIState.Chase,
                MonsterAIState.Attack => distanceToPlayer > AttackRange ? MonsterAIState.Chase : MonsterAIState.Attack,
                _ => CurrentState // Dead는 터미널, 소생 없음
            };

            return CurrentState;
        }

        // 플레이어 HP/피격 시스템이 아직 없어(2026-08-03 결정) 이 게이트를 호출해 실제
        // 데미지를 적용할 소비처가 없다. 게이트 자체는 완성해두고, 플레이어 HP 시스템이
        // 추가되는 시점에 MonsterView.Update()에서
        // "if (newState == Attack && _ai.TryConsumeAttack()) { ... }" 형태로 바로 연결한다.
        public bool TryConsumeAttack()
        {
            if (CurrentState != MonsterAIState.Attack || _attackCooldownRemaining > 0f)
            {
                return false;
            }

            _attackCooldownRemaining = AttackCooldownSeconds;
            return true;
        }
    }
}
