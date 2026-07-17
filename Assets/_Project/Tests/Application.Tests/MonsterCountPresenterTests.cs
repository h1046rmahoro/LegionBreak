using LegionBreak.Application.Spawning;
using LegionBreak.Application.UI;
using NUnit.Framework;
using UnityEngine;

namespace LegionBreak.Application.Tests
{
    public class MonsterCountPresenterTests
    {
        private sealed class FakeMonsterSpawner : IMonsterSpawner
        {
            public int ActiveCount { get; set; }

            public void Spawn(Vector2 position)
            {
            }
        }

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
            var spawner = new FakeMonsterSpawner { ActiveCount = 5 };
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(spawner, view);

            presenter.Tick(0.1f);

            Assert.AreEqual(0, view.SetCountCallCount);
        }

        [Test]
        public void Tick_AfterRefreshInterval_UpdatesViewWithCurrentCount()
        {
            var spawner = new FakeMonsterSpawner { ActiveCount = 5 };
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(spawner, view);

            presenter.Tick(0.3f);

            Assert.AreEqual(1, view.SetCountCallCount);
            Assert.AreEqual(5, view.LastCount);
        }

        [Test]
        public void Tick_CountUnchangedAcrossIntervals_DoesNotUpdateViewAgain()
        {
            var spawner = new FakeMonsterSpawner { ActiveCount = 5 };
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(spawner, view);

            presenter.Tick(0.3f);
            presenter.Tick(0.3f);

            Assert.AreEqual(1, view.SetCountCallCount);
        }

        [Test]
        public void Tick_CountChanges_UpdatesViewAgain()
        {
            var spawner = new FakeMonsterSpawner { ActiveCount = 5 };
            var view = new FakeMonsterCountView();
            var presenter = new MonsterCountPresenter(spawner, view);

            presenter.Tick(0.3f);
            spawner.ActiveCount = 8;
            presenter.Tick(0.3f);

            Assert.AreEqual(2, view.SetCountCallCount);
            Assert.AreEqual(8, view.LastCount);
        }
    }
}
