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
        // MonsterMovementResolver가 겹침 회피(MonsterSeparationJob)와 TransformAccessArray를
        // 공유하면서 도입한 필드다. 겹침 회피는 스폰(Idle) 시점부터 등록되지만 이동은 AI가
        // Chase로 전이할 때만 켜져야 하므로(Idle/Attack 상태 몬스터는 이동 정지), 배열 자체를
        // 분리하는 대신 인덱스별로 이동 여부만 플래그로 걸러낸다.
        [ReadOnly] public NativeArray<bool> MovementActive;
        public float2 GridOrigin;
        public float CellSize;
        public int GridWidth;
        public int GridHeight;
        public float2 FallbackTarget;
        public float MoveSpeed;
        public float DeltaTime;
        public float TurnSpeedDegreesPerSecond;

        public void Execute(int index, TransformAccess transform)
        {
            if (!MovementActive[index])
            {
                return;
            }

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

            RotateTowardsDirection(ref transform, direction);
        }

        // PlayerMoveUseCase(Quaternion.LookRotation + RotateTowards로 점진적 turn)와 개념은
        // 같지만, 이 메서드는 [BurstCompile] Job 안에서 실행되므로 UnityEngine.Quaternion의
        // LookRotation/RotateTowards(내부적으로 예외 처리 분기가 있어 Burst 호환이 불확실함)
        // 대신 이 파일 전반에서 이미 쓰고 있는 Unity.Mathematics(quaternion/math.*)만으로
        // 직접 구현했다 — 이 패키지는 애초에 Burst Job 안에서 안전하게 쓰도록 설계된 것이라
        // 신뢰할 수 있다. 이동이 UseCase 없이 이 Job 안에서 전부 계산되므로, 회전도 별도
        // 계층을 만들지 않고 같은 direction을 그대로 재사용한다. MovementActive가 false인
        // 프레임(Idle/Attack)은 메서드 진입 전에 이미 return하므로, 이동을 멈추면 회전도
        // 그 순간의 방향을 유지한 채 함께 멈춘다.
        private void RotateTowardsDirection(ref TransformAccess transform, float2 direction)
        {
            var currentRotation = transform.rotation;
            var from = new quaternion(currentRotation.x, currentRotation.y, currentRotation.z, currentRotation.w);
            var to = quaternion.LookRotationSafe(new float3(direction.x, 0f, direction.y), math.up());

            // 쿼터니언은 q와 -q가 같은 회전을 나타낸다(이중 피복) — dot이 음수면 최단 경로가
            // 아니라 먼 길로 도는 것이므로, to의 부호를 뒤집어 항상 최단 경로로 보간한다.
            var dot = math.clamp(math.dot(from.value, to.value), -1f, 1f);
            var angleDegrees = math.degrees(math.acos(math.abs(dot))) * 2f;
            if (angleDegrees < 0.0001f)
            {
                return;
            }

            if (dot < 0f)
            {
                to = new quaternion(-to.value);
            }

            var maxDegreesThisFrame = TurnSpeedDegreesPerSecond * DeltaTime;
            var t = math.min(1f, maxDegreesThisFrame / angleDegrees);
            var result = math.slerp(from, to, t);
            transform.rotation = new Quaternion(result.value.x, result.value.y, result.value.z, result.value.w);
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
