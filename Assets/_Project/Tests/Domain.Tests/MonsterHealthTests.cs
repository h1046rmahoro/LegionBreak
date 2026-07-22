using LegionBreak.Domain.Monsters;
using NUnit.Framework;

namespace LegionBreak.Domain.Tests
{
    public class MonsterHealthTests
    {
        [Test]
        public void TakeDamage_ReducesCurrentHpByAmount()
        {
            var health = new MonsterHealth(20f);

            health.TakeDamage(5f);

            Assert.AreEqual(15f, health.CurrentHp);
        }

        [Test]
        public void TakeDamage_DoesNotGoBelowZero()
        {
            var health = new MonsterHealth(20f);

            health.TakeDamage(100f);

            Assert.AreEqual(0f, health.CurrentHp);
        }

        [Test]
        public void IsDead_WhenCurrentHpReachesZero_IsTrue()
        {
            var health = new MonsterHealth(20f);

            health.TakeDamage(20f);

            Assert.IsTrue(health.IsDead);
        }

        [Test]
        public void IsDead_BeforeAnyDamage_IsFalse()
        {
            var health = new MonsterHealth(20f);

            Assert.IsFalse(health.IsDead);
        }
    }
}
