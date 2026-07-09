using System;
using System.Collections.Generic;
using LegionBreak.Application.Spawning;
using UnityEngine;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// After: 오브젝트 풀링. Destroy 대신 SetActive(false)로 반환해 재사용한다.
    /// 시작 시 프리워밍하여 스폰 순간의 GC Alloc/프레임 스파이크를 제거한다.
    /// </summary>
    public class PooledMonsterSpawner : MonoBehaviour, IMonsterSpawner
    {
        [SerializeField] private float _monsterLifetimeSeconds = 3f;
        [SerializeField] private int _prewarmCount = 500;

        private readonly Stack<DummyMonsterView> _pool = new Stack<DummyMonsterView>();
        private Action<DummyMonsterView> _onLifetimeEndedCached;

        private void Awake()
        {
            _onLifetimeEndedCached = OnMonsterLifetimeEnded;

            for (var i = 0; i < _prewarmCount; i++)
            {
                _pool.Push(CreatePooledInstance());
            }
        }

        public void Spawn(System.Numerics.Vector2 position)
        {
            var view = _pool.Count > 0 ? _pool.Pop() : CreatePooledInstance();
            view.transform.position = new Vector3(position.X, 0f, position.Y);
            view.gameObject.SetActive(true);
            view.Initialize(_monsterLifetimeSeconds, _onLifetimeEndedCached);
        }

        private DummyMonsterView CreatePooledInstance()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(transform);
            go.SetActive(false);
            return go.AddComponent<DummyMonsterView>();
        }

        private void OnMonsterLifetimeEnded(DummyMonsterView view)
        {
            view.gameObject.SetActive(false);
            _pool.Push(view);
        }
    }
}
