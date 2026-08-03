using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LegionBreak.Application.Events;
using LegionBreak.Application.Movement;
using LegionBreak.Application.Player;
using LegionBreak.Application.Spawning;
using LegionBreak.Data;
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
        [SerializeField] private MonsterData _monsterData;

        private readonly Stack<MonsterView> _pool = new Stack<MonsterView>();
        private readonly List<MonsterView> _activeViews = new List<MonsterView>();
        private readonly Dictionary<MonsterView, int> _activeIndexByView = new Dictionary<MonsterView, int>();
        private readonly List<MonsterView> _damageQueryBuffer = new List<MonsterView>();
        private Action<MonsterView> _onDeactivatedCached;
        private IMonsterMovementSystem _movementSystem;
        private IMonsterSeparationSystem _separationSystem;
        private IMonsterPrefabProvider _prefabProvider;
        private IEventBus _eventBus;
        private IPlayerMotor _playerMotor;
        private IPlayerHealth _playerHealth;
        private GameObject _monsterPrefab;

        public int ActiveCount { get; private set; }

        [Inject]
        public void Construct(
            IMonsterMovementSystem movementSystem,
            IMonsterSeparationSystem separationSystem,
            IMonsterPrefabProvider prefabProvider,
            IEventBus eventBus,
            IPlayerMotor playerMotor,
            IPlayerHealth playerHealth)
        {
            _movementSystem = movementSystem;
            _separationSystem = separationSystem;
            _prefabProvider = prefabProvider;
            _eventBus = eventBus;
            _playerMotor = playerMotor;
            _playerHealth = playerHealth;
        }

        private void Awake()
        {
            _onDeactivatedCached = OnMonsterDeactivated;
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
            // 이동 등록은 더 이상 여기서 하지 않는다 — MonsterView가 Idle→Chase 전이 시점에
            // 스스로 _movementSystem.Register를 호출한다(AI 상태 기반 이동 On/Off).
            view.Initialize(_monsterLifetimeSeconds, _monsterData, _playerMotor, _playerHealth, _movementSystem, _onDeactivatedCached);
            _separationSystem?.Register(view);

            _activeIndexByView[view] = _activeViews.Count;
            _activeViews.Add(view);

            ActiveCount++;
            _eventBus?.Publish(new MonsterCountChangedEvent(ActiveCount));
        }

        // SpatialHashMonsterSeparationSystem의 그리드를 재사용하지 않고 선형 순회로 구현했다.
        // 그 그리드는 "몬스터 기준" 이웃 조회를 매 프레임 반복하는 걸 전제로 비용을 상각하는
        // 구조인데, 스킬 시전은 쿨다운으로 제한된 이벤트성 호출(초당 1회 수준)이라 조회 빈도
        // 자체가 다르고, 조회 기준도 몬스터가 아니라 임의의 클릭 지점이라 쿼리 형태도 다르다.
        // 몬스터 200~500마리 규모에서 1회성 선형 순회는 이미 충분히 싸므로, 병목이 확인되지
        // 않은 곳에 공간 분할을 끌어오는 건 이 프로젝트의 트레이드오프 원칙(필요한 곳에만
        // 최적화 기술 적용)과 어긋난다. 조회 빈도가 실제로 문제가 되면(예: 다중 동시 AoE) 그때
        // 별도의 범용 공간 쿼리 서비스를 새로 만들 것이지, 분리 시스템 내부를 재사용하진 않는다.
        public int ApplyDamageInRange(Vector2 center, float radius, float damage)
        {
            // 수집(읽기 전용) → 적용(TakeDamage) 2단계로 나눈다. TakeDamage가 즉시
            // _activeViews를 스왑 제거할 수 있는데, 그 목록을 직접 순회하며 동시에
            // 제거하면 인덱스가 꼬이므로 별도 버퍼에 모아둔 뒤 적용한다.
            _damageQueryBuffer.Clear();
            var sqrRadius = radius * radius;
            for (var i = 0; i < _activeViews.Count; i++)
            {
                var view = _activeViews[i];
                var position = view.transform.position;
                var sqrDistance = (new Vector2(position.x, position.z) - center).sqrMagnitude;
                if (sqrDistance <= sqrRadius)
                {
                    _damageQueryBuffer.Add(view);
                }
            }

            foreach (var view in _damageQueryBuffer)
            {
                view.TakeDamage(damage);
            }

            return _damageQueryBuffer.Count;
        }

        private MonsterView CreatePooledInstance()
        {
            // 머티리얼 공유(sharedMaterial), 그림자 Off, Collider 없음은 전부 프리팹 자체에
            // 미리 구성되어 있다(4주차 렌더링 최적화 결과를 그대로 유지하기 위함) — 코드에서
            // 매번 다시 설정하지 않는다.
            var go = Instantiate(_monsterPrefab, transform);
            go.SetActive(false);
            return go.GetComponent<MonsterView>();
        }

        // 수명 만료와 피격 사망 둘 다의 콜백이라 이름을 LifetimeEnded에서 Deactivated로 바꿨다.
        private void OnMonsterDeactivated(MonsterView view)
        {
            _movementSystem?.Unregister(view);
            _separationSystem?.Unregister(view);
            RemoveFromActiveViews(view);
            view.gameObject.SetActive(false);
            _pool.Push(view);
            ActiveCount--;
            _eventBus?.Publish(new MonsterCountChangedEvent(ActiveCount));
        }

        private void RemoveFromActiveViews(MonsterView view)
        {
            var index = _activeIndexByView[view];
            var lastIndex = _activeViews.Count - 1;
            var lastView = _activeViews[lastIndex];

            _activeViews[index] = lastView;
            _activeIndexByView[lastView] = index;

            _activeViews.RemoveAt(lastIndex);
            _activeIndexByView.Remove(view);
        }
    }
}
