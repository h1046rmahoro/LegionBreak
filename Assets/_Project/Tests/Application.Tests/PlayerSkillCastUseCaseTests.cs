using LegionBreak.Application.Skills;
using LegionBreak.Application.Spawning;
using LegionBreak.Domain.Skills;
using NUnit.Framework;
using UnityEngine;

namespace LegionBreak.Application.Tests
{
    public class PlayerSkillCastUseCaseTests
    {
        private sealed class FakeMonsterSpawner : IMonsterSpawner
        {
            public int HitCountToReturn = 1;
            public Vector2? LastCenter;
            public float LastRadius;
            public float LastDamage;

            public int ActiveCount => 0;

            public void Spawn(Vector2 position)
            {
            }

            public int ApplyDamageInRange(Vector2 center, float radius, float damage)
            {
                LastCenter = center;
                LastRadius = radius;
                LastDamage = damage;
                return HitCountToReturn;
            }
        }

        [Test]
        public void Execute_WhenReady_ReturnsSuccessWithDamageAndHitCount()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var spawner = new FakeMonsterSpawner { HitCountToReturn = 2 };
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f), spawner);

            var result = useCase.Execute(new Vector2(1f, 2f));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(10f, result.Damage);
            Assert.AreEqual(2, result.HitCount);
        }

        [Test]
        public void Execute_AppliesDamageInRangeAtTargetPointWithSkillRange()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var spawner = new FakeMonsterSpawner();
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f), spawner);

            useCase.Execute(new Vector2(1f, 2f));

            Assert.AreEqual(new Vector2(1f, 2f), spawner.LastCenter);
            Assert.AreEqual(3f, spawner.LastRadius);
            Assert.AreEqual(10f, spawner.LastDamage);
        }

        [Test]
        public void Execute_WhileOnCooldown_ReturnsFailed()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f), new FakeMonsterSpawner());
            useCase.Execute(Vector2.zero);

            var result = useCase.Execute(Vector2.zero);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Execute_AfterCooldownElapsesViaTick_SucceedsAgain()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var useCase = new PlayerSkillCastUseCase(skill, new SkillDamageCalculator(1.5f), new FakeMonsterSpawner());
            useCase.Execute(Vector2.zero);

            useCase.Tick(1f);
            var result = useCase.Execute(Vector2.zero);

            Assert.IsTrue(result.Success);
        }
    }
}
