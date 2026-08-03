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
        private Camera _camera;

        [Inject]
        public void Construct(IPlayerSkillCastUseCase castUseCase)
        {
            _castUseCase = castUseCase;
        }

        private void Awake()
        {
            _camera = Camera.main;
            _castAction = new InputAction("CastSkill", binding: "<Mouse>/leftButton");
            _castAction.performed += OnCastPerformed;
            _castAction.Enable();
        }

        private void OnCastPerformed(InputAction.CallbackContext context)
        {
            if (_castUseCase == null || _camera == null)
            {
                return;
            }

            // 클릭한 화면 좌표를 Y=0 지면 평면과 수학적으로 교차시켜 월드 좌표를 구한다
            // (Physics.Raycast가 아니라 Plane.Raycast라 몬스터 Collider가 필요 없다 —
            // 4주차에 제거한 미사용 Collider 최적화를 유지하기 위한 선택).
            var ray = _camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            var groundPlane = new Plane(Vector3.up, Vector3.zero);
            if (!groundPlane.Raycast(ray, out var enter))
            {
                return;
            }

            var worldPoint = ray.GetPoint(enter);
            // PooledMonsterSpawner.Spawn과 동일한 XZ 평면 컨벤션(Y=0 고정)을 그대로 따른다.
            var targetPoint = new Vector2(worldPoint.x, worldPoint.z);

            var result = _castUseCase.Execute(targetPoint);

            var criticalSuffix = result.IsCritical ? " (CRIT)" : "";
            Debug.Log(result.Success ? $"Skill cast: {result.Damage} dmg x {result.HitCount} hit(s){criticalSuffix}" : "Skill on cooldown");
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
