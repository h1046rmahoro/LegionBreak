using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LegionBreak.Application.Spawning;
using LegionBreak.Infrastructure.Movement;
using LegionBreak.Infrastructure.Separation;
using UnityEngine;
using VContainer;

namespace LegionBreak.Infrastructure.Spawning
{
    /// <summary>
    /// After: 오브젝트 풀링. Destroy 대신 SetActive(false)로 반환해 재사용한다.
    /// 시작 시 프리워밍하여 스폰 순간의 GC Alloc/프레임 스파이크를 제거한다.
    /// 몬스터 프리팹은 Addressables로 비동기 로드하므로(IMonsterPrefabProvider), 프리워밍도
    /// 그 로드가 끝난 뒤에 UniTask로 진행한다.
    /// </summary>
    public class PooledMonsterSpawner : MonoBehaviour, IMonsterSpawner
    {
        [SerializeField] private float _monsterLifetimeSeconds = 3f;
        [SerializeField] private int _prewarmCount = 500;

        private readonly Stack<DummyMonsterView> _pool = new Stack<DummyMonsterView>();
        private Action<DummyMonsterView> _onLifetimeEndedCached;
        private IMonsterMovementSystem _movementSystem;
        private IMonsterSeparationSystem _separationSystem;
        private IMonsterPrefabProvider _prefabProvider;
        private GameObject _monsterPrefab;

        public int ActiveCount { get; private set; }

        [Inject]
        public void Construct(
            IMonsterMovementSystem movementSystem,
            IMonsterSeparationSystem separationSystem,
            IMonsterPrefabProvider prefabProvider)
        {
            _movementSystem = movementSystem;
            _separationSystem = separationSystem;
            _prefabProvider = prefabProvider;
        }

        private void Awake()
        {
            _onLifetimeEndedCached = OnMonsterLifetimeEnded;
            PrewarmAsync().Forget();
        }

        private async UniTaskVoid PrewarmAsync()
        {
            _monsterPrefab = await _prefabProvider.LoadAsync();

            for (var i = 0; i < _prewarmCount; i++)
            {
                _pool.Push(CreatePooledInstance());
            }
        }

        public void Spawn(Vector2 position)
        {
            if (_pool.Count == 0 && _monsterPrefab == null)
            {
                // 프리팹 로드가 아직 끝나지 않은 앱 시작 극초반에만 발생할 수 있는 경합이라
                // 이번 스폰 요청은 건너뛴다.
                return;
            }

            var view = _pool.Count > 0 ? _pool.Pop() : CreatePooledInstance();
            view.transform.position = new Vector3(position.x, 0f, position.y);
            view.gameObject.SetActive(true);
            view.Initialize(_monsterLifetimeSeconds, _onLifetimeEndedCached);
            _movementSystem?.Register(view);
            _separationSystem?.Register(view);
            ActiveCount++;
        }

        private DummyMonsterView CreatePooledInstance()
        {
            // 머티리얼 공유(sharedMaterial), 그림자 Off, Collider 없음은 전부 프리팹 자체에
            // 미리 구성되어 있다(4주차 렌더링 최적화 결과를 그대로 유지하기 위함) — 코드에서
            // 매번 다시 설정하지 않는다.
            var go = Instantiate(_monsterPrefab, transform);
            go.SetActive(false);
            return go.GetComponent<DummyMonsterView>();
        }

        private void OnMonsterLifetimeEnded(DummyMonsterView view)
        {
            _movementSystem?.Unregister(view);
            _separationSystem?.Unregister(view);
            view.gameObject.SetActive(false);
            _pool.Push(view);
            ActiveCount--;
        }
    }
}
