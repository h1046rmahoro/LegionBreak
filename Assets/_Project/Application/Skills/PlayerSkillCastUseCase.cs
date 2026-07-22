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

        public PlayerSkillCastUseCase(Skill skill, ISkillDamageCalculator damageCalculator, IMonsterSpawner monsterSpawner)
        {
            _skill = skill;
            _damageCalculator = damageCalculator;
            _monsterSpawner = monsterSpawner;
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

            // TODO: 크리티컬 확률 판정 시스템이 생기면 false 고정을 실제 판정으로 교체
            var damage = _damageCalculator.Calculate(_skill, isCritical: false);
            var hitCount = _monsterSpawner.ApplyDamageInRange(targetPoint, _skill.Range, damage);
            _skill.ConsumeCooldown();

            return new SkillCastResult(true, damage, hitCount);
        }
    }
}
