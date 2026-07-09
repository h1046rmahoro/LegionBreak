using UnityEngine;

namespace LegionBreak.Application.Movement
{
    public class PlayerMoveUseCase : IPlayerMoveUseCase
    {
        // TODO: 다음 단계에서 Data 계층의 캐릭터 스탯 설정으로 교체
        private const float MoveSpeed = 5f;

        private readonly IPlayerMotor _motor;

        public PlayerMoveUseCase(IPlayerMotor motor)
        {
            _motor = motor;
        }

        // 이동량 계산(정규화 * 속도 * deltaTime)은 분기·밸런스 없는 범용 벡터 연산이라
        // Domain 계층으로 분리하지 않고 여기에 인라인한다.
        // Domain은 데미지 공식 등 실제 도메인 규칙이 있는 로직에만 사용한다.
        public void Execute(Vector2 inputDirection, float deltaTime)
        {
            if (inputDirection == Vector2.zero)
            {
                return;
            }

            var displacement = inputDirection.normalized * MoveSpeed * deltaTime;
            _motor.Move(displacement);
        }
    }
}
