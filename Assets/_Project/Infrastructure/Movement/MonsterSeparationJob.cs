using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// SpatialHashMonsterSeparationSystem(메인 스레드 버전)과 동일한 3x3 버킷 겹침 회피
    /// 알고리즘을 Burst Job으로 옮긴 것. MonsterMovementResolver가 FlowFieldSeekJob과
    /// 같은 TransformAccessArray를 공유하며, 이 Job은 항상 그 Job의 JobHandle에 의존해
    /// 스케줄된다(이동이 먼저 적용된 뒤 겹침을 정리).
    ///
    /// 원본과 다른 점 두 가지:
    /// 1. 원본은 pair(i, j)를 한 번만 계산해 양쪽에 동시에 0.5씩 적용했다(양쪽 Transform을
    ///    같은 순간에 씀). 이 Job은 인덱스별 병렬 실행이라 "내 Transform만 쓴다"는 제약이
    ///    있으므로, 각 인덱스가 자기 3x3 주변 이웃 전체를 스캔해 받는 총 push를 혼자
    ///    누적한 뒤 자기 위치에만 적용한다 — 같은 pair를 양쪽에서 한 번씩(총 2번) 계산하게
    ///    되어 연산량은 늘지만, 다른 인덱스의 Transform을 절대 쓰지 않으므로 완전히
    ///    병렬 안전하다.
    /// 2. 이웃 위치(Positions)는 이번 프레임 이동(FlowFieldSeekJob) 적용 "이전" 스냅샷이다.
    ///    자기 자신의 현재 위치는 TransformAccess로 읽으므로 이동 반영 "이후" 값이다.
    ///    자기(이동 후) vs 이웃(이동 전) 사이에 한 프레임 미만의 시차가 생기지만, 프레임당
    ///    이동량이 겹침 판정 반경에 비해 매우 작아 무시 가능하다 — 원본도 이미 "완전한
    ///    물리가 아닌 근사"라고 명시하고 있어 정신은 동일하다.
    /// </summary>
    [BurstCompile]
    public struct MonsterSeparationJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeArray<float2> Positions;
        [ReadOnly] public NativeArray<int> BucketHeads;
        [ReadOnly] public NativeArray<int> Next;
        [ReadOnly] public NativeArray<bool> Walkable;

        public float CellSize;
        public float SeparationRadius;
        public int BucketCount;

        public float2 WalkableGridOrigin;
        public float WalkableCellSize;
        public int WalkableGridWidth;
        public int WalkableGridHeight;

        public void Execute(int index, TransformAccess transform)
        {
            // 자기 자신의 위치는 반드시 TransformAccess에서 읽어야 한다 — 이 Job은
            // FlowFieldSeekJob의 JobHandle에 의존해 스케줄되므로, 여기서 읽는
            // transform.position은 이미 이번 프레임 이동이 반영된 값이다. Positions[index]
            // (이웃 조회용으로 메인 스레드에서 채운 이동 "이전" 스냅샷)를 자기 위치로 잘못
            // 쓰면 이번 프레임 이동 결과를 덮어써버려, 겹침이 있는 몬스터(주변에 push를 받는
            // 몬스터)는 매 프레임 이동이 무효화되어 제자리에 멈춘 것처럼 보이는 버그가 있었다
            // (Positions는 이웃(j != index) 조회에만 써야 한다).
            var current = transform.position;
            var posA = new float2(current.x, current.z);
            var cellSizeInv = 1f / CellSize;
            var minDistanceSq = CellSize * CellSize;
            var cellX = (int)math.floor(posA.x * cellSizeInv);
            var cellZ = (int)math.floor(posA.y * cellSizeInv);

            var totalPush = float2.zero;

            for (var dx = -1; dx <= 1; dx++)
            {
                for (var dz = -1; dz <= 1; dz++)
                {
                    var bucket = HashCell(cellX + dx, cellZ + dz);
                    var j = BucketHeads[bucket];
                    while (j != -1)
                    {
                        if (j != index)
                        {
                            var posB = Positions[j];
                            var delta = posA - posB;
                            var distanceSq = math.lengthsq(delta);
                            if (distanceSq < minDistanceSq && distanceSq >= 0.0001f)
                            {
                                var distance = math.sqrt(distanceSq);
                                var overlap = CellSize - distance;
                                totalPush += delta / distance * overlap * 0.5f;
                            }
                        }

                        j = Next[j];
                    }
                }
            }

            if (math.all(totalPush == float2.zero))
            {
                return;
            }

            var candidate = posA + totalPush;
            if (!IsWalkable(candidate))
            {
                return;
            }

            transform.position = new Vector3(candidate.x, current.y, candidate.y);
        }

        // FlowFieldSeekJob.SampleWalkableDirection과 동일한 인라인 좌표 변환이다. Burst Job
        // 안에서는 WalkableGrid의 메서드를 호출할 수 없어 로직을 그대로 복제해야 한다.
        private bool IsWalkable(float2 worldXZ)
        {
            var local = (worldXZ - WalkableGridOrigin) / WalkableCellSize;
            var cellX = (int)math.floor(local.x);
            var cellZ = (int)math.floor(local.y);

            if (cellX < 0 || cellZ < 0 || cellX >= WalkableGridWidth || cellZ >= WalkableGridHeight)
            {
                return true;
            }

            return Walkable[cellX + cellZ * WalkableGridWidth];
        }

        // SpatialHashMonsterSeparationSystem.HashCell과 동일한 해시(부호 보정 포함)다.
        private int HashCell(int cellX, int cellZ)
        {
            unchecked
            {
                var hash = cellX * 92837111 ^ cellZ * 689287499;
                if (hash < 0)
                {
                    hash = -hash;
                }

                return hash % BucketCount;
            }
        }
    }
}
