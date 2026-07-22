using LegionBreak.Domain.Skills;

namespace LegionBreak.Application.Skills
{
    public sealed class PlayerSkillCastUseCase : IPlayerSkillCastUseCase
    {
        private readonly Skill _skill;
        private readonly ISkillDamageCalculator _damageCalculator;

        public PlayerSkillCastUseCase(Skill skill, ISkillDamageCalculator damageCalculator)
        {
            _skill = skill;
            _damageCalculator = damageCalculator;
        }

        public void Tick(float deltaTime)
        {
            _skill.Tick(deltaTime);
        }

        public SkillCastResult Execute()
        {
            if (!_skill.IsReady)
            {
                return SkillCastResult.Failed;
            }

            // TODO: 크리티컬 확률 판정 시스템이 생기면 false 고정을 실제 판정으로 교체
            var damage = _damageCalculator.Calculate(_skill, isCritical: false);
            _skill.ConsumeCooldown();

            return new SkillCastResult(true, damage);
        }
    }
}
