using LegionBreak.Application.Spawning;
using LegionBreak.Domain.Skills;
using UnityEngine;

namespace LegionBreak.Application.Skills
{
    public sealed class PlayerSkillCastUseCase : IPlayerSkillCastUseCase
    {
        private readonly Skill _skill;
        private readonly ISkillDamageCalculator _damageCalculator;
        private readonly IMonsterSpawner _monsterSpawner;
        private readonly ICriticalHitJudge _criticalHitJudge;
        private readonly IRandomProvider _randomProvider;
        private readonly float _criticalChance;

        public PlayerSkillCastUseCase(
            Skill skill,
            ISkillDamageCalculator damageCalculator,
            IMonsterSpawner monsterSpawner,
            ICriticalHitJudge criticalHitJudge,
            IRandomProvider randomProvider,
            float criticalChance)
        {
            _skill = skill;
            _damageCalculator = damageCalculator;
            _monsterSpawner = monsterSpawner;
            _criticalHitJudge = criticalHitJudge;
            _randomProvider = randomProvider;
            _criticalChance = criticalChance;
        }

        public void Tick(float deltaTime)
        {
            _skill.Tick(deltaTime);
        }

        public SkillCastResult Execute(Vector2 targetPoint)
        {
            if (!_skill.IsReady)
            {
                return SkillCastResult.Failed;
            }

            var isCritical = _criticalHitJudge.Judge(_criticalChance, _randomProvider.NextFloat01());
            var damage = _damageCalculator.Calculate(_skill, isCritical);
            var hitCount = _monsterSpawner.ApplyDamageInRange(targetPoint, _skill.Range, damage);
            _skill.ConsumeCooldown();

            return new SkillCastResult(true, damage, hitCount, isCritical);
        }
    }
}
