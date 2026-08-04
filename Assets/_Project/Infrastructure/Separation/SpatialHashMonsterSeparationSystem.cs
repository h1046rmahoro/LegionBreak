using System.Collections.Generic;
using LegionBreak.Infrastructure.Pathfinding;
using LegionBreak.Infrastructure.Spawning;
using UnityEngine;
using VContainer;

namespace LegionBreak.Infrastructure.Separation
{
    /// <summary>
    /// After: 몬스터를 셀 크기 = 겹침 판정 반경*2인 균일 그리드에 매 프레임 재삽입하고,
    /// 자기 셀 + 주변 8칸(총 3x3)만 검사해 겹침을 밀어낸다. 셀 크기를 판정 거리와
    /// 맞췄기 때문에 겹칠 수 있는 쌍은 반드시 3x3 범위 안에만 존재한다.
    /// 겹침 판정 반경과 밀어내는 공식은 Before(BruteForceMonsterSeparationSystem)와 동일하게
    /// 맞춰 알고리즘(O(n²) vs 공간 분할) 차이만 비교되도록 한다.
    ///
    /// Dictionary&lt;cell, List&lt;index&gt;&gt; 방식은 몬스터가 매 프레임 셀을 옮겨 다니므로
    /// List 재구성 과정에서 GC Alloc이 반복 발생할 위험이 있다(1주차에서 확인한 Collider
    /// 재삽입 비용과 유사한 함정). 대신 버킷 헤드(int[])+ 인덱스 기반 연결 리스트(int[])로
    /// 그리드를 구성해, 초기 용량 확보 이후에는 매 프레임 할당이 없다.
    ///
    /// (5주차 후속) 몬스터 밀도가 높아지면 이 push가 몬스터를 장애물 칸 안으로 밀어 넣어
    /// 장애물을 뚫고 지나가는 문제가 있었다 — 이 시스템은 원래 몬스터-몬스터 거리만 보고
    /// transform.position을 옮길 뿐 WalkableGrid를 전혀 몰랐기 때문이다. IWalkableGridProvider로
    /// FlowFieldMonsterMovementSystem이 베이크한 그리드를 주입받아, push 결과 위치가
    /// walkable이 아니면 그 push를 적용하지 않도록 막는다(반대쪽 몬스터는 그대로 밀림 —
    /// 벽 쪽 몬스터가 벽에 눌려 멈추는 것과 동일한 결과). 그리드 베이크/해제 소유권은 여전히
    /// FlowFieldMonsterMovementSystem에 있고, 여기서는 Update()에서 매 프레임 참조만 읽는다
    /// (생성자 시점에 캐시하지 않는 이유: Unity의 Awake 호출 순서는 컴포넌트 간에 보장되지
    /// 않아 이 시스템의 Construct가 먼저 실행되면 그리드가 아직 베이크되지 않았을 수 있다.
    /// 반면 Update는 씬의 모든 Awake가 끝난 뒤에만 시작되므로 항상 안전하다).
    /// </summary>
    public class SpatialHashMonsterSeparationSystem : MonoBehaviour, IMonsterSeparationSystem
    {
        [SerializeField] private float _separationRadius = 0.5f;
        [SerializeField] private int _initialCapacity = 512;
        [SerializeField] private int _bucketCount = 1024;

        private readonly List<MonsterView> _viewsByIndex = new List<MonsterView>();
        private readonly Dictionary<MonsterView, int> _indexByView = new Dictionary<MonsterView, int>();

        private int[] _bucketHeads;
        private int[] _next;
        private IWalkableGridProvider _gridProvider;

        [Inject]
        public void Construct(IWalkableGridProvider gridProvider)
        {
            _gridProvider = gridProvider;
        }

        private void Awake()
        {
            _bucketHeads = new int[_bucketCount];
            _next = new int[_initialCapacity];
        }

        public void Register(MonsterView view)
        {
            var index = _viewsByIndex.Count;
            _indexByView[view] = index;
            _viewsByIndex.Add(view);
            EnsureCapacity(_viewsByIndex.Count);
        }

        public void Unregister(MonsterView view)
        {
            if (!_indexByView.TryGetValue(view, out var index))
            {
                return;
            }

            var lastIndex = _viewsByIndex.Count - 1;
            var movedView = _viewsByIndex[lastIndex];
            _viewsByIndex[index] = movedView;
            _viewsByIndex.RemoveAt(lastIndex);
            _indexByView.Remove(view);

            if (movedView != view)
            {
                _indexByView[movedView] = index;
            }
        }

        private void EnsureCapacity(int requiredCount)
        {
            if (_next.Length >= requiredCount)
            {
                return;
            }

            var newCapacity = Mathf.Max(_next.Length * 2, requiredCount);
            _next = new int[newCapacity];
        }

        private void Update()
        {
            var count = _viewsByIndex.Count;
            if (count == 0)
            {
                return;
            }

            // 캐시하지 않고 매 프레임 Update()에서 읽는 이유는 클래스 주석 참고.
            var grid = _gridProvider?.Grid;

            var cellSize = _separationRadius * 2f;
            var cellSizeInv = 1f / cellSize;
            var minDistanceSq = cellSize * cellSize;

            for (var b = 0; b < _bucketHeads.Length; b++)
            {
                _bucketHeads[b] = -1;
            }

            for (var i = 0; i < count; i++)
            {
                var pos = _viewsByIndex[i].transform.position;
                var cellX = Mathf.FloorToInt(pos.x * cellSizeInv);
                var cellZ = Mathf.FloorToInt(pos.z * cellSizeInv);
                var bucket = HashCell(cellX, cellZ);
                _next[i] = _bucketHeads[bucket];
                _bucketHeads[bucket] = i;
            }

            for (var i = 0; i < count; i++)
            {
                var transformA = _viewsByIndex[i].transform;
                var posA = transformA.position;
                var cellX = Mathf.FloorToInt(posA.x * cellSizeInv);
                var cellZ = Mathf.FloorToInt(posA.z * cellSizeInv);

                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var bucket = HashCell(cellX + dx, cellZ + dz);
                        var j = _bucketHeads[bucket];
                        while (j != -1)
                        {
                            if (j > i)
                            {
                                var transformB = _viewsByIndex[j].transform;
                                var posB = transformB.position;

                                var deltaX = posA.x - posB.x;
                                var deltaZ = posA.z - posB.z;
                                var distanceSq = deltaX * deltaX + deltaZ * deltaZ;

                                if (distanceSq < minDistanceSq && distanceSq >= 0.0001f)
                                {
                                    var distance = Mathf.Sqrt(distanceSq);
                                    var overlap = cellSize - distance;
                                    var pushX = deltaX / distance * overlap * 0.5f;
                                    var pushZ = deltaZ / distance * overlap * 0.5f;

                                    // 장애물 칸으로 밀어 넣는 push는 적용하지 않는다(반대쪽은
                                    // 그대로 밀림) — 벽 쪽 몬스터가 벽에 눌려 멈추는 결과가 되어
                                    // 겹침은 완전히 해소되지 않을 수 있지만, 장애물을 뚫고
                                    // 지나가는 것보다는 낫다.
                                    var candidateA = new Vector3(posA.x + pushX, posA.y, posA.z + pushZ);
                                    var candidateB = new Vector3(posB.x - pushX, posB.y, posB.z - pushZ);

                                    if (IsWalkable(grid, candidateA))
                                    {
                                        posA = candidateA;
                                        transformA.position = posA;
                                    }

                                    if (IsWalkable(grid, candidateB))
                                    {
                                        posB = candidateB;
                                        transformB.position = posB;
                                    }
                                }
                            }

                            j = _next[j];
                        }
                    }
                }
            }
        }

        // grid가 없거나(그리드를 공유하는 FlowFieldMonsterMovementSystem이 씬에 없는 경우 —
        // 예: JobMonsterMovementSystem으로 되돌린 경우) 격자 범위 밖이면 항상 walkable로
        // 간주한다. 장애물 정보가 없는 상태에서 겹침 회피 자체를 막을 이유가 없기 때문이다.
        private static bool IsWalkable(WalkableGrid grid, Vector3 worldPos)
        {
            if (grid == null)
            {
                return true;
            }

            if (!grid.TryWorldToCell(new Vector2(worldPos.x, worldPos.z), out var cellX, out var cellZ))
            {
                return true;
            }

            return grid.IsWalkable(cellX, cellZ);
        }

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
