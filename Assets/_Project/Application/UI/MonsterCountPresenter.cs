using LegionBreak.Application.Spawning;

namespace LegionBreak.Application.UI
{
    /// <summary>
    /// MVP의 Presenter. "언제/무엇을 보여줄지"는 여기서 결정하고, View(MonoBehaviour)는
    /// 그리기만 한다. Unity 생명주기(Update)를 직접 가질 수 없으므로 View가 매 프레임
    /// Tick을 호출해 구동한다. UnityEngine.Object 의존이 없어 순수 C#으로 테스트 가능하다.
    /// </summary>
    public sealed class MonsterCountPresenter
    {
        private const float RefreshIntervalSeconds = 0.25f;

        private readonly IMonsterSpawner _spawner;
        private readonly IMonsterCountView _view;

        private int _lastDisplayedCount = -1;
        private float _elapsed;

        public MonsterCountPresenter(IMonsterSpawner spawner, IMonsterCountView view)
        {
            _spawner = spawner;
            _view = view;
        }

        public void Tick(float deltaTime)
        {
            // 스폰 스트레스 테스트(간격 0.02s)에서는 개체 수가 초당 수십 번 바뀌어 매 프레임
            // 갱신하면 이 표시 자체가 GC Alloc 0B/frame 측정을 오염시킨다(4주차에 실측 확인).
            // 갱신 주기를 던져 할당 빈도를 낮춘다.
            _elapsed += deltaTime;
            if (_elapsed < RefreshIntervalSeconds)
            {
                return;
            }

            _elapsed = 0f;

            var count = _spawner.ActiveCount;
            if (count == _lastDisplayedCount)
            {
                return;
            }

            _lastDisplayedCount = count;
            _view.SetCount(count);
        }
    }
}
