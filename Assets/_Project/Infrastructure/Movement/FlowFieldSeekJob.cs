using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// MonsterSeekJob과 같은 구조(IJobParallelForTransform+Burst)지만, 단일 직선 타겟 대신
    /// FlowFieldGenerator가 만든 셀별 방향장(Directions)을 샘플링해 이동한다.
    ///
    /// 몬스터가 서 있는 칸이 walkable이 아닌 경우(SpatialHashMonsterSeparationSystem이
    /// 밀도가 높을 때 겹침 회피로 몬스터를 장애물 칸 안으로 밀어 넣었거나, 스폰 위치가
    /// 우연히 장애물과 겹치는 등)에는 방향장 대신 근처 walkable 칸으로 빠져나가는 방향을
    /// 찾는다(FindEscapeDirection) — 이 경우에도 곧장 FallbackTarget(플레이어 위치)으로
    /// 직선 이동하면 장애물을 그대로 뚫고 지나가 버리기 때문이다. 격자 자체 바깥에 있거나
    /// 반경 3칸 안에 walkable 칸이 전혀 없는 완전한 예외 상황에서만 최후 수단으로
    /// FallbackTarget 직선 이동을 사용한다.
    /// </summary>
    [BurstCompile]
    public struct FlowFieldSeekJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float2> Directions;
        [ReadOnly] public NativeArray<bool> Walkable;
        public float2 GridOrigin;
        public float CellSize;
        public int GridWidth;
        public int GridHeight;
        public float2 FallbackTarget;
        public float MoveSpeed;
        public float DeltaTime;

        public void Execute(int index, TransformAccess transform)
        {
            var current = transform.position;
            var currentXZ = new float2(current.x, current.z);
            var direction = SampleWalkableDirection(currentXZ);

            if (math.all(direction == float2.zero))
            {
                var toTarget = FallbackTarget - currentXZ;
                var distanceSq = math.lengthsq(toTarget);
                if (distanceSq < 0.0001f)
                {
                    return;
                }

                direction = toTarget * math.rsqrt(distanceSq);
            }

            var delta = direction * MoveSpeed * DeltaTime;
            transform.position = new Vector3(current.x + delta.x, current.y, current.z + delta.y);
        }

        private float2 SampleWalkableDirection(float2 worldXZ)
        {
            var local = (worldXZ - GridOrigin) / CellSize;
            var cellX = (int)math.floor(local.x);
            var cellZ = (int)math.floor(local.y);

            if (cellX < 0 || cellZ < 0 || cellX >= GridWidth || cellZ >= GridHeight)
            {
                return float2.zero;
            }

            var cellIndex = cellX + cellZ * GridWidth;
            if (Walkable[cellIndex])
            {
                return Directions[cellIndex];
            }

            return FindEscapeDirection(worldXZ, cellX, cellZ);
        }

        // 현재 칸이 walkable이 아닐 때, 반경을 넓혀가며(1~3칸) 가장 먼저 발견되는 walkable
        // 칸의 중심을 향한 방향을 반환한다. 엄밀한 최근접 탐색은 아니지만(정사각 링 안에서
        // 처음 찾은 칸을 그대로 사용) 장애물 밖으로 빠져나가는 방향이면 충분하다.
        private float2 FindEscapeDirection(float2 worldXZ, int originCellX, int originCellZ)
        {
            for (var radius = 1; radius <= 3; radius++)
            {
                for (var dz = -radius; dz <= radius; dz++)
                {
                    for (var dx = -radius; dx <= radius; dx++)
                    {
                        var cx = originCellX + dx;
                        var cz = originCellZ + dz;
                        if (cx < 0 || cz < 0 || cx >= GridWidth || cz >= GridHeight)
                        {
                            continue;
                        }

                        var cellIndex = cx + cz * GridWidth;
                        if (!Walkable[cellIndex])
                        {
                            continue;
                        }

                        var cellCenter = GridOrigin + new float2(cx + 0.5f, cz + 0.5f) * CellSize;
                        var toCell = cellCenter - worldXZ;
                        var distanceSq = math.lengthsq(toCell);
                        if (distanceSq < 0.0001f)
                        {
                            return float2.zero;
                        }

                        return toCell * math.rsqrt(distanceSq);
                    }
                }
            }

            return float2.zero;
        }
    }
}
