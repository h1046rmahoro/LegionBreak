using System;
using LegionBreak.Application.Events;
using R3;

namespace LegionBreak.Application.UI
{
    /// <summary>
    /// MVP의 Presenter. "언제/무엇을 보여줄지"는 여기서 결정하고, View(MonoBehaviour)는
    /// 그리기만 한다. Unity 생명주기(Update)를 직접 가질 수 없으므로 View가 매 프레임
    /// Tick을 호출해 구동한다. UnityEngine.Object 의존이 없어 순수 C#으로 테스트 가능하다.
    ///
    /// IMonsterSpawner를 직접 폴링하던 이전 방식 대신 IEventBus를 구독한다. 스폰
    /// 스트레스 테스트(간격 0.02s)에서는 개체 수가 초당 수십 번 바뀌는데, 이벤트가 올
    /// 때마다 View를 바로 갱신하면 그 빈도 그대로 문자열 할당이 발생해 GC Alloc 0B/frame
    /// 측정을 오염시킨다(4주차에 실측 확인한 것과 같은 문제). 그래서 이벤트로 받은 최신
    /// 값은 필드에만 저장해두고, 실제 View 갱신은 기존과 동일하게 Tick의 시간 스로틀을
    /// 통과할 때만 수행한다.
    /// </summary>
    public sealed class MonsterCountPresenter : IDisposable
    {
        private const float RefreshIntervalSeconds = 0.25f;

        private readonly IMonsterCountView _view;
        private readonly IDisposable _subscription;

        private int _latestCount;
        private int _lastDisplayedCount = -1;
        private float _elapsed;

        public MonsterCountPresenter(IEventBus eventBus, IMonsterCountView view)
        {
            _view = view;
            _subscription = eventBus.Receive<MonsterCountChangedEvent>()
                .Subscribe(e => _latestCount = e.Count);
        }

        public void Tick(float deltaTime)
        {
            _elapsed += deltaTime;
            if (_elapsed < RefreshIntervalSeconds)
            {
                return;
            }

            _elapsed = 0f;

            if (_latestCount == _lastDisplayedCount)
            {
                return;
            }

            _lastDisplayedCount = _latestCount;
            _view.SetCount(_latestCount);
        }

        public void Dispose()
        {
            _subscription.Dispose();
        }
    }
}
