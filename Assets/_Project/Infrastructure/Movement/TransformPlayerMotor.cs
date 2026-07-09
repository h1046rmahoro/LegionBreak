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
        public void Move(System.Numerics.Vector2 displacement)
        {
            transform.position += new Vector3(displacement.X, 0f, displacement.Y);
        }
    }
}
