using System;
using LegionBreak.Application.Events;
using LegionBreak.Application.Movement;
using LegionBreak.Application.Player;
using LegionBreak.Application.Skills;
using R3;
using UnityEngine;
using VContainer;

namespace LegionBreak.Presentation.Player
{
    /// <summary>
    /// IPlayerMotor/IPlayerHealth/IEventBus가 들고 있는 상태를 PlayerAnimatorController의
    /// 파라미터로만 옮겨 반영한다. GameOverView와 같은 이유로 Presenter를 두지 않았다 —
    /// 상태→파라미터 매핑에 스로틀링이나 변경 판단처럼 별도로 테스트할 분기가 없다.
    ///
    /// 이동 여부(IsMoving)는 입력을 다시 읽지 않고 IPlayerMotor.Position의 프레임 간
    /// 변화량으로 판단한다 — PlayerInputController의 입력 읽기 로직을 중복시키지 않기 위함.
    /// 공격(Attack)은 IEventBus로 SkillCastResult를 구독해 트리거한다: 실제 소비처가 생기기
    /// 전까진 이벤트를 만들지 않는다는 원칙(이벤트 버스/스킬 조합 결정 참고)에 따라, 이번에
    /// 처음 생긴 소비처(애니메이션)에 맞춰 PlayerSkillCastUseCase가 시전 성공 시에만
    /// SkillCastResult를 발행하도록 했다(쿨다운 실패는 발행되지 않으므로 여기서 다시
    /// Success를 확인할 필요가 없다).
    /// </summary>
    public class PlayerAnimationView : MonoBehaviour
    {
        private const float MovementEpsilonSqr = 0.0001f;
        private const string IsMovingParam = "IsMoving";
        private const string AttackParam = "Attack";
        private const string IsDeadParam = "IsDead";

        [SerializeField] private Animator _animator;

        private IPlayerMotor _motor;
        private IPlayerHealth _playerHealth;
        private IDisposable _subscription;
        private Vector2 _lastPosition;

        [Inject]
        public void Construct(IPlayerMotor motor, IPlayerHealth playerHealth, IEventBus eventBus)
        {
            _motor = motor;
            _playerHealth = playerHealth;
            _lastPosition = motor.Position;
            _subscription = eventBus.Receive<SkillCastResult>()
                .Subscribe(_ => _animator.SetTrigger(AttackParam));
        }

        private void Update()
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            var currentPosition = _motor.Position;
            var isMoving = (currentPosition - _lastPosition).sqrMagnitude > MovementEpsilonSqr;
            _animator.SetBool(IsMovingParam, isMoving);
            _lastPosition = currentPosition;

            if (_playerHealth.IsDead)
            {
                _animator.SetBool(IsDeadParam, true);
            }
        }

        private void OnDestroy()
        {
            _subscription?.Dispose();
        }
    }
}
