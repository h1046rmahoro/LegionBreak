using System;
using LegionBreak.Application.Spawning;
using UnityEngine;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// Before 베이스라인: 매 스폰마다 Instantiate, 매 디스폰마다 Destroy.
    /// 풀링 적용 전 GC Alloc/프레임 스파이크를 그대로 노출하기 위한 구현체다.
    /// </summary>
    public class InstantiateMonsterSpawner : MonoBehaviour, IMonsterSpawner
    {
        [SerializeField] private float _monsterLifetimeSeconds = 3f;

        private Action<DummyMonsterView> _onLifetimeEndedCached;

        private void Awake()
        {
            _onLifetimeEndedCached = OnMonsterLifetimeEnded;
        }

        public void Spawn(Vector2 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = new Vector3(position.x, 0f, position.y);
            var view = go.AddComponent<DummyMonsterView>();
            view.Initialize(_monsterLifetimeSeconds, _onLifetimeEndedCached);
        }

        private void OnMonsterLifetimeEnded(DummyMonsterView view)
        {
            Destroy(view.gameObject);
        }
    }
}
