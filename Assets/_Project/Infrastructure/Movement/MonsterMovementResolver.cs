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
        // PlayerMoveUseCase의 TurnSpeedDegreesPerSecond와 같은 개념/기본값이다 — 플레이어와
        // 달리 몬스터는 이동 계산 자체가 Application UseCase 없이 FlowFieldSeekJob 안에서
        // 전부 끝나므로, 회전 속도도 별도 계층을 만들지 않고 이 Job의 파라미터로 그대로
        // 전달한다.
        [SerializeField] private float _turnSpeedDegreesPerSecond = 720f;
        [SerializeField] private int _initialCapacity = 500;
        [SerializeField] private float _gridHalfExtent = 40f;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [SerializeField] private float _regenerateInterval = 0.2f;
        [SerializeField] private float _separationRadius = 0.5f;
        [SerializeField] private int _bucketCount = 1024;

        // 겹치는 이웃마다 push를 그대로 누적하면(MonsterSeparationJob 참고) 밀집 클러스터에서
        // 오버슈트→반대편 오버슈트가 반복되는 진동(떨림)이 생긴다. 1이면 매 프레임 겹침을
        // 즉시 완전히 해소하려다 진동하고, 값을 낮출수록 여러 프레임에 걸쳐 서서히
        // 수렴한다(2026-08-27, 좀비 모델 연결 후 밀집 클러스터에서 떨림 발견/수정). 기본값
        // 0.2는 이론상 합리적인 초기값일 뿐 아직 Play 모드로 실측 튜닝하지 않았다 — 떨림이
        // 남아있으면 더 낮추고, 겹침 해소가 너무 느리면 조금 올려서 확인 필요.
        [SerializeField] private float _separationStrength = 0.2f;

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

        // 마지막으로 버킷/체인(_bucketHeads/_next/_positions)을 구성할 때의 몬스터 수. 에디터
        // 디버그 오버레이 전용이다(Job은 Update가 버킷을 구성한 직후에만 스케줄되므로 필요 없음).
        // 몬스터가 등록됐지만 Update가 아직 버킷을 만들기 전(재시작 직후 등)이나 EnsureCapacity가
        // _next를 새로 할당한 직후에는 0으로 초기화된 버퍼가 남아 있다. 이때 체인을 따라가면
        // next[0] == 0이라 0 → 0 → 0…으로 끝나지 않는다. 그래서 오버레이는 이 값 범위 안에서만
        // 체인을 따라간다.
        private int _bucketBuiltCount;

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
            // _next가 0으로 초기화된 새 버퍼로 바뀌어 기존 체인은 더는 유효하지 않다.
            _bucketBuiltCount = 0;
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

            _bucketBuiltCount = count;

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
                DeltaTime = Time.deltaTime,
                TurnSpeedDegreesPerSecond = _turnSpeedDegreesPerSecond
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
                SeparationStrength = _separationStrength,
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

        // ---- 에디터 디버그 오버레이 전용 읽기 접근자 (9주차, Editor/Movement/MovementDebugGizmo) ----
        // internal + InternalsVisibleTo("LegionBreak.Editor")라 런타임 어셈블리(Presentation 등)
        // 에서는 보이지 않는다 — 게임 코드가 이동 파이프라인 내부 버퍼에 의존하는 경로를 만들지
        // 않으면서, 씬 뷰에서 FlowField/Spatial Hash의 실제 런타임 상태를 그대로 그리기 위함이다.
        // 복사본을 만들지 않고 Job이 실제로 읽는 버퍼를 그대로 노출해야 "보이는 것 = Job이 쓰는
        // 것"이 보장된다.
        internal WalkableGrid DebugGrid => _grid;
        internal FlowFieldGenerator DebugFlowField => _flowField;
        internal int DebugMonsterCount => _viewsByIndex.Count;
        internal int DebugBucketBuiltCount => _bucketBuiltCount;
        internal float DebugSeparationRadius => _separationRadius;
        internal float DebugSeparationCellSize => _separationRadius * 2f;
        internal NativeArray<float2> DebugPositions => _positions;
        internal NativeArray<int> DebugBucketHeads => _bucketHeads;
        internal NativeArray<int> DebugNext => _next;
        internal int DebugHashCell(int cellX, int cellZ) => HashCell(cellX, cellZ);
        internal bool DebugTryGetIndex(MonsterView view, out int index) => _indexByView.TryGetValue(view, out index);

        // 스케줄된 Job이 아직 버퍼를 쓰는 중이면 메인 스레드 읽기가 세이프티 예외를 던진다.
        // 씬 뷰 GUI는 LateUpdate의 Complete() 이후에 그려지므로 보통은 이미 끝나 있지만,
        // 호출 시점을 에디터가 보장하지 않으므로 읽기 전에 명시적으로 완료시킨다.
        internal void DebugCompleteJobs() => _jobHandle.Complete();

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
