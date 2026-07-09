using System.Collections.Generic;
using LegionBreak.Application.Movement;
using LegionBreak.Infrastructure.Spawning;
using UnityEngine;
using VContainer;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// Before 베이스라인: 몬스터 이동/타겟팅을 메인 스레드 Update() 단일 루프에서
    /// 순차 계산한다. Job+Burst(JobMonsterMovementSystem) 도입 효과를 비교하기 위한
    /// 대조군이며, 등록/해제 대상과 이동 공식은 After와 동일하게 맞춘다.
    /// </summary>
    public class MonoMonsterMovementSystem : MonoBehaviour, IMonsterMovementSystem
    {
        [SerializeField] private float _moveSpeed = 3f;

        private IPlayerMotor _playerMotor;
        private readonly List<DummyMonsterView> _activeViews = new List<DummyMonsterView>();

        [Inject]
        public void Construct(IPlayerMotor playerMotor)
        {
            _playerMotor = playerMotor;
        }

        public void Register(DummyMonsterView view)
        {
            _activeViews.Add(view);
        }

        public void Unregister(DummyMonsterView view)
        {
            _activeViews.Remove(view);
        }

        private void Update()
        {
            if (_playerMotor == null)
            {
                return;
            }

            var target = _playerMotor.Position;
            var deltaTime = Time.deltaTime;

            for (var i = 0; i < _activeViews.Count; i++)
            {
                var monsterTransform = _activeViews[i].transform;
                var current = monsterTransform.position;
                var toTargetX = target.x - current.x;
                var toTargetZ = target.y - current.z;
                var distanceSq = toTargetX * toTargetX + toTargetZ * toTargetZ;
                if (distanceSq < 0.0001f)
                {
                    continue;
                }

                var invDistance = 1f / Mathf.Sqrt(distanceSq);
                var deltaX = toTargetX * invDistance * _moveSpeed * deltaTime;
                var deltaZ = toTargetZ * invDistance * _moveSpeed * deltaTime;
                monsterTransform.position = new Vector3(current.x + deltaX, current.y, current.z + deltaZ);
            }
        }
    }
}
