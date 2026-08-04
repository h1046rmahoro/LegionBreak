using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace LegionBreak.Infrastructure.Pathfinding
{
    /// <summary>
    /// WalkableGrid 위에서 목표 지점(플레이어) 기준 BFS 거리장을 계산하고, 거리장의
    /// 최급하강(steepest descent) 방향으로 셀별 이동 방향(Directions)을 만든다.
    /// BFS 자체는 웨이브프론트 특성상 병렬화가 까다로워(7주차 로드맵 메모 — 레이어별로
    /// 나눠 처리하는 방식 조사 예정) 지금은 메인 스레드에서 계산하고, 결과(Directions)만
    /// Burst Job(FlowFieldSeekJob)이 읽는다.
    ///
    /// GC Alloc 0B/frame 원칙에 맞춰 BFS 큐/거리 버퍼는 생성자에서 1회만 할당하고
    /// Generate() 호출마다 재사용한다.
    /// </summary>
    public sealed class FlowFieldGenerator
    {
        private static readonly (int Dx, int Dz)[] Neighbors8 =
        {
            (1, 0), (-1, 0), (0, 1), (0, -1),
            (1, 1), (1, -1), (-1, 1), (-1, -1)
        };

        // WalkableGrid.Walkable와 같은 이유(CS1612/CS1648)로 get 전용 프로퍼티도, readonly
        // 필드도 아니라 일반 필드로 둔다 — Generate() 내부에서 Directions[index] = ...로
        // 인덱서 대입을 해야 하기 때문.
        public NativeArray<float2> Directions;

        private readonly int _width;
        private readonly int _height;
        // Directions와 같은 이유(CS1648)로 readonly를 붙이지 않는다 — Generate()에서
        // _distances[i] = ...로 인덱서 대입이 필요하다.
        private NativeArray<int> _distances;
        private readonly int[] _queue;

        public FlowFieldGenerator(WalkableGrid grid)
        {
            _width = grid.Width;
            _height = grid.Height;
            Directions = new NativeArray<float2>(_width * _height, Allocator.Persistent);
            _distances = new NativeArray<int>(_width * _height, Allocator.Persistent);
            _queue = new int[_width * _height];
        }

        public void Generate(WalkableGrid grid, Vector2 goalWorldXZ)
        {
            if (!TryFindGoalCell(grid, goalWorldXZ, out var goalX, out var goalZ))
            {
                return;
            }

            for (var i = 0; i < _distances.Length; i++)
            {
                _distances[i] = int.MaxValue;
            }

            var head = 0;
            var tail = 0;
            var goalIndex = grid.CellIndex(goalX, goalZ);
            _distances[goalIndex] = 0;
            _queue[tail++] = goalIndex;

            while (head < tail)
            {
                var index = _queue[head++];
                var cx = index % _width;
                var cz = index / _width;
                var currentDistance = _distances[index];

                for (var n = 0; n < Neighbors8.Length; n++)
                {
                    var nx = cx + Neighbors8[n].Dx;
                    var nz = cz + Neighbors8[n].Dz;
                    if (!grid.IsWalkable(nx, nz))
                    {
                        continue;
                    }

                    var neighborIndex = grid.CellIndex(nx, nz);
                    if (_distances[neighborIndex] != int.MaxValue)
                    {
                        continue;
                    }

                    _distances[neighborIndex] = currentDistance + 1;
                    _queue[tail++] = neighborIndex;
                }
            }

            for (var z = 0; z < _height; z++)
            {
                for (var x = 0; x < _width; x++)
                {
                    var index = grid.CellIndex(x, z);
                    if (!grid.IsWalkable(x, z) || _distances[index] == int.MaxValue)
                    {
                        Directions[index] = float2.zero;
                        continue;
                    }

                    var bestDistance = _distances[index];
                    var bestDx = 0;
                    var bestDz = 0;
                    for (var n = 0; n < Neighbors8.Length; n++)
                    {
                        var nx = x + Neighbors8[n].Dx;
                        var nz = z + Neighbors8[n].Dz;
                        if (!grid.IsWalkable(nx, nz))
                        {
                            continue;
                        }

                        var neighborDistance = _distances[grid.CellIndex(nx, nz)];
                        if (neighborDistance < bestDistance)
                        {
                            bestDistance = neighborDistance;
                            bestDx = Neighbors8[n].Dx;
                            bestDz = Neighbors8[n].Dz;
                        }
                    }

                    Directions[index] = bestDx == 0 && bestDz == 0
                        ? float2.zero
                        : math.normalize(new float2(bestDx, bestDz));
                }
            }
        }

        // 플레이어가 장애물 위(콜라이더가 겹치는 셀 등)에 서 있는 예외적인 경우를 대비해,
        // 목표 셀이 walkable이 아니면 주변 3칸 반경 안에서 가장 가까운 walkable 셀로 대체한다.
        private bool TryFindGoalCell(WalkableGrid grid, Vector2 goalWorldXZ, out int goalX, out int goalZ)
        {
            grid.TryWorldToCell(goalWorldXZ, out goalX, out goalZ);
            goalX = Mathf.Clamp(goalX, 0, grid.Width - 1);
            goalZ = Mathf.Clamp(goalZ, 0, grid.Height - 1);

            if (grid.IsWalkable(goalX, goalZ))
            {
                return true;
            }

            for (var radius = 1; radius <= 3; radius++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var x = goalX + dx;
                        var z = goalZ + dz;
                        if (grid.IsWalkable(x, z))
                        {
                            goalX = x;
                            goalZ = z;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public void Dispose()
        {
            if (Directions.IsCreated)
            {
                Directions.Dispose();
            }

            if (_distances.IsCreated)
            {
                _distances.Dispose();
            }
        }
    }
}
