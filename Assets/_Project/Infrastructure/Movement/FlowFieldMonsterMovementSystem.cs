using System.Collections.Generic;
using LegionBreak.Application.Movement;
using LegionBreak.Infrastructure.Pathfinding;
using LegionBreak.Infrastructure.Spawning;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using VContainer;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// After(5주차): 몬스터가 직선(MonsterSeekJob)이 아니라 WalkableGrid+FlowFieldGenerator가
    /// 만든 방향장을 따라 장애물을 우회해 플레이어에게 접근한다. 이동 자체의 병렬화 방식은
    /// JobMonsterMovementSystem과 동일(IJobParallelForTransform+Burst, TransformAccessArray를
    /// Register/Unregister로 증분 갱신) — 이번 변경의 핵심은 "타겟 방향을 어디서 구하는가"
    /// (단일 직선 벡터 → 격자별 방향장 샘플링)이지 병렬화 기법 자체가 아니다.
    /// Register/Unregister의 Complete() 선행 호출, 스왑 제거 인덱스 관리 등은
    /// JobMonsterMovementSystem과 동일한 이유로 동일하게 구현되어 있다.
    ///
    /// FlowField는 매 프레임 재계산하지 않는다. BFS 비용 자체는 이 그리드 규모에서 이미
    /// 충분히 싸지만, 플레이어가 한두 프레임 사이 이동한 정도로 최단 경로가 바뀔 일은 거의
    /// 없어 _regenerateInterval(기본 0.2초)마다만 갱신한다(MonsterCountView의 0.25초
    /// 스로틀과 같은 이유 — 불필요한 재계산 자체를 줄이는 것이 목적).
    ///
    /// _obstacleLayerMask는 기본값(Nothing)일 경우 모든 셀이 walkable로 베이크되어
    /// 기존 직선 Seek와 동일하게 동작한다 — 씬에 장애물 레이어를 아직 구성하지 않은
    /// 상태에서도 안전하게 대체 가능하도록 하기 위함.
    /// </summary>
    public class FlowFieldMonsterMovementSystem : MonoBehaviour, IMonsterMovementSystem, IWalkableGridProvider
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private int _initialCapacity = 500;
        [SerializeField] private float _gridHalfExtent = 40f;
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private LayerMask _obstacleLayerMask;
        [SerializeField] private float _regenerateInterval = 0.2f;

        private IPlayerMotor _playerMotor;
        private TransformAccessArray _transformAccessArray;
        private readonly List<MonsterView> _viewsByIndex = new List<MonsterView>();
        private readonly Dictionary<MonsterView, int> _indexByView = new Dictionary<MonsterView, int>();
        private JobHandle _jobHandle;

        private WalkableGrid _grid;
        private FlowFieldGenerator _flowField;
        private float _regenerateTimer;

        // SpatialHashMonsterSeparationSystem이 겹침 회피 push가 몬스터를 장애물 칸으로
        // 밀어 넣지 않도록 검사할 때 같은 그리드를 읽어야 해서 IWalkableGridProvider로 노출한다.
        public WalkableGrid Grid => _grid;

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
        }

        public void Register(MonsterView view)
        {
            _jobHandle.Complete();

            _indexByView[view] = _transformAccessArray.length;
            _viewsByIndex.Add(view);
            _transformAccessArray.Add(view.transform);
        }

        public void Unregister(MonsterView view)
        {
            _jobHandle.Complete();

            if (!_indexByView.TryGetValue(view, out var index))
            {
                return;
            }

            var lastIndex = _transformAccessArray.length - 1;
            _transformAccessArray.RemoveAtSwapBack(index);

            var movedView = _viewsByIndex[lastIndex];
            _viewsByIndex[index] = movedView;
            _viewsByIndex.RemoveAt(lastIndex);
            _indexByView.Remove(view);

            if (movedView != view)
            {
                _indexByView[movedView] = index;
            }
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

            var job = new FlowFieldSeekJob
            {
                Directions = _flowField.Directions,
                Walkable = _grid.Walkable,
                GridOrigin = new float2(_grid.Origin.x, _grid.Origin.y),
                CellSize = _grid.CellSize,
                GridWidth = _grid.Width,
                GridHeight = _grid.Height,
                FallbackTarget = new float2(target.x, target.y),
                MoveSpeed = _moveSpeed,
                DeltaTime = Time.deltaTime
            };
            _jobHandle = job.Schedule(_transformAccessArray);
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
        }
    }
}
