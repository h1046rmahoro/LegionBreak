using LegionBreak.Application.Movement;
using LegionBreak.Application.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace LegionBreak.Presentation.Movement
{
    /// <summary>
    /// 입력을 읽어 Application 유스케이스에 전달만 한다.
    /// Infrastructure는 이 클래스에서 전혀 참조하지 않는다 (asmdef로 차단됨).
    /// </summary>
    public class PlayerInputController : MonoBehaviour
    {
        private IPlayerMoveUseCase _moveUseCase;
        private IPlayerHealth _playerHealth;
        private InputAction _moveAction;

        [Inject]
        public void Construct(IPlayerMoveUseCase moveUseCase, IPlayerHealth playerHealth)
        {
            _moveUseCase = moveUseCase;
            _playerHealth = playerHealth;
        }

        private void Awake()
        {
            _moveAction = new InputAction("Move");
            _moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _moveAction.Enable();
        }

        private void Update()
        {
            if (_moveUseCase == null || _playerHealth.IsDead)
            {
                return;
            }

            var input = _moveAction.ReadValue<Vector2>();
            _moveUseCase.Execute(input, Time.deltaTime);
        }

        private void OnDestroy()
        {
            _moveAction?.Dispose();
        }
    }
}
