using LegionBreak.Domain.Skills;
using NUnit.Framework;

namespace LegionBreak.Domain.Tests
{
    public class SkillDamageCalculatorTests
    {
        [Test]
        public void Calculate_NoCritical_ReturnsBaseDamage()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var calculator = new SkillDamageCalculator(criticalDamageMultiplier: 1.5f);

            var damage = calculator.Calculate(skill, isCritical: false);

            Assert.AreEqual(10f, damage);
        }

        [Test]
        public void Calculate_Critical_AppliesCriticalMultiplier()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var calculator = new SkillDamageCalculator(criticalDamageMultiplier: 1.5f);

            var damage = calculator.Calculate(skill, isCritical: true);

            Assert.AreEqual(15f, damage);
        }
    }
}
