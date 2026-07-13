using System.Collections.Generic;
using LegionBreak.Infrastructure.Spawning;
using UnityEngine;

namespace LegionBreak.Infrastructure.Separation
{
    /// <summary>
    /// Before 베이스라인: 등록된 몬스터 전체를 (i, j) 쌍으로 전수 비교해 겹침을 밀어낸다.
    /// n마리면 매 프레임 n(n-1)/2회 거리 계산이 발생하는 O(n²) 구조이며,
    /// SpatialHashMonsterSeparationSystem(After) 도입 효과를 비교하기 위한 대조군이다.
    /// 겹침 판정 반경과 밀어내는 공식은 After와 동일하게 맞춘다.
    /// </summary>
    public class BruteForceMonsterSeparationSystem : MonoBehaviour, IMonsterSeparationSystem
    {
        [SerializeField] private float _separationRadius = 0.5f;

        private readonly List<DummyMonsterView> _activeViews = new List<DummyMonsterView>();

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
            var minDistance = _separationRadius * 2f;
            var minDistanceSq = minDistance * minDistance;
            var count = _activeViews.Count;

            for (var i = 0; i < count; i++)
            {
                var transformA = _activeViews[i].transform;
                var posA = transformA.position;

                for (var j = i + 1; j < count; j++)
                {
                    var transformB = _activeViews[j].transform;
                    var posB = transformB.position;

                    var deltaX = posA.x - posB.x;
                    var deltaZ = posA.z - posB.z;
                    var distanceSq = deltaX * deltaX + deltaZ * deltaZ;
                    if (distanceSq >= minDistanceSq || distanceSq < 0.0001f)
                    {
                        continue;
                    }

                    var distance = Mathf.Sqrt(distanceSq);
                    var overlap = minDistance - distance;
                    var pushX = deltaX / distance * overlap * 0.5f;
                    var pushZ = deltaZ / distance * overlap * 0.5f;

                    posA.x += pushX;
                    posA.z += pushZ;
                    posB.x -= pushX;
                    posB.z -= pushZ;

                    transformA.position = posA;
                    transformB.position = posB;
                }
            }
        }
    }
}
