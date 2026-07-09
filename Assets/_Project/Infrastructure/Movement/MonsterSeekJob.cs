using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// 몬스터가 타겟(플레이어) 위치를 향해 XZ 평면에서 이동하는 연산을 병렬화한다.
    /// TransformAccessArray를 대상으로 스케줄하면 Unity가 내부적으로 워커 스레드에
    /// 배치를 나눠 처리한다.
    /// </summary>
    [BurstCompile]
    public struct MonsterSeekJob : IJobParallelForTransform
    {
        public float2 TargetPosition;
        public float MoveSpeed;
        public float DeltaTime;

        public void Execute(int index, TransformAccess transform)
        {
            var current = transform.position;
            var toTarget = TargetPosition - new float2(current.x, current.z);
            var distanceSq = math.lengthsq(toTarget);
            if (distanceSq < 0.0001f)
            {
                return;
            }

            var direction = toTarget * math.rsqrt(distanceSq);
            var delta = direction * MoveSpeed * DeltaTime;
            transform.position = new Vector3(current.x + delta.x, current.y, current.z + delta.y);
        }
    }
}
