using LegionBreak.Domain.Skills;
using NUnit.Framework;

namespace LegionBreak.Domain.Tests
{
    public class SkillTests
    {
        [Test]
        public void IsReady_BeforeAnyCast_IsTrue()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);

            Assert.IsTrue(skill.IsReady);
        }

        [Test]
        public void ConsumeCooldown_MakesSkillNotReady()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);

            skill.ConsumeCooldown();

            Assert.IsFalse(skill.IsReady);
        }

        [Test]
        public void Tick_AfterCooldownElapses_BecomesReadyAgain()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);
            skill.ConsumeCooldown();

            skill.Tick(1f);

            Assert.IsTrue(skill.IsReady);
        }

        [Test]
        public void Tick_PartialElapse_StaysNotReady()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);
            skill.ConsumeCooldown();

            skill.Tick(0.5f);

            Assert.IsFalse(skill.IsReady);
        }

        [Test]
        public void Tick_DoesNotGoBelowZero()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);
            skill.ConsumeCooldown();

            skill.Tick(10f);

            Assert.AreEqual(0f, skill.RemainingCooldown);
        }
    }
}
