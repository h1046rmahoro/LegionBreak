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

        // 스킬 쿨다운 판정: 밸런스 분기가 있는 도메인 규칙이라 범용 수학과 달리
        // Domain에 상태로 둔다 (CLAUDE.md Domain 계층 분리 기준의 명시적 예시).
        public float RemainingCooldown { get; private set; }
        public bool IsReady => RemainingCooldown <= 0f;

        public Skill(string id, string displayName, float baseDamage, float cooldownSeconds)
        {
            Id = id;
            DisplayName = displayName;
            BaseDamage = baseDamage;
            CooldownSeconds = cooldownSeconds;
        }

        public void Tick(float deltaTime)
        {
            RemainingCooldown = System.Math.Max(0f, RemainingCooldown - deltaTime);
        }

        public void ConsumeCooldown()
        {
            RemainingCooldown = CooldownSeconds;
        }
    }
}
