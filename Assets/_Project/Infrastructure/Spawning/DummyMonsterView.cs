using System;
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
        private Action<DummyMonsterView> _onLifetimeEnded;

        public void Initialize(float lifetimeSeconds, Action<DummyMonsterView> onLifetimeEnded)
        {
            _lifetimeSeconds = lifetimeSeconds;
            _onLifetimeEnded = onLifetimeEnded;
            _elapsed = 0f;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= _lifetimeSeconds)
            {
                _onLifetimeEnded?.Invoke(this);
            }
        }
    }
}
