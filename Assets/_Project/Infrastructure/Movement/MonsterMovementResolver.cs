using System.Collections.Generic;
using LegionBreak.Application.Movement;
using LegionBreak.Infrastructure.Pathfinding;
using LegionBreak.Infrastructure.Separation;
using LegionBreak.Infrastructure.Spawning;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using VContainer;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// 7주차: FlowFieldMonsterMovementSystem(이동)과 SpatialHashMonsterSeparationSystem
    /// (겹침 회피)을 하나의 Job 파이프라인으로 통합한 것. 두 시스템은 5주차까지 의도적으로
    /// 분리 유지했지만(각자 독립적으로 이미 검증됨), 겹침 회피를 별도의 Job(별도
    /// TransformAccessArray)으로 전환하려 하면 두 시스템이 서로 모르는 채 같은 몬스터
    /// Transform 집합에 동시에 쓰기 Job을 스케줄링하게 되어 Unity 세이프티 시스템이 레이스
    /// 컨디션 예외를 던질 위험이 생긴다. 이 위험을 근본적으로 없애려면 하나의
    /// TransformAccessArray를 공유하고 JobHandle 의존성으로 실행 순서를 명시적으로
    /// 체이닝해야 하므로, 이번 기회에 두 시스템을 합쳤다(성능이 아니라 안전성이 통합의
    /// 이유 — 겹침 회피 자체는 3주차 측정에서 이미 충분히 빨랐다).
    ///
    /// FlowFieldMonsterMovementSystem/SpatialHashMonsterSeparationSystem은 삭제하지 않고
    /// 씬에 비활성 상태로 남겨 Before 대조군으로 보존한다(이 프로젝트의 기존 관례).
    ///
    /// Register/Unregister 생명주기가 다르다는 문제: IMonsterSeparationSystem.Register는
    /// 스폰 즉시(Idle 상태부터) 호출되지만, IMonsterMovementSystem.Register는 AI가 Chase로
    /// 전이할 때만 호출된다(Idle/Attack 상태 몬스터는 이동해선 안 됨). 두 인터페이스의
    /// Register/Unregister는 시그니처가 우연히 같아(Register(MonsterView)) 암묵적 구현으로는
    /// 하나로 뭉개지므로, 명시적 인터페이스 구현(explicit interface implementation)으로
    /// 완전히 분리한다: Separation 쪽이 TransformAccessArray/리스트의 실제 추가/제거를
    /// 담당하고, Movement 쪽은 이미 등록된 인덱스의 _movementActive 플래그만 켜고 끈다
    /// (PooledMonsterSpawner.Spawn이 항상 Separation-Register를 먼저 호출하고, Chase 전이는
    /// 그 이후 프레임에 일어나므로 순서는 항상 보장된다).
    /// </summary>
    public class MonsterMovementResolver : MonoBehaviour, IMonsterMovementSystem, IMonsterSeparationSystem
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private int _initialCapacity = 500;
        [SerializeField] private float _gridHalfExtent = 40f;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [SerializeField] private float _regenerateInterval = 0.2f;
        [SerializeField] private float _separationRadius = 0.5f;
        [SerializeField] private int _bucketCount = 1024;

        private IPlayerMotor _playerMotor;
        private TransformAccessArray _transformAccessArray;
        private readonly List<MonsterView> _viewsByIndex = new List<MonsterView>();
        private readonly Dictionary<MonsterView, int> _indexByView = new Dictionary<MonsterView, int>();
        private JobHandle _jobHandle;

        private WalkableGrid _grid;
        private FlowFieldGenerator _flowField;
        private float _regenerateTimer;

        // _movementActive는 몬스터별 영구 상태(Chase 상태 여부)라 스왑 제거 시 값도 함께
        // 옮겨야 한다. 반면 _bucketHeads/_next/_positions는 매 프레임 처음부터 다시 채우는
        // 스크래치 버퍼라 성장 시 이전 내용을 보존할 필요가 없다(EnsureCapacity 참고).
        private NativeArray<bool> _movementActive;
        private NativeArray<int> _bucketHeads;
        private NativeArray<int> _next;
        private NativeArray<float2> _positions;
        private int _capacity;

        [Inject]
        public void Construct(IPlayerMotor playerMotor)
        {
            _playerMotor = playerMotor;
        }

        private void Awake()
        {
            _transformAccessArray = new TransformAccessArray(_initialCapacity);
            _grid = WalkableGrid.Bake(Vector2.zero, _gridHalfExtent, _cellSize, _obstacleLayerMask);
            _flowField = new FlowFieldGenerator(_grid);
            _regenerateTimer = _regenerateInterval;

            _bucketHeads = new NativeArray<int>(_bucketCount, Allocator.Persistent);
            _capacity = _initialCapacity;
            _movementActive = new NativeArray<bool>(_capacity, Allocator.Persistent);
            _next = new NativeArray<int>(_capacity, Allocator.Persistent);
            _positions = new NativeArray<float2>(_capacity, Allocator.Persistent);
        }

        void IMonsterSeparationSystem.Register(MonsterView view)
        {
            _jobHandle.Complete();

            var index = _viewsByIndex.Count;
            EnsureCapacity(index + 1);

            _indexByView[view] = index;
            _viewsByIndex.Add(view);
            _transformAccessArray.Add(view.transform);
            _movementActive[index] = false;
        }

        void IMonsterSeparationSystem.Unregister(MonsterView view)
        {
            _jobHandle.Complete();

            if (!_indexByView.TryGetValue(view, out var index))
            {
                return;
            }

            var lastIndex = _viewsByIndex.Count - 1;
            _transformAccessArray.RemoveAtSwapBack(index);

            var movedView = _viewsByIndex[lastIndex];
            _viewsByIndex[index] = movedView;
            _viewsByIndex.RemoveAt(lastIndex);
            _indexByView.Remove(view);
            _movementActive[index] = _movementActive[lastIndex];

            if (movedView != view)
            {
                _indexByView[movedView] = index;
            }
        }

        void IMonsterMovementSystem.Register(MonsterView view)
        {
            _jobHandle.Complete();

            if (_indexByView.TryGetValue(view, out var index))
            {
                _movementActive[index] = true;
            }
        }

        void IMonsterMovementSystem.Unregister(MonsterView view)
        {
            _jobHandle.Complete();

            if (_indexByView.TryGetValue(view, out var index))
            {
                _movementActive[index] = false;
            }
        }

        // _next/_positions는 매 프레임 재구성되는 스크래치 버퍼라 성장 시 그냥 새로 할당해도
        // 되지만, _movementActive는 영구 상태라 이전 내용을 복사해서 옮겨야 한다.
        private void EnsureCapacity(int requiredCount)
        {
            if (_capacity >= requiredCount)
            {
                return;
            }

            var newCapacity = Mathf.Max(_capacity * 2, requiredCount);

            var newMovementActive = new NativeArray<bool>(newCapacity, Allocator.Persistent);
            NativeArray<bool>.Copy(_movementActive, newMovementActive, _capacity);
            _movementActive.Dispose();
            _movementActive = newMovementActive;

            _next.Dispose();
            _next = new NativeArray<int>(newCapacity, Allocator.Persistent);

            _positions.Dispose();
            _positions = new NativeArray<float2>(newCapacity, Allocator.Persistent);

            _capacity = newCapacity;
        }

        private void Update()
        {
            if (_transformAccessArray.length == 0 || _playerMotor == null)
            {
                return;
            }

            var target = _playerMotor.Position;

            _regenerateTimer += Time.deltaTime;
            if (_regenerateTimer >= _regenerateInterval)
            {
                _regenerateTimer = 0f;
                _flowField.Generate(_grid, target);
            }

            // SpatialHashMonsterSeparationSystem이 이미 하던 것과 동일한 O(n) 메인 스레드
            // 패스: 몬스터 위치 스냅샷 + 버킷 구성. 이번 통합으로 비용이 늘지 않는다.
            var count = _transformAccessArray.length;
            var cellSize = _separationRadius * 2f;
            var cellSizeInv = 1f / cellSize;

            for (var b = 0; b < _bucketHeads.Length; b++)
            {
                _bucketHeads[b] = -1;
            }

            for (var i = 0; i < count; i++)
            {
                var pos = _viewsByIndex[i].transform.position;
                _positions[i] = new float2(pos.x, pos.z);

                var cellX = Mathf.FloorToInt(pos.x * cellSizeInv);
                var cellZ = Mathf.FloorToInt(pos.z * cellSizeInv);
                var bucket = HashCell(cellX, cellZ);
                _next[i] = _bucketHeads[bucket];
                _bucketHeads[bucket] = i;
            }

            var moveJob = new FlowFieldSeekJob
            {
                Directions = _flowField.Directions,
                Walkable = _grid.Walkable,
                MovementActive = _movementActive,
                GridOrigin = new float2(_grid.Origin.x, _grid.Origin.y),
                CellSize = _grid.CellSize,
                GridWidth = _grid.Width,
                GridHeight = _grid.Height,
                FallbackTarget = new float2(target.x, target.y),
                MoveSpeed = _moveSpeed,
                DeltaTime = Time.deltaTime
            };
            var moveHandle = moveJob.Schedule(_transformAccessArray);

            // separationJob은 moveHandle에 의존해 스케줄된다 — 이동이 먼저 Transform에
            // 반영된 뒤에만 겹침 회피가 실행되도록 Unity가 순서를 보장하며, 세이프티
            // 시스템도 두 Job이 같은 TransformAccessArray에 동시에 쓰지 않음을 인지한다.
            var separationJob = new MonsterSeparationJob
            {
                Positions = _positions,
                BucketHeads = _bucketHeads,
                Next = _next,
                Walkable = _grid.Walkable,
                CellSize = cellSize,
                SeparationRadius = _separationRadius,
                BucketCount = _bucketHeads.Length,
                WalkableGridOrigin = new float2(_grid.Origin.x, _grid.Origin.y),
                WalkableCellSize = _grid.CellSize,
                WalkableGridWidth = _grid.Width,
                WalkableGridHeight = _grid.Height
            };
            _jobHandle = separationJob.Schedule(_transformAccessArray, moveHandle);
        }

        private void LateUpdate()
        {
            _jobHandle.Complete();
        }

        private void OnDestroy()
        {
            _jobHandle.Complete();

            if (_transformAccessArray.isCreated)
            {
                _transformAccessArray.Dispose();
            }

            _flowField?.Dispose();
            _grid?.Dispose();

            if (_movementActive.IsCreated)
            {
                _movementActive.Dispose();
            }

            if (_bucketHeads.IsCreated)
            {
                _bucketHeads.Dispose();
            }

            if (_next.IsCreated)
            {
                _next.Dispose();
            }

            if (_positions.IsCreated)
            {
                _positions.Dispose();
            }
        }

        // SpatialHashMonsterSeparationSystem.HashCell과 동일하다 — 메인 스레드에서 버킷을
        // 구성할 때 쓰고, Job 내부(MonsterSeparationJob.HashCell)에서도 같은 해시를 다시
        // 계산한다(Burst Job은 이 클래스의 메서드를 호출할 수 없어 복제가 불가피하다).
        private int HashCell(int cellX, int cellZ)
        {
            unchecked
            {
                var hash = cellX * 92837111 ^ cellZ * 689287499;
                if (hash < 0)
                {
                    hash = -hash;
                }

                return hash % _bucketHeads.Length;
            }
        }
    }
}
