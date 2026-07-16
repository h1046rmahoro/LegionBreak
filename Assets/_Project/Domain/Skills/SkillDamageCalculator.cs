namespace LegionBreak.Domain.Skills
{
    /// <summary>
    /// 데미지 공식(기본 데미지 × 크리티컬 배율)을 계산하는 도메인 규칙.
    /// 크리티컬 배율은 전투 전반의 밸런스 수치라 하드코딩하지 않고 생성자로 주입받는다
    /// (실제 값은 Data 계층의 CombatBalanceData에서 온다).
    /// </summary>
    public sealed class SkillDamageCalculator : ISkillDamageCalculator
    {
        private readonly float _criticalDamageMultiplier;

        public SkillDamageCalculator(float criticalDamageMultiplier)
        {
            _criticalDamageMultiplier = criticalDamageMultiplier;
        }

        public float Calculate(Skill skill, bool isCritical)
        {
            var damage = skill.BaseDamage;

            if (isCritical)
            {
                damage *= _criticalDamageMultiplier;
            }

            return damage;
        }
    }
}
