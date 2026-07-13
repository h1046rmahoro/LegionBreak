using System.Collections.Generic;
using LegionBreak.Infrastructure.Spawning;
using UnityEngine;

namespace LegionBreak.Infrastructure.Separation
{
    /// <summary>
    /// After: 몬스터를 셀 크기 = 겹침 판정 반경*2인 균일 그리드에 매 프레임 재삽입하고,
    /// 자기 셀 + 주변 8칸(총 3x3)만 검사해 겹침을 밀어낸다. 셀 크기를 판정 거리와
    /// 맞췄기 때문에 겹칠 수 있는 쌍은 반드시 3x3 범위 안에만 존재한다.
    /// 겹침 판정 반경과 밀어내는 공식은 Before(BruteForceMonsterSeparationSystem)와 동일하게
    /// 맞춰 알고리즘(O(n²) vs 공간 분할) 차이만 비교되도록 한다.
    ///
    /// Dictionary&lt;cell, List&lt;index&gt;&gt; 방식은 몬스터가 매 프레임 셀을 옮겨 다니므로
    /// List 재구성 과정에서 GC Alloc이 반복 발생할 위험이 있다(1주차에서 확인한 Collider
    /// 재삽입 비용과 유사한 함정). 대신 버킷 헤드(int[])+ 인덱스 기반 연결 리스트(int[])로
    /// 그리드를 구성해, 초기 용량 확보 이후에는 매 프레임 할당이 없다.
    /// </summary>
    public class SpatialHashMonsterSeparationSystem : MonoBehaviour, IMonsterSeparationSystem
    {
        [SerializeField] private float _separationRadius = 0.5f;
        [SerializeField] private int _initialCapacity = 512;
        [SerializeField] private int _bucketCount = 1024;

        private readonly List<DummyMonsterView> _viewsByIndex = new List<DummyMonsterView>();
        private readonly Dictionary<DummyMonsterView, int> _indexByView = new Dictionary<DummyMonsterView, int>();

        private int[] _bucketHeads;
        private int[] _next;

        private void Awake()
        {
            _bucketHeads = new int[_bucketCount];
            _next = new int[_initialCapacity];
        }

        public void Register(DummyMonsterView view)
        {
            var index = _viewsByIndex.Count;
            _indexByView[view] = index;
            _viewsByIndex.Add(view);
            EnsureCapacity(_viewsByIndex.Count);
        }

        public void Unregister(DummyMonsterView view)
        {
            if (!_indexByView.TryGetValue(view, out var index))
            {
                return;
            }

            var lastIndex = _viewsByIndex.Count - 1;
            var movedView = _viewsByIndex[lastIndex];
            _viewsByIndex[index] = movedView;
            _viewsByIndex.RemoveAt(lastIndex);
            _indexByView.Remove(view);

            if (movedView != view)
            {
                _indexByView[movedView] = index;
            }
        }

        private void EnsureCapacity(int requiredCount)
        {
            if (_next.Length >= requiredCount)
            {
                return;
            }

            var newCapacity = Mathf.Max(_next.Length * 2, requiredCount);
            _next = new int[newCapacity];
        }

        private void Update()
        {
            var count = _viewsByIndex.Count;
            if (count == 0)
            {
                return;
            }

            var cellSize = _separationRadius * 2f;
            var cellSizeInv = 1f / cellSize;
            var minDistanceSq = cellSize * cellSize;

            for (var b = 0; b < _bucketHeads.Length; b++)
            {
                _bucketHeads[b] = -1;
            }

            for (var i = 0; i < count; i++)
            {
                var pos = _viewsByIndex[i].transform.position;
                var cellX = Mathf.FloorToInt(pos.x * cellSizeInv);
                var cellZ = Mathf.FloorToInt(pos.z * cellSizeInv);
                var bucket = HashCell(cellX, cellZ);
                _next[i] = _bucketHeads[bucket];
                _bucketHeads[bucket] = i;
            }

            for (var i = 0; i < count; i++)
            {
                var transformA = _viewsByIndex[i].transform;
                var posA = transformA.position;
                var cellX = Mathf.FloorToInt(posA.x * cellSizeInv);
                var cellZ = Mathf.FloorToInt(posA.z * cellSizeInv);

                for (var dx = -1; dx <= 1; dx++)
                {
                    for (var dz = -1; dz <= 1; dz++)
                    {
                        var bucket = HashCell(cellX + dx, cellZ + dz);
                        var j = _bucketHeads[bucket];
                        while (j != -1)
                        {
                            if (j > i)
                            {
                                var transformB = _viewsByIndex[j].transform;
                                var posB = transformB.position;

                                var deltaX = posA.x - posB.x;
                                var deltaZ = posA.z - posB.z;
                                var distanceSq = deltaX * deltaX + deltaZ * deltaZ;

                                if (distanceSq < minDistanceSq && distanceSq >= 0.0001f)
                                {
                                    var distance = Mathf.Sqrt(distanceSq);
                                    var overlap = cellSize - distance;
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

                            j = _next[j];
                        }
                    }
                }
            }
        }

        private int HashCell(int cellX, int cellZ)
        {
            unchecked
            {
                var hash = cellX * 92837111 ^ cellZ * 689287499;
                if (hash < 0)
                {
                    hash = -hash;
                }

                return hash % _bucketHeads.Length;
            }
        }
    }
}
