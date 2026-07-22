using LegionBreak.Application.Skills;
using UnityEngine;
using UnityEngine.InputSystem;
using VContainer;

namespace LegionBreak.Presentation.Skills
{
    /// <summary>
    /// 입력을 읽어 Application 유스케이스에 전달만 한다.
    /// Infrastructure는 이 클래스에서 전혀 참조하지 않는다 (asmdef로 차단됨).
    /// </summary>
    public class PlayerSkillInputController : MonoBehaviour
    {
        private IPlayerSkillCastUseCase _castUseCase;
        private InputAction _castAction;

        [Inject]
        public void Construct(IPlayerSkillCastUseCase castUseCase)
        {
            _castUseCase = castUseCase;
        }

        private void Awake()
        {
            _castAction = new InputAction("CastSkill", binding: "<Mouse>/leftButton");
            _castAction.performed += OnCastPerformed;
            _castAction.Enable();
        }

        private void OnCastPerformed(InputAction.CallbackContext context)
        {
            if (_castUseCase == null)
            {
                return;
            }

            var result = _castUseCase.Execute();

            // 몬스터 피격/HP 시스템 연결 전까지 결과 확인용 로그. 다음 단계에서 실제 적용으로 대체.
            Debug.Log(result.Success ? $"Skill cast: {result.Damage} dmg" : "Skill on cooldown");
        }

        private void Update()
        {
            _castUseCase?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _castAction.performed -= OnCastPerformed;
            _castAction?.Dispose();
        }
    }
}
