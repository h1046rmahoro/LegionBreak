using System;
using System.Collections.Generic;
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
    /// </summary>
    public class PooledMonsterSpawner : MonoBehaviour, IMonsterSpawner
    {
        [SerializeField] private float _monsterLifetimeSeconds = 3f;
        [SerializeField] private int _prewarmCount = 500;
        [SerializeField] private Material _monsterMaterial;

        private readonly Stack<DummyMonsterView> _pool = new Stack<DummyMonsterView>();
        private Action<DummyMonsterView> _onLifetimeEndedCached;
        private IMonsterMovementSystem _movementSystem;
        private IMonsterSeparationSystem _separationSystem;

        public int ActiveCount { get; private set; }

        [Inject]
        public void Construct(IMonsterMovementSystem movementSystem, IMonsterSeparationSystem separationSystem)
        {
            _movementSystem = movementSystem;
            _separationSystem = separationSystem;
        }

        private void Awake()
        {
            _onLifetimeEndedCached = OnMonsterLifetimeEnded;

            for (var i = 0; i < _prewarmCount; i++)
            {
                _pool.Push(CreatePooledInstance());
            }
        }

        public void Spawn(Vector2 position)
        {
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
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.transform.SetParent(transform);
            go.SetActive(false);

            // CreatePrimitive가 기본으로 붙이는 CapsuleCollider는 이 프로젝트에서 쓰지 않는다
            // (충돌 판정은 Physics가 아니라 SpatialHashMonsterSeparationSystem이 직접 계산).
            // 1주차 프로파일링에서 SetActive 토글마다 이 Collider가 PhysX 브로드페이즈에
            // 재삽입/제거되며 GC 절감분 이상의 CPU 비용을 유발했을 가능성이 의심됐다.
            Destroy(go.GetComponent<Collider>());

            var renderer = go.GetComponent<Renderer>();

            // GPU Instancing이 적용되려면 모든 인스턴스가 같은 머티리얼 에셋을 참조해야 한다.
            // .material로 접근하면 인스턴스별 복사본이 생겨 배칭이 깨지므로 반드시 sharedMaterial을 쓴다.
            if (_monsterMaterial != null)
            {
                renderer.sharedMaterial = _monsterMaterial;
            }

            // Before(내장 Default-Material)는 URP와 호환되지 않는 셰이더라 그림자를 만들지
            // 못했다. 그림자 유무가 배칭 비교의 변수로 섞이지 않도록 명시적으로 꺼서
            // Before와 동일한 조건을 유지한다.
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            return go.AddComponent<DummyMonsterView>();
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
