using System.Collections.Generic;
using LegionBreak.Application.Movement;
using LegionBreak.Infrastructure.Spawning;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;
using VContainer;

namespace LegionBreak.Infrastructure.Movement
{
    /// <summary>
    /// After: 몬스터 이동/타겟팅을 IJobParallelForTransform + Burst(MonsterSeekJob)로
    /// 병렬화한다. TransformAccessArray는 Register/Unregister로 증분 갱신하며(매 프레임
    /// 재구축하지 않음), 인덱스는 RemoveAtSwapBack에 맞춰 Dictionary로 추적한다.
    ///
    /// Register/Unregister가 Job 스케줄 도중(Update~LateUpdate 사이) 호출되면
    /// Safety System이 NativeContainer 변경을 막고 예외를 던질 수 있다. MonoBehaviour
    /// 간 Update() 실행 순서는 보장되지 않으므로(PooledMonsterSpawner가 이 컴포넌트보다
    /// 먼저/나중에 실행될 수 있음), 두 메서드 모두 방어적으로 Complete()를 먼저 호출한다.
    /// </summary>
    public class JobMonsterMovementSystem : MonoBehaviour, IMonsterMovementSystem
    {
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private int _initialCapacity = 500;

        private IPlayerMotor _playerMotor;
        private TransformAccessArray _transformAccessArray;
        private readonly List<MonsterView> _viewsByIndex = new List<MonsterView>();
        private readonly Dictionary<MonsterView, int> _indexByView = new Dictionary<MonsterView, int>();
        private JobHandle _jobHandle;

        [Inject]
        public void Construct(IPlayerMotor playerMotor)
        {
            _playerMotor = playerMotor;
        }

        private void Awake()
        {
            _transformAccessArray = new TransformAccessArray(_initialCapacity);
        }

        public void Register(MonsterView view)
        {
            _jobHandle.Complete();

            _indexByView[view] = _transformAccessArray.length;
            _viewsByIndex.Add(view);
            _transformAccessArray.Add(view.transform);
        }

        public void Unregister(MonsterView view)
        {
            _jobHandle.Complete();

            if (!_indexByView.TryGetValue(view, out var index))
            {
                return;
            }

            var lastIndex = _transformAccessArray.length - 1;
            _transformAccessArray.RemoveAtSwapBack(index);

            var movedView = _viewsByIndex[lastIndex];
            _viewsByIndex[index] = movedView;
            _viewsByIndex.RemoveAt(lastIndex);
            _indexByView.Remove(view);

            if (movedView != view)
            {
                _indexByView[movedView] = index;
            }
        }

        private void Update()
        {
            if (_transformAccessArray.length == 0 || _playerMotor == null)
            {
                return;
            }

            var target = _playerMotor.Position;
            var job = new MonsterSeekJob
            {
                TargetPosition = new float2(target.x, target.y),
                MoveSpeed = _moveSpeed,
                DeltaTime = Time.deltaTime
            };
            _jobHandle = job.Schedule(_transformAccessArray);
        }

        private void LateUpdate()
        {
            _jobHandle.Complete();
        }

        private void OnDestroy()
        {
            _jobHandle.Complete();
            if (_transformAccessArray.isCreated)
            {
                _transformAccessArray.Dispose();
            }
        }
    }
}
