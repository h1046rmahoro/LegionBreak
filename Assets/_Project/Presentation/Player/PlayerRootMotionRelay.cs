using LegionBreak.Application.Movement;
using UnityEngine;
using VContainer;

namespace LegionBreak.Presentation.Player
{
    /// <summary>
    /// Animator의 루트 모션을 상태에 따라 선택적으로 IPlayerMotor에 중계한다.
    /// Animator 컴포넌트가 있는 모델(GameObject)에 직접 부착해야 한다 — OnAnimatorMove는
    /// Animator와 같은 GameObject의 컴포넌트에서만 호출된다.
    ///
    /// 배경: Apply Root Motion을 끄면 이동(코드 구동)은 정상이지만 Attack 애니메이션에
    /// 내장된 전진 스텝이 통째로 사라져 발이 미끄러지듯 보였고, 반대로 켜두면 이동 중에
    /// 코드(TransformPlayerMotor)와 애니메이션 루트 모션이 Player Root/모델을 동시에
    /// 옮겨서 거리가 벌어졌다(둘 다 "전부 켜거나 끄거나"라 한쪽이 항상 깨짐).
    ///
    /// 해결: Apply Root Motion은 켠 채로 이 컴포넌트가 OnAnimatorMove를 구현하면 Unity가
    /// 루트 모션을 Transform에 자동 적용하지 않고 전적으로 이 메서드에 위임한다. Attack
    /// 상태일 때만 위치/회전 델타를 IPlayerMotor로 넘겨 "부모(Player Root)"를 직접
    /// 움직이고, 모델 자신의 로컬 Transform은 절대 건드리지 않는다 — 그래서 모델은 항상
    /// 루트에 고정된 채 같이 움직인다. Move/Idle/Death 상태에서는 아무 것도 하지 않아
    /// (델타를 버림) 기존 코드 이동과 충돌하지 않는다 — Apply Root Motion을 끈 것과 같은 효과.
    ///
    /// (수정 이력) 회전(Animator.deltaRotation)을 처음엔 빠뜨렸다 — 위치만 중계했더니
    /// 공격 시 캐릭터가 대상을 보고 도는 동작이 사라졌다. 또한 Any State→Attack 전이에
    /// 걸려있던 0.1초 크로스페이드(PlayerAnimatorController) 때문에 전이가 끝나기 전까지는
    /// GetCurrentAnimatorStateInfo가 여전히 이전 상태를 가리켜, 공격 애니메이션 초반(전진
    /// 스텝이 몰려있는 구간)의 루트 모션이 통째로 씹혀 "이동도 회전도 안 하는" 것처럼
    /// 보였다 — 그 전이의 Transition Duration을 0으로 줄여 즉시 전환되게 해서 해결했다.
    /// </summary>
    public class PlayerRootMotionRelay : MonoBehaviour
    {
        private const string AttackStateName = "Attack";

        private Animator _animator;
        private IPlayerMotor _motor;

        [Inject]
        public void Construct(IPlayerMotor motor)
        {
            _motor = motor;
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        private void OnAnimatorMove()
        {
            if (_animator == null || _motor == null)
            {
                return;
            }

            if (!_animator.GetCurrentAnimatorStateInfo(0).IsName(AttackStateName))
            {
                return;
            }

            var delta = _animator.deltaPosition;
            _motor.Move(new Vector2(delta.x, delta.z));
            _motor.Rotate(_animator.deltaRotation);
        }
    }
}
