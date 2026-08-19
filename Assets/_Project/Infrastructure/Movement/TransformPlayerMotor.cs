using LegionBreak.Application.Movement;
using UnityEngine;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// IPlayerMotor의 실제 구현. Transform.position을 직접 조작한다.
    /// 씬의 Player GameObject에 부착한다.
    /// </summary>
    public class TransformPlayerMotor : MonoBehaviour, IPlayerMotor
    {
        public void Move(Vector2 displacement)
        {
            transform.position += new Vector3(displacement.x, 0f, displacement.y);
        }

        public void Rotate(Quaternion delta)
        {
            transform.rotation *= delta;
        }

        // 몬스터 Seek 타겟팅(JobMonsterMovementSystem/MonoMonsterMovementSystem)이
        // 매 프레임 참조하는 플레이어 위치. XZ 평면 관례는 Move()와 동일하다.
        public Vector2 Position => new Vector2(transform.position.x, transform.position.z);
    }
}
