using System;
using LegionBreak.Application.Movement;
using LegionBreak.Data;
using LegionBreak.Domain.Monsters;
using LegionBreak.Infrastructure.Movement;
using UnityEngine;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// 몬스터 인스턴스의 스폰-디스폰/체력/AI 상태를 담당하는 View.
    /// 실제 아트는 아직 없어 프리미티브 메시(Addressables 프리팹)로 표현되지만,
    /// HP 판정(MonsterHealth)과 AI 상태(MonsterAI)를 실제로 들고 있어 더는 "더미" 스텁이 아니다
    /// (2026-07-23: DummyMonsterView에서 개명 — HP 시스템이 붙기 전까지는 수명 타이머만
    /// 있는 풀링 파이프라인 검증용 스텁이었으나, 이제 전투 상태를 가진 실제 컴포넌트다).
    /// </summary>
    public class MonsterView : MonoBehaviour
    {
        private float _lifetimeSeconds;
        private float _elapsed;
        private MonsterHealth _health;
        private MonsterAI _ai;
        private IPlayerMotor _playerMotor;
        private IMonsterMovementSystem _movementSystem;
        private Action<MonsterView> _onDeactivated;

        // lifetimeSeconds는 MonsterData(밸런스 데이터)가 아니라 스포너가 소유한 임시 수명
        // 타이머(사망 시스템이 생기기 전까지의 테스트 하네스 값)라 별도 파라미터로 받는다.
        public void Initialize(
            float lifetimeSeconds, MonsterData data,
            IPlayerMotor playerMotor, IMonsterMovementSystem movementSystem,
            Action<MonsterView> onDeactivated)
        {
            _lifetimeSeconds = lifetimeSeconds;
            _elapsed = 0f;
            _health = new MonsterHealth(data.MaxHp);
            _ai = new MonsterAI(data.ChaseRange, data.AttackRange, data.AttackCooldownSeconds);
            _playerMotor = playerMotor;
            _movementSystem = movementSystem;
            _onDeactivated = onDeactivated;
        }

        // 수명 만료와 같은 콜백을 재사용한다 — 반환 흐름(Unregister/풀 반환)이 원인과
        // 무관하게 동일해야 하므로 별도 사망 경로를 만들지 않는다.
        // 주의: TakeDamage는 사망 즉시 동기적으로 _onDeactivated를 호출해 비활성화하므로,
        // MonsterAIState.Dead는 실제로 Update()를 통해 관측되지 않는다(Domain 단위 테스트로는
        // 검증되지만 이 배선에서는 소비되지 않음). 사망 연출을 붙일 때 _onDeactivated 호출 전
        // _ai.Tick(distance, true, 0f)를 한 번 호출하도록 보강이 필요하다.
        public void TakeDamage(float amount)
        {
            _health.TakeDamage(amount);
            if (_health.IsDead)
            {
                _onDeactivated?.Invoke(this);
            }
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetimeSeconds)
            {
                _onDeactivated?.Invoke(this);
                return;
            }

            // previousState는 _ai.Tick 호출 "이전"에 반드시 읽어야 한다(Tick 내부에서 갱신됨).
            var previousState = _ai.CurrentState;
            var position = transform.position;
            var distance = Vector2.Distance(new Vector2(position.x, position.z), _playerMotor.Position);
            var currentState = _ai.Tick(distance, _health.IsDead, Time.deltaTime);

            // Register는 반드시 상태 전이 엣지에서만 호출해야 한다 — 중복 호출 시
            // JobMonsterMovementSystem은 인덱스가 깨지고 MonoMonsterMovementSystem은
            // 이동속도가 2배가 된다. currentState == previousState(Chase 유지 등)인
            // 프레임에는 아무 것도 호출하지 않도록 바깥에서 변화 여부를 먼저 확인한다.
            if (currentState != previousState)
            {
                if (currentState == MonsterAIState.Chase)
                {
                    _movementSystem.Register(this);
                }
                else if (previousState == MonsterAIState.Chase)
                {
                    _movementSystem.Unregister(this);
                }
            }

            // TryConsumeAttack()은 아직 호출하지 않는다 — 플레이어 HP/피격 시스템이 없어
            // 실제 데미지를 적용할 소비처가 없기 때문(MonsterAI.cs 참고).
        }
    }
}
