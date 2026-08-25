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

        // 현재 바라보는 방향에 델타를 합성하는 원시 연산(transform.rotation *= delta).
        // 목표 방향 계산은 전부 호출부 책임 — 이 메서드는 판단 없이 그대로 적용만 한다.
        // 호출부는 둘: PlayerRootMotionRelay(Attack 중 Animator.deltaRotation을 그대로
        // 전달)와 PlayerMoveUseCase(이동 방향을 바라보도록 계산한 델타를 전달).
        void Rotate(Quaternion delta);

        Vector2 Position { get; }

        // PlayerMoveUseCase가 이동 방향으로 회전할 델타를 계산하려면 현재 방향을 알아야
        // 한다(Position과 같은 이유로 포트에 노출).
        Quaternion Rotation { get; }
    }
}
