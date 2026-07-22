using System;
using LegionBreak.Domain.Monsters;
using UnityEngine;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// 풀링 파이프라인 검증용 더미 몬스터.
    /// 실제 몬스터 아트/AI가 붙기 전까지 스폰-디스폰 흐름만 확인하는 스텁이며,
    /// Addressables 프리팹 없이 프리미티브 메시로 생성된다.
    /// </summary>
    public class DummyMonsterView : MonoBehaviour
    {
        private float _lifetimeSeconds;
        private float _elapsed;
        private MonsterHealth _health;
        private Action<DummyMonsterView> _onDeactivated;

        public void Initialize(float lifetimeSeconds, float maxHp, Action<DummyMonsterView> onDeactivated)
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
