namespace LegionBreak.Domain.Skills
{
    public interface ISkillDamageCalculator
    {
        float Calculate(Skill skill, bool isCritical);
    }
}
