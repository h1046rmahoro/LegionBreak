using LegionBreak.Application.Movement;
using UnityEngine;
using VContainer;

namespace LegionBreak.Presentation.Cameras
{
    /// <summary>
    /// 카메라가 IPlayerMotor.Position(XZ 평면, Y는 항상 0인 기존 관례)을 매 프레임 추적한다.
    /// 오프셋을 하드코딩하지 않고 Start 시점에 "씬에 배치된 카메라 위치 - 플레이어 위치"를
    /// 계산해 캐시한다 — 에디터에서 잡아둔 탑다운 각도/거리를 그대로 유지한 채 위치만
    /// 따라가게 하기 위함이며, 카메라를 나중에 다시 배치해도 코드 수정이 필요 없다.
    /// LateUpdate에서 갱신한다 — 플레이어 이동(Update)이 끝난 뒤 카메라를 옮겨야 한 프레임
    /// 밀리는 지터를 피할 수 있다.
    ///
    /// 폴더/네임스페이스가 단수 "Camera"가 아니라 복수 "Cameras"인 이유: LegionBreak.Presentation
    /// 하위에 LegionBreak.Presentation.Camera 네임스페이스가 있으면, 같은 Presentation 하위의
    /// 다른 네임스페이스(예: PlayerSkillInputController가 속한 Presentation.Skills)에서 쓰는
    /// UnityEngine.Camera가 컴파일러에 의해 그 네임스페이스로 잘못 해석되어 CS0118 컴파일
    /// 에러가 났다(실제로 발생 확인). 형제 네임스페이스와 이름이 겹치지 않게 복수형으로 둔다.
    /// </summary>
    public class CameraFollowController : MonoBehaviour
    {
        private IPlayerMotor _motor;
        private Vector3 _offset;

        [Inject]
        public void Construct(IPlayerMotor motor)
        {
            _motor = motor;
        }

        private void Start()
        {
            _offset = transform.position - ToWorldPosition(_motor.Position);
        }

        private void LateUpdate()
        {
            if (_motor == null)
            {
                return;
            }

            transform.position = ToWorldPosition(_motor.Position) + _offset;
        }

        private static Vector3 ToWorldPosition(Vector2 xzPosition)
        {
            return new Vector3(xzPosition.x, 0f, xzPosition.y);
        }
    }
}
