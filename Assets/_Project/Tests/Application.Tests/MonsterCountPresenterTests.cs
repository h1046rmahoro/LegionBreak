using LegionBreak.Application.Events;
using LegionBreak.Application.UI;
using NUnit.Framework;

namespace LegionBreak.Application.Tests
{
    public class MonsterCountPresenterTests
    {
        private sealed class FakeMonsterCountView : IMonsterCountView
        {
            public int? LastCount { get; private set; }
            public int SetCountCallCount { get; private set; }

            public void SetCount(int count)
            {
                LastCount = count;
                SetCountCallCount++;
            }
        }

        [Test]
        public void Tick_BeforeRefreshInterval_DoesNotUpdateView()
        {
            var eventBus = new EventBus();
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(eventBus, view);

            eventBus.Publish(new MonsterCountChangedEvent(5));
            presenter.Tick(0.1f);

            Assert.AreEqual(0, view.SetCountCallCount);
        }

        [Test]
        public void Tick_AfterRefreshInterval_UpdatesViewWithLatestPublishedCount()
        {
            var eventBus = new EventBus();
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(eventBus, view);

            eventBus.Publish(new MonsterCountChangedEvent(5));
            presenter.Tick(0.3f);

            Assert.AreEqual(1, view.SetCountCallCount);
            Assert.AreEqual(5, view.LastCount);
        }

        [Test]
        public void Tick_CountUnchangedAcrossIntervals_DoesNotUpdateViewAgain()
        {
            var eventBus = new EventBus();
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(eventBus, view);

            eventBus.Publish(new MonsterCountChangedEvent(5));
            presenter.Tick(0.3f);
            presenter.Tick(0.3f);

            Assert.AreEqual(1, view.SetCountCallCount);
        }

        [Test]
        public void Tick_CountChangesBetweenIntervals_UpdatesViewWithNewestValue()
        {
            var eventBus = new EventBus();
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(eventBus, view);

            eventBus.Publish(new MonsterCountChangedEvent(5));
            presenter.Tick(0.3f);
            eventBus.Publish(new MonsterCountChangedEvent(8));
            presenter.Tick(0.3f);

            Assert.AreEqual(2, view.SetCountCallCount);
            Assert.AreEqual(8, view.LastCount);
        }

        [Test]
        public void Dispose_UnsubscribesFromEventBus()
        {
            var eventBus = new EventBus();
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(eventBus, view);

            // 최초 Tick은 구독 여부와 무관하게 기본값(0)과 초기 _lastDisplayedCount(-1)가
            // 달라 한 번 호출된다 — 그 상태를 먼저 소비해둔 뒤 Dispose 효과만 검증한다.
            presenter.Tick(0.3f);
            presenter.Dispose();

            eventBus.Publish(new MonsterCountChangedEvent(5));
            presenter.Tick(0.3f);

            Assert.AreEqual(1, view.SetCountCallCount);
            Assert.AreEqual(0, view.LastCount);
        }
    }
}
