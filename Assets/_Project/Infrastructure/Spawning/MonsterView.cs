using System;
using LegionBreak.Domain.Monsters;
using UnityEngine;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// 몬스터 인스턴스의 스폰-디스폰/체력 상태를 담당하는 View.
    /// 실제 아트/AI는 아직 없어 프리미티브 메시(Addressables 프리팹)로 표현되지만,
    /// HP 판정(MonsterHealth)을 실제로 들고 있어 더는 "더미" 스텁이 아니다
    /// (2026-07-23: DummyMonsterView에서 개명 — HP 시스템이 붙기 전까지는 수명 타이머만
    /// 있는 풀링 파이프라인 검증용 스텁이었으나, 이제 전투 상태를 가진 실제 컴포넌트다).
    /// </summary>
    public class MonsterView : MonoBehaviour
    {
        private float _lifetimeSeconds;
        private float _elapsed;
        private MonsterHealth _health;
        private Action<MonsterView> _onDeactivated;

        public void Initialize(float lifetimeSeconds, float maxHp, Action<MonsterView> onDeactivated)
        {
            _lifetimeSeconds = lifetimeSeconds;
            _elapsed = 0f;
            _health = new MonsterHealth(maxHp);
            _onDeactivated = onDeactivated;
        }

        // 수명 만료와 같은 콜백을 재사용한다 — 반환 흐름(Unregister/풀 반환)이 원인과
        // 무관하게 동일해야 하므로 별도 사망 경로를 만들지 않는다.
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
            }
        }
    }
}
