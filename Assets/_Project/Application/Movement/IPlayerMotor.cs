using UnityEngine;

namespace LegionBreak.Application.Movement
{
    /// <summary>
    /// 실제 이동을 실행하는 포트(port). Infrastructure에서 구현한다.
    /// (예: Transform 이동, 추후 CharacterController나 Rigidbody로 교체 가능)
    /// </summary>
    public interface IPlayerMotor
    {
        void Move(Vector2 displacement);

        // 루트 모션 회전 중계(PlayerRootMotionRelay) 전용. 이 게임은 이동 방향에 따라
        // 캐릭터를 자동으로 회전시키지 않아(PlayerMoveUseCase가 회전을 전혀 다루지 않음)
        // 별도 회전 판단 로직이 없고, Animator.deltaRotation을 그대로 누적 적용하는
        // 단순 위임만 필요하다.
        void Rotate(Quaternion delta);

        Vector2 Position { get; }
    }
}
