using LegionBreak.Application.Movement;
using NUnit.Framework;
using UnityEngine;

namespace LegionBreak.Application.Tests
{
    public class PlayerMoveUseCaseTests
    {
        // TransformPlayerMotor의 Rotate(transform.rotation *= delta) 합성 방식을 그대로
        // 흉내내야 회전 관련 단언이 의미가 있어, 호출 여부만 기록하는 대신 실제로 합성한다.
        private sealed class FakePlayerMotor : IPlayerMotor
        {
            public Vector2? LastDisplacement;
            public int MoveCallCount;

            public void Move(Vector2 displacement)
            {
                LastDisplacement = displacement;
                MoveCallCount++;
            }

            public void Rotate(Quaternion delta)
            {
                Rotation *= delta;
            }

            public Vector2 Position => Vector2.zero;
            public Quaternion Rotation { get; private set; } = Quaternion.identity;
        }

        [Test]
        public void Execute_WithZeroInput_DoesNotMoveOrRotate()
        {
            var motor = new FakePlayerMotor();
            var useCase = new PlayerMoveUseCase(motor);

            useCase.Execute(Vector2.zero, 1f);

            Assert.AreEqual(0, motor.MoveCallCount);
            Assert.AreEqual(Quaternion.identity, motor.Rotation);
        }

        [Test]
        public void Execute_WithInput_MovesInNormalizedScaledDirection()
        {
            var motor = new FakePlayerMotor();
            var useCase = new PlayerMoveUseCase(motor);

            useCase.Execute(new Vector2(3f, 4f), 0.5f);

            var expectedDirection = new Vector2(3f, 4f).normalized;
            Assert.AreEqual(1, motor.MoveCallCount);
            Assert.AreEqual(expectedDirection.x * 5f * 0.5f, motor.LastDisplacement.Value.x, 0.0001f);
            Assert.AreEqual(expectedDirection.y * 5f * 0.5f, motor.LastDisplacement.Value.y, 0.0001f);
        }

        [Test]
        public void Execute_WithInput_RotatesToFaceInputDirection()
        {
            var motor = new FakePlayerMotor();
            var useCase = new PlayerMoveUseCase(motor);

            useCase.Execute(new Vector2(1f, 0f), 1f);

            var expected = Quaternion.LookRotation(Vector3.right, Vector3.up);
            Assert.Less(Quaternion.Angle(expected, motor.Rotation), 0.01f);
        }

        [Test]
        public void Execute_WithSmallDeltaTime_LimitsRotationByTurnSpeed()
        {
            var motor = new FakePlayerMotor();
            var useCase = new PlayerMoveUseCase(motor);

            // 목표 방향(+X, identity 대비 90도)까지 한 번에 도달하지 못하도록 아주 작은
            // deltaTime을 준다. 초당 720도 회전 한도라 0.01초면 최대 7.2도만 회전해야 한다.
            useCase.Execute(new Vector2(1f, 0f), 0.01f);

            var turnedDegrees = Quaternion.Angle(Quaternion.identity, motor.Rotation);
            Assert.Greater(turnedDegrees, 0f);
            Assert.LessOrEqual(turnedDegrees, 7.2f + 0.01f);
        }

        [Test]
        public void Execute_CalledAgainWithZeroInput_KeepsLastFacing()
        {
            var motor = new FakePlayerMotor();
            var useCase = new PlayerMoveUseCase(motor);

            useCase.Execute(new Vector2(0f, 1f), 1f);
            var rotationAfterMove = motor.Rotation;

            useCase.Execute(Vector2.zero, 1f);

            Assert.AreEqual(rotationAfterMove, motor.Rotation);
        }
    }
}
