using LegionBreak.Application.Skills;
using LegionBreak.Domain.Skills;
using NUnit.Framework;

namespace LegionBreak.Application.Tests
{
    public class PlayerSkillCastUseCaseTests
    {
        [Test]
        public void Execute_WhenReady_ReturnsSuccessWithDamage()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f));

            var result = useCase.Execute();

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10f, result.Damage);
        }

        [Test]
        public void Execute_WhileOnCooldown_ReturnsFailed()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f));
            useCase.Execute();

            var result = useCase.Execute();

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Execute_AfterCooldownElapsesViaTick_SucceedsAgain()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f);
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f));
            useCase.Execute();

            useCase.Tick(1f);
            var result = useCase.Execute();

            Assert.IsTrue(result.Success);
        }
    }
}
