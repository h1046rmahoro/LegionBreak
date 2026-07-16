namespace LegionBreak.Domain.Skills
{
    /// <summary>
    /// 스킬 밸런스 수치를 담는 순수 도메인 모델. Data 계층의 SkillData(ScriptableObject)를
    /// 그대로 노출하지 않고 이 타입으로 변환해 사용해, Domain이 UnityEngine을 참조하지
    /// 않는다는 규칙을 지킨다.
    /// </summary>
    public sealed class Skill
    {
        public string Id { get; }
        public string DisplayName { get; }
        public float BaseDamage { get; }
        public float CooldownSeconds { get; }

        public Skill(string id, string displayName, float baseDamage, float cooldownSeconds)
        {
            Id = id;
            DisplayName = displayName;
            BaseDamage = baseDamage;
            CooldownSeconds = cooldownSeconds;
        }
    }
}
