using LegionBreak.Data;
using LegionBreak.Domain.Skills;

namespace LegionBreak.Application.Skills
{
    /// <summary>
    /// SkillData(ScriptableObject)를 Domain의 Skill로 변환한다. Presentation/Infrastructure가
    /// SkillData를 직접 들고 다니지 않고 이 변환을 거친 Skill만 사용하게 하기 위한 경계다.
    /// </summary>
    public static class SkillFactory
    {
        public static Skill Create(SkillData data)
        {
            return new Skill(data.SkillId, data.DisplayName, data.BaseDamage, data.CooldownSeconds);
        }
    }
}
