using System;
using System.Collections.Generic;
using LegionBreak.Application.Movement;
using LegionBreak.Application.Spawning;
using LegionBreak.Data;
using LegionBreak.Infrastructure.Movement;
using UnityEngine;
using VContainer;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// Before 베이스라인: 매 스폰마다 Instantiate, 매 디스폰마다 Destroy.
    /// 풀링 적용 전 GC Alloc/프레임 스파이크를 그대로 노출하기 위한 구현체다.
    /// 성능 비교용으로 고정된 코드라 ApplyDamageInRange도 인터페이스 계약만
    /// 충족하는 단순 선형 순회로 구현한다(PooledMonsterSpawner처럼 정교화하지 않음).
    /// </summary>
    public class InstantiateMonsterSpawner : MonoBehaviour, IMonsterSpawner
    {
        [SerializeField] private float _monsterLifetimeSeconds = 3f;
        [SerializeField] private MonsterData _monsterData;

        private readonly List<MonsterView> _activeViews = new List<MonsterView>();
        private Action<MonsterView> _onDeactivatedCached;
        private IPlayerMotor _playerMotor;
        private IMonsterMovementSystem _movementSystem;

        public int ActiveCount { get; private set; }

        [Inject]
        public void Construct(IPlayerMotor playerMotor, IMonsterMovementSystem movementSystem)
        {
            _playerMotor = playerMotor;
            _movementSystem = movementSystem;
        }

        private void Awake()
        {
            _onDeactivatedCached = OnMonsterDeactivated;
        }

        public void Spawn(Vector2 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.position = new Vector3(position.x, 0f, position.y);
            var view = go.AddComponent<MonsterView>();
            view.Initialize(_monsterLifetimeSeconds, _monsterData, _playerMotor, _movementSystem, _onDeactivatedCached);
            _activeViews.Add(view);
            ActiveCount++;
        }

        public int ApplyDamageInRange(Vector2 center, float radius, float damage)
        {
            var hitViews = new List<MonsterView>();
            var sqrRadius = radius * radius;
            foreach (var view in _activeViews)
            {
                var position = view.transform.position;
                var sqrDistance = (new Vector2(position.x, position.z) - center).sqrMagnitude;
                if (sqrDistance <= sqrRadius)
                {
                    hitViews.Add(view);
                }
            }

            foreach (var view in hitViews)
            {
                view.TakeDamage(damage);
            }

            return hitViews.Count;
        }

        private void OnMonsterDeactivated(MonsterView view)
        {
            _activeViews.Remove(view);
            Destroy(view.gameObject);
            ActiveCount--;
        }
    }
}
