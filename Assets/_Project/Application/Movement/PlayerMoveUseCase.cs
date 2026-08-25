using UnityEngine;

namespace LegionBreak.Application.Movement
{
    public class PlayerMoveUseCase : IPlayerMoveUseCase
    {
        // TODO: 다음 단계에서 Data 계층의 캐릭터 스탯 설정으로 교체
        private const float MoveSpeed = 5f;
        private const float TurnSpeedDegreesPerSecond = 720f;

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

            var direction = inputDirection.normalized;
            var displacement = direction * MoveSpeed * deltaTime;
            _motor.Move(displacement);

            // 입력 방향으로 TurnSpeedDegreesPerSecond 한도 내에서 이번 프레임만큼만 회전한다
            // (즉시 스냅이 아니라 RotateTowards로 점진적 turn). 목표 방향 자체는 매 프레임
            // 새로 계산하므로 정지 중에는 Execute가 호출되지 않아(위 early return) 마지막
            // 으로 바라보던 방향이 그대로 유지된다.
            var targetRotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y), Vector3.up);
            var maxDegreesThisFrame = TurnSpeedDegreesPerSecond * deltaTime;
            var steppedRotation = Quaternion.RotateTowards(_motor.Rotation, targetRotation, maxDegreesThisFrame);
            var rotationDelta = Quaternion.Inverse(_motor.Rotation) * steppedRotation;
            _motor.Rotate(rotationDelta);
        }
    }
}
