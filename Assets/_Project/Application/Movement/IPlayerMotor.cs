using System.Numerics;

namespace LegionBreak.Application.Movement
{
    /// <summary>
    /// 실제 이동을 실행하는 포트(port). Infrastructure에서 구현한다.
    /// (예: Transform 이동, 추후 CharacterController나 Rigidbody로 교체 가능)
    /// </summary>
    public interface IPlayerMotor
    {
        void Move(Vector2 displacement);
    }
}
