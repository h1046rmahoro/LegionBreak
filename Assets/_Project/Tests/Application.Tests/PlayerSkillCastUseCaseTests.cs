using LegionBreak.Application.Events;
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

        // 크리티컬 판정 로직(CriticalHitJudge) 자체는 순수 함수라 페이크할 이유가 없어 실제
        // 구현을 그대로 쓰고, 난수만 고정값으로 페이크해 분기를 결정적으로 만든다.
        private sealed class FakeRandomProvider : IRandomProvider
        {
            private readonly float _value;

            public FakeRandomProvider(float value)
            {
                _value = value;
            }

            public float NextFloat01() => _value;
        }

        private static PlayerSkillCastUseCase CreateUseCase(
            Skill skill,
            IMonsterSpawner spawner,
            float criticalChance = 0f,
            float roll = 1f)
        {
            return new PlayerSkillCastUseCase(
                skill,
                new SkillDamageCalculator(1.5f),
                spawner,
                new CriticalHitJudge(),
                new FakeRandomProvider(roll),
                new EventBus(),
                criticalChance);
        }

        [Test]
        public void Execute_WhenReady_ReturnsSuccessWithDamageAndHitCount()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var spawner = new FakeMonsterSpawner { HitCountToReturn = 2 };
            var useCase = CreateUseCase(skill, spawner);

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
            var useCase = CreateUseCase(skill, spawner);

            useCase.Execute(new Vector2(1f, 2f));

            Assert.AreEqual(new Vector2(1f, 2f), spawner.LastCenter);
            Assert.AreEqual(3f, spawner.LastRadius);
            Assert.AreEqual(10f, spawner.LastDamage);
        }

        [Test]
        public void Execute_WhileOnCooldown_ReturnsFailed()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var useCase = CreateUseCase(skill, new FakeMonsterSpawner());
            useCase.Execute(Vector2.zero);

            var result = useCase.Execute(Vector2.zero);

            Assert.IsFalse(result.Success);
        }

        [Test]
        public void Execute_AfterCooldownElapsesViaTick_SucceedsAgain()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var useCase = CreateUseCase(skill, new FakeMonsterSpawner());
            useCase.Execute(Vector2.zero);

            useCase.Tick(1f);
            var result = useCase.Execute(Vector2.zero);

            Assert.IsTrue(result.Success);
        }

        [Test]
        public void Execute_RollBelowCriticalChance_ReturnsCriticalDamage()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var useCase = CreateUseCase(skill, new FakeMonsterSpawner(), criticalChance: 1f, roll: 0f);

            var result = useCase.Execute(Vector2.zero);

            Assert.IsTrue(result.IsCritical);
            Assert.AreEqual(15f, result.Damage);
        }

        [Test]
        public void Execute_CriticalChanceZero_NeverReturnsCritical()
        {
            var skill = new Skill("fireball", "Fireball", 10f, 1f, 3f);
            var useCase = CreateUseCase(skill, new FakeMonsterSpawner(), criticalChance: 0f, roll: 0f);

            var result = useCase.Execute(Vector2.zero);

            Assert.IsFalse(result.IsCritical);
            Assert.AreEqual(10f, result.Damage);
        }
    }
}
